using Orders.Service.Application;
using Orders.Service.Models;
using Orders.Service.Tests.Infrastructure;

namespace Orders.Service.Tests.Application;

public sealed class CallerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static Order OrderIn(OrderStatus status)
    {
        var now = DateTime.UtcNow;
        var order = Order.Create(Guid.NewGuid(), CustomerId, Guid.NewGuid(), OwnerId, null, now);

        switch (status)
        {
            case OrderStatus.Confirmed:
                order.Confirm(now);
                break;
            case OrderStatus.ReadyForPickup:
                order.Confirm(now);
                order.MarkReadyForPickup(now);
                break;
            case OrderStatus.PickedUp:
                order.Confirm(now);
                order.MarkReadyForPickup(now);
                order.MarkPickedUp(now);
                break;
            case OrderStatus.Cancelled:
                order.Cancel(null, true, now);
                break;
        }

        return order;
    }

    [Fact]
    public void Customer_owner_and_admin_can_view_but_others_cannot()
    {
        var order = OrderIn(OrderStatus.Pending);

        Assert.True(Callers.Client(CustomerId).CanView(order));
        Assert.True(Callers.Manager(OwnerId).CanView(order));
        Assert.True(Callers.Admin().CanView(order));
        Assert.False(Callers.Client().CanView(order));
        Assert.False(Callers.Manager().CanView(order));
    }

    [Fact]
    public void Only_the_farm_owner_or_admin_manage_the_order()
    {
        var order = OrderIn(OrderStatus.Pending);

        Assert.True(Callers.Manager(OwnerId).CanManage(order));
        Assert.True(Callers.Admin().CanManage(order));
        Assert.False(Callers.Client(CustomerId).CanManage(order));
        Assert.False(Callers.Manager().CanManage(order));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.ReadyForPickup, false)]
    [InlineData(OrderStatus.PickedUp, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Customer_cancels_only_until_the_order_is_ready(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, Callers.Client(CustomerId).CanCancel(OrderIn(status)));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.ReadyForPickup, true)]
    [InlineData(OrderStatus.PickedUp, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Farm_owner_cancels_until_pickup(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, Callers.Manager(OwnerId).CanCancel(OrderIn(status)));
    }

    [Fact]
    public void Strangers_cannot_cancel()
    {
        Assert.False(Callers.Client().CanCancel(OrderIn(OrderStatus.Pending)));
        Assert.False(Callers.Manager().CanCancel(OrderIn(OrderStatus.Pending)));
    }

    [Fact]
    public void Anonymous_callers_are_unauthenticated_not_forbidden()
    {
        var order = OrderIn(OrderStatus.Pending);

        Assert.Throws<UnauthenticatedException>(() => Caller.Anonymous.EnsureCanView(order));
        Assert.Throws<UnauthenticatedException>(() => Caller.Anonymous.EnsureCanCancel(order));
        Assert.Throws<ForbiddenException>(() => Callers.Client().EnsureCanView(order));
    }
}
