using System.ComponentModel.DataAnnotations;

namespace CropConnect.ApiGateway.Infrastructure;

// gRPC addresses of the internal services the gateway translates REST calls to.
public sealed class DownstreamOptions
{
    public const string SectionName = "Downstream";

    [Required]
    [Url]
    public string Auth { get; set; } = "http://localhost:5298";

    [Required]
    [Url]
    public string Users { get; set; } = "http://localhost:5001";

    [Required]
    [Url]
    public string Farms { get; set; } = "http://localhost:5002";

    [Required]
    [Url]
    public string Orders { get; set; } = "http://localhost:5003";

    [Required]
    [Url]
    public string Sensors { get; set; } = "http://localhost:5004";

    [Required]
    [Url]
    public string Notifications { get; set; } = "http://localhost:5005";

    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}
