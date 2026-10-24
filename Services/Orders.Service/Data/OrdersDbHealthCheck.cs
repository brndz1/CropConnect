using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orders.Service.Data;

public sealed class OrdersDbHealthCheck : IHealthCheck
{
    private readonly OrdersDbContext _dbContext;

    public OrdersDbHealthCheck(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the orders database.");
}
