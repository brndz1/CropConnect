using System.Net;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// RF04, RF16-RF18: thresholds, readings from RabbitMQ, alerts with the Open-Meteo context.
public sealed class SensorTests
{
    [Fact]
    public async Task Thresholds_are_read_by_managers_and_changed_only_by_administrators()
    {
        var manager = await RegisterAsync("Manager");

        var thresholds = await GetAsync("api/sensor-thresholds", manager.Token);
        var change = await PutAsync("api/sensor-thresholds/Humidity", new { minValue = 30, maxValue = 80 }, manager.Token);

        Assert.Equal(2, thresholds.Body!.AsArray().Count);
        Assert.Equal(HttpStatusCode.Forbidden, change.Status);
    }

    [Fact]
    public async Task Reading_published_to_rabbitmq_is_stored_for_the_farm_manager()
    {
        var setup = await Scenario.FarmWithProductAsync();

        await RabbitMqManagement.PublishReadingAsync(setup.FarmId, "e2e-sensor", "temperature", 21.5);

        var latest = await Eventually.GetAsync(
            () => GetAsync($"api/farms/{setup.FarmId}/readings/latest", setup.Manager.Token),
            response => response.Body?.AsArray().Count > 0,
            because: "the reading to be consumed");
        Assert.Equal("e2e-sensor", latest.Body![0]!["sensorId"]!.ToString());
    }

    [Fact]
    public async Task Low_humidity_raises_an_alert_with_the_weather_forecast()
    {
        var setup = await Scenario.FarmWithProductAsync(withCoordinates: true);

        await RabbitMqManagement.PublishReadingAsync(setup.FarmId, "e2e-sensor", "humidity", 19.5);

        var alerts = await Eventually.GetAsync(
            () => GetAsync($"api/farms/{setup.FarmId}/alerts", setup.Manager.Token),
            response => response.Body?["totalCount"]?.GetValue<int>() > 0,
            because: "the alert to be raised");
        var alert = alerts.Body!["items"]![0]!;
        Assert.Equal("Humidity", alert["type"]!.ToString());
        Assert.False(string.IsNullOrEmpty(alert["weatherContext"]?.ToString()), "Open-Meteo context for a farm with coordinates");

        var acknowledged = await PostAsync($"api/alerts/{alert["id"]}/acknowledge", token: setup.Manager.Token);
        Assert.NotNull(acknowledged.Body!["acknowledgedAt"]);
    }

    [Fact]
    public async Task Telemetry_is_hidden_from_clients_and_other_managers()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");
        var otherManager = await RegisterAsync("Manager");

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync($"api/farms/{setup.FarmId}/readings", client.Token)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync($"api/farms/{setup.FarmId}/alerts", otherManager.Token)).Status);
    }

    [Fact]
    public async Task Reading_of_an_unknown_farm_goes_to_the_dead_letter_queue()
    {
        const string queue = "sensors-service.readings.dead-letter";
        var before = await RabbitMqManagement.QueueMessageCountAsync(queue);

        await RabbitMqManagement.PublishReadingAsync(Guid.NewGuid().ToString(), "e2e-sensor", "humidity", 50);

        // The management API refreshes queue counters every few seconds.
        await Eventually.GetAsync(() => RabbitMqManagement.QueueMessageCountAsync(queue), count => count > before, because: "the reading to be dead-lettered");
    }
}
