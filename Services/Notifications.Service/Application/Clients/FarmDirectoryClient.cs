using Farms.Service.Protos;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Notifications.Service.Models;

namespace Notifications.Service.Application.Clients;

public sealed class FarmDirectoryClient : IFarmDirectoryClient
{
    private readonly FarmDirectoryService.FarmDirectoryServiceClient _client;
    private readonly IMemoryCache _cache;
    private readonly FarmDirectoryClientOptions _options;
    private readonly ILogger<FarmDirectoryClient> _logger;

    public FarmDirectoryClient(
        FarmDirectoryService.FarmDirectoryServiceClient client,
        IMemoryCache cache,
        IOptions<FarmDirectoryClientOptions> options,
        ILogger<FarmDirectoryClient> logger)
    {
        _client = client;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    // Order events only carry IDs; the farm name makes the message readable and rarely changes.
    public async Task<string> GetFarmNameAsync(Guid farmId, CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync($"farm-name:{farmId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _options.CacheDuration;
            return await FetchAsync(farmId, cancellationToken);
        }) ?? throw new NotFoundException(ErrorMessages.FarmNotFound);

    private async Task<string> FetchAsync(Guid farmId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetFarmOwnershipAsync(
                new GetFarmOwnershipRequest { FarmId = farmId.ToString() },
                headers: new Metadata { { "x-api-key", _options.ApiKey } },
                cancellationToken: cancellationToken);

            return response.Name;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new NotFoundException(ErrorMessages.FarmNotFound);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to resolve farm {FarmId}: {Detail}", farmId, ex.Status.Detail);
            throw new InvalidOperationException($"Farms service communication failure: {ex.Status.Detail}", ex);
        }
    }
}
