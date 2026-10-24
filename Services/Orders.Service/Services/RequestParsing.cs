using Orders.Service.Models;

namespace Orders.Service.Services;

public static class RequestParsing
{
    public static Guid ParseId(string value, string field) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidInputException($"{field} is not a valid identifier.");

    public static Guid? ParseOptionalId(bool hasValue, string value, string field) =>
        hasValue ? ParseId(value, field) : null;

    public static string? OptionalText(bool hasValue, string value) =>
        hasValue ? value : null;

    public static int? OptionalNumber(bool hasValue, int value) =>
        hasValue ? value : null;
}
