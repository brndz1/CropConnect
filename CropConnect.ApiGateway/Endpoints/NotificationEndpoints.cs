using CropConnect.ApiGateway.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Notifications.Service.Protos;
using ProtoEmailStatus = Notifications.Service.Protos.EmailStatus;
using ProtoNotificationType = Notifications.Service.Protos.NotificationType;

namespace CropConnect.ApiGateway.Endpoints;

// RF19-RF21: the signed-in user's notifications (Notifications.Service).
public static class NotificationEndpoints
{
    #region Contracts

    public enum NotificationType
    {
        OrderCreated,
        OrderStatusChanged,
        OrderCancelled,
        SensorAlert
    }

    public enum EmailStatus
    {
        Pending,
        Sent,
        Failed,
        Skipped
    }

    public sealed record NotificationResponse(
        string Id,
        NotificationType? Type,
        string Title,
        string Message,
        string RelatedEntityId,
        string FarmId,
        DateTime? CreatedAt,
        DateTime? ReadAt,
        EmailStatus? EmailStatus);

    public sealed record NotificationPageResponse(
        IReadOnlyList<NotificationResponse> Items,
        int TotalCount,
        int UnreadCount,
        int Page,
        int PageSize);

    public sealed record MarkedResponse(int MarkedCount);

    #endregion

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder routes)
    {
        var notifications = routes.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        notifications.MapGet("/", async Task<Ok<NotificationPageResponse>> (bool? onlyUnread, int? page, int? pageSize, NotificationsService.NotificationsServiceClient client, CancellationToken ct) =>
            {
                var response = await client.ListMyNotificationsAsync(
                    new ListMyNotificationsRequest { OnlyUnread = onlyUnread ?? false, Page = page ?? 1, PageSize = pageSize ?? 0 },
                    cancellationToken: ct);
                return TypedResults.Ok(new NotificationPageResponse(
                    response.Notifications.Select(ToResponse).ToList(), response.TotalCount, response.UnreadCount, response.Page, response.PageSize));
            })
            .WithSummary("Your notifications, newest first, with the unread count");

        notifications.MapPost("/{id}/read", async Task<Ok<NotificationResponse>> (string id, NotificationsService.NotificationsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.MarkNotificationReadAsync(new MarkNotificationReadRequest { Id = id }, cancellationToken: ct))))
            .WithSummary("Mark one of your notifications as read");

        notifications.MapPost("/read-all", async Task<Ok<MarkedResponse>> (NotificationsService.NotificationsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(new MarkedResponse((await client.MarkAllNotificationsReadAsync(new MarkAllNotificationsReadRequest(), cancellationToken: ct)).MarkedCount)))
            .WithSummary("Mark all your notifications as read");

        return routes;
    }

    private static NotificationResponse ToResponse(NotificationInfo notification) => new(
        notification.Id,
        notification.Type switch
        {
            ProtoNotificationType.OrderCreated => NotificationType.OrderCreated,
            ProtoNotificationType.OrderStatusChanged => NotificationType.OrderStatusChanged,
            ProtoNotificationType.OrderCancelled => NotificationType.OrderCancelled,
            ProtoNotificationType.SensorAlert => NotificationType.SensorAlert,
            _ => null
        },
        notification.Title,
        notification.Message,
        notification.RelatedEntityId,
        notification.FarmId,
        notification.CreatedAt.AsDateTime(),
        notification.ReadAt.AsDateTime(),
        notification.EmailStatus switch
        {
            ProtoEmailStatus.Pending => EmailStatus.Pending,
            ProtoEmailStatus.Sent => EmailStatus.Sent,
            ProtoEmailStatus.Failed => EmailStatus.Failed,
            ProtoEmailStatus.Skipped => EmailStatus.Skipped,
            _ => null
        });
}
