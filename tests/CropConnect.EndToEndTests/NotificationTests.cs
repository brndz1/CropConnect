using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// RF19-RF21: who is notified of what, by app and by email.
public sealed class NotificationTests
{
    private static async Task<JsonNode> WaitForNotificationAsync(TestUser user, Func<JsonNode, bool> match, string because) =>
        (await Eventually.GetAsync(
            () => GetAsync("api/notifications?pageSize=100", user.Token),
            response => response.Body?["items"]?.AsArray().Any(n => n is not null && match(n)) == true,
            because: because)).Body!["items"]!.AsArray().First(n => n is not null && match(n!))!;

    private static async Task<JsonArray> InboxAsync(TestUser user) =>
        (await GetAsync("api/notifications?pageSize=100", user.Token)).Body!["items"]!.AsArray();

    [Fact]
    public async Task Manager_is_notified_of_new_orders()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");
        var orderId = await Scenario.PlaceOrderAsync(client, setup.ProductId);

        var notification = await WaitForNotificationAsync(setup.Manager, n => n["relatedEntityId"]!.ToString() == orderId, "RF19 new order notification");

        Assert.Equal("OrderCreated", notification["type"]!.ToString());
        Assert.Contains(setup.FarmName, notification["title"]!.ToString());
        Assert.DoesNotContain(await InboxAsync(client), n => n!["relatedEntityId"]!.ToString() == orderId);
    }

    [Fact]
    public async Task Client_is_notified_of_each_status_change()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");
        var orderId = await Scenario.PlaceOrderAsync(client, setup.ProductId);

        foreach (var (status, title) in new[] { ("Confirmed", "confirmed"), ("ReadyForPickup", "ready for pickup"), ("PickedUp", "picked up") })
        {
            await PutAsync($"api/orders/{orderId}/status", new { status }, setup.Manager.Token);
            await WaitForNotificationAsync(client, n => n["relatedEntityId"]!.ToString() == orderId && n["title"]!.ToString().Contains(title), $"RF20 {status} notification");
        }
    }

    [Fact]
    public async Task Cancellation_notifies_the_other_party()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");

        var byClient = await Scenario.PlaceOrderAsync(client, setup.ProductId);
        await PostAsync($"api/orders/{byClient}/cancel", new { reason = "Mudei de ideias" }, client.Token);
        var toManager = await WaitForNotificationAsync(setup.Manager, n => n["relatedEntityId"]!.ToString() == byClient && n["type"]!.ToString() == "OrderCancelled", "manager told of the client's cancellation");
        Assert.Contains("Mudei de ideias", toManager["message"]!.ToString());

        var byManager = await Scenario.PlaceOrderAsync(client, setup.ProductId);
        await PostAsync($"api/orders/{byManager}/cancel", new { reason = "Sem stock" }, setup.Manager.Token);
        await WaitForNotificationAsync(client, n => n["relatedEntityId"]!.ToString() == byManager && n["type"]!.ToString() == "OrderCancelled", "client told of the manager's cancellation");

        Assert.DoesNotContain(await InboxAsync(client), n => n!["relatedEntityId"]!.ToString() == byClient);
    }

    [Fact]
    public async Task Manager_is_notified_of_sensor_alerts()
    {
        var setup = await Scenario.FarmWithProductAsync(withCoordinates: true);

        await RabbitMqManagement.PublishReadingAsync(setup.FarmId, "e2e-sensor", "humidity", 18.4);

        var notification = await WaitForNotificationAsync(setup.Manager, n => n["type"]!.ToString() == "SensorAlert" && n["farmId"]!.ToString() == setup.FarmId, "RF21 alert notification");
        Assert.Contains("Humidity alert", notification["title"]!.ToString());
    }

    [Fact]
    public async Task Each_notification_is_emailed_to_its_recipient()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");
        var orderId = await Scenario.PlaceOrderAsync(client, setup.ProductId);

        var notification = await Eventually.GetAsync(
            async () => (await InboxAsync(setup.Manager)).FirstOrDefault(n => n!["relatedEntityId"]!.ToString() == orderId),
            n => n?["emailStatus"]?.ToString() == "Sent",
            because: "the email to be sent");

        var emails = await Mailpit.MessagesToAsync(setup.Manager.Email);
        var email = Assert.Single(emails);
        Assert.Equal(notification!["title"]!.ToString(), email["Subject"]!.ToString());
        Assert.Contains(notification["message"]!.ToString(), await Mailpit.TextAsync(email));
    }

    [Fact]
    public async Task Users_read_their_inbox_and_cannot_touch_others()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");
        await Scenario.PlaceOrderAsync(client, setup.ProductId);
        await Scenario.PlaceOrderAsync(client, setup.ProductId);
        await Eventually.GetAsync(() => InboxAsync(setup.Manager), inbox => inbox.Count == 2, because: "two notifications");

        var first = (await InboxAsync(setup.Manager))[0]!;
        var read = await PostAsync($"api/notifications/{first["id"]}/read", token: setup.Manager.Token);
        Assert.NotNull(read.Body!["readAt"]);

        var unread = await GetAsync("api/notifications?onlyUnread=true", setup.Manager.Token);
        Assert.Equal(1, unread.Body!["unreadCount"]!.GetValue<int>());

        var otherUsers = await PostAsync($"api/notifications/{first["id"]}/read", token: client.Token);
        Assert.Equal(HttpStatusCode.NotFound, otherUsers.Status);

        var all = await PostAsync("api/notifications/read-all", token: setup.Manager.Token);
        Assert.Equal(1, all.Body!["markedCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task Redelivered_event_notifies_only_once()
    {
        var manager = await RegisterAsync("Manager");
        var eventId = Guid.NewGuid().ToString();
        var payload = JsonSerializer.Serialize(new
        {
            alertId = Guid.NewGuid(), farmId = Guid.NewGuid(), farmOwnerId = manager.Id, farmName = "Quinta Duplicada", type = "temperature",
            value = 41.0, minValue = 5.0, maxValue = 35.0, unit = "°C", description = "Duplicate test", weatherContext = (string?)null, occurredAt = DateTime.UtcNow
        });

        await RabbitMqManagement.PublishAsync("cropconnect.events", "sensor.alert.raised", payload, eventId);
        await RabbitMqManagement.PublishAsync("cropconnect.events", "sensor.alert.raised", payload, eventId);

        await WaitForNotificationAsync(manager, n => n["message"]!.ToString() == "Duplicate test", "the alert notification");
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.Single(await InboxAsync(manager));
    }

    [Fact]
    public async Task Malformed_event_goes_to_the_dead_letter_queue()
    {
        const string queue = "notifications-service.events.dead-letter";
        var before = await RabbitMqManagement.QueueMessageCountAsync(queue);

        await RabbitMqManagement.PublishAsync("cropconnect.events", "order.created", "{not json", Guid.NewGuid().ToString());

        await Eventually.GetAsync(() => RabbitMqManagement.QueueMessageCountAsync(queue), count => count > before, because: "the event to be dead-lettered");
    }
}
