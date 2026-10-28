using Orders.Service.Models;

namespace Orders.Service.Application.Events;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    decimal TotalAmount,
    int ItemCount,
    DateTime OccurredAt);

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus,
    DateTime OccurredAt);

// CancelledByCustomer tells consumers who the "other party" to notify is (RF20).
public sealed record OrderCancelledEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    OrderStatus PreviousStatus,
    string? Reason,
    bool CancelledByCustomer,
    DateTime OccurredAt);
