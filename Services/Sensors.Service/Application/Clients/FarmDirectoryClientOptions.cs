using System.ComponentModel.DataAnnotations;

namespace Sensors.Service.Application.Clients;

public sealed class FarmDirectoryClientOptions
{
    public const string SectionName = "FarmsService";

    [Required]
    public string Url { get; set; } = "http://localhost:5002";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(typeof(TimeSpan), "00:00:00", "01:00:00")]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}
