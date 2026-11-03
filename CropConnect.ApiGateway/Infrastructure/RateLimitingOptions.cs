using System.ComponentModel.DataAnnotations;

namespace CropConnect.ApiGateway.Infrastructure;

// Limits per fixed window. The counters live in Redis (ConnectionStrings:redis), so every replica of the gateway
// counts the same requests; without Redis each replica counts on its own.
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    // Every API call, per signed-in user or, without a token, per client IP.
    [Range(1, 100_000)]
    public int PermitLimit { get; set; } = 300;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    // Login attempts per account, against guessing its password.
    [Range(1, 1_000)]
    public int LoginPermitLimit { get; set; } = 10;

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan LoginWindow { get; set; } = TimeSpan.FromMinutes(15);
}
