using Farms.Service.Application;
using Farms.Service.Models;
using Farms.Service.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Farms.Service.Tests.Application;

// Reserving stock (the first step of every order). Confirming and releasing lock rows with
// SELECT ... FOR UPDATE, which only PostgreSQL runs, so the end-to-end tests cover those.
public sealed class StockReservationServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private StockApplicationService Service() => new(
        _database.CreateContext(),
        TimeProvider.System,
        Options.Create(new StockReservationOptions { MaxLinesPerOrder = 3 }),
        NullLogger<StockApplicationService>.Instance);

    private async Task<Product> SeedProductAsync(int stock = 10, Farm? farm = null, bool available = true)
    {
        var now = DateTime.UtcNow;
        await using var context = _database.CreateContext();

        if (farm is null)
        {
            farm = Farm.Create(Guid.NewGuid(), "Quinta", null, "Braga", now);
            context.Farms.Add(farm);
        }
        else
        {
            context.Farms.Attach(farm);
        }

        var product = Product.Create(farm, "Batata", null, null, 1.20m, stock, ProductType.Product, null, now);
        if (!available)
        {
            product.Update(null, null, null, null, false, null, now);
        }

        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private async Task<int> StockOfAsync(Guid productId)
    {
        await using var context = _database.CreateContext();
        return (await context.Products.SingleAsync(p => p.Id == productId)).StockQuantity;
    }

    private static ReserveStockCommand Reserve(Guid orderId, params (Guid ProductId, int Quantity)[] lines) =>
        new(orderId, lines.Select(l => new StockLine(l.ProductId, l.Quantity)).ToList());

    [Fact]
    public async Task Reserving_takes_the_stock_and_returns_the_price_and_farm()
    {
        var product = await SeedProductAsync(stock: 10);

        var result = await Service().ReserveAsync(Reserve(Guid.NewGuid(), (product.Id, 3), (product.Id, 1)), CancellationToken.None);

        var line = Assert.Single(result.Lines);
        Assert.Equal(4, line.Quantity);
        Assert.Equal(1.20m, line.UnitPrice);
        Assert.Equal(product.FarmId, line.FarmId);
        Assert.False(result.AlreadyProcessed);
        Assert.Equal(6, await StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Insufficient_stock_reserves_nothing()
    {
        var plenty = await SeedProductAsync(stock: 10);
        var scarce = await SeedProductAsync(stock: 1, farm: plenty.Farm);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service().ReserveAsync(Reserve(Guid.NewGuid(), (plenty.Id, 2), (scarce.Id, 2)), CancellationToken.None));

        Assert.Equal(10, await StockOfAsync(plenty.Id));
        Assert.Equal(1, await StockOfAsync(scarce.Id));
    }

    [Fact]
    public async Task Retrying_the_same_reservation_does_not_take_stock_twice()
    {
        var product = await SeedProductAsync(stock: 10);
        var orderId = Guid.NewGuid();

        await Service().ReserveAsync(Reserve(orderId, (product.Id, 2)), CancellationToken.None);
        var retry = await Service().ReserveAsync(Reserve(orderId, (product.Id, 2)), CancellationToken.None);

        Assert.True(retry.AlreadyProcessed);
        Assert.Equal(8, await StockOfAsync(product.Id));

        await Assert.ThrowsAsync<ConflictException>(() =>
            Service().ReserveAsync(Reserve(orderId, (product.Id, 5)), CancellationToken.None));
    }

    [Fact]
    public async Task One_order_buys_from_a_single_farm()
    {
        var first = await SeedProductAsync();
        var second = await SeedProductAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service().ReserveAsync(Reserve(Guid.NewGuid(), (first.Id, 1), (second.Id, 1)), CancellationToken.None));
    }

    [Fact]
    public async Task Unavailable_or_unknown_products_cannot_be_reserved()
    {
        var hidden = await SeedProductAsync(available: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service().ReserveAsync(Reserve(Guid.NewGuid(), (hidden.Id, 1)), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service().ReserveAsync(Reserve(Guid.NewGuid(), (Guid.NewGuid(), 1)), CancellationToken.None));
    }

    [Fact]
    public async Task Empty_oversized_or_invalid_lines_are_rejected()
    {
        var product = await SeedProductAsync();

        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Service().ReserveAsync(Reserve(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Service().ReserveAsync(Reserve(Guid.NewGuid(), (product.Id, 1), (product.Id, 1), (product.Id, 1), (product.Id, 1)), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Service().ReserveAsync(Reserve(Guid.NewGuid(), (product.Id, 0)), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Service().ReserveAsync(Reserve(Guid.NewGuid(), (product.Id, int.MaxValue), (product.Id, 1)), CancellationToken.None));
    }
}
