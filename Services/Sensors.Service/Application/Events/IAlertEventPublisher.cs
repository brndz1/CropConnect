using System.Text.Json;
using System.Text.Json.Serialization;
using Sensors.Service.Data;
using Sensors.Service.Messaging;
using Sensors.Service.Models;

namespace Sensors.Service.Application.Events;

public interface IAlertEventPublisher
{
    // Stages the event in the current unit of work; it is only sent once SaveChanges commits it.
    void StageAlertRaised(SensorAlertRaisedEvent @event);
}

public sealed class OutboxAlertEventPublisher : IAlertEventPublisher
{
    public const string AlertRaisedEventType = "SensorAlertRaised";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SensorsDbContext _dbContext;

    public OutboxAlertEventPublisher(SensorsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void StageAlertRaised(SensorAlertRaisedEvent @event) =>
        _dbContext.OutboxMessages.Add(OutboxMessage.Create(
            AlertRaisedEventType,
            MessagingTopology.AlertRaisedRoutingKey,
            JsonSerializer.Serialize(@event, SerializerOptions),
            @event.OccurredAt,
            MessagingTelemetry.CurrentTraceParent()));
}
