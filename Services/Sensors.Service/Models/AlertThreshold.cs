namespace Sensors.Service.Models;

public sealed class AlertThreshold : VersionedEntity
{
    #region Properties

    private AlertThreshold()
    {
    }

    public SensorType Type { get; private set; }

    public double MinValue { get; private set; }

    public double MaxValue { get; private set; }

    public override string EntityName => "alert threshold";

    #endregion

    #region Factory

    public static AlertThreshold Create(SensorType type, double minValue, double maxValue, DateTime now)
    {
        var threshold = new AlertThreshold { Type = type.Required() };
        threshold.SetRange(minValue, maxValue);
        threshold.Initialize(now);
        return threshold;
    }

    #endregion

    #region Behaviour

    public bool IsWithin(double value) => value >= MinValue && value <= MaxValue;

    public void Update(double minValue, double maxValue, DateTime now)
    {
        SetRange(minValue, maxValue);
        Touch(now);
    }

    private void SetRange(double minValue, double maxValue)
    {
        var range = Type.PhysicalRange();
        Guard.InRange(minValue, "Minimum value", range);
        Guard.InRange(maxValue, "Maximum value", range);

        if (minValue >= maxValue)
        {
            throw new InvalidInputException("Minimum value must be lower than the maximum value.");
        }

        MinValue = minValue;
        MaxValue = maxValue;
    }

    #endregion
}
