using System.ComponentModel.DataAnnotations;

namespace CropConnect.ApiGateway.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "JwtSettings";

    [Required]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;
}
