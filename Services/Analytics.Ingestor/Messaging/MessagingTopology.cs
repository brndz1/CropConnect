using RabbitMQ.Client;

namespace Analytics.Ingestor.Messaging;

// The ingestor has its own queues on the two exchanges that already exist, so it gets a copy of every message
// without taking any from Sensors or Notifications. Declaring them is idempotent, like everywhere else.
public static class MessagingTopology
{
    #region Readings (Node-RED -> Analytics)

    public const string ReadingsExchange = "sensors.readings";
    public const string ReadingsQueue = "analytics-ingestor.readings";
    public const string ReadingsBindingKey = "farm.#";

    #endregion

    #region Domain events (Orders, Sensors, Auth -> Analytics)

    public const string EventsExchange = "cropconnect.events";
    public const string EventsQueue = "analytics-ingestor.events";

    // Every event, the ones that exist today and the ones added later: the raw zone keeps everything.
    public const string EventsBindingKey = "#";

    #endregion

    // Messages that cannot be stored at all (not JSON, no MessageId) end up here for inspection.
    public const string DeadLetterExchange = "analytics-ingestor.dead-letter";
    public const string DeadLetterQueue = "analytics-ingestor.dead-letter";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, routingKey: string.Empty, cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange };

        await channel.ExchangeDeclareAsync(ReadingsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(ReadingsQueue, durable: true, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(ReadingsQueue, ReadingsExchange, ReadingsBindingKey, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(EventsQueue, durable: true, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(EventsQueue, EventsExchange, EventsBindingKey, cancellationToken: cancellationToken);
    }
}
