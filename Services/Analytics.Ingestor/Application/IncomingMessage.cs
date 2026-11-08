namespace Analytics.Ingestor.Application;

// What the consumer hands over from an AMQP delivery: the body and the properties worth keeping.
public sealed record IncomingMessage(
    string RoutingKey,
    string? MessageId,
    string? EventType,
    DateTime? PublishedAt,
    string? TraceParent,
    ReadOnlyMemory<byte> Body);

public enum StoreOutcome
{
    Stored,

    // Already stored by an earlier delivery: the message is acknowledged and nothing changes.
    Duplicate
}

// A message that can never be stored (not JSON, no ID), so retrying is pointless: it goes to the dead-letter queue.
public sealed class InvalidMessageException(string message) : Exception(message);
