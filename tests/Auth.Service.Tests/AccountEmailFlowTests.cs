using Auth.Service.Application;
using Auth.Service.Models;
using Auth.Service.Protos;
using Auth.Service.Services;
using CropConnect.Tests.Shared;

namespace Auth.Service.Tests;

// Email verification, password reset, invitations and the other account emails (the events in the outbox).
public sealed partial class AuthGrpcServiceTests
{
    private const string NewPassword = "N3w-Passw0rd-unit";

    private Task RequestResetAsync(string email) =>
        Auth().RequestPasswordReset(new EmailRequest { Email = email }, TestServerCallContext.Anonymous());

    private Task ResetAsync(string token, string password = NewPassword) =>
        Auth().ResetPassword(new ResetPasswordRequest { Token = token, NewPassword = password }, TestServerCallContext.Anonymous());

    #region Email verification

    [Fact]
    public async Task Registration_sends_a_verification_link_and_login_waits_for_it()
    {
        await RegisterAsync("ana", verify: false);

        Assert.Equal(["account.email-verification.requested"], await RoutingKeysAsync());
        var refused = await Assert.ThrowsAsync<ForbiddenException>(() => LoginAsync("ana@cropconnect.test"));
        Assert.Equal(AuthGrpcService.EmailNotVerifiedMessage, refused.Message);

        await Auth().VerifyEmail(new TokenRequest { Token = await LastTokenAsync("account.email-verification.requested") }, TestServerCallContext.Anonymous());

        Assert.NotEmpty((await LoginAsync("ana@cropconnect.test")).Token);
        Assert.Equal("account.email.verified", (await RoutingKeysAsync()).Last());
    }

    [Fact]
    public async Task Unverified_login_with_a_wrong_password_reveals_nothing()
    {
        await RegisterAsync("ana", verify: false);

        await Assert.ThrowsAsync<UnauthenticatedException>(() => LoginAsync("ana@cropconnect.test", "Wrong-pass1"));
    }

    [Fact]
    public async Task Verification_links_work_once_and_expire()
    {
        await RegisterAsync("ana", verify: false);
        var token = await LastTokenAsync("account.email-verification.requested");
        await Auth().VerifyEmail(new TokenRequest { Token = token }, TestServerCallContext.Anonymous());

        var reused = await Assert.ThrowsAsync<InvalidInputException>(() =>
            Auth().VerifyEmail(new TokenRequest { Token = token }, TestServerCallContext.Anonymous()));
        Assert.Equal(AccountTokenService.InvalidLinkMessage, reused.Message);

        await RegisterAsync("rui", verify: false);
        _clock.Advance(_accountOptions.EmailVerificationLifetime + TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Auth().VerifyEmail(new TokenRequest { Token = "not-a-real-token" }, TestServerCallContext.Anonymous()));
        await Assert.ThrowsAsync<InvalidInputException>(async () =>
            await Auth().VerifyEmail(new TokenRequest { Token = await LastTokenAsync("account.email-verification.requested") }, TestServerCallContext.Anonymous()));
    }

    [Fact]
    public async Task Resending_the_verification_is_limited_and_only_the_newest_link_works()
    {
        await RegisterAsync("ana", verify: false);
        var first = await LastTokenAsync("account.email-verification.requested");
        var resend = () => Auth().ResendVerification(new EmailRequest { Email = "ana@cropconnect.test" }, TestServerCallContext.Anonymous());

        await resend();
        Assert.Single(await RoutingKeysAsync());

        _clock.Advance(_accountOptions.ResendCooldown + TimeSpan.FromSeconds(1));
        await resend();
        Assert.Equal(2, (await RoutingKeysAsync()).Length);

        await Assert.ThrowsAsync<InvalidInputException>(() => Auth().VerifyEmail(new TokenRequest { Token = first }, TestServerCallContext.Anonymous()));
        await Auth().VerifyEmail(new TokenRequest { Token = await LastTokenAsync("account.email-verification.requested") }, TestServerCallContext.Anonymous());
    }

    [Fact]
    public async Task Resend_and_reset_requests_answer_the_same_for_unknown_emails()
    {
        await Auth().ResendVerification(new EmailRequest { Email = "nobody@cropconnect.test" }, TestServerCallContext.Anonymous());
        await RequestResetAsync("nobody@cropconnect.test");

        Assert.Empty(await RoutingKeysAsync());
    }

    #endregion

    #region Password reset and change

    [Fact]
    public async Task Password_reset_link_sets_a_new_password_once_and_ends_old_sessions()
    {
        await RegisterAsync("ana");
        var oldToken = (await LoginAsync("ana@cropconnect.test")).Token;
        await RequestResetAsync("ana@cropconnect.test");
        var link = await LastTokenAsync("account.password-reset.requested");

        // iat has a precision of seconds: the change must come in a later second than the old login.
        _clock.Advance(TimeSpan.FromSeconds(2));
        await ResetAsync(link);

        await Assert.ThrowsAsync<UnauthenticatedException>(() => LoginAsync("ana@cropconnect.test"));
        Assert.NotEmpty((await LoginAsync("ana@cropconnect.test", NewPassword)).Token);
        Assert.False((await Auth().ValidateToken(new ValidateTokenRequest { Token = oldToken }, TestServerCallContext.Anonymous())).IsValid);
        await Assert.ThrowsAsync<InvalidInputException>(() => ResetAsync(link, "Another-pass1"));
        Assert.Equal("account.password.changed", (await RoutingKeysAsync()).Last());
    }

