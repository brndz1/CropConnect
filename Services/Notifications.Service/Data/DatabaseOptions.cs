using System.ComponentModel.DataAnnotations;

namespace Notifications.Service.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "NotificationsDb";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public bool ApplyMigrationsOnStartup { get; set; }
}
