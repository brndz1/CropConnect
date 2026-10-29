using System.Diagnostics;
using System.Text;
using Notifications.Service.Models;
using RabbitMQ.Client;

namespace Notifications.Service.Messaging;

// Keeps the notification, and its email, in the trace of the order or alert that caused it. RabbitMQ.Client
// only links its deliver span to the publisher, so processing resumes the trace from the message headers;
// the email is sent later by a background worker, so the notification stores the context to resume it.
public static class MessagingTelemetry
{
    // Named after the assembly, which ServiceDefaults already listens to.
    public static readonly ActivitySource Source = new(typeof(MessagingTelemetry).Assembly.GetName().Name!);

    // The W3C traceparent of the current operation, or null outside a trace.
    public static string? CurrentTraceParent() =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C, Id: { Length: <= FieldLimits.TraceParentMaxLength } id } ? id : null;

    public static Activity? StartEmail(string? traceParent, Guid notificationId)
    {
        ActivityContext.TryParse(traceParent, null, out var parent);

        var activity = Source.StartActivity("send notification email", ActivityKind.Internal, parent);
        activity?.SetTag("notification.id", notificationId.ToString());
        return activity;
    }

    // Messages without a trace context are processed under the current deliver span.
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
