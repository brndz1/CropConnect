using System.Net;
using System.Text;
using System.Text.Json;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// Login sessions through the gateway: RS256 tokens, refresh tokens with rotation, and logout.
public sealed class SessionTests
{
    private const string Password = "Passw0rd-e2e";

    private static async Task<(TestUser User, ApiResponse Login)> LoggedInClientAsync()
    {
        var user = await RegisterAsync("Client");
        var login = await PostAsync("api/auth/login", new { email = user.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.Status);
        return (user, login);
    }

    [Fact]
    public async Task Token_is_signed_with_RS256_and_comes_with_a_refresh_token()
    {
        var (_, login) = await LoggedInClientAsync();

        var header = JsonDocument.Parse(Base64UrlDecode(login.Text("token").Split('.')[0])).RootElement;
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.NotEmpty(login.Text("refreshToken"));
        Assert.Equal("900", login.Text("expiresIn"));
    }

    [Fact]
    public async Task Refresh_token_gets_a_working_token_once()
    {
        var (user, login) = await LoggedInClientAsync();

        var refreshed = await PostAsync("api/auth/refresh", new { refreshToken = login.Text("refreshToken") });
        Assert.Equal(HttpStatusCode.OK, refreshed.Status);
        Assert.NotEqual(login.Text("refreshToken"), refreshed.Text("refreshToken"));
        Assert.Equal(HttpStatusCode.OK, (await GetAsync($"api/users/{user.Id}/profile", refreshed.Text("token"))).Status);

        // The first refresh token was already used: presenting it again ends the whole session.
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("api/auth/refresh", new { refreshToken = login.Text("refreshToken") })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("api/auth/refresh", new { refreshToken = refreshed.Text("refreshToken") })).Status);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var (_, login) = await LoggedInClientAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("api/auth/logout", new { refreshToken = login.Text("refreshToken") })).Status);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("api/auth/refresh", new { refreshToken = login.Text("refreshToken") })).Status);
    }

    [Fact]
    public async Task Token_signed_with_HS256_is_refused()
    {
        var (user, login) = await LoggedInClientAsync();
        var parts = login.Text("token").Split('.');
        var forgedHeader = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}""");
        var forged = $"{forgedHeader}.{parts[1]}.{parts[2]}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync($"api/users/{user.Id}/profile", forged)).Status);
    }

    // Against guessing a password: 10 attempts per account every 15 minutes, counted in Redis by every gateway replica.
    [Fact]
    public async Task Too_many_login_attempts_for_one_account_get_429()
    {
        var email = $"brute{Guid.NewGuid():N}"[..20] + "@e2e.test";

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("api/auth/login", new { email, password = "Wrong-pass1" })).Status);
        }

        var refused = await PostAsync("api/auth/login", new { email, password = "Wrong-pass1" });
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.Status);
        Assert.Equal("429", refused.Text("status"));
    }

    private static string Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private static string Base64UrlEncode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
