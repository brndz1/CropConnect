using System.ComponentModel.DataAnnotations;

namespace Auth.Service.Security;

public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";

    [Required]
    [MinLength(32)]
    public string ApiKey { get; set; } = string.Empty;
}
