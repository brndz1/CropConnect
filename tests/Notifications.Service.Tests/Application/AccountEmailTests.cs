using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notifications.Service.Application;
using Notifications.Service.Application.Clients;
using Notifications.Service.Application.Email;
using Notifications.Service.Models;
using Notifications.Service.Tests.Infrastructure;

namespace Notifications.Service.Tests.Application;

// The account events Auth publishes become emails with links to the web app; they are not in the inbox.
public sealed class AccountEmailTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ManualClock _clock = new();
    private readonly RecordingSender _sender = new();
    private readonly EmailOptions _options = new() { AppBaseUrl = "https://cropconnect.example.org", MaxAttempts = 3 };
    private readonly Guid _userId = Guid.NewGuid();

    public void Dispose() => _database.Dispose();

    private NotificationIngestionService Ingestion() => new(
        _database.CreateContext(), new NoFarms(), _clock, Options.Create(_options), NullLogger<NotificationIngestionService>.Instance);

    private EmailDispatchService Dispatcher() => new(
        _database.CreateContext(), new NoUsers(), _sender, _clock, Options.Create(_options), NullLogger<EmailDispatchService>.Instance);

    private Task<bool> HandleAsync(string routingKey, string json, Guid? eventId = null) =>
        Ingestion().HandleAsync(routingKey, eventId ?? Guid.NewGuid(), Encoding.UTF8.GetBytes(json), CancellationToken.None);

    private string TokenEvent(string email = "ana@mail.pt", string role = "Client") =>
        $$"""{"userId":"{{_userId}}","email":"{{email}}","username":"ana","role":"{{role}}","token":"abc-123_x","expiresAt":"{{_clock.Now.AddHours(1).UtcDateTime:O}}","occurredAt":"{{_clock.Now.UtcDateTime:O}}"}""";

    private string InfoEvent() =>
        $$"""{"userId":"{{_userId}}","email":"ana@mail.pt","username":"ana","role":"Manager","occurredAt":"{{_clock.Now.UtcDateTime:O}}"}""";

    private List<AccountEmail> Stored()
    {
        using var context = _database.CreateContext();
        return context.AccountEmails.ToList();
    }

    [Theory]
    [InlineData("account.email-verification.requested", AccountEmailKind.EmailVerification, "Confirm your email address", "verificar-email?token=abc-123_x")]
    [InlineData("account.password-reset.requested", AccountEmailKind.PasswordReset, "Reset your password", "redefinir-palavra-passe?token=abc-123_x")]
    [InlineData("account.invited", AccountEmailKind.Invitation, "Your CropConnect account is ready", "redefinir-palavra-passe?token=abc-123_x")]
    public async Task Link_events_become_an_email_with_the_link_to_the_web_app(string routingKey, AccountEmailKind kind, string subject, string path)
    {
        Assert.True(await HandleAsync(routingKey, TokenEvent()));

        var email = Assert.Single(Stored());
        Assert.Equal(kind, email.Kind);
        Assert.Equal(subject, email.Subject);
        Assert.Equal("ana@mail.pt", email.ToAddress);
        Assert.Contains($"https://cropconnect.example.org/{path}", email.Text);
        Assert.NotNull(email.LinkExpiresAt);

        using var context = _database.CreateContext();
        Assert.Empty(context.Notifications);
    }

    [Theory]
    [InlineData("account.email.verified", AccountEmailKind.Welcome, "register your farm")]
    [InlineData("account.password.changed", AccountEmailKind.PasswordChanged, "recuperar-palavra-passe")]
    [InlineData("account.deactivated", AccountEmailKind.AccountDeactivated, "can no longer log in")]
    [InlineData("account.reactivated", AccountEmailKind.AccountReactivated, "/entrar")]
    public async Task Informative_events_become_an_email_without_a_link_to_expire(string routingKey, AccountEmailKind kind, string text)
    {
        Assert.True(await HandleAsync(routingKey, InfoEvent()));

        var email = Assert.Single(Stored());
        Assert.Equal(kind, email.Kind);
        Assert.Contains(text, email.Text);
        Assert.Null(email.LinkExpiresAt);
    }

    [Fact]
    public async Task A_redelivered_event_sends_one_email()
    {
        var eventId = Guid.NewGuid();

        Assert.True(await HandleAsync("account.password-reset.requested", TokenEvent(), eventId));
        Assert.False(await HandleAsync("account.password-reset.requested", TokenEvent(), eventId));

        Assert.Single(Stored());
    }

    [Fact]
    public async Task Unknown_account_events_are_rejected()
    {
        await Assert.ThrowsAsync<InvalidInputException>(() => HandleAsync("account.something.else", InfoEvent()));
    }

    [Fact]
    public async Task Sent_email_has_an_html_button_and_loses_its_one_time_link_afterwards()
    {
        await HandleAsync("account.password-reset.requested", TokenEvent());

        Assert.Equal(1, await Dispatcher().DispatchDueAsync(CancellationToken.None));

        var sent = Assert.Single(_sender.Sent);
        Assert.Contains("redefinir-palavra-passe?token=abc-123_x", sent.Text);
        Assert.Contains("Choose a new password</a>", sent.Html);
        var stored = Assert.Single(Stored());
        Assert.Equal(EmailStatus.Sent, stored.Status);
        Assert.Equal(AccountEmail.RemovedText, stored.Text);
    }

    [Fact]
    public async Task A_link_that_expired_before_sending_is_not_sent()
    {
        await HandleAsync("account.email-verification.requested", TokenEvent());
        _clock.Advance(TimeSpan.FromHours(2));

        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        Assert.Empty(_sender.Sent);
        Assert.Equal(EmailStatus.Skipped, Assert.Single(Stored()).Status);
    }

    [Theory]
    [InlineData("ana@cropconnect.test", true)]
    [InlineData("ana@shop.example.com", true)]
    [InlineData("ana@mail.pt", false)]
    public async Task With_real_delivery_reserved_test_domains_are_skipped(string address, bool skipped)
    {
        _options.SkipReservedDomains = true;
        await HandleAsync("account.email-verification.requested", TokenEvent(address));

        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        Assert.Equal(skipped ? 0 : 1, _sender.Sent.Count);
        Assert.Equal(skipped ? EmailStatus.Skipped : EmailStatus.Sent, Assert.Single(Stored()).Status);
    }

    [Fact]
    public async Task A_failed_send_is_retried_like_the_notifications()
    {
        await HandleAsync("account.password-reset.requested", TokenEvent());
        _sender.Failure = new IOException("SMTP down");

        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        var stored = Assert.Single(Stored());
        Assert.Equal(EmailStatus.Pending, stored.Status);
        Assert.Equal("SMTP down", stored.LastError);
        Assert.Contains("token=abc-123_x", stored.Text);
    }

    private sealed class RecordingSender : IEmailSender
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

    private sealed class NoUsers : IUserDirectoryClient
    {
        public Task<UserContact> GetContactAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Account emails carry their own address.");
    }

    private sealed class NoFarms : IFarmDirectoryClient
    {
        public Task<string> GetFarmNameAsync(Guid farmId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Account emails are not about a farm.");
    }
}
