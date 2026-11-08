using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace Analytics.Ingestor.Storage;

public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly MongoRawStore _store;

    public MongoHealthCheck(MongoRawStore store)
    {
        _store = store;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Cannot reach MongoDB.", exception);
        }
    }
}
