namespace Orders.Service.Application.Events;

public interface IOrderEventPublisher
{
    Task PublishOrderCreatedAsync(OrderCreatedEvent @event, CancellationToken cancellationToken);

    Task PublishOrderStatusChangedAsync(OrderStatusChangedEvent @event, CancellationToken cancellationToken);

    Task PublishOrderCancelledAsync(OrderCancelledEvent @event, CancellationToken cancellationToken);
}

public sealed class LoggingOrderEventPublisher : IOrderEventPublisher
{
    private readonly ILogger<LoggingOrderEventPublisher> _logger;

    public LoggingOrderEventPublisher(ILogger<LoggingOrderEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishOrderCreatedAsync(OrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Event Published: OrderCreated [OrderId: {OrderId}, FarmId: {FarmId}, Total: {Total}]",
            @event.OrderId, @event.FarmId, @event.TotalAmount);
        return Task.CompletedTask;
    }

    public Task PublishOrderStatusChangedAsync(OrderStatusChangedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Event Published: OrderStatusChanged [OrderId: {OrderId}, From: {PreviousStatus}, To: {NewStatus}]",
            @event.OrderId, @event.PreviousStatus, @event.NewStatus);
        return Task.CompletedTask;
    }

    public Task PublishOrderCancelledAsync(OrderCancelledEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Event Published: OrderCancelled [OrderId: {OrderId}, Reason: {Reason}]",
            @event.OrderId, @event.Reason ?? "none");
        return Task.CompletedTask;
    }
}
