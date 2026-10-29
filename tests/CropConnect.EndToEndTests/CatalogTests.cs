using System.Net;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// RF04-RF09: farms, pickup schedules, products and seasonal baskets.
public sealed class CatalogTests
{
    [Fact]
    public async Task Manager_registers_a_farm_with_pickup_schedule_visible_without_login()
    {
        var setup = await Scenario.FarmWithProductAsync();

        var schedule = await PostAsync($"api/farms/{setup.FarmId}/pickup-schedules", new { dayOfWeek = "Saturday", openTime = "09:00", closeTime = "13:00" }, setup.Manager.Token);
        var farm = await GetAsync($"api/farms/{setup.FarmId}");

        Assert.Equal(HttpStatusCode.Created, schedule.Status);
        Assert.Equal(setup.Manager.Id, farm.Text("ownerId"));
        Assert.Equal("Saturday", farm.Body!["pickupSchedules"]![0]!["dayOfWeek"]!.ToString());
    }

    [Fact]
    public async Task Catalog_is_public_and_keeps_decimal_prices()
    {
        var setup = await Scenario.FarmWithProductAsync();

        var catalog = await GetAsync($"api/products?farmId={setup.FarmId}");

        Assert.Equal(HttpStatusCode.OK, catalog.Status);
        Assert.Equal(2.5m, catalog.Body!["items"]![0]!["price"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Clients_cannot_register_farms()
    {
        var client = await RegisterAsync("Client");

        var response = await PostAsync("api/farms", new { name = "x", location = "y" }, client.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }

    [Fact]
    public async Task Manager_adjusts_stock()
    {
        var setup = await Scenario.FarmWithProductAsync(stock: 20);

        var adjusted = await PostAsync($"api/products/{setup.ProductId}/stock-adjustments", new { delta = 5 }, setup.Manager.Token);

        Assert.Equal(25, adjusted.Body!["stockQuantity"]!.GetValue<int>());
    }

    [Fact]
    public async Task Administrator_deactivates_a_farm_and_it_leaves_the_catalog()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var admin = await AdminTokenAsync();

        var deactivated = await PutAsync($"api/farms/{setup.FarmId}/active", new { isActive = false }, admin);
        var catalog = await GetAsync($"api/products?farmId={setup.FarmId}");

        Assert.False(deactivated.Body!["isActive"]!.GetValue<bool>());
        Assert.Equal(0, catalog.Body!["totalCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task Surprise_basket_contents_are_only_shown_to_its_manager_and_administrators()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");
        var otherManager = await RegisterAsync("Manager");
        var admin = await AdminTokenAsync();

        var basket = await PostAsync(
            "api/products",
            new { farmId = setup.FarmId, name = "Cabaz surpresa", unit = "cabaz", price = 10, stockQuantity = 5, type = "SurpriseBasket", availableUntil = DateTime.UtcNow.AddDays(7), contents = "Abóbora e maçãs" },
            setup.Manager.Token);

        Assert.Equal("Abóbora e maçãs", basket.Text("contents"));
        Assert.Null((await GetAsync($"api/products/{basket.Id}")).Body!["contents"]);
        Assert.Null((await GetAsync($"api/products/{basket.Id}", client.Token)).Body!["contents"]);
        Assert.Null((await GetAsync($"api/products/{basket.Id}", otherManager.Token)).Body!["contents"]);
        Assert.Equal("Abóbora e maçãs", (await GetAsync($"api/products/{basket.Id}", admin)).Text("contents"));
        Assert.All((await GetAsync($"api/products?farmId={setup.FarmId}", client.Token)).Body!["items"]!.AsArray(), p => Assert.Null(p!["contents"]));
    }

    [Fact]
    public async Task Pre_defined_basket_contents_are_public()
    {
        var setup = await Scenario.FarmWithProductAsync();

        var basket = await PostAsync(
            "api/products",
            new { farmId = setup.FarmId, name = "Cabaz da semana", unit = "cabaz", price = 12, stockQuantity = 5, type = "PreDefinedBasket", availableUntil = DateTime.UtcNow.AddDays(7), contents = "Batata, alface, ovos" },
            setup.Manager.Token);

        Assert.Equal("Batata, alface, ovos", (await GetAsync($"api/products/{basket.Id}")).Text("contents"));
    }

    [Fact]
    public async Task Plain_products_cannot_have_contents()
    {
        var setup = await Scenario.FarmWithProductAsync();

        var response = await PostAsync(
            "api/products",
            new { farmId = setup.FarmId, name = "Cenoura", unit = "kg", price = 1, stockQuantity = 5, type = "Product", contents = "x" },
            setup.Manager.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }
}
