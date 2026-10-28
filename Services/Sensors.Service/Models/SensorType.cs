namespace Sensors.Service.Models;

public enum SensorType
{
    Humidity = 1,
    Temperature = 2
}

public static class SensorTypeExtensions
{
    public static string Unit(this SensorType type) => type switch
    {
        SensorType.Humidity => "%",
        SensorType.Temperature => "°C",
        _ => string.Empty
    };

    public static string DisplayName(this SensorType type) => type switch
    {
        SensorType.Humidity => "Humidity",
        SensorType.Temperature => "Temperature",
        _ => type.ToString()
    };

    // Values a real sensor can produce; anything outside is a faulty or forged reading.
    public static (double Min, double Max) PhysicalRange(this SensorType type) => type switch
    {
        SensorType.Humidity => (0, 100),
        SensorType.Temperature => (-50, 70),
        _ => throw new InvalidInputException("Invalid sensor type.")
    };

    public static SensorType Required(this SensorType type) =>
        Enum.IsDefined(type) ? type : throw new InvalidInputException("Invalid sensor type.");
}