    [Fact]
    public async Task A_weak_password_does_not_use_up_the_reset_link()
    {
        await RegisterAsync("ana");
        await RequestResetAsync("ana@cropconnect.test");
        var link = await LastTokenAsync("account.password-reset.requested");

        await Assert.ThrowsAsync<InvalidInputException>(() => ResetAsync(link, "short"));
        await ResetAsync(link);
    }

    [Fact]
    public async Task Reset_links_expire_after_an_hour()
    {
        await RegisterAsync("ana");
        await RequestResetAsync("ana@cropconnect.test");

        _clock.Advance(_accountOptions.PasswordResetLifetime + TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<InvalidInputException>(async () => await ResetAsync(await LastTokenAsync("account.password-reset.requested")));
    }

    [Fact]
    public async Task Resetting_the_password_also_confirms_the_email()
    {
        await RegisterAsync("ana", verify: false);
        await RequestResetAsync("ana@cropconnect.test");

        await ResetAsync(await LastTokenAsync("account.password-reset.requested"));

        Assert.NotEmpty((await LoginAsync("ana@cropconnect.test", NewPassword)).Token);
    }

    [Fact]
    public async Task Deactivated_accounts_get_no_reset_link()
    {
        var adminId = await SeedAdminAsync();
        var anaId = await RegisterAsync("ana");
        await Accounts().SetUserActive(new SetUserActiveRequest { Id = anaId.ToString(), IsActive = false }, TestServerCallContext.For(adminId, "Admin"));

        await RequestResetAsync("ana@cropconnect.test");

        Assert.DoesNotContain("account.password-reset.requested", await RoutingKeysAsync());
        Assert.Equal("account.deactivated", (await RoutingKeysAsync()).Last());
    }

    [Fact]
    public async Task Changing_the_password_needs_the_current_one_and_returns_a_working_token()
    {
        var anaId = await RegisterAsync("ana");
        var asAna = TestServerCallContext.For(anaId, "Client");

        await Assert.ThrowsAsync<InvalidInputException>(() =>
            Auth().ChangePassword(new ChangePasswordRequest { CurrentPassword = "Wrong-pass1", NewPassword = NewPassword }, asAna));

        var changed = await Auth().ChangePassword(new ChangePasswordRequest { CurrentPassword = Password, NewPassword = NewPassword }, asAna);

        Assert.True((await Auth().ValidateToken(new ValidateTokenRequest { Token = changed.Token }, TestServerCallContext.Anonymous())).IsValid);
        Assert.NotEmpty((await LoginAsync("ana@cropconnect.test", NewPassword)).Token);
        Assert.Equal("account.password.changed", (await RoutingKeysAsync()).Last());
    }

    #endregion

    #region Administrator

    [Fact]
    public async Task Users_an_administrator_creates_are_verified_and_invited_to_choose_a_password()
    {
        var adminId = await SeedAdminAsync();

        var created = await Accounts().CreateUser(
            new CreateUserRequest { Username = "rui", Email = "rui@cropconnect.test", Password = Password, Role = "Manager" },
            TestServerCallContext.For(adminId, "Admin"));

        Assert.True(created.EmailVerified);
        Assert.NotEmpty((await LoginAsync("rui@cropconnect.test")).Token);
        Assert.Equal(["account.invited"], await RoutingKeysAsync());

        await ResetAsync(await LastTokenAsync("account.invited"));
        Assert.NotEmpty((await LoginAsync("rui@cropconnect.test", NewPassword)).Token);
    }

    [Fact]
    public async Task Reactivation_is_announced_only_when_the_state_changes()
    {
        var adminId = await SeedAdminAsync();
        var anaId = await RegisterAsync("ana");
        var asAdmin = TestServerCallContext.For(adminId, "Admin");

        await Accounts().SetUserActive(new SetUserActiveRequest { Id = anaId.ToString(), IsActive = true }, asAdmin);
        await Accounts().SetUserActive(new SetUserActiveRequest { Id = anaId.ToString(), IsActive = false }, asAdmin);
        await Accounts().SetUserActive(new SetUserActiveRequest { Id = anaId.ToString(), IsActive = true }, asAdmin);

        Assert.Equal(
            ["account.email-verification.requested", "account.email.verified", "account.deactivated", "account.reactivated"],
            await RoutingKeysAsync());
    }

    #endregion

    [Fact]
    public void Only_the_hash_of_a_token_is_stored()
    {
        var (entity, token) = AccountToken.Issue(Guid.NewGuid(), AccountTokenPurpose.PasswordReset, DateTime.UtcNow, TimeSpan.FromHours(1));

        Assert.NotEqual(token, entity.TokenHash);
        Assert.Equal(AccountToken.Hash(token), entity.TokenHash);
        Assert.Equal(64, entity.TokenHash.Length);
    }
}
