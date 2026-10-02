using Farms.Service.Models;
using Farms.Service.Protos;

namespace Farms.Service.Services;

public static class DecimalValueConversions
{
    private const decimal NanoFactor = 1_000_000_000m;
    private const int MaxNanos = 999_999_999;

    public static DecimalValue ToDecimalValue(this decimal value)
    {
        var units = decimal.ToInt64(decimal.Truncate(value));
        var nanos = decimal.ToInt32((value - units) * NanoFactor);
        return new DecimalValue { Units = units, Nanos = nanos };
    }

    public static decimal ToDecimal(this DecimalValue value, string field)
    {
        var validNanos = value.Nanos is >= -MaxNanos and <= MaxNanos;
        var consistentSign = (value.Units >= 0 || value.Nanos <= 0) && (value.Units <= 0 || value.Nanos >= 0);

        if (!validNanos || !consistentSign)
        {
            throw new InvalidInputException($"{field} is not a valid decimal value.");
        }

        return value.Units + value.Nanos / NanoFactor;
    }
}
