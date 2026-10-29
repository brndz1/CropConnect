using System.Diagnostics;
using Orders.Service.Models;

namespace Orders.Service.Messaging;

// Keeps an event in the trace of the request that raised it, even though the outbox publishes it later
// from a background worker: the trace context is stored with the message and resumed when publishing.
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
}
