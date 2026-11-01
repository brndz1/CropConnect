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

// The web defaults PostAsJsonAsync uses, without the null fields (no HTML part, no sandbox).
internal static class EmailJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
