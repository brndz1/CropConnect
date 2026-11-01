namespace Notifications.Service.Application.Email;

// Html is the same content as Text, for the clients that show it; Text is always sent.
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string Text, string? Html = null);

public interface IEmailSender
{
    // Throws when the provider does not accept the email, so the dispatcher can retry it.
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
