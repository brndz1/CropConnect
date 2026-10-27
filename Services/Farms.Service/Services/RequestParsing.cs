using Farms.Service.Models;
using Farms.Service.Protos;
using Google.Protobuf.WellKnownTypes;

namespace Farms.Service.Services;

public static class RequestParsing
{
    #region Identifiers

    public static Guid ParseId(string value, string field) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidInputException($"{field} is not a valid identifier.");

    public static Guid? ParseOptionalId(bool hasValue, string value, string field) =>
        hasValue ? ParseId(value, field) : null;

    #endregion

    #region Values

    public static decimal RequiredDecimal(DecimalValue? value, string field) =>
        value is null
            ? throw new InvalidInputException($"{field} is required.")
            : value.ToDecimal(field);

    public static decimal? OptionalDecimal(DecimalValue? value, string field) =>
        value?.ToDecimal(field);

    public static string? OptionalText(bool hasValue, string value) =>
        hasValue ? value : null;

    public static int? OptionalNumber(bool hasValue, int value) =>
        hasValue ? value : null;

    public static bool? OptionalFlag(bool hasValue, bool value) =>
        hasValue ? value : null;

    public static DateTime? OptionalTimestamp(Timestamp? value) =>
        value?.ToDateTime();

    public static TimeOnly ParseTime(string value, string field) =>
        TimeOnly.TryParse(value, out var time)
            ? time
            : throw new InvalidInputException($"{field} is not a valid time format (expected HH:mm).");

    public static TimeOnly? ParseOptionalTime(bool hasValue, string value, string field) =>
        hasValue ? ParseTime(value, field) : null;

    #endregion
}
