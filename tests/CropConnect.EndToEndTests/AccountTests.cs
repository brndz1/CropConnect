using System.Net;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// Email verification, password reset and invitations, through the gateway, RabbitMQ, the email dispatcher and Mailpit.
public sealed class AccountTests
{
    private const string Password = "Passw0rd-e2e";
    private const string NewPassword = "N3w-Passw0rd-e2e";

    private static string NewEmail(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..24].ToLowerInvariant() + "@e2e.test";

    [Fact]
    public async Task New_account_logs_in_only_after_opening_the_verification_link()
    {
        var email = NewEmail("verify");
        var username = email.Split('@')[0];
        var registered = await PostAsync("api/auth/register", new { username, email, password = Password, role = "Client", firstName = "Marta", lastName = "Ferreira" });
        Assert.Equal(HttpStatusCode.Created, registered.Status);

        var early = await PostAsync("api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, early.Status);
        Assert.StartsWith("Confirm your email", early.Text("detail"));

        var token = await Mailpit.TokenAsync(email, "Confirm your email address");
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("api/auth/verify-email", new { token })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync("api/auth/verify-email", new { token })).Status);

        var token2 = await LoginAsync(email, Password);
        await Mailpit.ArrivesAsync(email, "Your email is confirmed");

        // The names given at registration are already in the profile.
        var profile = await GetAsync($"api/users/{registered.Text("userId")}/profile", token2);
        Assert.Equal(HttpStatusCode.OK, profile.Status);
        Assert.Equal("Marta", profile.Text("firstName"));
        Assert.Equal("Ferreira", profile.Text("lastName"));
    }

    [Fact]
    public async Task Forgotten_password_is_reset_from_the_emailed_link()
    {
        var user = await RegisterAsync("Client");

        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync("api/auth/forgot-password", new { email = user.Email })).Status);
        var token = await Mailpit.TokenAsync(user.Email, "Reset your password");

        // Sessions opened with the old password end; iat has a precision of seconds.
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("api/auth/reset-password", new { token, newPassword = NewPassword })).Status);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("api/auth/login", new { email = user.Email, password = Password })).Status);
        Assert.NotEmpty(await LoginAsync(user.Email, NewPassword));
        await Eventually.GetAsync(
            () => GetAsync("api/notifications", user.Token),
            response => response.Status == HttpStatusCode.Unauthorized,
            because: "the old token to stop working (the gateway caches the check for 15 s)");
        await Mailpit.ArrivesAsync(user.Email, "Your password was changed");
    }

    [Fact]
    public async Task Registration_needs_the_first_and_last_name()
    {
        var email = NewEmail("noname");
        var refused = await PostAsync("api/auth/register", new { username = email.Split('@')[0], email, password = Password, role = "Client", firstName = " ", lastName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync("api/auth/login", new { email, password = Password })).Status);
    }

    [Fact]
    public async Task Unknown_emails_get_the_same_answer()
    {
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync("api/auth/forgot-password", new { email = NewEmail("nobody") })).Status);
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync("api/auth/resend-verification", new { email = NewEmail("nobody") })).Status);
    }

    [Fact]
    public async Task Changing_the_password_returns_a_new_token()
    {
        var user = await RegisterAsync("Manager");

        var wrong = await PostAsync("api/auth/change-password", new { currentPassword = "Wrong-pass1", newPassword = NewPassword }, user.Token);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.Status);

        var changed = await PostAsync("api/auth/change-password", new { currentPassword = Password, newPassword = NewPassword }, user.Token);
        Assert.Equal(HttpStatusCode.OK, changed.Status);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("api/notifications", changed.Text("token"))).Status);
        Assert.NotEmpty(await LoginAsync(user.Email, NewPassword));
    }

    [Fact]
    public async Task Users_the_administrator_creates_are_invited_to_choose_their_password()
    {
        var email = NewEmail("invited");
        var created = await PostAsync(
            "api/users",
            new { username = email.Split('@')[0], email, password = Password, role = "Manager" },
            await AdminTokenAsync());
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.True(created.Body!["emailVerified"]!.GetValue<bool>());

        var token = await Mailpit.TokenAsync(email, "Your CropConnect account is ready");
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync("api/auth/reset-password", new { token, newPassword = NewPassword })).Status);
        Assert.NotEmpty(await LoginAsync(email, NewPassword));
    }
}
