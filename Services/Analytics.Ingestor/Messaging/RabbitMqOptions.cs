using System.ComponentModel.DataAnnotations;

namespace Analytics.Ingestor.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string HostName { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string VirtualHost { get; set; } = "/";

    // Messages are acknowledged one by one, so this bounds how many are stored concurrently.
    [Range(1, 1000)]
    public ushort PrefetchCount { get; set; } = 50;

    // Wait before retrying a message that failed for a transient reason (MongoDB down).
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    // With the default delay, about five minutes before a message is dead-lettered.
    [Range(0, 10_000)]
    public int MaxRetries { get; set; } = 60;

    public bool EnableConsumer { get; set; } = true;
}
