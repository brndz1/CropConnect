using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Sensors.Service.Application.Clients;

public sealed class OpenMeteoClient : IWeatherClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenMeteoOptions _options;

    public OpenMeteoClient(HttpClient httpClient, IOptions<OpenMeteoOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<PrecipitationForecast> GetPrecipitationForecastAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={latitude}&longitude={longitude}&hourly=precipitation,precipitation_probability&forecast_hours={_options.ForecastHours}&timezone=UTC");

        var response = await _httpClient.GetFromJsonAsync<ForecastResponse>(query, cancellationToken)
                       ?? throw new InvalidOperationException("Open-Meteo returned an empty response.");

        var precipitation = response.Hourly?.Precipitation ?? [];
        var probability = response.Hourly?.PrecipitationProbability ?? [];

        return new PrecipitationForecast(
            _options.ForecastHours,
            precipitation.Sum(value => value ?? 0),
            probability.Max());
    }

    private sealed record ForecastResponse(
        [property: JsonPropertyName("hourly")] HourlyForecast? Hourly);

    private sealed record HourlyForecast(
        [property: JsonPropertyName("precipitation")] double?[]? Precipitation,
        [property: JsonPropertyName("precipitation_probability")] int?[]? PrecipitationProbability);
}
