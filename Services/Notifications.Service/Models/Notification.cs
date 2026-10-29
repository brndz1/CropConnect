namespace Notifications.Service.Models;

// A message for one user, kept so it can be listed in the app, and delivered by email in the background.
public sealed class Notification
{
    #region Properties

    private Notification()
    {
    }

    public Guid Id { get; private set; }

    public Guid RecipientId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    // The order or alert the notification is about.
    public Guid RelatedEntityId { get; private set; }

    public Guid FarmId { get; private set; }

    // MessageId of the event that produced it; with RecipientId it makes redelivered events idempotent.
    public Guid SourceEventId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ReadAt { get; private set; }

    public EmailStatus EmailStatus { get; private set; }

    public int EmailAttempts { get; private set; }

    public DateTime? EmailNextAttemptAt { get; private set; }

    public DateTime? EmailSentAt { get; private set; }

    public string? EmailLastError { get; private set; }

    #endregion

    #region Factory

    public static Notification Create(
        Guid sourceEventId,
        Guid recipientId,
        NotificationType type,
        string title,
        string message,
        Guid relatedEntityId,
        Guid farmId,
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        SourceEventId = Guard.NotEmpty(sourceEventId, "Source event ID"),
        RecipientId = Guard.NotEmpty(recipientId, "Recipient ID"),
        Type = type,
        Title = Guard.Required(title, "Title", FieldLimits.TitleMaxLength),
        Message = Guard.Required(message, "Message", FieldLimits.MessageMaxLength),
        RelatedEntityId = Guard.NotEmpty(relatedEntityId, "Related entity ID"),
        FarmId = Guard.NotEmpty(farmId, "Farm ID"),
        CreatedAt = now,
        EmailStatus = EmailStatus.Pending,
        EmailNextAttemptAt = now
    };

    #endregion

    #region Behaviour

    public bool IsRead => ReadAt.HasValue;

    // Idempotent: reading it again keeps the first read time.
    public void MarkRead(DateTime now) => ReadAt ??= now;

    public void MarkEmailSent(DateTime now)
    {
        EmailAttempts++;
        EmailStatus = EmailStatus.Sent;
        EmailSentAt = now;
        EmailNextAttemptAt = null;
        EmailLastError = null;
    }

    public void SkipEmail(string reason)
    {
        EmailStatus = EmailStatus.Skipped;
        EmailNextAttemptAt = null;
        EmailLastError = Guard.Truncate(reason, FieldLimits.EmailErrorMaxLength);
    }

    // Retries with an exponential backoff (base, 2x, 4x...) until maxAttempts, then gives up.
    public void MarkEmailFailed(string error, DateTime now, int maxAttempts, TimeSpan baseDelay)
    {
        EmailAttempts++;
        EmailLastError = Guard.Truncate(error, FieldLimits.EmailErrorMaxLength);

        if (EmailAttempts >= maxAttempts)
        {
            EmailStatus = EmailStatus.Failed;
            EmailNextAttemptAt = null;
            return;
        }

        EmailNextAttemptAt = now + baseDelay * Math.Pow(2, EmailAttempts - 1);
    }

    #endregion
}
