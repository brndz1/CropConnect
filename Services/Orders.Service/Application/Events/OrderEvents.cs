using Orders.Service.Models;

namespace Orders.Service.Application.Events;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    decimal TotalAmount,
    DateTime OccurredAt);

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus,
    DateTime OccurredAt);

public sealed record OrderCancelledEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    string? Reason,
    DateTime OccurredAt);
