using System.ComponentModel.DataAnnotations;

namespace Analytics.Ingestor.Catalog;

public sealed class CatalogSnapshotOptions
{
    public const string SectionName = "CatalogSnapshots";

    public bool Enabled { get; set; } = true;

    [Required]
    public string FarmsServiceUrl { get; set; } = "http://localhost:5002";

    // A snapshot a day is enough to follow the prices and the farms over time (slowly changing dimensions).
    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromDays(1);

    // Wait before trying again when Farms is not reachable.
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
}
