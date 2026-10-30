using Orders.Service.Application;
using Orders.Service.Models;
using Orders.Service.Tests.Infrastructure;

namespace Orders.Service.Tests.Application;

public sealed class ReviewApplicationServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();

    public void Dispose() => _database.Dispose();

    private ReviewApplicationService Service() => new(_database.CreateContext(), TimeProvider.System);

    private async Task<Guid> SeedOrderAsync(OrderStatus status)
    {
        var now = DateTime.UtcNow;
        var order = Order.Create(Guid.NewGuid(), _customerId, _farmId, Guid.NewGuid(), null, now);
        order.AddItem(OrderItem.Create(order.Id, Guid.NewGuid(), "Alface", 1, 1m));

        if (status != OrderStatus.Pending)
        {
            order.Confirm(now);
        }

        if (status == OrderStatus.PickedUp)
        {
            order.MarkReadyForPickup(now);
            order.MarkPickedUp(now);
        }

        await using var context = _database.CreateContext();
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order.Id;
    }

    [Fact]
    public async Task Customer_reviews_a_picked_up_order_once()
    {
        var orderId = await SeedOrderAsync(OrderStatus.PickedUp);
        var customer = Callers.Client(_customerId);

        var review = await Service().CreateReviewAsync(customer, new CreateReviewCommand(orderId, 5, " Excelente "), CancellationToken.None);

        Assert.Equal(_farmId, review.FarmId);
        Assert.Equal("Excelente", review.Comment);
        await Assert.ThrowsAsync<ConflictException>(() =>
            Service().CreateReviewAsync(customer, new CreateReviewCommand(orderId, 4, null), CancellationToken.None));

        var farmReviews = await Service().ListFarmReviewsAsync(new ListFarmReviewsQuery(_farmId, PageRequest.Create(1, 10)), CancellationToken.None);
        Assert.Equal(1, farmReviews.TotalCount);
    }

    [Fact]
    public async Task Orders_not_yet_picked_up_cannot_be_reviewed()
    {
        var orderId = await SeedOrderAsync(OrderStatus.Confirmed);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service().CreateReviewAsync(Callers.Client(_customerId), new CreateReviewCommand(orderId, 5, null), CancellationToken.None));
    }

    [Fact]
    public async Task Only_the_customer_reviews_the_order()
    {
        var orderId = await SeedOrderAsync(OrderStatus.PickedUp);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service().CreateReviewAsync(Callers.Client(), new CreateReviewCommand(orderId, 5, null), CancellationToken.None));
    }
}
