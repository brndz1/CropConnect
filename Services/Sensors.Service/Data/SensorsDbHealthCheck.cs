using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sensors.Service.Data;

public sealed class SensorsDbHealthCheck : IHealthCheck
{
    private readonly SensorsDbContext _dbContext;

    public SensorsDbHealthCheck(SensorsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the sensors database.");
}
