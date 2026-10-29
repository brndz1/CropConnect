using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Users.Service.Data;

public sealed class UsersDbHealthCheck : IHealthCheck
{
    private readonly UsersDbContext _dbContext;

    public UsersDbHealthCheck(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the users database.");
}
