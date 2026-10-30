using Orders.Service.Application;
using Orders.Service.Application.Clients;
using Orders.Service.Models;

namespace Orders.Service.Tests.Infrastructure;

// Stands in for Farms.Service: every product costs 2.50 and belongs to the same farm.
public sealed class FakeFarmStockClient : IFarmStockClient
{
    public Guid FarmId { get; } = Guid.NewGuid();

    public Guid FarmOwnerId { get; } = Guid.NewGuid();

    public List<(Guid OrderId, IReadOnlyList<CreateOrderItemCommand> Items)> Reserved { get; } = [];

    public List<Guid> Confirmed { get; } = [];

    public List<Guid> Released { get; } = [];

    public Exception? ConfirmFailure { get; set; }

    public Exception? ReleaseFailure { get; set; }

    public Task<ReservedStockResult> ReserveStockAsync(Guid orderId, IReadOnlyList<CreateOrderItemCommand> items, CancellationToken cancellationToken)
    {
        Reserved.Add((orderId, items));

        var reserved = items
            .Select(i => new ReservedStockItemResult(i.ProductId, "Produto " + i.ProductId.ToString()[..4], i.Quantity, 2.50m))
            .ToList();

        return Task.FromResult(new ReservedStockResult(orderId, FarmId, FarmOwnerId, reserved));
    }

    public Task ConfirmStockAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (ConfirmFailure is not null)
        {
            throw ConfirmFailure;
        }

        Confirmed.Add(orderId);
        return Task.CompletedTask;
    }

    public Task ReleaseStockAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (ReleaseFailure is not null)
        {
            throw ReleaseFailure;
        }

        Released.Add(orderId);
        return Task.CompletedTask;
    }
}

public static class Callers
{
    public static Caller Client(Guid? id = null) => new(id ?? Guid.NewGuid(), Role.Client);

    public static Caller Manager(Guid? id = null) => new(id ?? Guid.NewGuid(), Role.Manager);

    public static Caller Admin() => new(Guid.NewGuid(), Role.Admin);
}
