using Auth.Service.Models;
using Auth.Service.Protos;
using CropConnect.Tests.Shared;

namespace Auth.Service.Tests;

// Login sessions: a refresh token gets a new JWT once, and is replaced by the next one (rotation).
public sealed partial class AuthGrpcServiceTests
{
    private Task<LoginResponse> RefreshAsync(string refreshToken) =>
        Auth().RefreshToken(new RefreshTokenRequest { RefreshToken = refreshToken }, TestServerCallContext.Anonymous());

    [Fact]
    public async Task Login_returns_a_short_lived_token_and_a_refresh_token()
    {
        await RegisterAsync("ana");

        var login = await LoginAsync("ana@cropconnect.test");

        Assert.NotEmpty(login.Token);
        Assert.NotEmpty(login.RefreshToken);
        Assert.Equal(60 * 60, login.ExpiresIn);
    }

    [Fact]
    public async Task Refresh_gives_a_new_token_and_replaces_the_refresh_token()
    {
        await RegisterAsync("ana");
        var login = await LoginAsync("ana@cropconnect.test");

        var refreshed = await RefreshAsync(login.RefreshToken);

        Assert.True((await Auth().ValidateToken(new ValidateTokenRequest { Token = refreshed.Token }, TestServerCallContext.Anonymous())).IsValid);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.NotEmpty((await RefreshAsync(refreshed.RefreshToken)).Token);
    }

    // A used token being presented again means it was copied: the whole session ends, the thief's and the user's.
    [Fact]
    public async Task Reusing_a_refresh_token_ends_the_session()
    {
        await RegisterAsync("ana");
        var login = await LoginAsync("ana@cropconnect.test");
        var refreshed = await RefreshAsync(login.RefreshToken);

        await Assert.ThrowsAsync<UnauthenticatedException>(() => RefreshAsync(login.RefreshToken));
        await Assert.ThrowsAsync<UnauthenticatedException>(() => RefreshAsync(refreshed.RefreshToken));
    }

    [Fact]
    public async Task Other_sessions_are_not_affected_by_a_reuse()
    {
        await RegisterAsync("ana");
        var phone = await LoginAsync("ana@cropconnect.test");
        var laptop = await LoginAsync("ana@cropconnect.test");
        await RefreshAsync(phone.RefreshToken);

        await Assert.ThrowsAsync<UnauthenticatedException>(() => RefreshAsync(phone.RefreshToken));

        Assert.NotEmpty((await RefreshAsync(laptop.RefreshToken)).Token);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        await RegisterAsync("ana");
        var login = await LoginAsync("ana@cropconnect.test");

        await Auth().Logout(new RefreshTokenRequest { RefreshToken = login.RefreshToken }, TestServerCallContext.Anonymous());

        await Assert.ThrowsAsync<UnauthenticatedException>(() => RefreshAsync(login.RefreshToken));
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_succeeds_quietly()
    {
        await Auth().Logout(new RefreshTokenRequest { RefreshToken = "not-a-token" }, TestServerCallContext.Anonymous());
    }

    [Fact]
    public async Task Expired_refresh_token_is_refused()
    {
        await RegisterAsync("ana");
        var login = await LoginAsync("ana@cropconnect.test");

        _clock.Advance(_accountOptions.RefreshTokenLifetime + TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<UnauthenticatedException>(() => RefreshAsync(login.RefreshToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Unknown_refresh_token_is_refused(string token)
    {
        await Assert.ThrowsAsync<UnauthenticatedException>(() => RefreshAsync(token));
    }

    [Fact]
    public async Task A_password_change_ends_the_other_sessions_but_not_the_one_that_made_it()
    {
        var anaId = await RegisterAsync("ana");
        var other = await LoginAsync("ana@cropconnect.test");
        _clock.Advance(TimeSpan.FromSeconds(2));

        var changed = await Auth().ChangePassword(
            new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "N3w-Passw0rd" },
            TestServerCallContext.For(anaId, "Client"));

        await Assert.ThrowsAsync<UnauthenticatedException>(() => RefreshAsync(other.RefreshToken));
        Assert.NotEmpty((await RefreshAsync(changed.RefreshToken)).Token);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_refresh()
    {
        var adminId = await SeedAdminAsync();
        var anaId = await RegisterAsync("ana");
        var login = await LoginAsync("ana@cropconnect.test");

        await Accounts().SetUserActive(new SetUserActiveRequest { Id = anaId.ToString(), IsActive = false }, TestServerCallContext.For(adminId, "Admin"));

        await Assert.ThrowsAsync<UnauthenticatedException>(() => RefreshAsync(login.RefreshToken));
    }
}
