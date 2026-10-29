using System.Net;

namespace CropConnect.EndToEndTests.Infrastructure;

// Builds the data most tests start from: a manager's farm with a product, and a client to buy it.
public static class Scenario
{
    public sealed record FarmWithProduct(TestUser Manager, string FarmId, string FarmName, string ProductId);

    public static async Task<FarmWithProduct> FarmWithProductAsync(int stock = 20, bool withCoordinates = false)
    {
        var manager = await GatewayClient.RegisterAsync("Manager");
        var name = $"Quinta E2E {Guid.NewGuid():N}"[..20];

        object body = withCoordinates
            ? new { name, description = "e2e", location = "Braga", latitude = 41.55, longitude = -8.42 }
            : new { name, description = "e2e", location = "Braga" };
        var farm = await GatewayClient.PostAsync("api/farms", body, manager.Token);
        Assert.Equal(HttpStatusCode.Created, farm.Status);

        var product = await GatewayClient.PostAsync(
            "api/products",
            new { farmId = farm.Id, name = "Tomate", description = "e2e", unit = "kg", price = 2.5, stockQuantity = stock, type = "Product" },
            manager.Token);
        Assert.Equal(HttpStatusCode.Created, product.Status);

        return new FarmWithProduct(manager, farm.Id, name, product.Id);
    }

    public static async Task<string> PlaceOrderAsync(TestUser client, string productId, int quantity = 1)
    {
        var order = await GatewayClient.PostAsync("api/orders", new { items = new[] { new { productId, quantity } } }, client.Token);
        Assert.True(order.Status == HttpStatusCode.Created, $"Create order: {(int)order.Status} {order.Body}");
        return order.Id;
    }

    public static async Task<int> StockAsync(string productId) =>
        (await GatewayClient.GetAsync($"api/products/{productId}")).Body!["stockQuantity"]!.GetValue<int>();
}
