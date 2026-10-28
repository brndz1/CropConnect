using System.Globalization;
using Microsoft.Extensions.Options;
using Sensors.Service.Application.Clients;
using Sensors.Service.Models;

namespace Sensors.Service.Application;

public interface IAlertWeatherContextService
{
    Task<string?> DescribeAsync(Alert alert, FarmOwnership farm, CancellationToken cancellationToken);
}

// Adds the rain forecast to low-humidity alerts, so the manager can tell whether to irrigate.
public sealed class AlertWeatherContextService : IAlertWeatherContextService
{
    private const double RainThresholdMillimetres = 1.0;

    private readonly IWeatherClient _weather;
    private readonly OpenMeteoOptions _options;
    private readonly ILogger<AlertWeatherContextService> _logger;

    public AlertWeatherContextService(
        IWeatherClient weather,
        IOptions<OpenMeteoOptions> options,
        ILogger<AlertWeatherContextService> logger)
    {
        _weather = weather;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> DescribeAsync(Alert alert, FarmOwnership farm, CancellationToken cancellationToken)
    {
        var isLowHumidity = alert.Type == SensorType.Humidity && alert.Value < alert.MinValue;

        if (!_options.Enabled || !isLowHumidity || farm.Latitude is not { } latitude || farm.Longitude is not { } longitude)
        {
            return null;
        }

        try
        {
            var forecast = await _weather.GetPrecipitationForecastAsync(latitude, longitude, cancellationToken);
            return Describe(forecast);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The weather is only context: the alert is raised without it (RNF03).
            _logger.LogWarning(exception, "Could not get the weather forecast for farm {FarmId}", farm.FarmId);
            return null;
        }
    }

    private static string Describe(PrecipitationForecast forecast)
    {
        var probability = forecast.MaxProbabilityPercent is { } percent ? $" (up to {percent}% chance)" : string.Empty;

        return forecast.TotalMillimetres >= RainThresholdMillimetres
            ? string.Create(CultureInfo.InvariantCulture,
                $"Rain forecast: {forecast.TotalMillimetres:0.#} mm in the next {forecast.Hours}h{probability}; irrigation may not be needed.")
            : string.Create(CultureInfo.InvariantCulture,
                $"No significant rain forecast in the next {forecast.Hours}h ({forecast.TotalMillimetres:0.#} mm{probability}); consider irrigating.");
    }
}
