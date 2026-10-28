using Sensors.Service.Models;

namespace Sensors.Service.Application;

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

    // RF18: telemetry is exclusive to the farm's manager; administrators don't see it either.
    public bool CanReadTelemetry(Guid farmOwnerId) => IsManager && UserId == farmOwnerId;

    public void EnsureCanReadTelemetry(Guid farmOwnerId)
    {
        if (!IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        if (!CanReadTelemetry(farmOwnerId))
        {
            throw new ForbiddenException(ErrorMessages.NotAllowedToReadTelemetry);
        }
    }

    public void EnsureCanViewThresholds()
    {
        if (!IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        if (!IsAdmin && !IsManager)
        {
            throw new ForbiddenException(ErrorMessages.NotAllowedToViewThresholds);
        }
    }

    public void EnsureAdmin()
    {
        if (!IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        if (!IsAdmin)
        {
            throw new ForbiddenException(ErrorMessages.OnlyAdminsCanChangeThresholds);
        }
    }

    #endregion
}
