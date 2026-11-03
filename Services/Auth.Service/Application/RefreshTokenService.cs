using Auth.Service.Data;
using Auth.Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.Service.Application;

// Issues, rotates and revokes the refresh tokens of the login sessions (see RefreshToken).
public sealed class RefreshTokenService
{
    public const string InvalidMessage = "The session has expired. Log in again.";

    private readonly AuthDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly AccountOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        AuthDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<AccountOptions> options,
        ILogger<RefreshTokenService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    // Adds a token to the context, of a new session unless a family is given; the caller saves it.
    public string Issue(Guid userId, Guid? familyId = null)
    {
        var (entity, token) = RefreshToken.Issue(userId, familyId ?? Guid.NewGuid(), Now, _options.RefreshTokenLifetime);
        _dbContext.RefreshTokens.Add(entity);
        return token;
    }

    // Exchanges a token for its successor (saved) and returns the user to issue the new JWT for.
    public async Task<(User User, string Token)> RotateAsync(string token, CancellationToken cancellationToken)
    {
        var now = Now;
        var hash = AccountToken.Hash(token ?? string.Empty);
        var current = await _dbContext.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new UnauthenticatedException(InvalidMessage);

        if (current.RevokedAt is not null || current.ExpiresAt <= now)
        {
            throw new UnauthenticatedException(InvalidMessage);
        }

        if (current.UsedAt is not null)
        {
            await RevokeReusedFamilyAsync(current, cancellationToken);
        }

        // A deactivated account, or a password changed after the login, ends the session.
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == current.UserId, cancellationToken);
        if (user is not { IsActive: true } || (user.PasswordChangedAt is { } changedAt && current.CreatedAt < changedAt))
        {
            await RevokeFamilyAsync(current.FamilyId, cancellationToken);
            throw new UnauthenticatedException(InvalidMessage);
        }

        // Only one exchange of a token can win, even when two arrive at the same time.
        var claimed = await _dbContext.RefreshTokens
            .Where(t => t.Id == current.Id && t.UsedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAt, now), cancellationToken);
        if (claimed == 0)
        {
            await RevokeReusedFamilyAsync(current, cancellationToken);
        }

        var next = Issue(user.Id, current.FamilyId);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (user, next);
    }

    // Logout: ends the session the token belongs to. Unknown tokens are ignored.
    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        var hash = AccountToken.Hash(token ?? string.Empty);
        var familyId = await _dbContext.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (familyId is not null)
        {
            await RevokeFamilyAsync(familyId.Value, cancellationToken);
        }
    }

    private async Task RevokeReusedFamilyAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Refresh token of user {UserId} was used twice; session {FamilyId} revoked.", token.UserId, token.FamilyId);
        await RevokeFamilyAsync(token.FamilyId, cancellationToken);
        throw new UnauthenticatedException(InvalidMessage);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var now = Now;
        return _dbContext.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }
}
