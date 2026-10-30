using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notifications.Service.Application;
using Notifications.Service.Application.Clients;
using Notifications.Service.Application.Email;
using Notifications.Service.Models;
using Notifications.Service.Tests.Infrastructure;

namespace Notifications.Service.Tests.Application;

public sealed class EmailAndInboxTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ManualClock _clock = new();
    private readonly FakeUsers _users = new();
    private readonly FakeSender _sender = new();
    private readonly EmailOptions _options = new() { MaxAttempts = 3, RetryBaseDelay = TimeSpan.FromSeconds(30) };

    public void Dispose() => _database.Dispose();

    private EmailDispatchService Dispatcher() => new(
        _database.CreateContext(), _users, _sender, _clock, Options.Create(_options), NullLogger<EmailDispatchService>.Instance);

    private NotificationQueryService Inbox() => new(_database.CreateContext(), _clock);

    private async Task<Notification> SeedAsync(Guid recipientId, string title = "Nova encomenda")
    {
        var notification = Notification.Create(
            Guid.NewGuid(), recipientId, NotificationType.OrderCreated, title, "Uma mensagem.", Guid.NewGuid(), Guid.NewGuid(), _clock.Now.UtcDateTime);

        await using var context = _database.CreateContext();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        return notification;
    }

    private Notification Reload(Guid id)
    {
        using var context = _database.CreateContext();
        return context.Notifications.Single(n => n.Id == id);
    }

    #region Email dispatch

    [Fact]
    public async Task Due_email_goes_to_the_recipient_address_once()
    {
        var notification = await SeedAsync(_users.Active.UserId);

        Assert.Equal(1, await Dispatcher().DispatchDueAsync(CancellationToken.None));
        Assert.Equal(0, await Dispatcher().DispatchDueAsync(CancellationToken.None));

        var email = Assert.Single(_sender.Sent);
        Assert.Equal(_users.Active.Email, email.ToAddress);
        Assert.Equal("Nova encomenda", email.Subject);
        Assert.Contains("Hello ana,", email.Text);
        Assert.Contains("Uma mensagem.", email.Text);
        Assert.Equal(EmailStatus.Sent, Reload(notification.Id).EmailStatus);
    }

    [Fact]
    public async Task Failed_email_is_retried_after_the_backoff_and_then_given_up()
    {
        var notification = await SeedAsync(_users.Active.UserId);
        _sender.Failure = new HttpRequestException("SMTP down");

        await Dispatcher().DispatchDueAsync(CancellationToken.None);
        Assert.Equal(1, Reload(notification.Id).EmailAttempts);

        // Not due again until the backoff has passed.
        _clock.Advance(TimeSpan.FromSeconds(29));
        await Dispatcher().DispatchDueAsync(CancellationToken.None);
        Assert.Equal(1, Reload(notification.Id).EmailAttempts);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await Dispatcher().DispatchDueAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        var failed = Reload(notification.Id);
        Assert.Equal(EmailStatus.Failed, failed.EmailStatus);
        Assert.Equal(3, failed.EmailAttempts);
        Assert.Equal("SMTP down", failed.EmailLastError);
    }

    [Fact]
    public async Task Email_recovers_when_the_provider_comes_back()
    {
        var notification = await SeedAsync(_users.Active.UserId);
        _sender.Failure = new HttpRequestException("SMTP down");
        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        _sender.Failure = null;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(1, await Dispatcher().DispatchDueAsync(CancellationToken.None));
        Assert.Equal(EmailStatus.Sent, Reload(notification.Id).EmailStatus);
    }

    [Fact]
    public async Task Inactive_or_deleted_recipients_are_skipped_without_retries()
    {
        var inactive = await SeedAsync(_users.Inactive.UserId);
        var deleted = await SeedAsync(Guid.NewGuid());

        Assert.Equal(0, await Dispatcher().DispatchDueAsync(CancellationToken.None));

        Assert.Empty(_sender.Sent);
        Assert.Equal(EmailStatus.Skipped, Reload(inactive.Id).EmailStatus);
        Assert.Equal(EmailStatus.Skipped, Reload(deleted.Id).EmailStatus);
    }

    #endregion

    #region Inbox

    [Fact]
    public async Task Users_only_see_and_read_their_own_notifications()
    {
        var mine = await SeedAsync(_users.Active.UserId, "Primeira");
        await SeedAsync(_users.Active.UserId, "Segunda");
        var someoneElses = await SeedAsync(Guid.NewGuid());
        var me = new Caller(_users.Active.UserId, Role.Client);

        var page = await Inbox().ListMineAsync(me, new NotificationQuery(false, 1, 20), CancellationToken.None);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.UnreadCount);

        await Inbox().MarkReadAsync(me, mine.Id, CancellationToken.None);
        var unread = await Inbox().ListMineAsync(me, new NotificationQuery(true, 1, 20), CancellationToken.None);
        Assert.Equal("Segunda", Assert.Single(unread.Items).Title);

        await Assert.ThrowsAsync<NotFoundException>(() => Inbox().MarkReadAsync(me, someoneElses.Id, CancellationToken.None));
        Assert.Equal(1, await Inbox().MarkAllReadAsync(me, CancellationToken.None));
        Assert.Null(Reload(someoneElses.Id).ReadAt);
    }

    [Fact]
    public async Task Anonymous_callers_have_no_inbox()
    {
        await Assert.ThrowsAsync<UnauthenticatedException>(() =>
            Inbox().ListMineAsync(Caller.Anonymous, new NotificationQuery(false, 1, 20), CancellationToken.None));
    }

    #endregion

    private sealed class FakeUsers : IUserDirectoryClient
    {
        public UserContact Active { get; } = new(Guid.NewGuid(), "ana", "ana@cropconnect.test", true);

        public UserContact Inactive { get; } = new(Guid.NewGuid(), "rui", "rui@cropconnect.test", false);

        public Task<UserContact> GetContactAsync(Guid userId, CancellationToken cancellationToken) =>
            userId == Active.UserId ? Task.FromResult(Active)
            : userId == Inactive.UserId ? Task.FromResult(Inactive)
            : throw new NotFoundException("User not found.");
    }

    private sealed class FakeSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Exception? Failure { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
