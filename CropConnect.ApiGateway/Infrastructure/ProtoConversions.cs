using Google.Protobuf.WellKnownTypes;
using FarmsDecimal = Farms.Service.Protos.DecimalValue;
using OrdersDecimal = Orders.Service.Protos.DecimalValue;

namespace CropConnect.ApiGateway.Infrastructure;

// Proto types that don't map to JSON naturally: money as units + nanos, and timestamps.
public static class ProtoConversions
{
    private const decimal NanoFactor = 1_000_000_000m;

    public static decimal ToDecimal(this FarmsDecimal? value) =>
        value is null ? 0m : value.Units + value.Nanos / NanoFactor;

    public static decimal ToDecimal(this OrdersDecimal? value) =>
        value is null ? 0m : value.Units + value.Nanos / NanoFactor;

    public static FarmsDecimal ToFarmsDecimal(this decimal value)
    {
        var units = decimal.ToInt64(decimal.Truncate(value));
        return new FarmsDecimal { Units = units, Nanos = decimal.ToInt32((value - units) * NanoFactor) };
    }

    public static DateTime? AsDateTime(this Timestamp? value) =>
        value?.ToDateTime();

    public static Timestamp ToTimestamp(this DateTime value) =>
        Timestamp.FromDateTime(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime());

    public static Timestamp? ToTimestamp(this DateTime? value) =>
        value?.ToTimestamp();
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
