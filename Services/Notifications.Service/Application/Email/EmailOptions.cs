using System.ComponentModel.DataAnnotations;

namespace Notifications.Service.Application.Email;

public enum EmailProvider
{
    // Local mail catcher used in development and Docker Compose (no account needed).
    Mailpit = 1,

    // Mailtrap Email API: real delivery, or a Mailtrap sandbox inbox when SandboxInboxId is set.
    Mailtrap = 2
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailProvider Provider { get; set; } = EmailProvider.Mailpit;

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "notifications@cropconnect.test";

    [Required]
    public string FromName { get; set; } = "CropConnect";

    public bool EnableDispatcher { get; set; } = true;

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    // After this many failed attempts an email is given up (Failed); the notification itself stays in the inbox.
    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public MailpitOptions Mailpit { get; set; } = new();

    public MailtrapOptions Mailtrap { get; set; } = new();
}

public sealed class MailpitOptions
{
    [Required]
    public string BaseUrl { get; set; } = "http://localhost:8025/";
}

public sealed class MailtrapOptions
{
    public string ApiToken { get; set; } = string.Empty;

    // Set to deliver into a Mailtrap sandbox inbox instead of real recipients.
    public long? SandboxInboxId { get; set; }
}
