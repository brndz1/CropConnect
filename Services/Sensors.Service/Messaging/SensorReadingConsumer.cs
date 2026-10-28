using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sensors.Service.Application;
using Sensors.Service.Models;

namespace Sensors.Service.Messaging;

// RF16: consumes the readings Node-RED publishes and hands them to the ingestion service.
public sealed class SensorReadingConsumer : BackgroundService
{
    #region Dependencies

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly RabbitMqConnectionProvider _connections;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<SensorReadingConsumer> _logger;
    private IChannel? _channel;

    public SensorReadingConsumer(
        RabbitMqConnectionProvider connections,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<SensorReadingConsumer> logger)
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
                _logger.LogInformation("Consuming sensor readings from {Queue}", MessagingTopology.ReadingsQueue);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Cannot start the sensor readings consumer; retrying in {Delay}", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var connection = await _connections.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await MessagingTopology.DeclareReadingsAsync(_channel, stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(_channel, delivery, stoppingToken);

        await _channel.BasicConsumeAsync(MessagingTopology.ReadingsQueue, autoAck: false, consumer, stoppingToken);
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        try
        {
            var command = SensorReadingMessage.Parse(delivery.Body.Span).ToCommand();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<IReadingIngestionService>();
            await ingestion.RecordAsync(command, stoppingToken);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (DomainException exception)
        {
            // Retrying won't fix an invalid reading or an unknown farm: send it to the dead-letter queue.
            _logger.LogWarning("Reading rejected ({RoutingKey}): {Reason}", delivery.RoutingKey, exception.Message);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Failed to process reading ({RoutingKey}); it will be retried", delivery.RoutingKey);
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
