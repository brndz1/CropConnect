using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notifications.Service.Application.Clients;
using Notifications.Service.Data;
using Notifications.Service.Messaging;
using Notifications.Service.Models;

namespace Notifications.Service.Application.Email;

public interface IEmailDispatchService
{
    // Sends the emails that are due and returns how many were sent.
    Task<int> DispatchDueAsync(CancellationToken cancellationToken);
}

public sealed class EmailDispatchService : IEmailDispatchService
{
    #region Dependencies

    private readonly NotificationsDbContext _dbContext;
    private readonly IUserDirectoryClient _users;
    private readonly IEmailSender _sender;
    private readonly TimeProvider _timeProvider;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailDispatchService> _logger;

    public EmailDispatchService(
        NotificationsDbContext dbContext,
        IUserDirectoryClient users,
        IEmailSender sender,
        TimeProvider timeProvider,
        IOptions<EmailOptions> options,
        ILogger<EmailDispatchService> logger)
    {
        _dbContext = dbContext;
        _users = users;
        _sender = sender;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #endregion

    #region Dispatch

    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken)
    {
        // Account emails first: someone may be waiting for the link.
        return await DispatchAccountEmailsAsync(cancellationToken) + await DispatchNotificationsAsync(cancellationToken);
    }

    private async Task<int> DispatchNotificationsAsync(CancellationToken cancellationToken)
    {
        var now = Now;
        var due = await _dbContext.Notifications
            .Where(n => n.EmailStatus == EmailStatus.Pending && n.EmailNextAttemptAt <= now)
            .OrderBy(n => n.EmailNextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var notification in due)
        {
            if (await TrySendAsync(notification, cancellationToken))
            {
                sent++;
            }

            // Saved one by one, so an email that went out is never sent again after a crash mid-batch.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }

    private async Task<bool> TrySendAsync(Notification notification, CancellationToken cancellationToken)
    {
        using var activity = MessagingTelemetry.StartEmail(notification.TraceParent, notification.Id);

        try
        {
            var contact = await _users.GetContactAsync(notification.RecipientId, cancellationToken);

            if (!contact.IsActive)
            {
                notification.SkipEmail("The recipient's account is inactive.");
                return false;
            }

            if (IsUndeliverable(contact.Email))
            {
                notification.SkipEmail(ReservedDomainReason);
                return false;
            }

            var text = ComposeText(contact, notification);
            await _sender.SendAsync(
                new EmailMessage(contact.Email, contact.Username, notification.Title, text, EmailHtml.FromText(notification.Title, text)),
                cancellationToken);

            notification.MarkEmailSent(Now);
            return true;
        }
        catch (NotFoundException)
        {
            notification.SkipEmail("The recipient no longer exists.");
            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            notification.MarkEmailFailed(exception.Message, Now, _options.MaxAttempts, _options.RetryBaseDelay);
            _logger.LogWarning(
                exception,
                "Email for notification {NotificationId} failed (attempt {Attempt}/{MaxAttempts})",
                notification.Id,
                notification.EmailAttempts,
                _options.MaxAttempts);
            return false;
        }
    }

    private string ComposeText(UserContact contact, Notification notification) =>
        $"Hello {contact.Username},\n\n{notification.Message}\n\nYou can see all your notifications in the CropConnect app:\n\n" +
        $"{new Uri(_options.AppBaseUri, "notificacoes")}";

    #endregion

    #region Account emails

    private async Task<int> DispatchAccountEmailsAsync(CancellationToken cancellationToken)
    {
        var now = Now;
        var due = await _dbContext.AccountEmails
            .Where(e => e.Status == EmailStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var email in due)
        {
            if (await TrySendAsync(email, cancellationToken))
            {
                sent++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }

    private async Task<bool> TrySendAsync(AccountEmail email, CancellationToken cancellationToken)
    {
        using var activity = MessagingTelemetry.StartEmail(email.TraceParent, email.Id);

        if (email.LinkExpiresAt <= Now)
        {
            email.Skip("The link expired before the email could be sent.");
            return false;
        }

        if (IsUndeliverable(email.ToAddress))
        {
            email.Skip(ReservedDomainReason);
            return false;
        }

        try
        {
            await _sender.SendAsync(
                new EmailMessage(email.ToAddress, email.ToName, email.Subject, email.Text, EmailHtml.FromText(email.Subject, email.Text, ButtonLabel(email.Kind))),
                cancellationToken);

            email.MarkSent(Now);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            email.MarkFailed(exception.Message, Now, _options.MaxAttempts, _options.RetryBaseDelay);
            _logger.LogWarning(
                exception,
                "Account email {EmailId} ({Kind}) failed (attempt {Attempt}/{MaxAttempts})",
                email.Id,
                email.Kind,
                email.Attempts,
                _options.MaxAttempts);
            return false;
        }
    }

    private static string ButtonLabel(AccountEmailKind kind) => kind switch
    {
        AccountEmailKind.EmailVerification => "Confirm my email",
        AccountEmailKind.PasswordReset => "Choose a new password",
        AccountEmailKind.Invitation => "Choose my password",
        AccountEmailKind.PasswordChanged => "Reset my password",
        _ => "Open CropConnect"
    };

    #endregion

    #region Reserved domains

    private const string ReservedDomainReason = "The address is on a reserved test domain, which cannot receive email.";

    private static readonly string[] ReservedSuffixes = [".test", ".example", ".invalid", ".localhost"];
    private static readonly string[] ReservedDomains = ["example.com", "example.net", "example.org"];

    private bool IsUndeliverable(string address)
    {
        if (!_options.SkipReservedDomains)
        {
            return false;
        }

        var domain = address[(address.LastIndexOf('@') + 1)..].ToLowerInvariant();
        return ReservedSuffixes.Any(suffix => domain.EndsWith(suffix, StringComparison.Ordinal))
               || ReservedDomains.Any(reserved => domain == reserved || domain.EndsWith("." + reserved, StringComparison.Ordinal));
    }

    #endregion
}
