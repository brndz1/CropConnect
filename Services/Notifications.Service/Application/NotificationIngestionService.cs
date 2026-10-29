using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notifications.Service.Application.Clients;
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
    private readonly ILogger<NotificationIngestionService> _logger;

    public NotificationIngestionService(
        NotificationsDbContext dbContext,
        IFarmDirectoryClient farms,
        TimeProvider timeProvider,
        ILogger<NotificationIngestionService> logger)
    {
        _dbContext = dbContext;
        _farms = farms;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion

    #region Handling

    public async Task<bool> HandleAsync(string routingKey, Guid eventId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        Guard.NotEmpty(eventId, "Event ID");

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
            _timeProvider.GetUtcNow().UtcDateTime));

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
