using System.IdentityModel.Tokens.Jwt;
using Auth.Service.Models;
using Auth.Service.Tests.Infrastructure;

namespace Auth.Service.Tests;

public sealed class UserInputAndJwtTests
{
    #region Input

    [Theory]
    [InlineData("ab")]
    [InlineData("   ab   ")]
    public void Username_must_have_at_least_three_characters(string username)
    {
        Assert.Throws<InvalidInputException>(() => UserInput.Username(username));
    }

    [Fact]
    public void Username_is_trimmed_and_capped()
    {
        Assert.Equal("ana", UserInput.Username("  ana  "));
        Assert.Throws<InvalidInputException>(() => UserInput.Username(new string('a', 51)));
    }

    [Fact]
    public void Email_is_normalised_to_lower_case()
    {
        Assert.Equal("ana@cropconnect.test", UserInput.Email("  Ana@CropConnect.TEST "));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Ana Silva <ana@cropconnect.test>")]
    [InlineData("ana@")]
    public void Malformed_emails_are_rejected(string email)
    {
        Assert.Throws<InvalidInputException>(() => UserInput.Email(email));
    }

    [Fact]
    public void Password_is_eight_characters_to_seventy_two_bytes()
    {
        Assert.Throws<InvalidInputException>(() => UserInput.Password("Short-1"));
        Assert.Equal("Passw0rd", UserInput.Password("Passw0rd"));

        // 36 two-byte characters fit BCrypt's 72 bytes; one more does not.
        UserInput.Password(new string('é', 36));
        Assert.Throws<InvalidInputException>(() => UserInput.Password(new string('é', 37)));
    }

    [Theory]
    [InlineData("client", Role.Client)]
    [InlineData("MANAGER", Role.Manager)]
    public void Role_is_case_insensitive(string value, Role expected)
    {
        Assert.Equal(expected, UserInput.Role(value));
    }

    [Theory]
    [InlineData("farmer")]
    [InlineData("7")]
    [InlineData("")]
    public void Unknown_roles_are_rejected(string value)
    {
        Assert.Throws<InvalidInputException>(() => UserInput.Role(value));
    }

    #endregion

    #region JWT

    private static readonly User Ana = new() { Id = Guid.NewGuid(), Username = "ana", Email = "ana@cropconnect.test", Role = Role.Manager };

    [Fact]
    public void Token_carries_the_user_id_email_and_role()
    {
        var principal = TestJwt.Validator().Validate(TestJwt.Generator().GenerateToken(Ana));

        Assert.NotNull(principal);
        Assert.Equal(Ana.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(Ana.Email, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal("Manager", principal.FindFirst("role")?.Value);
    }

    [Fact]
    public void Token_signed_with_another_secret_is_rejected()
    {
        var token = TestJwt.Generator(secret: "AnotherSecretKeyThatIsLongEnough-9876543210").GenerateToken(Ana);

        Assert.Null(TestJwt.Validator().Validate(token));
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var token = TestJwt.Generator(expirationMinutes: -5).GenerateToken(Ana);

        Assert.Null(TestJwt.Validator().Validate(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not.a.token")]
    public void Garbage_is_not_a_token(string token)
    {
        Assert.Null(TestJwt.Validator().Validate(token));
    }

    #endregion
}
