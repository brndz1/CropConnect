using System.Security.Cryptography;

namespace Auth.Service.Models;

// A long-lived token that gets a new short-lived JWT without asking for the password again. It is used once:
// each refresh replaces it with a new one of the same family (rotation). Presenting a token that was already
// used means it was copied, so the whole family is revoked and that session has to log in again.
// Like the account tokens, only the SHA-256 of the token is stored.
public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    // Every token of one login session, from the login to the logout.
    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    // When it was exchanged for its successor.
    public DateTime? UsedAt { get; private set; }

    // Logout, or reuse of a token of the family.
    public DateTime? RevokedAt { get; private set; }

    public static (RefreshToken Entity, string Token) Issue(Guid userId, Guid familyId, DateTime now, TimeSpan lifetime)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = AccountToken.Hash(token),
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };

        return (entity, token);
    }

    public bool IsUsable(DateTime now) => UsedAt is null && RevokedAt is null && ExpiresAt > now;
}
