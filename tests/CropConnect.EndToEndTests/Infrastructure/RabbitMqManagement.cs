using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CropConnect.EndToEndTests.Infrastructure;

// Publishes test messages through the RabbitMQ management API, as Node-RED or another service would over AMQP.
public static class RabbitMqManagement
{
    private static readonly HttpClient Http = CreateClient();

    // A sensor reading, as the Node-RED flow sends it to the sensors.readings exchange (RF16).
    public static Task PublishReadingAsync(string farmId, string sensorId, string type, double value) =>
        PublishAsync(
            "sensors.readings",
            $"farm.{farmId}.{type}",
            JsonSerializer.Serialize(new { messageId = Guid.NewGuid(), farmId, sensorId, type, value, timestamp = DateTime.UtcNow }));

    public static async Task PublishAsync(string exchange, string routingKey, string payload, string? messageId = null)
    {
        object properties = messageId is null
            ? new { content_type = "application/json" }
            : new { content_type = "application/json", message_id = messageId };

        var response = await Http.PostAsJsonAsync(
            $"api/exchanges/%2F/{exchange}/publish",
            new { properties, routing_key = routingKey, payload, payload_encoding = "string" });

        var result = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode && result.Contains("\"routed\":true"), $"Publish to {exchange}/{routingKey}: {result}");
    }

    public static async Task<int> QueueMessageCountAsync(string queue)
    {
        var json = JsonNode.Parse(await Http.GetStringAsync($"api/queues/%2F/{queue}"));
        return json?["messages"]?.GetValue<int>() ?? 0;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { BaseAddress = TestEnvironment.RabbitMqManagement };
        var credentials = Encoding.ASCII.GetBytes($"{TestEnvironment.RabbitMqUser}:{TestEnvironment.RabbitMqPassword}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));
        return client;
    }
}
