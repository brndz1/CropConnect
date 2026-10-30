using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sensors.Service.Application;
using Sensors.Service.Application.Clients;
using Sensors.Service.Application.Events;
using Sensors.Service.Models;
using Sensors.Service.Tests.Infrastructure;

namespace Sensors.Service.Tests.Application;

// RF16-RF17 and RF21 against the seeded thresholds: humidity 30-80 %, temperature 5-35 °C.
public sealed class ReadingIngestionServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly FakeFarms _farms = new();
    private readonly FakeWeather _weather = new();

    public void Dispose()
    {
        _cache.Dispose();
        _database.Dispose();
    }

    private ReadingIngestionService Service()
    {
        var context = _database.CreateContext();
        return new ReadingIngestionService(
            context,
            _farms,
            new ThresholdApplicationService(context, _cache, TimeProvider.System, NullLogger<ThresholdApplicationService>.Instance),
            new OutboxAlertEventPublisher(context),
            new AlertWeatherContextService(_weather, Options.Create(new OpenMeteoOptions()), NullLogger<AlertWeatherContextService>.Instance),
            TimeProvider.System,
            Options.Create(new SensorReadingOptions()),
            NullLogger<ReadingIngestionService>.Instance);
    }

    private RecordReadingCommand ReadingOf(SensorType type, double value, Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), _farms.Farm.FarmId, "s-01", type, value, DateTime.UtcNow);

    [Fact]
    public async Task Normal_reading_is_stored_without_an_alert()
    {
        var result = await Service().RecordAsync(ReadingOf(SensorType.Humidity, 55), CancellationToken.None);

        Assert.Null(result.Alert);
        using var context = _database.CreateContext();
        Assert.Single(context.SensorReadings);
        Assert.Empty(context.OutboxMessages);
    }

    [Fact]
    public async Task Out_of_range_reading_raises_an_alert_for_the_owner_with_the_forecast()
    {
        var result = await Service().RecordAsync(ReadingOf(SensorType.Humidity, 18.4), CancellationToken.None);

        var alert = Assert.IsType<Alert>(result.Alert);
        Assert.Equal(_farms.Farm.OwnerId, alert.FarmOwnerId);
        Assert.Contains("consider irrigating", alert.WeatherContext);

        using var context = _database.CreateContext();
        var message = Assert.Single(context.OutboxMessages);
        Assert.Equal("sensor.alert.raised", message.RoutingKey);
        var payload = JsonNode.Parse(message.Payload)!;
        Assert.Equal(_farms.Farm.Name, payload["farmName"]!.ToString());
        Assert.Equal("%", payload["unit"]!.ToString());
    }

    [Fact]
    public async Task Weather_outage_does_not_block_the_alert()
    {
        _weather.Failure = new HttpRequestException("Open-Meteo is down");

        var result = await Service().RecordAsync(ReadingOf(SensorType.Humidity, 10), CancellationToken.None);

        Assert.NotNull(result.Alert);
        Assert.Null(result.Alert!.WeatherContext);
    }

    [Fact]
    public async Task High_temperature_alert_has_no_rain_forecast()
    {
        var result = await Service().RecordAsync(ReadingOf(SensorType.Temperature, 41), CancellationToken.None);

        Assert.NotNull(result.Alert);
        Assert.Null(result.Alert!.WeatherContext);
        Assert.Equal(0, _weather.Calls);
    }

    [Fact]
    public async Task Repeated_breaches_within_the_cooldown_raise_one_alert_per_type()
    {
        await Service().RecordAsync(ReadingOf(SensorType.Humidity, 10), CancellationToken.None);
        var second = await Service().RecordAsync(ReadingOf(SensorType.Humidity, 12), CancellationToken.None);
        var temperature = await Service().RecordAsync(ReadingOf(SensorType.Temperature, 41), CancellationToken.None);

        Assert.Null(second.Alert);
        Assert.NotNull(temperature.Alert);
        using var context = _database.CreateContext();
        Assert.Equal(3, context.SensorReadings.Count());
        Assert.Equal(2, context.Alerts.Count());
    }

    [Fact]
    public async Task Redelivered_message_is_recorded_once()
    {
        var messageId = Guid.NewGuid();

        var first = await Service().RecordAsync(ReadingOf(SensorType.Humidity, 10, messageId), CancellationToken.None);
        var again = await Service().RecordAsync(ReadingOf(SensorType.Humidity, 10, messageId), CancellationToken.None);

        Assert.False(first.AlreadyProcessed);
        Assert.True(again.AlreadyProcessed);
        Assert.Equal(first.Alert!.Id, again.Alert!.Id);
        using var context = _database.CreateContext();
        Assert.Single(context.SensorReadings);
        Assert.Single(context.OutboxMessages);
    }

    [Fact]
    public async Task Readings_of_inactive_farms_are_refused()
    {
        _farms.Farm = _farms.Farm with { IsActive = false };

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service().RecordAsync(ReadingOf(SensorType.Humidity, 50), CancellationToken.None));
    }

    [Fact]
    public async Task Updated_threshold_applies_to_the_next_reading()
    {
        var admin = new Caller(Guid.NewGuid(), Role.Admin);
        await using (var context = _database.CreateContext())
        {
            var thresholds = new ThresholdApplicationService(context, _cache, TimeProvider.System, NullLogger<ThresholdApplicationService>.Instance);
            await thresholds.GetForEvaluationAsync(SensorType.Humidity, CancellationToken.None);
            await thresholds.UpdateAsync(admin, new UpdateThresholdCommand(SensorType.Humidity, 60, 90, null), CancellationToken.None);
        }

        var result = await Service().RecordAsync(ReadingOf(SensorType.Humidity, 55), CancellationToken.None);

        Assert.NotNull(result.Alert);
    }

    [Fact]
    public async Task Only_admins_change_thresholds()
    {
        await using var context = _database.CreateContext();
        var thresholds = new ThresholdApplicationService(context, _cache, TimeProvider.System, NullLogger<ThresholdApplicationService>.Instance);
        var command = new UpdateThresholdCommand(SensorType.Humidity, 20, 90, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => thresholds.UpdateAsync(new Caller(Guid.NewGuid(), Role.Manager), command, CancellationToken.None));
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            thresholds.UpdateAsync(new Caller(Guid.NewGuid(), Role.Admin), command with { ExpectedVersion = 7 }, CancellationToken.None));
    }

    [Fact]
    public void Telemetry_is_only_for_the_farm_manager()
    {
        var ownerId = Guid.NewGuid();

        Assert.True(new Caller(ownerId, Role.Manager).CanReadTelemetry(ownerId));
        Assert.False(new Caller(Guid.NewGuid(), Role.Admin).CanReadTelemetry(ownerId));
        Assert.False(new Caller(Guid.NewGuid(), Role.Manager).CanReadTelemetry(ownerId));
    }

    private sealed class FakeFarms : IFarmDirectoryClient
    {
        public FarmOwnership Farm { get; set; } = new(Guid.NewGuid(), Guid.NewGuid(), "Quinta do Vale", true, 41.55, -8.42);

        public Task<FarmOwnership> GetOwnershipAsync(Guid farmId, CancellationToken cancellationToken) =>
            farmId == Farm.FarmId ? Task.FromResult(Farm) : throw new NotFoundException("Farm not found.");
    }

    private sealed class FakeWeather : IWeatherClient
    {
        public Exception? Failure { get; set; }

        public int Calls { get; private set; }

        public Task<PrecipitationForecast> GetPrecipitationForecastAsync(double latitude, double longitude, CancellationToken cancellationToken)
        {
            Calls++;
            return Failure is null ? Task.FromResult(new PrecipitationForecast(24, 0.2, 10)) : throw Failure;
        }
    }
}
