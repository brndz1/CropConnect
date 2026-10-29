using RabbitMQ.Client;

namespace Orders.Service.Messaging;

// Order events go to the shared domain events exchange, where Notification.Service picks them up.
// Declaring the exchange is idempotent, so the startup order of producers and consumers doesn't matter.
public static class MessagingTopology
{
    public const string EventsExchange = "cropconnect.events";
    public const string OrderCreatedRoutingKey = "order.created";
    public const string OrderStatusChangedRoutingKey = "order.status.changed";
    public const string OrderCancelledRoutingKey = "order.cancelled";

    public static Task DeclareEventsAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.ExchangeDeclareAsync(EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
}
