using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notifications.Service.Application.Clients;
using Notifications.Service.Application.Email;
using Notifications.Service.Application.Events;
using Notifications.Service.Data;
using Notifications.Service.Messaging;
using Notifications.Service.Models;

namespace Notifications.Service.Application;

public interface INotificationIngestionService
{
    // Turns one domain event into the notification for whoever must be told about it.
    // Returns false when the event had already been handled (redelivery).
    Task<bool> HandleAsync(string routingKey, Guid eventId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);
}

public sealed class NotificationIngestionService : INotificationIngestionService
{
    #region Dependencies

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("pt-PT");

    private readonly NotificationsDbContext _dbContext;
    private readonly IFarmDirectoryClient _farms;
    private readonly TimeProvider _timeProvider;
    private readonly EmailOptions _email;
    private readonly ILogger<NotificationIngestionService> _logger;

    public NotificationIngestionService(
        NotificationsDbContext dbContext,
        IFarmDirectoryClient farms,
        TimeProvider timeProvider,
        IOptions<EmailOptions> email,
        ILogger<NotificationIngestionService> logger)
    {
        _dbContext = dbContext;
        _farms = farms;
        _timeProvider = timeProvider;
        _email = email.Value;
        _logger = logger;
    }

    #endregion

    #region Handling

