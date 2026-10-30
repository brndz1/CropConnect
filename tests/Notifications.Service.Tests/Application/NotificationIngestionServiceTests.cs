using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Service.Application;
using Notifications.Service.Application.Clients;
using Notifications.Service.Models;
using Notifications.Service.Tests.Infrastructure;

namespace Notifications.Service.Tests.Application;

// RF19-RF21: which event notifies whom, with what text. The JSON bodies are what Orders and Sensors publish.
public sealed class NotificationIngestionServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeFarms _farms = new();

    private readonly Guid _orderId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();

    public void Dispose() => _database.Dispose();

    private NotificationIngestionService Service() =>
        new(_database.CreateContext(), _farms, TimeProvider.System, NullLogger<NotificationIngestionService>.Instance);

    private Task<bool> HandleAsync(string routingKey, string json, Guid? eventId = null) =>
        Service().HandleAsync(routingKey, eventId ?? Guid.NewGuid(), Encoding.UTF8.GetBytes(json), CancellationToken.None);

    private List<Notification> Stored()
    {
        using var context = _database.CreateContext();
        return context.Notifications.ToList();
    }

    private string OrderCreated() =>
        $$"""{"orderId":"{{_orderId}}","customerId":"{{_customerId}}","farmId":"{{_farmId}}","farmOwnerId":"{{_ownerId}}","totalAmount":12.5,"itemCount":2,"occurredAt":"2026-05-01T10:00:00Z"}""";

    private string StatusChanged(string newStatus) =>
        $$"""{"orderId":"{{_orderId}}","customerId":"{{_customerId}}","farmId":"{{_farmId}}","farmOwnerId":"{{_ownerId}}","previousStatus":"pending","newStatus":"{{newStatus}}","occurredAt":"2026-05-01T10:00:00Z"}""";

    private string Cancelled(bool byCustomer, string? reason) =>
        $$"""{"orderId":"{{_orderId}}","customerId":"{{_customerId}}","farmId":"{{_farmId}}","farmOwnerId":"{{_ownerId}}","previousStatus":"pending","reason":{{(reason is null ? "null" : $"\"{reason}\"")}},"cancelledByCustomer":{{(byCustomer ? "true" : "false")}},"occurredAt":"2026-05-01T10:00:00Z"}""";

    [Fact]
    public async Task New_order_notifies_the_farm_owner()
    {
        Assert.True(await HandleAsync("order.created", OrderCreated()));

        var notification = Assert.Single(Stored());
        Assert.Equal(_ownerId, notification.RecipientId);
        Assert.Equal(NotificationType.OrderCreated, notification.Type);
        Assert.Equal("New order #3FA85F64 at Quinta do Vale", notification.Title);
        Assert.Contains("2 item(s)", notification.Message);
        Assert.Contains("12,50", notification.Message);
        Assert.Equal(_orderId, notification.RelatedEntityId);
    }

    [Theory]
    [InlineData("confirmed", "Order #3FA85F64 confirmed")]
    [InlineData("readyForPickup", "Order #3FA85F64 is ready for pickup")]
    [InlineData("pickedUp", "Order #3FA85F64 picked up")]
    [InlineData("somethingNew", "Order #3FA85F64 updated")]
    public async Task Status_changes_notify_the_customer(string status, string title)
    {
        Assert.True(await HandleAsync("order.status.changed", StatusChanged(status)));

        var notification = Assert.Single(Stored());
        Assert.Equal(_customerId, notification.RecipientId);
        Assert.Equal(title, notification.Title);
    }

    [Fact]
    public async Task Moving_back_to_pending_notifies_nobody()
    {
        Assert.False(await HandleAsync("order.status.changed", StatusChanged("pending")));
        Assert.Empty(Stored());
    }

    [Fact]
    public async Task Customer_cancellation_tells_the_farm_owner_with_the_reason()
    {
        await HandleAsync("order.cancelled", Cancelled(byCustomer: true, "Mudei de ideias"));

        var notification = Assert.Single(Stored());
        Assert.Equal(_ownerId, notification.RecipientId);
        Assert.EndsWith("Reason: Mudei de ideias", notification.Message);
    }

    [Fact]
    public async Task Farm_cancellation_tells_the_customer()
    {
        await HandleAsync("order.cancelled", Cancelled(byCustomer: false, null));

        var notification = Assert.Single(Stored());
        Assert.Equal(_customerId, notification.RecipientId);
        Assert.Equal("Quinta do Vale cancelled your order #3FA85F64.", notification.Message);
    }

    [Fact]
    public async Task Deleted_farm_still_gets_a_readable_message()
    {
        _farms.Missing = true;

        await HandleAsync("order.created", OrderCreated());

        Assert.Equal("New order #3FA85F64 at the farm", Assert.Single(Stored()).Title);
    }

    [Fact]
    public async Task Sensor_alert_notifies_the_owner_with_the_weather()
    {
        var alertId = Guid.NewGuid();
        var json = $$"""{"alertId":"{{alertId}}","farmId":"{{_farmId}}","farmOwnerId":"{{_ownerId}}","farmName":"Quinta do Vale","type":"humidity","value":18.4,"minValue":30,"maxValue":80,"unit":"%","description":"Humidity of 18.4% is below the minimum of 30%.","weatherContext":"No significant rain forecast; consider irrigating.","occurredAt":"2026-05-01T10:00:00Z"}""";

        await HandleAsync("sensor.alert.raised", json);

        var notification = Assert.Single(Stored());
        Assert.Equal(_ownerId, notification.RecipientId);
        Assert.Equal("Humidity alert at Quinta do Vale", notification.Title);
        Assert.Equal("Humidity of 18.4% is below the minimum of 30%. No significant rain forecast; consider irrigating.", notification.Message);
        Assert.Equal(alertId, notification.RelatedEntityId);
    }

    [Fact]
    public async Task Redelivered_event_notifies_once()
    {
        var eventId = Guid.NewGuid();

        Assert.True(await HandleAsync("order.created", OrderCreated(), eventId));
        Assert.False(await HandleAsync("order.created", OrderCreated(), eventId));

        Assert.Single(Stored());
    }

    [Theory]
    [InlineData("order.created", "{not json")]
    [InlineData("order.created", "null")]
    [InlineData("farm.deleted", "{}")]
    public async Task Malformed_or_unknown_events_are_invalid_input(string routingKey, string json)
    {
        await Assert.ThrowsAsync<InvalidInputException>(() => HandleAsync(routingKey, json));
    }

    private sealed class FakeFarms : IFarmDirectoryClient
    {
        public bool Missing { get; set; }

        public Task<string> GetFarmNameAsync(Guid farmId, CancellationToken cancellationToken) =>
            Missing ? throw new NotFoundException("Farm not found.") : Task.FromResult("Quinta do Vale");
    }
}
