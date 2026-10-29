using Auth.Service.Protos;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Notifications.Service.Models;

namespace Notifications.Service.Application.Clients;

public sealed class UserDirectoryClient : IUserDirectoryClient
{
    private readonly UserDirectoryService.UserDirectoryServiceClient _client;
    private readonly IMemoryCache _cache;
    private readonly UserDirectoryClientOptions _options;
    private readonly ILogger<UserDirectoryClient> _logger;

    public UserDirectoryClient(
        UserDirectoryService.UserDirectoryServiceClient client,
        IMemoryCache cache,
        IOptions<UserDirectoryClientOptions> options,
        ILogger<UserDirectoryClient> logger)
    {
        _client = client;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    // Cached briefly: a changed email or a deactivated account is picked up within CacheDuration.
    public async Task<UserContact> GetContactAsync(Guid userId, CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync($"user-contact:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _options.CacheDuration;
            return await FetchAsync(userId, cancellationToken);
        }) ?? throw new NotFoundException(ErrorMessages.UserNotFound);

    private async Task<UserContact> FetchAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetUserContactAsync(
                new GetUserContactRequest { UserId = userId.ToString() },
                headers: new Metadata { { "x-api-key", _options.ApiKey } },
                cancellationToken: cancellationToken);

            return new UserContact(Guid.Parse(response.UserId), response.Username, response.Email, response.IsActive);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new NotFoundException(ErrorMessages.UserNotFound);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to resolve contact of user {UserId}: {Detail}", userId, ex.Status.Detail);
            throw new InvalidOperationException($"Auth service communication failure: {ex.Status.Detail}", ex);
        }
    }
}
