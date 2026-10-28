namespace Sensors.Service.Application;

public static class ErrorMessages
{
    public const string FarmNotFound = "Farm not found.";
    public const string AlertNotFound = "Alert not found.";
    public const string ThresholdNotFound = "Alert threshold not found.";
    public const string NotAllowedToReadTelemetry = "Only the farm's manager can access its sensor data.";
    public const string NotAllowedToViewThresholds = "Only managers and administrators can view alert thresholds.";
    public const string OnlyAdminsCanChangeThresholds = "Only administrators can change alert thresholds.";
}
