using Farms.Service.Models;

namespace Farms.Service.Application;

public sealed record Caller(Guid? UserId, Role? UserRole)
{
    #region Identity

    public static Caller Anonymous { get; } = new(null, null);

    public bool IsAuthenticated => UserId.HasValue;

    public bool IsAdmin => UserRole == Role.Admin;

    public bool IsManager => UserRole == Role.Manager;

    public Guid RequireUserId() => UserId ?? throw new UnauthenticatedException();

    #endregion

    #region Authorization

    public bool Owns(Farm farm) => UserId.HasValue && farm.OwnerId == UserId.Value;

    public bool CanManage(Farm farm) => IsAdmin || (IsManager && Owns(farm));

    // RF08: a surprise basket's contents stay hidden from customers until pickup.
    public bool CanSeeContents(Product product) => !product.HidesContents || CanManage(product.Farm);

    public void EnsureCanManage(Farm farm)
    {
        if (!IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        if (!CanManage(farm))
        {
            throw new ForbiddenException(ErrorMessages.NotAllowedToManageFarm);
        }
    }

    #endregion
}
