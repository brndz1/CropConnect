using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orders.Service.Application;
using Orders.Service.Application.Events;
using Orders.Service.Data;
using Orders.Service.Models;
using Orders.Service.Tests.Infrastructure;

namespace Orders.Service.Tests.Application;

public sealed class OrderApplicationServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeFarmStockClient _stock = new();

    public void Dispose() => _database.Dispose();

    // A fresh context per call, as each gRPC request gets its own scope.
    private OrderApplicationService Service(OrdersDbContext? context = null)
    {
        context ??= _database.CreateContext();
        return new OrderApplicationService(
            context,
            _stock,
            new OutboxOrderEventPublisher(context),
            TimeProvider.System,
            Options.Create(new OrderOptions { MaxLinesPerOrder = 3 }),
            NullLogger<OrderApplicationService>.Instance);
    }

    private static CreateOrderCommand OrderOf(params (Guid ProductId, int Quantity)[] lines) =>
        new("Sábado de manhã", lines.Select(l => new CreateOrderItemCommand(l.ProductId, l.Quantity)).ToList());

    private async Task<OrderResult> PlaceOrderAsync(Caller client) =>
        await Service().CreateOrderAsync(client, OrderOf((Guid.NewGuid(), 2)), CancellationToken.None);

    private List<OutboxMessage> Outbox()
    {
        using var context = _database.CreateContext();
        return context.OutboxMessages.OrderBy(m => m.OccurredAt).ToList();
    }

    #region Create

    [Fact]
    public async Task Creating_an_order_reserves_confirms_and_stages_the_event()
    {
        var client = Callers.Client();
        var lettuce = Guid.NewGuid();
        var carrots = Guid.NewGuid();

        var order = await Service().CreateOrderAsync(client, OrderOf((lettuce, 2), (carrots, 1), (lettuce, 3)), CancellationToken.None);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(_stock.FarmId, order.FarmId);
        Assert.Equal(15m, order.TotalAmount);
        Assert.Equal([order.Id], _stock.Confirmed);

        // Lines for the same product are merged before reserving.
        var reserved = Assert.Single(_stock.Reserved).Items;
        Assert.Equal(5, reserved.Single(i => i.ProductId == lettuce).Quantity);
        Assert.Equal(2, reserved.Count);

        var message = Assert.Single(Outbox());
        Assert.Equal("order.created", message.RoutingKey);
        var payload = JsonNode.Parse(message.Payload)!;
        Assert.Equal(order.Id.ToString(), payload["orderId"]!.ToString());
        Assert.Equal(_stock.FarmOwnerId.ToString(), payload["farmOwnerId"]!.ToString());
        Assert.Equal(2, payload["itemCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task Only_clients_place_orders()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service().CreateOrderAsync(Callers.Manager(), OrderOf((Guid.NewGuid(), 1)), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthenticatedException>(() =>
            Service().CreateOrderAsync(Caller.Anonymous, OrderOf((Guid.NewGuid(), 1)), CancellationToken.None));

        Assert.Empty(_stock.Reserved);
    }

    [Fact]
    public async Task Empty_or_oversized_orders_are_rejected_before_reserving()
    {
        var client = Callers.Client();

        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Service().CreateOrderAsync(client, OrderOf(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Service().CreateOrderAsync(client, OrderOf((Guid.NewGuid(), 1), (Guid.NewGuid(), 1), (Guid.NewGuid(), 1), (Guid.NewGuid(), 1)), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Service().CreateOrderAsync(client, OrderOf((Guid.NewGuid(), 0)), CancellationToken.None));

        Assert.Empty(_stock.Reserved);
    }

    [Fact]
    public async Task Failed_creation_releases_the_reserved_stock_and_saves_nothing()
    {
        _stock.ConfirmFailure = new InvalidOperationException("Farms is down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => PlaceOrderAsync(Callers.Client()));

        var orderId = Assert.Single(_stock.Reserved).OrderId;
        Assert.Equal([orderId], _stock.Released);

        await using var context = _database.CreateContext();
        Assert.Empty(context.Orders);
        Assert.Empty(context.OutboxMessages);
    }

    #endregion

    #region Status and cancellation

    [Fact]
    public async Task Farm_owner_moves_the_status_and_each_change_is_an_event()
    {
        var order = await PlaceOrderAsync(Callers.Client());
        var owner = Callers.Manager(_stock.FarmOwnerId);

        var confirmed = await Service().UpdateOrderStatusAsync(owner, new UpdateOrderStatusCommand(order.Id, OrderStatus.Confirmed, order.Version), CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, confirmed.Status);
        Assert.Equal(2, confirmed.Version);
        var payload = JsonNode.Parse(Outbox().Single(m => m.RoutingKey == "order.status.changed").Payload)!;
        Assert.Equal("pending", payload["previousStatus"]!.ToString());
        Assert.Equal("confirmed", payload["newStatus"]!.ToString());
    }

    [Fact]
    public async Task Other_managers_cannot_change_the_status()
    {
        var order = await PlaceOrderAsync(Callers.Client());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service().UpdateOrderStatusAsync(Callers.Manager(), new UpdateOrderStatusCommand(order.Id, OrderStatus.Confirmed, null), CancellationToken.None));
    }

    [Fact]
    public async Task Stale_version_is_a_conflict()
    {
        var order = await PlaceOrderAsync(Callers.Client());
        var owner = Callers.Manager(_stock.FarmOwnerId);
        await Service().UpdateOrderStatusAsync(owner, new UpdateOrderStatusCommand(order.Id, OrderStatus.Confirmed, order.Version), CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            Service().UpdateOrderStatusAsync(owner, new UpdateOrderStatusCommand(order.Id, OrderStatus.ReadyForPickup, order.Version), CancellationToken.None));
    }

    [Fact]
    public async Task Customer_cancellation_releases_stock_and_tells_the_farm()
    {
        var client = Callers.Client();
        var order = await PlaceOrderAsync(client);

        var cancelled = await Service().CancelOrderAsync(client, new CancelOrderCommand(order.Id, "Mudei de ideias", null), CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal([order.Id], _stock.Released);

        var payload = JsonNode.Parse(Outbox().Single(m => m.RoutingKey == "order.cancelled").Payload)!;
        Assert.True(payload["cancelledByCustomer"]!.GetValue<bool>());
        Assert.Equal("Mudei de ideias", payload["reason"]!.ToString());

        await using var context = _database.CreateContext();
        Assert.NotNull(context.Orders.Single().StockReleasedAt);
    }

    [Fact]
    public async Task Manager_cancelling_through_the_status_is_not_by_the_customer()
    {
        var order = await PlaceOrderAsync(Callers.Client());

        await Service().UpdateOrderStatusAsync(Callers.Manager(_stock.FarmOwnerId), new UpdateOrderStatusCommand(order.Id, OrderStatus.Cancelled, null), CancellationToken.None);

        var payload = JsonNode.Parse(Outbox().Single(m => m.RoutingKey == "order.cancelled").Payload)!;
        Assert.False(payload["cancelledByCustomer"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Cancellation_survives_a_farms_outage_and_the_release_is_retried()
    {
        var client = Callers.Client();
        var order = await PlaceOrderAsync(client);
        _stock.ReleaseFailure = new InvalidOperationException("Farms is down");

        var cancelled = await Service().CancelOrderAsync(client, new CancelOrderCommand(order.Id, null, null), CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        await using (var context = _database.CreateContext())
        {
            Assert.Null(context.Orders.Single().StockReleasedAt);
        }

        Assert.Equal(0, await Service().ReleasePendingStockAsync(CancellationToken.None));

        _stock.ReleaseFailure = null;
        Assert.Equal(1, await Service().ReleasePendingStockAsync(CancellationToken.None));
        Assert.Equal(0, await Service().ReleasePendingStockAsync(CancellationToken.None));
        Assert.Equal([order.Id], _stock.Released);
    }

    [Fact]
    public async Task Missing_reservation_counts_as_released()
    {
        var client = Callers.Client();
        var order = await PlaceOrderAsync(client);
        _stock.ReleaseFailure = new NotFoundException("No reservation.");

        await Service().CancelOrderAsync(client, new CancelOrderCommand(order.Id, null, null), CancellationToken.None);

        await using var context = _database.CreateContext();
        Assert.NotNull(context.Orders.Single().StockReleasedAt);
    }

    #endregion

    #region Reading

    [Fact]
    public async Task Clients_list_only_their_own_orders_and_managers_only_their_farms()
    {
        var alice = Callers.Client();
        var bob = Callers.Client();
        await PlaceOrderAsync(alice);
        await PlaceOrderAsync(alice);
        await PlaceOrderAsync(bob);

        var mine = await Service().ListMyOrdersAsync(alice, new ListMyOrdersQuery(PageRequest.Create(1, 10), null), CancellationToken.None);
        Assert.Equal(2, mine.TotalCount);

        var farm = await Service().ListOrdersAsync(Callers.Manager(_stock.FarmOwnerId), new ListOrdersQuery(PageRequest.Create(1, 10), null, null), CancellationToken.None);
        Assert.Equal(3, farm.TotalCount);

        var otherFarm = await Service().ListOrdersAsync(Callers.Manager(), new ListOrdersQuery(PageRequest.Create(1, 10), null, null), CancellationToken.None);
        Assert.Equal(0, otherFarm.TotalCount);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service().ListOrdersAsync(alice, new ListOrdersQuery(PageRequest.Create(1, 10), null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetOrderByIdAsync(Callers.Admin(), Guid.NewGuid(), CancellationToken.None));
    }

    #endregion
}
