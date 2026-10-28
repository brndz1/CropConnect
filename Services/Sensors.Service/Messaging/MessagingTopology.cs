using RabbitMQ.Client;

namespace Sensors.Service.Messaging;

// Exchanges and queues used by this service. Declaring them is idempotent, so every
// component declares what it needs and the startup order of producers and consumers doesn't matter.
public static class MessagingTopology
{
    #region Readings (Node-RED -> Sensors)

    // Node-RED publishes each reading with the routing key "farm.{farmId}.{type}".
    public const string ReadingsExchange = "sensors.readings";
    public const string ReadingsQueue = "sensors-service.readings";
    public const string ReadingsBindingKey = "farm.#";

    // Readings that can never be processed (malformed, unknown farm...) end up here for inspection.
    public const string ReadingsDeadLetterExchange = "sensors.readings.dead-letter";
    public const string ReadingsDeadLetterQueue = "sensors-service.readings.dead-letter";

    #endregion

    #region Domain events (Sensors -> Notifications)

    public const string EventsExchange = "cropconnect.events";
    public const string AlertRaisedRoutingKey = "sensor.alert.raised";

    #endregion

    public static async Task DeclareReadingsAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(ReadingsDeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(ReadingsDeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(ReadingsDeadLetterQueue, ReadingsDeadLetterExchange, routingKey: string.Empty, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(ReadingsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            ReadingsQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = ReadingsDeadLetterExchange },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(ReadingsQueue, ReadingsExchange, ReadingsBindingKey, cancellationToken: cancellationToken);
    }

    public static Task DeclareEventsAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.ExchangeDeclareAsync(EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
}
