using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;

namespace Analytics.Ingestor.Messaging;

// Storing a message continues the trace of the operation that published it, and the stored document keeps that
// trace's W3C traceparent, so a row in Databricks can be followed back to the request in the Aspire dashboard.
public static class MessagingTelemetry
{
    // Named after the assembly, which ServiceDefaults already listens to.
    public static readonly ActivitySource Source = new(typeof(MessagingTelemetry).Assembly.GetName().Name!);

    public static Activity? StartProcess(IReadOnlyBasicProperties properties, string routingKey, out string? traceParent)
    {
        DistributedContextPropagator.Current.ExtractTraceIdAndState(
            properties.Headers,
            static (carrier, name, out value, out values) =>
            {
                values = null;
                value = carrier is IDictionary<string, object?> headers && headers.TryGetValue(name, out var raw)
                    ? raw switch { byte[] bytes => Encoding.UTF8.GetString(bytes), string text => text, _ => null }
                    : null;
            },
            out traceParent,
            out var traceState);

        ActivityContext.TryParse(traceParent, traceState, out var parent);

        var activity = Source.StartActivity($"store {routingKey}", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.message.id", properties.MessageId);
        return activity;
    }
}
