using System.ComponentModel.DataAnnotations;

namespace Sensors.Service.Application;

public sealed class SensorReadingOptions
{
    public const string SectionName = "SensorReadings";

    // Tolerates clock drift between the sensors and this service.
    [Range(typeof(TimeSpan), "00:00:00", "01:00:00")]
    public TimeSpan MaxClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    // A farm gets at most one alert per sensor type in this window, instead of one per reading.
    [Range(typeof(TimeSpan), "00:00:00", "1.00:00:00")]
    public TimeSpan AlertCooldown { get; set; } = TimeSpan.FromMinutes(30);
}
