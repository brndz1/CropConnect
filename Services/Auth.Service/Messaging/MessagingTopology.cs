using RabbitMQ.Client;

namespace Auth.Service.Messaging;

// Account events go to the shared domain events exchange, where Notifications.Service turns them into emails.
// Declaring the exchange is idempotent, so the startup order of producers and consumers doesn't matter.
public static class MessagingTopology
{
    public const string EventsExchange = "cropconnect.events";

    // With a one-time link (the token travels in the event, the email is the only place it is shown).
    public const string EmailVerificationRequestedRoutingKey = "account.email-verification.requested";
    public const string PasswordResetRequestedRoutingKey = "account.password-reset.requested";
    public const string InvitedRoutingKey = "account.invited";

    // Information only.
    public const string EmailVerifiedRoutingKey = "account.email.verified";
    public const string PasswordChangedRoutingKey = "account.password.changed";
    public const string DeactivatedRoutingKey = "account.deactivated";
    public const string ReactivatedRoutingKey = "account.reactivated";

    public static Task DeclareEventsAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.ExchangeDeclareAsync(EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
}
