using System.ComponentModel.DataAnnotations;

namespace Orders.Service.Application.Clients;

public sealed class FarmStockClientOptions
{
    public const string SectionName = "FarmsService";

    [Required]
    public string Url { get; set; } = "http://localhost:5002";

    [Required]
    public string ApiKey { get; set; } = string.Empty;
}
