using System.ComponentModel.DataAnnotations;

namespace Farms.Service.Application;

public sealed class StockReservationOptions
{
    public const string SectionName = "StockReservations";

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), "00:00:05", "01:00:00")]
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(1);

    [Range(1, 1000)]
    public int SweepBatchSize { get; set; } = 100;

    [Range(1, 500)]
    public int MaxLinesPerOrder { get; set; } = 100;
    public bool EnableExpiryWorker { get; set; } = true;
}
