namespace Sensors.Service.Application.Events;

public interface IAlertEventPublisher
{
    Task PublishAlertRaisedAsync(SensorAlertRaisedEvent @event, CancellationToken cancellationToken);
}

public sealed class LoggingAlertEventPublisher : IAlertEventPublisher
{
    private readonly ILogger<LoggingAlertEventPublisher> _logger;

    public LoggingAlertEventPublisher(ILogger<LoggingAlertEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAlertRaisedAsync(SensorAlertRaisedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Event Published: SensorAlertRaised [AlertId: {AlertId}, FarmId: {FarmId}, {Description}]",
            @event.AlertId, @event.FarmId, @event.Description);
        return Task.CompletedTask;
    }
}
