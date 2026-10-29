using System.Security.Claims;
using Grpc.Core;
using Notifications.Service.Application;
using Notifications.Service.Models;

namespace Notifications.Service.Services;

public static class CallerExtensions
{
    private const string SubjectClaim = "sub";
    private const string RoleClaim = "role";

    public static Caller GetCaller(this ServerCallContext context) =>
        context.GetHttpContext().User.ToCaller();

    public static Caller ToCaller(this ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Caller.Anonymous;
        }

        if (!Guid.TryParse(principal.FindFirst(SubjectClaim)?.Value, out var userId) || userId == Guid.Empty)
        {
            return Caller.Anonymous;
        }

        var roleValue = principal.FindFirst(ClaimTypes.Role)?.Value
                        ?? principal.FindFirst(RoleClaim)?.Value;

        Role? role = Enum.TryParse<Role>(roleValue, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;

        return new Caller(userId, role);
    }
}
