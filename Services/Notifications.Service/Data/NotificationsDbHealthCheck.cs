using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Notifications.Service.Data;

public sealed class NotificationsDbHealthCheck : IHealthCheck
{
    private readonly NotificationsDbContext _dbContext;

    public NotificationsDbHealthCheck(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the notifications database.");
}
