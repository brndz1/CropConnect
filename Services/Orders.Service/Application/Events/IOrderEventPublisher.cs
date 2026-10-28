using System.Text.Json;
using System.Text.Json.Serialization;
using Orders.Service.Data;
using Orders.Service.Messaging;
using Orders.Service.Models;

namespace Orders.Service.Application.Events;

// Each method stages the event in the current unit of work; it is only sent once SaveChanges commits it,
// together with the order change that produced it.
public interface IOrderEventPublisher
{
    void StageOrderCreated(OrderCreatedEvent @event);

    void StageOrderStatusChanged(OrderStatusChangedEvent @event);

    void StageOrderCancelled(OrderCancelledEvent @event);
}

public sealed class OutboxOrderEventPublisher : IOrderEventPublisher
{
    public const string OrderCreatedEventType = "OrderCreated";
    public const string OrderStatusChangedEventType = "OrderStatusChanged";
    public const string OrderCancelledEventType = "OrderCancelled";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly OrdersDbContext _dbContext;

    public OutboxOrderEventPublisher(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void StageOrderCreated(OrderCreatedEvent @event) =>
        Stage(OrderCreatedEventType, MessagingTopology.OrderCreatedRoutingKey, @event, @event.OccurredAt);

    public void StageOrderStatusChanged(OrderStatusChangedEvent @event) =>
        Stage(OrderStatusChangedEventType, MessagingTopology.OrderStatusChangedRoutingKey, @event, @event.OccurredAt);

    public void StageOrderCancelled(OrderCancelledEvent @event) =>
        Stage(OrderCancelledEventType, MessagingTopology.OrderCancelledRoutingKey, @event, @event.OccurredAt);

    private void Stage<TEvent>(string eventType, string routingKey, TEvent @event, DateTime occurredAt) =>
        _dbContext.OutboxMessages.Add(OutboxMessage.Create(
            eventType,
            routingKey,
            JsonSerializer.Serialize(@event, SerializerOptions),
            occurredAt));
}
