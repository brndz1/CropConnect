using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Notifications.Service.Application.Email;

// Any SMTP server with a login, such as Brevo's relay: real delivery to the recipients' inboxes.
// One connection per email keeps it simple; the dispatcher sends a handful of emails per round.
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.Text, HtmlBody = message.Html }.ToMessageBody();

        var smtp = _options.Smtp;
        using var client = new SmtpClient { Timeout = (int)_options.Timeout.TotalMilliseconds };

        await client.ConnectAsync(
            smtp.Host,
            smtp.Port,
            smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrEmpty(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password, cancellationToken);
        }

        // Throws when the server refuses the email, so the dispatcher retries it.
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
