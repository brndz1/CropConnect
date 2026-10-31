using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Orders.Service.Data;

namespace Orders.Service.Messaging;

// Publishes the pending outbox messages to the cropconnect.events exchange, oldest first.
// A message is marked as published only after the broker confirms it.
public sealed class OutboxPublisher : BackgroundService
{
    #region Dependencies

    private readonly RabbitMqConnectionProvider _connections;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxPublisher> _logger;
    private IChannel? _channel;

    public OutboxPublisher(
        RabbitMqConnectionProvider connections,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxPublisher> logger)
    {
        _connections = connections;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    #endregion

    #region Execution

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnablePublisher)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.PollInterval, _timeProvider);

        do
        {
            await PublishPendingAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishPendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

            // Another replica is publishing: one publisher at a time keeps the events in order.
            await using var workerLock = await WorkerLock.TryAcquireAsync(dbContext, WorkerLock.OutboxPublisher, stoppingToken);

            if (workerLock is null)
            {
                return;
            }

            var pending = await dbContext.OutboxMessages
                .Where(m => m.PublishedAt == null)
                .OrderBy(m => m.OccurredAt)
                .Take(_options.BatchSize)
                .ToListAsync(stoppingToken);

            if (pending.Count == 0)
            {
                return;
            }

            var channel = await GetChannelAsync(stoppingToken);

            foreach (var message in pending)
            {
                try
                {
                    using var activity = MessagingTelemetry.StartPublish(message.TraceParent, message.RoutingKey, message.Id);

                    var properties = new BasicProperties
                    {
                        MessageId = message.Id.ToString(),
                        Type = message.EventType,
                        ContentType = "application/json",
                        DeliveryMode = DeliveryModes.Persistent,
                        Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt).ToUnixTimeSeconds())
                    };

                    // Mandatory: if no queue is bound yet (Notification.Service never started), the broker returns
                    // the message, the publish throws and the event stays in the outbox instead of being dropped.
                    await channel.BasicPublishAsync(
                        MessagingTopology.EventsExchange,
                        message.RoutingKey,
                        mandatory: true,
                        properties,
                        Encoding.UTF8.GetBytes(message.Payload),
                        stoppingToken);

                    message.MarkPublished(_timeProvider.GetUtcNow().UtcDateTime);
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // Keep the order: stop at the first failure and retry the rest on the next tick.
                    message.MarkFailed(exception is PublishException { IsReturn: true }
                        ? $"No queue is bound to '{message.RoutingKey}' yet."
                        : exception.Message);
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogWarning(exception, "Failed to publish outbox message {MessageId}; it will be retried", message.Id);
                    await ResetChannelAsync();
                    return;
                }
            }

            _logger.LogInformation("Published {Count} outbox message(s)", pending.Count);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Outbox publishing failed; it will be retried");
            await ResetChannelAsync();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken stoppingToken)
    {
        if (_channel is { IsOpen: true } open)
        {
            return open;
        }

        var connection = await _connections.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);

        await MessagingTopology.DeclareEventsAsync(_channel, stoppingToken);
        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await ResetChannelAsync();
    }

    #endregion
}
