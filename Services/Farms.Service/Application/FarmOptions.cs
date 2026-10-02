using System.ComponentModel.DataAnnotations;

namespace Farms.Service.Application;

public sealed class FarmOptions
{
    public const string SectionName = "Farms";

    [Range(1, 1000)]
    public int MaxFarmsPerOwner { get; set; } = 10;
}
