using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notifications.Service.Application.Clients;
using Notifications.Service.Data;
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
        try
        {
            var contact = await _users.GetContactAsync(notification.RecipientId, cancellationToken);

            if (!contact.IsActive)
            {
                notification.SkipEmail("The recipient's account is inactive.");
                return false;
            }

            await _sender.SendAsync(
                new EmailMessage(contact.Email, contact.Username, notification.Title, ComposeText(contact, notification)),
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

    private static string ComposeText(UserContact contact, Notification notification) =>
        $"Hello {contact.Username},\n\n{notification.Message}\n\nYou can see all your notifications in the CropConnect app.\n\nCropConnect";

    #endregion
}
