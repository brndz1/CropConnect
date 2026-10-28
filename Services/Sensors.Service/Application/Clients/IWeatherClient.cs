namespace Sensors.Service.Application.Clients;

public sealed record PrecipitationForecast(int Hours, double TotalMillimetres, int? MaxProbabilityPercent);

public interface IWeatherClient
{
    Task<PrecipitationForecast> GetPrecipitationForecastAsync(double latitude, double longitude, CancellationToken cancellationToken);
}
