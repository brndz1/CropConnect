using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Notifications.Service.Messaging;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConnectionProvider _connections;

    public RabbitMqHealthCheck(RabbitMqConnectionProvider connections)
    {
        _connections = connections;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connections.GetConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The RabbitMQ connection is closed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to RabbitMQ.", exception);
        }
    }
}
