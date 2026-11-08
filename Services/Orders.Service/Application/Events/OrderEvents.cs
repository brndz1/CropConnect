using Orders.Service.Models;

namespace Orders.Service.Application.Events;

// Items carries the lines with the prices Farms reserved them at, so the analytics platform can rebuild the sales
// per product without reading the Orders database.
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    decimal TotalAmount,
    int ItemCount,
    IReadOnlyList<OrderLineEvent> Items,
    DateTime OccurredAt);

public sealed record OrderLineEvent(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

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

// Tells the farm owner a customer rated a picked-up order (RF15).
public sealed record ReviewCreatedEvent(
    Guid ReviewId,
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    int Rating,
    string? Comment,
    DateTime OccurredAt);
