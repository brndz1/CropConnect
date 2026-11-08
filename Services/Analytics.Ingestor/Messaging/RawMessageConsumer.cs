using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Analytics.Ingestor.Application;

namespace Analytics.Ingestor.Messaging;

// Consumes both of the ingestor's queues and stores each message in MongoDB. A message is acknowledged only once
// it is stored (at least once); duplicates are recognised by their ID, so redeliveries are harmless.
public sealed class RawMessageConsumer : BackgroundService
{
    #region Dependencies

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly RabbitMqConnectionProvider _connections;
    private readonly RawMessageHandler _handler;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RawMessageConsumer> _logger;
    private IChannel? _channel;

    public RawMessageConsumer(
        RabbitMqConnectionProvider connections,
        RawMessageHandler handler,
        IOptions<RabbitMqOptions> options,
        ILogger<RawMessageConsumer> logger)
    {
        _connections = connections;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    #endregion

    #region Execution

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableConsumer)
        {
            return;
        }

        // The broker may start after this service; keep trying until the consumers are attached.
        // After that, the client's automatic recovery takes care of dropped connections.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                _logger.LogInformation(
                    "Storing the messages of {ReadingsQueue} and {EventsQueue}",
                    MessagingTopology.ReadingsQueue,
                    MessagingTopology.EventsQueue);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Cannot start the consumers; retrying in {Delay}", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var connection = await _connections.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await MessagingTopology.DeclareAsync(_channel, stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, stoppingToken);

        await ConsumeAsync(_channel, MessagingTopology.ReadingsQueue, _handler.HandleReadingAsync, stoppingToken);
        await ConsumeAsync(_channel, MessagingTopology.EventsQueue, _handler.HandleEventAsync, stoppingToken);
    }

    private Task ConsumeAsync(
        IChannel channel,
        string queue,
        Func<IncomingMessage, CancellationToken, Task<StoreOutcome>> store,
        CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, queue, delivery, store, stoppingToken);
        return channel.BasicConsumeAsync(queue, autoAck: false, consumer, stoppingToken);
    }

    private async Task HandleAsync(
        IChannel channel,
        string queue,
        BasicDeliverEventArgs delivery,
        Func<IncomingMessage, CancellationToken, Task<StoreOutcome>> store,
        CancellationToken stoppingToken)
    {
        var routingKey = DeliveryRetries.RoutingKey(delivery);
        var properties = delivery.BasicProperties;
        using var activity = MessagingTelemetry.StartProcess(properties, routingKey, out var traceParent);

        try
        {
            var message = new IncomingMessage(
                routingKey,
                properties.MessageId,
                properties.Type,
                properties.IsTimestampPresent() ? DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime).UtcDateTime : null,
                traceParent,
                delivery.Body);

            var outcome = await store(message, stoppingToken);

            _logger.LogDebug("Message {MessageId} ({RoutingKey}): {Outcome}", properties.MessageId, routingKey, outcome);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (InvalidMessageException exception)
        {
            // Retrying won't make it valid: send it to the dead-letter queue.
            _logger.LogWarning("Message rejected ({RoutingKey}): {Reason}", routingKey, exception.Message);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_options.RetryDelay, stoppingToken);
            var retried = await DeliveryRetries.RetryOrDeadLetterAsync(channel, delivery, queue, _options.MaxRetries, stoppingToken);
            _logger.LogError(
                exception,
                retried ? "Failed to store a message ({RoutingKey}); it will be retried" : "Failed to store a message ({RoutingKey}) too many times; it was dead-lettered",
                routingKey);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
    }

    #endregion
}