    public async Task<bool> HandleAsync(string routingKey, Guid eventId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        Guard.NotEmpty(eventId, "Event ID");

        if (routingKey.StartsWith(AccountEventPrefix, StringComparison.Ordinal))
        {
            return await HandleAccountEventAsync(routingKey, eventId, body, cancellationToken);
        }

        var draft = routingKey switch
        {
            MessagingTopology.OrderCreatedRoutingKey => await FromOrderCreatedAsync(Parse<OrderCreatedMessage>(body), cancellationToken),
            MessagingTopology.OrderStatusChangedRoutingKey => await FromOrderStatusChangedAsync(Parse<OrderStatusChangedMessage>(body), cancellationToken),
            MessagingTopology.OrderCancelledRoutingKey => await FromOrderCancelledAsync(Parse<OrderCancelledMessage>(body), cancellationToken),
            MessagingTopology.SensorAlertRaisedRoutingKey => FromSensorAlert(Parse<SensorAlertRaisedMessage>(body)),
            _ => throw new InvalidInputException($"Unsupported event '{routingKey}'.")
        };

        if (draft is null)
        {
            return false;
        }

        if (await _dbContext.Notifications.AnyAsync(n => n.SourceEventId == eventId && n.RecipientId == draft.RecipientId, cancellationToken))
        {
            return false;
        }

        _dbContext.Notifications.Add(Notification.Create(
            eventId,
            draft.RecipientId,
            draft.Type,
            draft.Title,
            draft.Message,
            draft.RelatedEntityId,
            draft.FarmId,
            _timeProvider.GetUtcNow().UtcDateTime,
            MessagingTelemetry.CurrentTraceParent()));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // The same event was handled concurrently by another delivery.
            return false;
        }
    }

    private static TMessage Parse<TMessage>(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<TMessage>(body.Span, SerializerOptions)
                   ?? throw new InvalidInputException("The event body is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidInputException($"The event body is not valid JSON: {exception.Message}");
        }
    }

    #endregion

    #region Accounts

    private const string AccountEventPrefix = "account.";

    // Account emails go out by email only: they are not notifications in the app's inbox.
    private async Task<bool> HandleAccountEventAsync(string routingKey, Guid eventId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var appBaseUrl = _email.AppBaseUri;
        var (userId, email, name, draft) = routingKey switch
        {
            MessagingTopology.EmailVerificationRequestedRoutingKey => WithToken(body, m => AccountEmailComposer.EmailVerification(m, appBaseUrl)),
            MessagingTopology.PasswordResetRequestedRoutingKey => WithToken(body, m => AccountEmailComposer.PasswordReset(m, appBaseUrl)),
            MessagingTopology.InvitedRoutingKey => WithToken(body, m => AccountEmailComposer.Invitation(m, appBaseUrl)),
            MessagingTopology.EmailVerifiedRoutingKey => Informative(body, m => AccountEmailComposer.Welcome(m, appBaseUrl)),
            MessagingTopology.PasswordChangedRoutingKey => Informative(body, m => AccountEmailComposer.PasswordChanged(m, appBaseUrl)),
            MessagingTopology.DeactivatedRoutingKey => Informative(body, AccountEmailComposer.Deactivated),
            MessagingTopology.ReactivatedRoutingKey => Informative(body, m => AccountEmailComposer.Reactivated(m, appBaseUrl)),
            _ => throw new InvalidInputException($"Unsupported event '{routingKey}'.")
        };

        if (await _dbContext.AccountEmails.AnyAsync(e => e.SourceEventId == eventId, cancellationToken))
        {
            return false;
        }

        _dbContext.AccountEmails.Add(AccountEmail.Create(
            eventId,
            userId,
            draft.Kind,
            email,
            name,
            draft.Subject,
            draft.Text,
            draft.LinkExpiresAt,
            _timeProvider.GetUtcNow().UtcDateTime,
            MessagingTelemetry.CurrentTraceParent()));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return false;
        }
    }

    private static (Guid, string, string, AccountEmailDraft) WithToken(ReadOnlyMemory<byte> body, Func<AccountTokenMessage, AccountEmailDraft> compose)
    {
        var message = Parse<AccountTokenMessage>(body);
        return (message.UserId, message.Email, message.Username, compose(message));
    }

    private static (Guid, string, string, AccountEmailDraft) Informative(ReadOnlyMemory<byte> body, Func<AccountMessage, AccountEmailDraft> compose)
    {
        var message = Parse<AccountMessage>(body);
        return (message.UserId, message.Email, message.Username, compose(message));
    }

    #endregion

    #region Orders (RF19, RF20)

    private async Task<NotificationDraft> FromOrderCreatedAsync(OrderCreatedMessage message, CancellationToken cancellationToken)
    {
        var farm = await FarmNameAsync(message.FarmId, cancellationToken);
        var reference = Reference(message.OrderId);

        return new NotificationDraft(
            message.FarmOwnerId,
            NotificationType.OrderCreated,
            $"New order {reference} at {farm}",
            $"A customer placed order {reference} with {message.ItemCount} item(s), total {message.TotalAmount.ToString("C", Money)}. " +
            "The stock is reserved until you confirm or cancel it.",
            message.OrderId,
            message.FarmId);
    }

    private async Task<NotificationDraft?> FromOrderStatusChangedAsync(OrderStatusChangedMessage message, CancellationToken cancellationToken)
    {
        var farm = await FarmNameAsync(message.FarmId, cancellationToken);
        var reference = Reference(message.OrderId);

        (string Title, string Text)? content = message.NewStatus.ToLowerInvariant() switch
        {
            "confirmed" => ($"Order {reference} confirmed",
                $"{farm} confirmed your order {reference}. You will be notified when it is ready for pickup."),
            "readyforpickup" => ($"Order {reference} is ready for pickup",
                $"Your order {reference} is ready for pickup at {farm}. You pay when you pick it up."),
            "pickedup" => ($"Order {reference} picked up",
                $"{farm} registered your order {reference} as picked up. Thank you for buying locally!"),
            "pending" => null,
            _ => ($"Order {reference} updated",
                $"The status of your order {reference} at {farm} changed to {message.NewStatus}.")
        };

        if (content is null)
        {
            _logger.LogDebug("Order {OrderId} moved to {Status}; the customer is not notified", message.OrderId, message.NewStatus);
            return null;
        }

        return new NotificationDraft(
            message.CustomerId,
            NotificationType.OrderStatusChanged,
            content.Value.Title,
            content.Value.Text,
            message.OrderId,
            message.FarmId);
    }

    // The other party is told: the manager when the customer cancels, the customer otherwise.
    private async Task<NotificationDraft> FromOrderCancelledAsync(OrderCancelledMessage message, CancellationToken cancellationToken)
    {
        var farm = await FarmNameAsync(message.FarmId, cancellationToken);
        var reference = Reference(message.OrderId);
        var reason = string.IsNullOrWhiteSpace(message.Reason) ? string.Empty : $" Reason: {message.Reason.Trim()}";

        return message.CancelledByCustomer
            ? new NotificationDraft(
                message.FarmOwnerId,
                NotificationType.OrderCancelled,
                $"Order {reference} cancelled by the customer",
                $"The customer cancelled order {reference} at {farm}; its stock is back in the catalog.{reason}",
                message.OrderId,
                message.FarmId)
            : new NotificationDraft(
                message.CustomerId,
                NotificationType.OrderCancelled,
                $"Order {reference} cancelled",
                $"{farm} cancelled your order {reference}.{reason}",
                message.OrderId,
                message.FarmId);
    }

    // A farm deleted after the order still deserves a readable message.
    private async Task<string> FarmNameAsync(Guid farmId, CancellationToken cancellationToken)
    {
        try
        {
            return await _farms.GetFarmNameAsync(farmId, cancellationToken);
        }
        catch (NotFoundException)
        {
            return "the farm";
        }
    }

    private static string Reference(Guid orderId) => $"#{orderId.ToString("N")[..8].ToUpperInvariant()}";

    #endregion

    #region Sensors (RF21)

    private static NotificationDraft FromSensorAlert(SensorAlertRaisedMessage message)
    {
        var sensor = message.Type.Length == 0 ? "Sensor" : char.ToUpperInvariant(message.Type[0]) + message.Type[1..];
        var weather = string.IsNullOrWhiteSpace(message.WeatherContext) ? string.Empty : $" {message.WeatherContext}";

        return new NotificationDraft(
            message.FarmOwnerId,
            NotificationType.SensorAlert,
            $"{sensor} alert at {message.FarmName}",
            $"{message.Description}{weather}",
            message.AlertId,
            message.FarmId);
    }

    #endregion

    private sealed record NotificationDraft(
        Guid RecipientId,
        NotificationType Type,
        string Title,
        string Message,
        Guid RelatedEntityId,
        Guid FarmId);
}
