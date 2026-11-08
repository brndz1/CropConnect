using System.ComponentModel.DataAnnotations;

namespace Analytics.Ingestor.Storage;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    // ConnectionStrings:AnalyticsDb wins when set (Aspire, Compose, Kubernetes).
    public const string ConnectionStringName = "AnalyticsDb";

    [Required]
    public string ConnectionString { get; set; } = "mongodb://localhost:27018";

    [Required]
    public string Database { get; set; } = "cropconnect_raw";

    // How long MongoDB keeps the readings before deleting them by itself; empty keeps them for good.
    // Once Databricks has copied them to Bronze they are no longer needed here.
    [Range(typeof(TimeSpan), "1.00:00:00", "3650.00:00:00")]
    public TimeSpan? ReadingsRetention { get; set; }
}
