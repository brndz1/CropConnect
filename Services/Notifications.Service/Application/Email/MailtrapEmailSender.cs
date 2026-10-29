using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Notifications.Service.Application.Email;

// Mailtrap Email API: send.api.mailtrap.io for real delivery, sandbox.api.mailtrap.io/{inbox} for testing.
public sealed class MailtrapEmailSender : IEmailSender
{
    public const string SendingBaseUrl = "https://send.api.mailtrap.io/";
    public const string SandboxBaseUrl = "https://sandbox.api.mailtrap.io/";

    private readonly HttpClient _httpClient;
    private readonly EmailOptions _options;

    public MailtrapEmailSender(HttpClient httpClient, IOptions<EmailOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.Mailtrap.ApiToken);
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var path = _options.Mailtrap.SandboxInboxId is { } inboxId ? $"api/send/{inboxId}" : "api/send";
        var request = new
        {
            from = new { email = _options.FromAddress, name = _options.FromName },
            to = new[] { new { email = message.ToAddress, name = message.ToName } },
            subject = message.Subject,
            text = message.Text,
            category = "CropConnect notification"
        };

        using var response = await _httpClient.PostAsJsonAsync(path, request, cancellationToken);
        await response.EnsureAcceptedAsync("Mailtrap", cancellationToken);
    }
}
