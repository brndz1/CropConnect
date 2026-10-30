using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using AuthGrpc = Auth.Service.Protos;

namespace CropConnect.ApiGateway.Infrastructure;

// RF03: deactivating an account must cut its access at once, not when its token expires. The services only
// check the token's signature, so the gateway asks Auth.Service whether the user is still active, caching
// the answer briefly to keep that call off most requests.
public sealed class ActiveUserCheck
{
    private readonly AuthGrpc.AuthService.AuthServiceClient _auth;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;

    public ActiveUserCheck(AuthGrpc.AuthService.AuthServiceClient auth, IMemoryCache cache, IOptions<JwtOptions> options)
    {
        _auth = auth;
        _cache = cache;
        _cacheDuration = options.Value.ActiveUserCacheDuration;
    }

    // Throws RpcException when Auth.Service is unreachable: the request then fails with 503 instead of
    // letting a possibly deactivated user through.
    public async Task<bool> IsActiveAsync(string userId, string token, CancellationToken cancellationToken)
    {
        if (_cacheDuration <= TimeSpan.Zero)
        {
            return await AskAuthAsync(userId, token, cancellationToken);
        }

        return await _cache.GetOrCreateAsync($"active-user:{userId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
            return AskAuthAsync(userId, token, cancellationToken);
        });
    }

    private async Task<bool> AskAuthAsync(string userId, string token, CancellationToken cancellationToken)
    {
        var response = await _auth.ValidateTokenAsync(
            new AuthGrpc.ValidateTokenRequest { Token = token },
            cancellationToken: cancellationToken);

        return response.IsValid && response.UserId == userId;
    }
}
