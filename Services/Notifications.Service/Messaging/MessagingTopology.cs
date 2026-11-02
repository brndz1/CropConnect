using RabbitMQ.Client;

namespace Notifications.Service.Messaging;

// Notifications listens to the domain events other services publish on the shared exchange.
// Declaring the topology is idempotent, so the startup order of producers and consumers doesn't matter.
public static class MessagingTopology
{
    public const string EventsExchange = "cropconnect.events";

    // Orders -> Notifications (RF15, RF19, RF20).
    public const string OrderCreatedRoutingKey = "order.created";
    public const string OrderStatusChangedRoutingKey = "order.status.changed";
    public const string OrderCancelledRoutingKey = "order.cancelled";
    public const string ReviewCreatedRoutingKey = "review.created";

    // Sensors -> Notifications (RF21).
    public const string SensorAlertRaisedRoutingKey = "sensor.alert.raised";

    // Auth -> Notifications: the account emails.
    public const string EmailVerificationRequestedRoutingKey = "account.email-verification.requested";
    public const string PasswordResetRequestedRoutingKey = "account.password-reset.requested";
    public const string InvitedRoutingKey = "account.invited";
    public const string EmailVerifiedRoutingKey = "account.email.verified";
    public const string PasswordChangedRoutingKey = "account.password.changed";
    public const string DeactivatedRoutingKey = "account.deactivated";
    public const string ReactivatedRoutingKey = "account.reactivated";

    public const string EventsQueue = "notifications-service.events";

    // Events that can never be turned into a notification (malformed, unsupported) end up here.
    public const string EventsDeadLetterExchange = "notifications-service.events.dead-letter";
    public const string EventsDeadLetterQueue = "notifications-service.events.dead-letter";

    private static readonly string[] Bindings =
    [
        OrderCreatedRoutingKey,
        OrderStatusChangedRoutingKey,
        OrderCancelledRoutingKey,
        ReviewCreatedRoutingKey,
        SensorAlertRaisedRoutingKey,
        EmailVerificationRequestedRoutingKey,
        PasswordResetRequestedRoutingKey,
        InvitedRoutingKey,
        EmailVerifiedRoutingKey,
        PasswordChangedRoutingKey,
        DeactivatedRoutingKey,
        ReactivatedRoutingKey
    ];

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(EventsDeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(EventsDeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(EventsDeadLetterQueue, EventsDeadLetterExchange, routingKey: string.Empty, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            EventsQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = EventsDeadLetterExchange },
            cancellationToken: cancellationToken);

        foreach (var routingKey in Bindings)
        {
            await channel.QueueBindAsync(EventsQueue, EventsExchange, routingKey, cancellationToken: cancellationToken);
        }
    }
}
