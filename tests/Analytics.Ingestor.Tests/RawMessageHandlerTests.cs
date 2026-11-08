using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Analytics.Ingestor.Application;

namespace Analytics.Ingestor.Tests;

// The documents the raw zone gets from the messages Node-RED, Orders, Sensors and Auth publish.
public sealed class RawMessageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeRawStore _store = new();
    private readonly Guid _farmId = Guid.NewGuid();

    private RawMessageHandler Handler() =>
        new(_store, Scrubber(), new FixedClock(Now));

    internal static PersonalDataScrubber Scrubber() =>
        new(Options.Create(new PseudonymizationOptions { Key = "test-pseudonymization-key-0123456789abcdef" }));

    private static IncomingMessage Message(string routingKey, string json, string? messageId = null, string? type = null) =>
        new(routingKey, messageId, type, new DateTime(2026, 11, 8, 11, 59, 59, DateTimeKind.Utc), "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", Encoding.UTF8.GetBytes(json));

    private string Reading(string messageId, string type = "humidity", string value = "42.5", string timestamp = "2026-11-08T11:59:30Z") =>
        $$"""{"messageId":"{{messageId}}","farmId":"{{_farmId}}","sensorId":"sim-1","type":"{{type}}","value":{{value}},"timestamp":"{{timestamp}}"}""";

    #region Readings

    [Fact]
    public async Task A_reading_is_stored_with_its_time_and_sensor_and_the_payload_as_it_arrived()
    {
        var messageId = Guid.NewGuid().ToString();

        var outcome = await Handler().HandleReadingAsync(Message($"farm.{_farmId}.humidity", Reading(messageId)), CancellationToken.None);

        Assert.Equal(StoreOutcome.Stored, outcome);
        var document = Assert.Single(_store.Readings);
        Assert.Equal(new DateTime(2026, 11, 8, 11, 59, 30, DateTimeKind.Utc), document["timestamp"].ToUniversalTime());
        Assert.Equal("payload", document["timestampSource"].AsString);
        Assert.Equal(_farmId.ToString(), document["meta"]["farmId"].AsString);
        Assert.Equal("sim-1", document["meta"]["sensorId"].AsString);
        Assert.Equal("humidity", document["meta"]["type"].AsString);
        Assert.Equal(messageId, document["messageId"].AsString);
        Assert.Equal(Now.UtcDateTime, document["receivedAt"].ToUniversalTime());
        Assert.StartsWith("00-0af7651916cd43dd", document["traceParent"].AsString);
        Assert.Equal(42.5, document["payload"]["value"].AsDouble);
    }

    [Fact]
    public async Task A_redelivered_reading_is_not_stored_twice()
    {
        var message = Message($"farm.{_farmId}.humidity", Reading(Guid.NewGuid().ToString()));

        Assert.Equal(StoreOutcome.Stored, await Handler().HandleReadingAsync(message, CancellationToken.None));
        Assert.Equal(StoreOutcome.Duplicate, await Handler().HandleReadingAsync(message, CancellationToken.None));
        Assert.Single(_store.Readings);
    }

    [Fact]
    public async Task An_out_of_range_reading_is_kept_for_the_silver_layer_to_quarantine()
    {
        await Handler().HandleReadingAsync(Message($"farm.{_farmId}.humidity", Reading(Guid.NewGuid().ToString(), value: "150")), CancellationToken.None);

        Assert.Equal(150, Assert.Single(_store.Readings)["payload"]["value"].ToDouble());
    }

    [Fact]
    public async Task A_reading_without_a_valid_time_is_placed_when_it_arrived()
    {
        await Handler().HandleReadingAsync(Message($"farm.{_farmId}.temperature", Reading(Guid.NewGuid().ToString(), "temperature", "21", "yesterday")), CancellationToken.None);

        var document = Assert.Single(_store.Readings);
        Assert.Equal(Now.UtcDateTime, document["timestamp"].ToUniversalTime());
        Assert.Equal("receivedAt", document["timestampSource"].AsString);
        Assert.Equal("yesterday", document["payload"]["timestamp"].AsString);
    }

    [Fact]
    public async Task The_sensor_comes_from_the_routing_key_when_the_payload_lacks_it()
    {
        var json = $$"""{"messageId":"{{Guid.NewGuid()}}","value":20,"timestamp":"2026-11-08T11:59:30Z"}""";

        await Handler().HandleReadingAsync(Message($"farm.{_farmId}.temperature", json), CancellationToken.None);

        var meta = Assert.Single(_store.Readings)["meta"];
        Assert.Equal(_farmId.ToString(), meta["farmId"].AsString);
        Assert.Equal("temperature", meta["type"].AsString);
        Assert.Equal(BsonNull.Value, meta["sensorId"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{"farmId":"x","value":1}""")]
    public async Task A_reading_that_cannot_be_stored_is_rejected(string json)
    {
        await Assert.ThrowsAsync<InvalidMessageException>(() =>
            Handler().HandleReadingAsync(Message($"farm.{_farmId}.humidity", json), CancellationToken.None));
        Assert.Empty(_store.Readings);
    }

    #endregion

    #region Domain events

    [Fact]
    public async Task An_event_is_stored_under_its_message_id()
    {
        var eventId = Guid.NewGuid();
        var json = $$"""{"orderId":"{{Guid.NewGuid()}}","farmId":"{{_farmId}}","totalAmount":12.5,"itemCount":1,"items":[{"productId":"p","productName":"Alface","quantity":5,"unitPrice":2.5}],"occurredAt":"2026-11-08T11:59:59Z"}""";

        var outcome = await Handler().HandleEventAsync(Message("order.created", json, eventId.ToString(), "OrderCreated"), CancellationToken.None);

        Assert.Equal(StoreOutcome.Stored, outcome);
        var document = Assert.Single(_store.Events);
        Assert.Equal(eventId.ToString(), document["_id"].AsString);
        Assert.Equal("order.created", document["routingKey"].AsString);
        Assert.Equal("OrderCreated", document["eventType"].AsString);
        Assert.Equal(new DateTime(2026, 11, 8, 11, 59, 59, DateTimeKind.Utc), document["publishedAt"].ToUniversalTime());
        Assert.Equal(Now.UtcDateTime, document["receivedAt"].ToUniversalTime());
        Assert.Equal(12.5, document["payload"]["totalAmount"].AsDouble);
        Assert.Equal("Alface", document["payload"]["items"][0]["productName"].AsString);
    }

    [Fact]
    public async Task A_redelivered_event_is_not_stored_twice()
    {
        var message = Message("review.created", """{"rating":5}""", Guid.NewGuid().ToString(), "ReviewCreated");

        Assert.Equal(StoreOutcome.Stored, await Handler().HandleEventAsync(message, CancellationToken.None));
        Assert.Equal(StoreOutcome.Duplicate, await Handler().HandleEventAsync(message, CancellationToken.None));
        Assert.Single(_store.Events);
    }

    [Fact]
    public async Task Account_events_lose_the_token_and_the_username_and_keep_a_hash_of_the_email()
    {
        var json = """{"userId":"u1","email":"Ana@Example.com","username":"ana","role":"Client","token":"secret-link-token","expiresAt":"2026-11-09T12:00:00Z","occurredAt":"2026-11-08T12:00:00Z"}""";

        await Handler().HandleEventAsync(Message("account.email-verification.requested", json, Guid.NewGuid().ToString()), CancellationToken.None);

        var payload = Assert.Single(_store.Events)["payload"].AsBsonDocument;
        Assert.False(payload.Contains("token"));
        Assert.False(payload.Contains("username"));
        Assert.False(payload.Contains("email"));
        Assert.Equal(Scrubber().HashEmail("ana@example.com"), payload["emailHash"].AsString);
        Assert.Equal("Client", payload["role"].AsString);
        Assert.DoesNotContain("secret-link-token", payload.ToJson());
        Assert.DoesNotContain("Ana@Example.com", payload.ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task An_event_without_a_valid_message_id_is_rejected(string? messageId)
    {
        await Assert.ThrowsAsync<InvalidMessageException>(() =>
            Handler().HandleEventAsync(Message("order.created", "{}", messageId), CancellationToken.None));
    }

    [Fact]
    public async Task An_event_that_is_not_json_is_rejected()
    {
        await Assert.ThrowsAsync<InvalidMessageException>(() =>
            Handler().HandleEventAsync(Message("order.created", "{oops", Guid.NewGuid().ToString()), CancellationToken.None));
    }

    #endregion
}
