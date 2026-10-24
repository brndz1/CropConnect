using Google.Protobuf.WellKnownTypes;
using Orders.Service.Application;
using Orders.Service.Models;
using Orders.Service.Protos;

namespace Orders.Service.Services;

public static class ProtoMappings
{
    public static OrderInfo ToProto(this OrderResult result)
    {
        var info = new OrderInfo
        {
            Id = result.Id.ToString(),
            CustomerId = result.CustomerId.ToString(),
            FarmId = result.FarmId.ToString(),
            Status = result.Status.ToProto(),
            TotalAmount = result.TotalAmount.ToDecimalValue(),
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc)),
            Version = result.Version
        };

        if (result.UpdatedAt.HasValue)
        {
            info.UpdatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(result.UpdatedAt.Value, DateTimeKind.Utc));
        }

        if (!string.IsNullOrEmpty(result.CancellationReason))
        {
            info.CancellationReason = result.CancellationReason;
        }

        if (!string.IsNullOrEmpty(result.Notes))
        {
            info.Notes = result.Notes;
        }

        foreach (var item in result.Items)
        {
            info.Items.Add(item.ToProto());
        }

        return info;
    }

    public static OrderItemInfo ToProto(this OrderItemResult result) => new()
    {
        ProductId = result.ProductId.ToString(),
        ProductName = result.ProductName,
        Quantity = result.Quantity,
        UnitPrice = result.UnitPrice.ToDecimalValue(),
        TotalPrice = result.TotalPrice.ToDecimalValue()
    };

    public static ReviewInfo ToProto(this ReviewResult result) => new()
    {
        Id = result.Id.ToString(),
        OrderId = result.OrderId.ToString(),
        CustomerId = result.CustomerId.ToString(),
        FarmId = result.FarmId.ToString(),
        Rating = result.Rating,
        Comment = result.Comment ?? string.Empty,
        CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc))
    };

    public static OrderStatusProto ToProto(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => OrderStatusProto.OrderStatusPending,
        OrderStatus.Confirmed => OrderStatusProto.OrderStatusConfirmed,
        OrderStatus.ReadyForPickup => OrderStatusProto.OrderStatusReadyForPickup,
        OrderStatus.PickedUp => OrderStatusProto.OrderStatusPickedUp,
        OrderStatus.Cancelled => OrderStatusProto.OrderStatusCancelled,
        _ => OrderStatusProto.OrderStatusUnspecified
    };

    public static OrderStatus? ToDomain(this OrderStatusProto proto) => proto switch
    {
        OrderStatusProto.OrderStatusPending => OrderStatus.Pending,
        OrderStatusProto.OrderStatusConfirmed => OrderStatus.Confirmed,
        OrderStatusProto.OrderStatusReadyForPickup => OrderStatus.ReadyForPickup,
        OrderStatusProto.OrderStatusPickedUp => OrderStatus.PickedUp,
        OrderStatusProto.OrderStatusCancelled => OrderStatus.Cancelled,
        _ => null
    };

    public static OrderStatus ToDomainRequired(this OrderStatusProto proto) =>
        proto.ToDomain() ?? throw new InvalidInputException("A valid order status must be specified.");
}
