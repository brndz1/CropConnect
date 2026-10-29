using Microsoft.EntityFrameworkCore;
using Notifications.Service.Data;
using Notifications.Service.Models;

namespace Notifications.Service.Application;

public interface INotificationQueryService
{
    Task<NotificationPage> ListMineAsync(Caller caller, NotificationQuery query, CancellationToken cancellationToken);

    Task<NotificationResult> MarkReadAsync(Caller caller, Guid notificationId, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(Caller caller, CancellationToken cancellationToken);
}

public sealed class NotificationQueryService : INotificationQueryService
{
    #region Dependencies

    private readonly NotificationsDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public NotificationQueryService(NotificationsDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #endregion

    #region Queries

    public async Task<NotificationPage> ListMineAsync(Caller caller, NotificationQuery query, CancellationToken cancellationToken)
    {
        var recipientId = caller.RequireUserId();
        var mine = _dbContext.Notifications.AsNoTracking().Where(n => n.RecipientId == recipientId);

        var unreadCount = await mine.CountAsync(n => n.ReadAt == null, cancellationToken);
        var filtered = query.OnlyUnread ? mine.Where(n => n.ReadAt == null) : mine;

        var page = await filtered
            .OrderByDescending(n => n.CreatedAt)
            .ToPagedResultAsync(PageRequest.Create(query.Page, query.PageSize), cancellationToken);

        return new NotificationPage(page.Items.Select(ToResult).ToList(), page.TotalCount, unreadCount, page.Page, page.PageSize);
    }

    #endregion

    #region Commands

    public async Task<NotificationResult> MarkReadAsync(Caller caller, Guid notificationId, CancellationToken cancellationToken)
    {
        var recipientId = caller.RequireUserId();

        // Someone else's notification is reported as missing, so its existence isn't revealed.
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == recipientId, cancellationToken)
            ?? throw new NotFoundException(ErrorMessages.NotificationNotFound);

        notification.MarkRead(Now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(notification);
    }

    public async Task<int> MarkAllReadAsync(Caller caller, CancellationToken cancellationToken)
    {
        var recipientId = caller.RequireUserId();
        var now = Now;

        return await _dbContext.Notifications
            .Where(n => n.RecipientId == recipientId && n.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, now), cancellationToken);
    }

    #endregion

    private static NotificationResult ToResult(Notification n) => new(
        n.Id, n.Type, n.Title, n.Message, n.RelatedEntityId, n.FarmId, n.CreatedAt, n.ReadAt, n.EmailStatus);
}
