using System.ComponentModel.DataAnnotations;

namespace Sensors.Service.Application.Clients;

public sealed class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    // Open-Meteo is free for non-commercial use and needs no API key.
    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.open-meteo.com/";

    // Bounds the whole call, retries included: the alert is saved without weather context after it.
    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    [Range(1, 72)]
    public int ForecastHours { get; set; } = 24;

    public bool Enabled { get; set; } = true;
}
