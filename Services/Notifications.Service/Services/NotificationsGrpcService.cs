using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Notifications.Service.Application;
using Notifications.Service.Protos;

namespace Notifications.Service.Services;

[Authorize]
public sealed class NotificationsGrpcService : NotificationsService.NotificationsServiceBase
{
    #region Dependencies

    private readonly INotificationQueryService _notifications;

    public NotificationsGrpcService(INotificationQueryService notifications)
    {
        _notifications = notifications;
    }

    #endregion

    #region Endpoints

    public override async Task<ListNotificationsResponse> ListMyNotifications(ListMyNotificationsRequest request, ServerCallContext context)
    {
        var page = await _notifications.ListMineAsync(
            context.GetCaller(),
            new NotificationQuery(request.OnlyUnread, request.Page, request.PageSize),
            context.CancellationToken);

        var response = new ListNotificationsResponse
        {
            TotalCount = page.TotalCount,
            UnreadCount = page.UnreadCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
        response.Notifications.AddRange(page.Items.Select(n => n.ToProto()));
        return response;
    }

    public override async Task<NotificationInfo> MarkNotificationRead(MarkNotificationReadRequest request, ServerCallContext context)
    {
        var id = RequestParsing.ParseId(request.Id, "Notification ID");
        var notification = await _notifications.MarkReadAsync(context.GetCaller(), id, context.CancellationToken);
        return notification.ToProto();
    }

    public override async Task<MarkAllNotificationsReadResponse> MarkAllNotificationsRead(MarkAllNotificationsReadRequest request, ServerCallContext context)
    {
        var marked = await _notifications.MarkAllReadAsync(context.GetCaller(), context.CancellationToken);
        return new MarkAllNotificationsReadResponse { MarkedCount = marked };
    }

    #endregion
}
