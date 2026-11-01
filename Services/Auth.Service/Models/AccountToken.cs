using System.Security.Cryptography;
using System.Text;

namespace Auth.Service.Models;

public enum AccountTokenPurpose
{
    EmailVerification = 1,

    // Also used for the invitation an administrator sends: the new user sets their own password with it.
    PasswordReset = 2
}

// A one-time link sent by email. Only the SHA-256 of the token is stored, so a copy of the database
// cannot be used to verify an email or reset a password.
public sealed class AccountToken
{
    private AccountToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public AccountTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? UsedAt { get; private set; }

    // Returns the entity to save and the token to put in the email (never stored).
    public static (AccountToken Entity, string Token) Issue(Guid userId, AccountTokenPurpose purpose, DateTime now, TimeSpan lifetime)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        var entity = new AccountToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = purpose,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };

        return (entity, token);
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool IsUsable(DateTime now) => UsedAt is null && ExpiresAt > now;

    public void MarkUsed(DateTime now) => UsedAt ??= now;

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
