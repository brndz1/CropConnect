using Microsoft.Extensions.Options;

namespace Notifications.Service.Application.Email;

// Mailpit's send API (POST /api/v1/send): the email shows up in its web inbox, nothing leaves the machine.
public sealed class MailpitEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly EmailOptions _options;

    public MailpitEmailSender(HttpClient httpClient, IOptions<EmailOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var request = new
        {
            From = new { Email = _options.FromAddress, Name = _options.FromName },
            To = new[] { new { Email = message.ToAddress, Name = message.ToName } },
            message.Subject,
            message.Text
        };

        using var response = await _httpClient.PostAsJsonAsync("api/v1/send", request, cancellationToken);
        await response.EnsureAcceptedAsync("Mailpit", cancellationToken);
    }
}
