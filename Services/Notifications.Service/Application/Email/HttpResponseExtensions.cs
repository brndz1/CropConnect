namespace Notifications.Service.Application.Email;

internal static class HttpResponseExtensions
{
    public static async Task EnsureAcceptedAsync(this HttpResponseMessage response, string provider, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"{provider} rejected the email ({(int)response.StatusCode}): {body}",
            inner: null,
            response.StatusCode);
    }
}
