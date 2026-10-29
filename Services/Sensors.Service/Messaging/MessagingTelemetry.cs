using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;
using Sensors.Service.Models;

namespace Sensors.Service.Messaging;

// Keeps messages in the trace that caused them. Alerts are published later by the outbox worker, so the
// trace context is stored with the message and resumed when publishing; on the consuming side RabbitMQ.Client
// only links its deliver span to the publisher, so processing resumes the trace from the message headers.
public static class MessagingTelemetry
{
    // Named after the assembly, which ServiceDefaults already listens to.
    public static readonly ActivitySource Source = new(typeof(MessagingTelemetry).Assembly.GetName().Name!);

    // The W3C traceparent of the current request, or null outside a trace.
    public static string? CurrentTraceParent() =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C, Id: { Length: <= FieldLimits.TraceParentMaxLength } id } ? id : null;

    // RabbitMQ.Client's publish span becomes a child of this one and carries the context in the message headers.
    public static Activity? StartPublish(string? traceParent, string routingKey, Guid messageId)
    {
        ActivityContext.TryParse(traceParent, null, out var parent);

        var activity = Source.StartActivity($"outbox publish {routingKey}", ActivityKind.Internal, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", MessagingTopology.EventsExchange);
        activity?.SetTag("messaging.message.id", messageId.ToString());
        return activity;
    }

    // Messages without a trace context (e.g. from Node-RED) are processed under the current deliver span.
    public static Activity? StartProcess(IReadOnlyBasicProperties properties, string routingKey)
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
            out var traceParent,
            out var traceState);

        ActivityContext.TryParse(traceParent, traceState, out var parent);

        var activity = Source.StartActivity($"process {routingKey}", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.message.id", properties.MessageId);
        return activity;
    }
}
