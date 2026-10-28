using System.ComponentModel.DataAnnotations;

namespace Orders.Service.Messaging;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    public bool EnablePublisher { get; set; } = true;
}
