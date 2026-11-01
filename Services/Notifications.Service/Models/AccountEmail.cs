namespace Notifications.Service.Models;

public enum AccountEmailKind
{
    EmailVerification = 1,
    PasswordReset = 2,
    Invitation = 3,
    Welcome = 4,
    PasswordChanged = 5,
    AccountDeactivated = 6,
    AccountReactivated = 7
}

// An email about the user's account (verification, password reset, ...), sent by the same dispatcher as the
// notifications but not shown in the app's inbox. Emails with a one-time link lose their text once sent.
public sealed class AccountEmail
{
    public const string RemovedText = "(Removed after sending: it contained a one-time link.)";

    #region Properties

    private AccountEmail()
    {
    }

    public Guid Id { get; private set; }

    // MessageId of the account event; makes redelivered events idempotent.
    public Guid SourceEventId { get; private set; }

    public Guid UserId { get; private set; }

    public AccountEmailKind Kind { get; private set; }

    public string ToAddress { get; private set; } = string.Empty;

    public string ToName { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    // The link's own expiry: an email that could not be sent before it is not worth sending late.
    public DateTime? LinkExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public EmailStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTime? NextAttemptAt { get; private set; }

    public DateTime? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public string? TraceParent { get; private set; }

    #endregion

    #region Factory

    public static AccountEmail Create(
        Guid sourceEventId,
        Guid userId,
        AccountEmailKind kind,
        string toAddress,
        string toName,
        string subject,
        string text,
        DateTime? linkExpiresAt,
        DateTime now,
        string? traceParent = null) => new()
    {
        Id = Guid.NewGuid(),
        SourceEventId = Guard.NotEmpty(sourceEventId, "Source event ID"),
        UserId = Guard.NotEmpty(userId, "User ID"),
        Kind = kind,
        ToAddress = Guard.Required(toAddress, "Email address", FieldLimits.EmailAddressMaxLength),
        ToName = Guard.Required(toName, "Name", FieldLimits.EmailNameMaxLength),
        Subject = Guard.Required(subject, "Subject", FieldLimits.TitleMaxLength),
        Text = Guard.Required(text, "Text", FieldLimits.AccountEmailTextMaxLength),
        LinkExpiresAt = linkExpiresAt,
        CreatedAt = now,
        Status = EmailStatus.Pending,
        NextAttemptAt = now,
        TraceParent = traceParent is { Length: <= FieldLimits.TraceParentMaxLength } ? traceParent : null
    };

    #endregion

    #region Behaviour

    public bool HasLink => LinkExpiresAt.HasValue;

    public void MarkSent(DateTime now)
    {
        Attempts++;
        Status = EmailStatus.Sent;
        SentAt = now;
        NextAttemptAt = null;
        LastError = null;

        if (HasLink)
        {
            Text = RemovedText;
        }
    }

    public void Skip(string reason)
    {
        Status = EmailStatus.Skipped;
        NextAttemptAt = null;
        LastError = Guard.Truncate(reason, FieldLimits.EmailErrorMaxLength);

        if (HasLink)
        {
            Text = RemovedText;
        }
    }

    // Retries with an exponential backoff (base, 2x, 4x...) until maxAttempts, then gives up.
    public void MarkFailed(string error, DateTime now, int maxAttempts, TimeSpan baseDelay)
    {
        Attempts++;
        LastError = Guard.Truncate(error, FieldLimits.EmailErrorMaxLength);

        if (Attempts >= maxAttempts)
        {
            Status = EmailStatus.Failed;
            NextAttemptAt = null;
            return;
        }

        NextAttemptAt = now + baseDelay * Math.Pow(2, Attempts - 1);
    }

    #endregion
}
