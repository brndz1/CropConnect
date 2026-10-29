using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CropConnect.EndToEndTests.Infrastructure;

public sealed record ApiResponse(HttpStatusCode Status, JsonNode? Body)
{
    public string Text(string property) => Body?[property]?.ToString() ?? string.Empty;

    public string Id => Text("id");
}

public sealed record TestUser(string Id, string Email, string Token);

// The REST API of the gateway, the only entry point clients use.
public sealed class GatewayClient
{
    private static readonly HttpClient Http = new() { BaseAddress = TestEnvironment.Gateway, Timeout = TimeSpan.FromSeconds(60) };

    public static async Task<ApiResponse> SendAsync(HttpMethod method, string path, object? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{') && !text.TrimStart().StartsWith('[')
            ? null
            : JsonNode.Parse(text);

        return new ApiResponse(response.StatusCode, json);
    }

    public static Task<ApiResponse> GetAsync(string path, string? token = null) => SendAsync(HttpMethod.Get, path, token: token);

    public static Task<ApiResponse> PostAsync(string path, object? body = null, string? token = null) => SendAsync(HttpMethod.Post, path, body, token);

    public static Task<ApiResponse> PutAsync(string path, object body, string? token = null) => SendAsync(HttpMethod.Put, path, body, token);

    public static Task<ApiResponse> PatchAsync(string path, object body, string? token = null) => SendAsync(HttpMethod.Patch, path, body, token);

    public static Task<string> GetStringAsync(string path) => Http.GetStringAsync(path.TrimStart('/'));

    // Every test registers its own users, so tests never depend on each other's data.
    public static async Task<TestUser> RegisterAsync(string role)
    {
        var name = $"e2e{role}{Guid.NewGuid():N}"[..24].ToLowerInvariant();
        var email = $"{name}@e2e.test";

        var registered = await PostAsync("api/auth/register", new { username = name, email, password = "Passw0rd-e2e", role });
        Assert.True(registered.Status == HttpStatusCode.Created, $"Register {role}: {(int)registered.Status} {registered.Body}");

        return new TestUser(registered.Text("userId"), email, await LoginAsync(email, "Passw0rd-e2e"));
    }

    public static async Task<string> LoginAsync(string email, string password)
    {
        var login = await PostAsync("api/auth/login", new { email, password });
        Assert.True(login.Status == HttpStatusCode.OK, $"Login {email}: {(int)login.Status} {login.Body}");
        return login.Text("token");
    }

    public static Task<string> AdminTokenAsync() => LoginAsync(TestEnvironment.AdminEmail, TestEnvironment.AdminPassword);
}
