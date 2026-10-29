namespace Sensors.Service.Models;

// A domain event saved in the same transaction as the change that produced it and published
// to RabbitMQ afterwards, so an event is never lost when the broker is down (at-least-once).
public sealed class OutboxMessage
{
    #region Properties

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTime OccurredAt { get; private set; }

    // W3C trace context of the request that raised the event, resumed when the message is published.
    public string? TraceParent { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    #endregion

    #region Factory

    public static OutboxMessage Create(string eventType, string routingKey, string payload, DateTime now, string? traceParent = null) => new()
    {
        Id = Guid.NewGuid(),
        EventType = Guard.Required(eventType, "Event type", FieldLimits.EventTypeMaxLength),
        RoutingKey = Guard.Required(routingKey, "Routing key", FieldLimits.RoutingKeyMaxLength),
        Payload = Guard.Required(payload, "Payload", int.MaxValue),
        OccurredAt = now,
        TraceParent = traceParent is { Length: <= FieldLimits.TraceParentMaxLength } ? traceParent : null
    };

    #endregion

    #region Behaviour

    public void MarkPublished(DateTime now)
    {
        Attempts++;
        PublishedAt = now;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > FieldLimits.OutboxErrorMaxLength ? error[..FieldLimits.OutboxErrorMaxLength] : error;
    }

    #endregion
}
