using System.ComponentModel.DataAnnotations;

namespace Notifications.Service.Application.Clients;

public sealed class UserDirectoryClientOptions
{
    public const string SectionName = "AuthService";

    [Required]
    public string Url { get; set; } = "http://localhost:5298";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(typeof(TimeSpan), "00:00:00", "01:00:00")]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}
