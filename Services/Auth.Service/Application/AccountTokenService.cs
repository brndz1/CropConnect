using Auth.Service.Data;
using Auth.Service.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.Service.Application;

// Issues and redeems the one-time links of the account emails. Nothing is saved here: the caller saves the token,
// its outbox event and the account change together.
public sealed class AccountTokenService
{
    public const string InvalidLinkMessage = "This link is invalid or has expired. Request a new one.";

    private readonly AuthDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly AccountOptions _options;

    public AccountTokenService(AuthDbContext dbContext, TimeProvider timeProvider, IOptions<AccountOptions> options)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    public AccountOptions Options => _options;

    // False (and nothing issued) when a link of the same kind went out less than ResendCooldown ago.
    public async Task<bool> IssueAsync(
        User user,
        AccountTokenPurpose purpose,
        TimeSpan lifetime,
        string routingKey,
        bool respectCooldown,
        CancellationToken cancellationToken)
    {
        var now = Now;
        var previous = await _dbContext.AccountTokens
            .Where(t => t.UserId == user.Id && t.Purpose == purpose && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        if (respectCooldown && previous.Any(t => t.CreatedAt > now - _options.ResendCooldown))
        {
            return false;
        }

        // Only the newest link works.
        foreach (var token in previous)
        {
            token.MarkUsed(now);
        }

        var (entity, raw) = AccountToken.Issue(user.Id, purpose, now, lifetime);
        _dbContext.AccountTokens.Add(entity);
        AccountEvents.AddTokenEvent(_dbContext, routingKey, user, raw, entity.ExpiresAt, now);
        return true;
    }

    // The token and its user, or InvalidInputException when the link is unknown, used, expired or the account inactive.
    public async Task<(AccountToken Token, User User)> RedeemAsync(string token, AccountTokenPurpose purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > FieldLimits.TokenMaxLength)
        {
            throw new InvalidInputException(InvalidLinkMessage);
        }

        var hash = AccountToken.Hash(token.Trim());
        var entity = await _dbContext.AccountTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, cancellationToken);
        var user = entity is null ? null : await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == entity.UserId, cancellationToken);

        if (entity is null || user is null || !user.IsActive || !entity.IsUsable(Now))
        {
            throw new InvalidInputException(InvalidLinkMessage);
        }

        entity.MarkUsed(Now);
        return (entity, user);
    }
}
