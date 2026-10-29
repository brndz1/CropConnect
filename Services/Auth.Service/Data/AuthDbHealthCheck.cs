using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Auth.Service.Data;

public sealed class AuthDbHealthCheck : IHealthCheck
{
    private readonly AuthDbContext _dbContext;

    public AuthDbHealthCheck(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the auth database.");
}
