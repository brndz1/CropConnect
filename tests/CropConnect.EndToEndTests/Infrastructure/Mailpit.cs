using System.Text.Json.Nodes;

namespace CropConnect.EndToEndTests.Infrastructure;

// Reads the notification emails Mailpit caught.
public static class Mailpit
{
    private static readonly HttpClient Http = new() { BaseAddress = TestEnvironment.Mailpit };

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
