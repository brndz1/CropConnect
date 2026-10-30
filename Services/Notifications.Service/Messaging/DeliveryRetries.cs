using System.Globalization;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Notifications.Service.Messaging;

// Requeueing a failed message in place retries it forever when it can never succeed, so it is republished
// to the back of its queue with a retry count instead, and dead-lettered once the count reaches the limit.
public static class DeliveryRetries
{
    private const string RetryCountHeader = "x-retry-count";
    private const string RoutingKeyHeader = "x-original-routing-key";

    public static int RetryCount(BasicDeliverEventArgs delivery) =>
        delivery.BasicProperties.Headers?.TryGetValue(RetryCountHeader, out var value) == true && value is not null
            ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
            : 0;

    // A republished message arrives through the default exchange, so its original routing key travels in a header.
    public static string RoutingKey(BasicDeliverEventArgs delivery) =>
        delivery.BasicProperties.Headers?.TryGetValue(RoutingKeyHeader, out var value) == true && value is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : delivery.RoutingKey;

    // Returns false when the message was dead-lettered instead of retried.
    public static async Task<bool> RetryOrDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        string queue,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var retries = RetryCount(delivery);

        if (retries >= maxRetries)
        {
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            return false;
        }

        var headers = delivery.BasicProperties.Headers is { } existing
            ? new Dictionary<string, object?>(existing)
            : new Dictionary<string, object?>();
        headers[RetryCountHeader] = retries + 1;
        headers[RoutingKeyHeader] = RoutingKey(delivery);

        var properties = new BasicProperties(delivery.BasicProperties) { Headers = headers };

        await channel.BasicPublishAsync(string.Empty, queue, mandatory: false, properties, delivery.Body, cancellationToken);
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        return true;
    }
}
