using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Notifications.Service.Application;
using Notifications.Service.Models;

namespace Notifications.Service.Messaging;

// Consumes the order and sensor events and turns each into a notification (RF19, RF20, RF21).
public sealed class NotificationEventConsumer : BackgroundService
{
    #region Dependencies

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly RabbitMqConnectionProvider _connections;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<NotificationEventConsumer> _logger;
    private IChannel? _channel;

    public NotificationEventConsumer(
        RabbitMqConnectionProvider connections,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<NotificationEventConsumer> logger)
    {
        _connections = connections;
        _scopeFactory = scopeFactory;
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

        // The broker may start after this service; keep trying until the consumer is attached.
        // After that, the client's automatic recovery takes care of dropped connections.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                _logger.LogInformation("Consuming domain events from {Queue}", MessagingTopology.EventsQueue);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Cannot start the domain events consumer; retrying in {Delay}", ReconnectDelay);
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

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(_channel, delivery, stoppingToken);

        await _channel.BasicConsumeAsync(MessagingTopology.EventsQueue, autoAck: false, consumer, stoppingToken);
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        try
        {
            // Producers set MessageId to their outbox message ID, which makes redeliveries idempotent.
            if (!Guid.TryParse(delivery.BasicProperties.MessageId, out var eventId))
            {
                throw new InvalidInputException("The event has no valid MessageId.");
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<INotificationIngestionService>();
            var created = await ingestion.HandleAsync(delivery.RoutingKey, eventId, delivery.Body, stoppingToken);

            _logger.LogDebug("Event {EventId} ({RoutingKey}) handled; notification created: {Created}", eventId, delivery.RoutingKey, created);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (DomainException exception)
        {
            // Retrying won't fix a malformed or unsupported event: send it to the dead-letter queue.
            _logger.LogWarning("Event rejected ({RoutingKey}): {Reason}", delivery.RoutingKey, exception.Message);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Failed to process event ({RoutingKey}); it will be retried", delivery.RoutingKey);
            await Task.Delay(_options.RetryDelay, stoppingToken);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, stoppingToken);
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
