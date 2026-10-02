using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Farms.Service.Data;

public sealed class FarmsDbHealthCheck : IHealthCheck
{
    private readonly FarmsDbContext _dbContext;

    public FarmsDbHealthCheck(FarmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the farms database.");
}
