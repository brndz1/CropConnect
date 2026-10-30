using System.ComponentModel.DataAnnotations;

namespace Notifications.Service.Messaging;

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

    // Events are acknowledged one by one, so this bounds how many are processed concurrently.
    [Range(1, 1000)]
    public ushort PrefetchCount { get; set; } = 10;

    // Wait before requeueing an event that failed for a transient reason (e.g. Farms or the database down).
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    // Retries of a message that keeps failing for a non-domain reason before it is dead-lettered
    // (with the default delay, about five minutes: long enough to ride out a dependency restarting).
    [Range(0, 10_000)]
    public int MaxRetries { get; set; } = 60;

    public bool EnableConsumer { get; set; } = true;
}
