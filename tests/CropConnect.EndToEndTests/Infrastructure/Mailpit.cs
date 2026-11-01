using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CropConnect.EndToEndTests.Infrastructure;

// Reads the notification emails Mailpit caught.
public static partial class Mailpit
{
    [GeneratedRegex(@"token=([A-Za-z0-9_\-]+)")]
    private static partial Regex TokenLink();

    // The one-time token in the newest email with this subject, once it arrives.
    public static async Task<string> TokenAsync(string address, string subject)
    {
        var message = await Eventually.GetAsync(
            async () => (await MessagesToAsync(address)).FirstOrDefault(m => m["Subject"]?.ToString() == subject),
            m => m is not null,
            because: $"the email '{subject}' to {address}");

        var match = TokenLink().Match(await TextAsync(message!));
        Assert.True(match.Success, $"No link with a token in '{subject}'.");
        return match.Groups[1].Value;
    }

    private static readonly HttpClient Http = new() { BaseAddress = TestEnvironment.Mailpit };

    // Waits for an informative email (no link) with this subject.
    public static Task ArrivesAsync(string address, string subject) =>
        Eventually.GetAsync(
            async () => (await MessagesToAsync(address)).Any(m => m["Subject"]?.ToString() == subject),
            found => found,
            because: $"the email '{subject}' to {address}");

    public static async Task<IReadOnlyList<JsonNode>> MessagesToAsync(string address)
    {
        var json = JsonNode.Parse(await Http.GetStringAsync($"api/v1/search?query={Uri.EscapeDataString($"to:{address}")}&limit=100"));
        return json?["messages"]?.AsArray().Where(m => m is not null).Select(m => m!).ToList() ?? [];
    }

    public static async Task<string> TextAsync(JsonNode message)
    {
        var json = JsonNode.Parse(await Http.GetStringAsync($"api/v1/message/{message["ID"]}"));
        return json?["Text"]?.GetValue<string>() ?? string.Empty;
    }
}
