using System.Net;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// RF10-RF15: orders, stock reservation, the order life cycle and reviews.
public sealed class OrderTests
{
    [Fact]
    public async Task Order_reserves_stock_and_starts_pending()
    {
        var setup = await Scenario.FarmWithProductAsync(stock: 20);
        var client = await RegisterAsync("Client");

        var order = await PostAsync("api/orders", new { items = new[] { new { productId = setup.ProductId, quantity = 4 } }, notes = "Sábado" }, client.Token);

        Assert.Equal(HttpStatusCode.Created, order.Status);
        Assert.Equal("Pending", order.Text("status"));
        Assert.Equal(10m, order.Body!["totalAmount"]!.GetValue<decimal>());
        Assert.Equal(16, await Scenario.StockAsync(setup.ProductId));
    }

    [Fact]
    public async Task Order_beyond_available_stock_is_rejected()
    {
        var setup = await Scenario.FarmWithProductAsync(stock: 2);
        var client = await RegisterAsync("Client");

        var order = await PostAsync("api/orders", new { items = new[] { new { productId = setup.ProductId, quantity = 3 } } }, client.Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, order.Status);
        Assert.Equal(2, await Scenario.StockAsync(setup.ProductId));
    }

    [Fact]
    public async Task Managers_cannot_place_orders()
    {
        var setup = await Scenario.FarmWithProductAsync();

        var order = await PostAsync("api/orders", new { items = new[] { new { productId = setup.ProductId, quantity = 1 } } }, setup.Manager.Token);

        Assert.Equal(HttpStatusCode.Forbidden, order.Status);
    }

    [Fact]
    public async Task Manager_moves_order_through_its_life_cycle_and_client_reviews_it()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");
        var orderId = await Scenario.PlaceOrderAsync(client, setup.ProductId);

        Assert.Contains((await GetAsync("api/orders", setup.Manager.Token)).Body!["items"]!.AsArray(), o => o!["id"]!.ToString() == orderId);

        foreach (var status in new[] { "Confirmed", "ReadyForPickup", "PickedUp" })
        {
            var moved = await PutAsync($"api/orders/{orderId}/status", new { status }, setup.Manager.Token);
            Assert.Equal(status, moved.Text("status"));
        }

        var backwards = await PutAsync($"api/orders/{orderId}/status", new { status = "Confirmed" }, setup.Manager.Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, backwards.Status);

        var review = await PostAsync($"api/orders/{orderId}/review", new { rating = 5, comment = "Excelente" }, client.Token);
        Assert.Equal(HttpStatusCode.Created, review.Status);
        Assert.Equal(1, (await GetAsync($"api/farms/{setup.FarmId}/reviews")).Body!["totalCount"]!.GetValue<int>());

        var history = await GetAsync("api/orders/mine?status=PickedUp", client.Token);
        Assert.Single(history.Body!["items"]!.AsArray());
    }

    [Fact]
    public async Task Client_cancels_a_pending_order_and_stock_returns()
    {
        var setup = await Scenario.FarmWithProductAsync(stock: 10);
        var client = await RegisterAsync("Client");
        var orderId = await Scenario.PlaceOrderAsync(client, setup.ProductId, quantity: 3);

        var cancelled = await PostAsync($"api/orders/{orderId}/cancel", new { reason = "Engano" }, client.Token);

        Assert.Equal("Cancelled", cancelled.Text("status"));
        Assert.Equal("Engano", cancelled.Text("cancellationReason"));
        await Eventually.GetAsync(() => Scenario.StockAsync(setup.ProductId), stock => stock == 10, because: "stock returned to the catalog");
    }

    [Fact]
    public async Task Client_cannot_cancel_once_the_order_is_ready_for_pickup()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");
        var orderId = await Scenario.PlaceOrderAsync(client, setup.ProductId);
        await PutAsync($"api/orders/{orderId}/status", new { status = "Confirmed" }, setup.Manager.Token);
        await PutAsync($"api/orders/{orderId}/status", new { status = "ReadyForPickup" }, setup.Manager.Token);

        var cancel = await PostAsync($"api/orders/{orderId}/cancel", new { reason = "tarde" }, client.Token);

        Assert.Equal(HttpStatusCode.Forbidden, cancel.Status);
    }

    [Fact]
    public async Task Clients_only_see_their_own_orders()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var owner = await RegisterAsync("Client");
        var other = await RegisterAsync("Client");
        var orderId = await Scenario.PlaceOrderAsync(owner, setup.ProductId);

        var response = await GetAsync($"api/orders/{orderId}", other.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }
}
