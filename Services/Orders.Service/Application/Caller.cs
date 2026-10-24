using Orders.Service.Models;

namespace Orders.Service.Application;

public sealed record Caller(Guid? UserId, Role? UserRole)
{
    #region Identity

    public static Caller Anonymous { get; } = new(null, null);

    public bool IsAuthenticated => UserId.HasValue;

    public bool IsAdmin => UserRole == Role.Admin;

    public bool IsManager => UserRole == Role.Manager;

    public bool IsClient => UserRole == Role.Client;

    public Guid RequireUserId() => UserId ?? throw new UnauthenticatedException();

    #endregion

    #region Authorization

    public bool CanView(Order order) =>
        IsAdmin || (UserId.HasValue && (order.CustomerId == UserId.Value || order.FarmOwnerId == UserId.Value));

    public bool CanManage(Order order) =>
        IsAdmin || (IsManager && UserId.HasValue && order.FarmOwnerId == UserId.Value);

    public bool CanCancel(Order order)
    {
        if (IsAdmin)
        {
            return true;
        }

        if (IsManager && UserId.HasValue && order.FarmOwnerId == UserId.Value)
        {
            return order.Status != OrderStatus.PickedUp && order.Status != OrderStatus.Cancelled;
        }

        if (UserId.HasValue && order.CustomerId == UserId.Value)
        {
            return order.Status is OrderStatus.Pending or OrderStatus.Confirmed;
        }

        return false;
    }

    public void EnsureCanView(Order order)
    {
        if (!IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        if (!CanView(order))
        {
            throw new ForbiddenException(ErrorMessages.NotAllowedToViewOrder);
        }
    }

    public void EnsureCanManage(Order order)
    {
        if (!IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        if (!CanManage(order))
        {
            throw new ForbiddenException(ErrorMessages.NotAllowedToManageOrder);
        }
    }

    public void EnsureCanCancel(Order order)
    {
        if (!IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        if (!CanCancel(order))
        {
            throw new ForbiddenException(ErrorMessages.NotAllowedToCancelOrder);
        }
    }

    #endregion
}
