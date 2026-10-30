using Orders.Service.Models;

namespace Orders.Service.Tests.Models;

public sealed class OrderTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Order NewOrder(OrderStatus status = OrderStatus.Pending)
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Sábado  ", Now);

        if (status is OrderStatus.Confirmed or OrderStatus.ReadyForPickup or OrderStatus.PickedUp)
        {
            order.Confirm(Now);
        }

        if (status is OrderStatus.ReadyForPickup or OrderStatus.PickedUp)
        {
            order.MarkReadyForPickup(Now);
        }

        if (status is OrderStatus.PickedUp)
        {
            order.MarkPickedUp(Now);
        }

        if (status is OrderStatus.Cancelled)
        {
            order.Cancel("motivo", isManagerOrAdmin: true, Now);
        }

        return order;
    }

    private static OrderItem Item(Order order, int quantity, decimal unitPrice) =>
        OrderItem.Create(order.Id, Guid.NewGuid(), "Alface", quantity, unitPrice);

    [Fact]
    public void New_order_is_pending_at_version_one_with_trimmed_notes()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(1, order.Version);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal("Sábado", order.Notes);
    }

    [Fact]
    public void Order_needs_a_customer_farm_and_owner()
    {
        Assert.Throws<InvalidInputException>(() => Order.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), null, Now));
        Assert.Throws<InvalidInputException>(() => Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), null, Now));
        Assert.Throws<InvalidInputException>(() => Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, null, Now));
    }

    [Fact]
    public void Adding_items_accumulates_the_total()
    {
        var order = NewOrder();

        order.AddItem(Item(order, 4, 2.50m));
        order.AddItem(Item(order, 3, 1.15m));

        Assert.Equal(13.45m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void Items_cannot_be_added_once_the_order_left_pending()
    {
        var order = NewOrder(OrderStatus.Confirmed);

        Assert.Throws<BusinessRuleException>(() => order.AddItem(Item(order, 1, 1m)));
    }

    [Fact]
    public void Order_moves_forward_through_its_life_cycle_bumping_the_version()
    {
        var order = NewOrder();
        var later = Now.AddHours(1);

        order.Confirm(later);
        order.MarkReadyForPickup(later);
        order.MarkPickedUp(later);

        Assert.Equal(OrderStatus.PickedUp, order.Status);
        Assert.Equal(4, order.Version);
        Assert.Equal(later, order.UpdatedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.ReadyForPickup)]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.Cancelled)]
    public void Only_pending_orders_can_be_confirmed(OrderStatus status)
    {
        var order = NewOrder(status);

        Assert.Throws<BusinessRuleException>(() => order.Confirm(Now));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.Cancelled)]
    public void Only_confirmed_orders_can_be_made_ready(OrderStatus status)
    {
        var order = NewOrder(status);

        Assert.Throws<BusinessRuleException>(() => order.MarkReadyForPickup(Now));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    public void Only_ready_orders_can_be_picked_up(OrderStatus status)
    {
        var order = NewOrder(status);

        Assert.Throws<BusinessRuleException>(() => order.MarkPickedUp(Now));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.ReadyForPickup, true)]
    public void Order_can_be_cancelled_before_pickup(OrderStatus status, bool isManagerOrAdmin)
    {
        var order = NewOrder(status);

        order.Cancel("  Sem stock  ", isManagerOrAdmin, Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Sem stock", order.CancellationReason);
    }

    [Fact]
    public void Customers_cannot_cancel_an_order_ready_for_pickup()
    {
        var order = NewOrder(OrderStatus.ReadyForPickup);

        Assert.Throws<BusinessRuleException>(() => order.Cancel(null, isManagerOrAdmin: false, Now));
    }

    [Theory]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.Cancelled)]
    public void Finished_orders_cannot_be_cancelled(OrderStatus status)
    {
        var order = NewOrder(status);

        Assert.Throws<BusinessRuleException>(() => order.Cancel(null, isManagerOrAdmin: true, Now));
    }

    [Fact]
    public void Stock_release_is_recorded_once_and_only_for_cancelled_orders()
    {
        var pending = NewOrder();
        Assert.Throws<BusinessRuleException>(() => pending.MarkStockReleased(Now));

        var cancelled = NewOrder(OrderStatus.Cancelled);
        cancelled.MarkStockReleased(Now);
        cancelled.MarkStockReleased(Now.AddHours(1));

        Assert.Equal(Now, cancelled.StockReleasedAt);
    }

    [Fact]
    public void Stale_version_is_a_concurrency_conflict()
    {
        var order = NewOrder(OrderStatus.Confirmed);

        order.EnsureVersion(null);
        order.EnsureVersion(2);
        Assert.Throws<ConcurrencyException>(() => order.EnsureVersion(1));
    }
}
