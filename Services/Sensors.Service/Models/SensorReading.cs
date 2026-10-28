namespace Sensors.Service.Models;

public sealed class SensorReading
{
    #region Properties

    private SensorReading()
    {
    }

    public Guid Id { get; private set; }

    // Set by the producer (Node-RED); makes a redelivered message a no-op.
    public Guid MessageId { get; private set; }

    public Guid FarmId { get; private set; }

    public string SensorId { get; private set; } = string.Empty;

    public SensorType Type { get; private set; }

    public double Value { get; private set; }

    public DateTime Timestamp { get; private set; }

    public DateTime ReceivedAt { get; private set; }

    #endregion

    #region Factory

    public static SensorReading Create(
        Guid messageId,
        Guid farmId,
        string sensorId,
        SensorType type,
        double value,
        DateTime timestamp,
        DateTime now,
        TimeSpan maxClockSkew)
    {
        type = type.Required();
        timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        if (timestamp > now + maxClockSkew)
        {
            throw new InvalidInputException("Reading timestamp is in the future.");
        }

        return new SensorReading
        {
            Id = Guid.NewGuid(),
            MessageId = Guard.NotEmpty(messageId, "Message ID"),
            FarmId = Guard.NotEmpty(farmId, "Farm ID"),
            SensorId = Guard.Required(sensorId, "Sensor ID", FieldLimits.SensorIdMaxLength),
            Type = type,
            Value = Guard.InRange(value, "Value", type.PhysicalRange()),
            Timestamp = timestamp,
            ReceivedAt = now
        };
    }

    #endregion
}
