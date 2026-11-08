using System.Globalization;
using System.Text;
using MongoDB.Bson;
using Analytics.Ingestor.Storage;

namespace Analytics.Ingestor.Application;

// Turns each message into the document stored in the raw zone. The payload is kept as it arrived (schema-on-read):
// validating and typing it is the job of the Silver layer in Databricks, so a reading out of range or an event with
// a new field is stored all the same. Only what MongoDB needs to place and find the document is taken out of it.
public sealed class RawMessageHandler
{
    #region Dependencies

    private readonly IRawStore _store;
    private readonly PersonalDataScrubber _scrubber;
    private readonly TimeProvider _timeProvider;

    public RawMessageHandler(IRawStore store, PersonalDataScrubber scrubber, TimeProvider timeProvider)
    {
        _store = store;
        _scrubber = scrubber;
        _timeProvider = timeProvider;
    }

    #endregion

    #region Readings

    // Node-RED publishes { messageId, farmId, sensorId, type, value, timestamp } with the routing key
    // "farm.{farmId}.{type}". The time-series collection needs the measuring time and the sensor (its metaField).
    public async Task<StoreOutcome> HandleReadingAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        var payload = ParseObject(message.Body);
        var receivedAt = Now;
        var routingKeyParts = message.RoutingKey.Split('.');

        var messageId = StringField(payload, "messageId") ?? message.MessageId
            ?? throw new InvalidMessageException("The reading has no messageId.");

        var measuredAt = ParseTime(StringField(payload, "timestamp"));

        var document = new BsonDocument
        {
            ["timestamp"] = measuredAt ?? receivedAt,
            // Readings without a valid time of their own are placed at the time they arrived, and marked.
            ["timestampSource"] = measuredAt is null ? "receivedAt" : "payload",
            ["meta"] = new BsonDocument
            {
                ["farmId"] = StringField(payload, "farmId") ?? Part(routingKeyParts, 1),
                ["sensorId"] = StringField(payload, "sensorId") ?? (BsonValue)BsonNull.Value,
                ["type"] = StringField(payload, "type") ?? Part(routingKeyParts, 2)
            },
            ["messageId"] = messageId,
            ["routingKey"] = message.RoutingKey,
            ["receivedAt"] = receivedAt,
            ["traceParent"] = (BsonValue?)message.TraceParent ?? BsonNull.Value,
            ["payload"] = payload
        };

        return await _store.InsertReadingAsync(document, messageId, cancellationToken)
            ? StoreOutcome.Stored
            : StoreOutcome.Duplicate;
    }

    #endregion

    #region Domain events

    // The producers' outboxes set MessageId (the outbox ID) and Type (the event name); the ID becomes the document's
    // _id, so a redelivered event is a duplicate key and is not stored twice.
    public async Task<StoreOutcome> HandleEventAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.MessageId, out var eventId))
        {
            throw new InvalidMessageException("The event has no valid MessageId.");
        }

        var payload = _scrubber.Scrub(ParseObject(message.Body));

        var document = new BsonDocument
        {
            ["_id"] = eventId.ToString(),
            ["routingKey"] = message.RoutingKey,
            ["eventType"] = (BsonValue?)message.EventType ?? BsonNull.Value,
            ["publishedAt"] = message.PublishedAt is { } publishedAt ? publishedAt : BsonNull.Value,
            ["receivedAt"] = Now,
            ["traceParent"] = (BsonValue?)message.TraceParent ?? BsonNull.Value,
            ["payload"] = payload
        };

        return await _store.InsertEventAsync(document, cancellationToken)
            ? StoreOutcome.Stored
            : StoreOutcome.Duplicate;
    }

    #endregion

    #region Helpers

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    private static BsonDocument ParseObject(ReadOnlyMemory<byte> body)
    {
        try
        {
            return BsonDocument.Parse(Encoding.UTF8.GetString(body.Span));
        }
        catch (Exception exception) when (exception is FormatException or BsonException or EndOfStreamException or InvalidCastException)
        {
            throw new InvalidMessageException("The message body is not a JSON object.");
        }
    }

    private static string? StringField(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString)
            ? value.AsString
            : null;

    private static BsonValue Part(string[] parts, int index) =>
        parts.Length > index && !string.IsNullOrWhiteSpace(parts[index]) ? parts[index] : BsonNull.Value;

    private static DateTime? ParseTime(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time.UtcDateTime
            : null;

    #endregion
}
