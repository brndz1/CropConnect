namespace Notifications.Service.Application.Email;

public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string Text);

public interface IEmailSender
{
    // Throws when the provider does not accept the email, so the dispatcher can retry it.
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
