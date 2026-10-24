using System.ComponentModel.DataAnnotations;

namespace Orders.Service.Application;

public sealed class OrderOptions
{
    public const string SectionName = "Orders";

    [Range(1, 1000)]
    public int MaxLinesPerOrder { get; set; } = 100;
}
