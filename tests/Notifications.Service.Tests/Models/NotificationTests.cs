using Notifications.Service.Models;

namespace Notifications.Service.Tests.Models;

public sealed class NotificationTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);

    private static Notification NewNotification() => Notification.Create(
        Guid.NewGuid(), Guid.NewGuid(), NotificationType.OrderCreated, "Nova encomenda", "Texto", Guid.NewGuid(), Guid.NewGuid(), Now);

    [Fact]
    public void New_notification_is_unread_and_its_email_is_due_now()
    {
        var notification = NewNotification();

        Assert.False(notification.IsRead);
        Assert.Equal(EmailStatus.Pending, notification.EmailStatus);
        Assert.Equal(Now, notification.EmailNextAttemptAt);
    }

    [Fact]
    public void Reading_again_keeps_the_first_read_time()
    {
        var notification = NewNotification();

        notification.MarkRead(Now);
        notification.MarkRead(Now.AddHours(1));

        Assert.Equal(Now, notification.ReadAt);
    }

    [Fact]
    public void Failed_email_backs_off_exponentially_then_gives_up()
    {
        var notification = NewNotification();

        notification.MarkEmailFailed("timeout", Now, maxAttempts: 4, BaseDelay);
        Assert.Equal(Now + BaseDelay, notification.EmailNextAttemptAt);

        notification.MarkEmailFailed("timeout", Now, maxAttempts: 4, BaseDelay);
        Assert.Equal(Now + BaseDelay * 2, notification.EmailNextAttemptAt);

        notification.MarkEmailFailed("timeout", Now, maxAttempts: 4, BaseDelay);
        Assert.Equal(Now + BaseDelay * 4, notification.EmailNextAttemptAt);
        Assert.Equal(EmailStatus.Pending, notification.EmailStatus);

        notification.MarkEmailFailed("timeout", Now, maxAttempts: 4, BaseDelay);
        Assert.Equal(EmailStatus.Failed, notification.EmailStatus);
        Assert.Null(notification.EmailNextAttemptAt);
        Assert.Equal(4, notification.EmailAttempts);
    }

    [Fact]
    public void Sent_email_clears_the_last_error()
    {
        var notification = NewNotification();
        notification.MarkEmailFailed("timeout", Now, 5, BaseDelay);

        notification.MarkEmailSent(Now.AddMinutes(1));

        Assert.Equal(EmailStatus.Sent, notification.EmailStatus);
        Assert.Null(notification.EmailLastError);
        Assert.Null(notification.EmailNextAttemptAt);
        Assert.Equal(2, notification.EmailAttempts);
    }

    [Fact]
    public void Long_errors_are_truncated()
    {
        var notification = NewNotification();

        notification.SkipEmail(new string('e', 10_000));

        Assert.Equal(EmailStatus.Skipped, notification.EmailStatus);
        Assert.True(notification.EmailLastError!.Length <= FieldLimits.EmailErrorMaxLength);
    }

    [Fact]
    public void Notification_needs_its_source_recipient_and_text()
    {
        Assert.Throws<InvalidInputException>(() =>
            Notification.Create(Guid.Empty, Guid.NewGuid(), NotificationType.OrderCreated, "t", "m", Guid.NewGuid(), Guid.NewGuid(), Now));
        Assert.Throws<InvalidInputException>(() =>
            Notification.Create(Guid.NewGuid(), Guid.Empty, NotificationType.OrderCreated, "t", "m", Guid.NewGuid(), Guid.NewGuid(), Now));
        Assert.Throws<InvalidInputException>(() =>
            Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.OrderCreated, " ", "m", Guid.NewGuid(), Guid.NewGuid(), Now));
    }
}
