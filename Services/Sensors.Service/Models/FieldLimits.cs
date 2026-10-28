namespace Sensors.Service.Models;

public static class FieldLimits
{
    public const int SensorIdMaxLength = 100;
    public const int SensorTypeMaxLength = 20;
    public const int DescriptionMaxLength = 500;
    public const int WeatherContextMaxLength = 500;
    public const int EventTypeMaxLength = 100;
    public const int RoutingKeyMaxLength = 255;
    public const int OutboxErrorMaxLength = 1000;
}
