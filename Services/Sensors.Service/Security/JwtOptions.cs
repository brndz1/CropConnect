using System.ComponentModel.DataAnnotations;

namespace Sensors.Service.Security;

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
}
