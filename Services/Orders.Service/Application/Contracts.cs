using Orders.Service.Models;

namespace Orders.Service.Application;

#region Commands

public sealed record CreateOrderItemCommand(Guid ProductId, int Quantity);

public sealed record CreateOrderCommand(
    string? Notes,
    IReadOnlyList<CreateOrderItemCommand> Items);

public sealed record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatus NewStatus,
    int? ExpectedVersion);

public sealed record CancelOrderCommand(
    Guid OrderId,
    string? Reason,
    int? ExpectedVersion);

public sealed record CreateReviewCommand(
    Guid OrderId,
    int Rating,
    string? Comment);

#endregion

#region Queries

public sealed record ListOrdersQuery(
    PageRequest Page,
    Guid? FarmId,
    OrderStatus? Status);

public sealed record ListMyOrdersQuery(
    PageRequest Page,
    OrderStatus? Status);

public sealed record ListFarmReviewsQuery(
    Guid FarmId,
    PageRequest Page);

#endregion

#region Results

public sealed record OrderItemResult(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

public sealed record OrderResult(
    Guid Id,
    Guid CustomerId,
    Guid FarmId,
    OrderStatus Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemResult> Items,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int Version,
    string? CancellationReason,
    string? Notes);

public sealed record ReviewResult(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

#endregion
