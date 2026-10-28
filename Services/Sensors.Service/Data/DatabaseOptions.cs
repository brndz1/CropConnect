using System.ComponentModel.DataAnnotations;

namespace Sensors.Service.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "SensorsDb";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public bool ApplyMigrationsOnStartup { get; set; }
}
