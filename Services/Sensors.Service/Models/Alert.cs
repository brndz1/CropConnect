using System.Globalization;

namespace Sensors.Service.Models;

public sealed class Alert
{
    #region Properties

    private Alert()
    {
    }

    public Guid Id { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid FarmOwnerId { get; private set; }

    public Guid ReadingId { get; private set; }

    public SensorType Type { get; private set; }

    public double Value { get; private set; }

    // Copied from the threshold at the time of the alert, so later threshold changes don't rewrite history.
    public double MinValue { get; private set; }

    public double MaxValue { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string? WeatherContext { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? AcknowledgedAt { get; private set; }

    public Guid? AcknowledgedBy { get; private set; }

    public bool IsOpen => AcknowledgedAt is null;

    #endregion

    #region Factory

    public static Alert Create(SensorReading reading, AlertThreshold threshold, Guid farmOwnerId, DateTime now)
    {
        if (reading.Type != threshold.Type)
        {
            throw new InvalidOperationException("The threshold does not match the reading type.");
        }

        if (threshold.IsWithin(reading.Value))
        {
            throw new InvalidOperationException("The reading is within the threshold.");
        }

        return new Alert
        {
            Id = Guid.NewGuid(),
            FarmId = reading.FarmId,
            FarmOwnerId = Guard.NotEmpty(farmOwnerId, "Farm owner"),
            ReadingId = reading.Id,
            Type = reading.Type,
            Value = reading.Value,
            MinValue = threshold.MinValue,
            MaxValue = threshold.MaxValue,
            Description = Describe(reading, threshold),
            CreatedAt = now
        };
    }

    private static string Describe(SensorReading reading, AlertThreshold threshold)
    {
        var unit = reading.Type.Unit();
        var (comparison, limit) = reading.Value < threshold.MinValue
            ? ("below the minimum", threshold.MinValue)
            : ("above the maximum", threshold.MaxValue);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{reading.Type.DisplayName()} of {reading.Value:0.#}{unit} from sensor {reading.SensorId} is {comparison} of {limit:0.#}{unit}.");
    }

    #endregion

    #region Behaviour

    public void AttachWeatherContext(string? weatherContext) =>
        WeatherContext = Guard.Optional(weatherContext, "Weather context", FieldLimits.WeatherContextMaxLength);

    public bool Acknowledge(Guid userId, DateTime now)
    {
        if (!IsOpen)
        {
            return false;
        }

        AcknowledgedAt = now;
        AcknowledgedBy = Guard.NotEmpty(userId, "User");
        return true;
    }

    #endregion
}
