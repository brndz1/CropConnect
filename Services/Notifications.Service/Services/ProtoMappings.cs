using Google.Protobuf.WellKnownTypes;
using Notifications.Service.Application;
using Notifications.Service.Protos;
using EmailStatus = Notifications.Service.Models.EmailStatus;
using NotificationType = Notifications.Service.Models.NotificationType;

namespace Notifications.Service.Services;

public static class ProtoMappings
{
    public static NotificationInfo ToProto(this NotificationResult notification)
    {
        var info = new NotificationInfo
        {
            Id = notification.Id.ToString(),
            Type = notification.Type.ToProto(),
            Title = notification.Title,
            Message = notification.Message,
            RelatedEntityId = notification.RelatedEntityId.ToString(),
            FarmId = notification.FarmId.ToString(),
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc)),
            EmailStatus = notification.EmailStatus.ToProto()
        };

        if (notification.ReadAt is { } readAt)
        {
            info.ReadAt = Timestamp.FromDateTime(DateTime.SpecifyKind(readAt, DateTimeKind.Utc));
        }

        return info;
    }

    private static Protos.NotificationType ToProto(this NotificationType type) => type switch
    {
        NotificationType.OrderCreated => Protos.NotificationType.OrderCreated,
        NotificationType.OrderStatusChanged => Protos.NotificationType.OrderStatusChanged,
        NotificationType.OrderCancelled => Protos.NotificationType.OrderCancelled,
        NotificationType.SensorAlert => Protos.NotificationType.SensorAlert,
        _ => Protos.NotificationType.Unspecified
    };

    private static Protos.EmailStatus ToProto(this EmailStatus status) => status switch
    {
        EmailStatus.Pending => Protos.EmailStatus.Pending,
        EmailStatus.Sent => Protos.EmailStatus.Sent,
        EmailStatus.Failed => Protos.EmailStatus.Failed,
        EmailStatus.Skipped => Protos.EmailStatus.Skipped,
        _ => Protos.EmailStatus.Unspecified
    };
}
