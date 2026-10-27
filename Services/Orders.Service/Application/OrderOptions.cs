using System.ComponentModel.DataAnnotations;

namespace Orders.Service.Application;

public sealed class OrderOptions
{
    public const string SectionName = "Orders";

    [Range(1, 1000)]
    public int MaxLinesPerOrder { get; set; } = 100;

    [Range(typeof(TimeSpan), "00:00:05", "01:00:00")]
    public TimeSpan StockReleaseRetryInterval { get; set; } = TimeSpan.FromMinutes(1);

    [Range(1, 1000)]
    public int StockReleaseBatchSize { get; set; } = 50;

    public bool EnableStockReleaseWorker { get; set; } = true;
}
