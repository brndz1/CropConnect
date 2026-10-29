using Notifications.Service.Models;

namespace Notifications.Service.Application;

public sealed record Caller(Guid? UserId, Role? UserRole)
{
    public static Caller Anonymous { get; } = new(null, null);

    public bool IsAuthenticated => UserId.HasValue;

    // Every user, whatever the role, only ever sees their own notifications.
    public Guid RequireUserId() => UserId ?? throw new UnauthenticatedException();
}
