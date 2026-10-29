using Notifications.Service.Models;

namespace Notifications.Service.Application;

public sealed record NotificationQuery(bool OnlyUnread, int Page, int PageSize);

public sealed record NotificationResult(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    Guid RelatedEntityId,
    Guid FarmId,
    DateTime CreatedAt,
    DateTime? ReadAt,
    EmailStatus EmailStatus);

public sealed record NotificationPage(
    IReadOnlyList<NotificationResult> Items,
    int TotalCount,
    int UnreadCount,
    int Page,
    int PageSize);
