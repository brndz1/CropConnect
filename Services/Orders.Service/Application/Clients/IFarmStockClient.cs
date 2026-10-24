namespace Orders.Service.Application.Clients;

public sealed record ReservedStockItemResult(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record ReservedStockResult(
    Guid OrderId,
    Guid FarmId,
    Guid FarmOwnerId,
    IReadOnlyList<ReservedStockItemResult> Items);

public interface IFarmStockClient
{
    Task<ReservedStockResult> ReserveStockAsync(
        Guid orderId,
        IReadOnlyList<CreateOrderItemCommand> items,
        CancellationToken cancellationToken);

    Task ConfirmStockAsync(Guid orderId, CancellationToken cancellationToken);

    Task ReleaseStockAsync(Guid orderId, CancellationToken cancellationToken);
}
