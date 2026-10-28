using System.ComponentModel.DataAnnotations;

namespace Sensors.Service.Security;

public sealed class JwtOptions
{
    public const string SectionName = "JwtSettings";

    [Required]
    [MinLength(16)]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;
}
