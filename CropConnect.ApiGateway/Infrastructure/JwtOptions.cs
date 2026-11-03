using System.ComponentModel.DataAnnotations;

namespace CropConnect.ApiGateway.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "JwtSettings";

    // The RSA public key that verifies the RS256 signature (see JwtSigningKeys); only Auth.Service has the private one.
    [Required]
    public string PublicKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    // How long the gateway trusts Auth.Service's answer that a user is still active.
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan ActiveUserCacheDuration { get; set; } = TimeSpan.FromSeconds(15);
}
