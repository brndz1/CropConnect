using Notifications.Service.Models;

namespace Notifications.Service.Services;

public static class RequestParsing
{
    public static Guid ParseId(string value, string field) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidInputException($"{field} is not a valid identifier.");
}
