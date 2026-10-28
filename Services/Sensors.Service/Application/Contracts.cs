using Sensors.Service.Models;

namespace Sensors.Service.Application;

#region Thresholds

public sealed record UpdateThresholdCommand(SensorType Type, double MinValue, double MaxValue, int? ExpectedVersion);

#endregion

#region Readings

public sealed record RecordReadingCommand(
    Guid MessageId,
    Guid FarmId,
    string SensorId,
    SensorType Type,
    double Value,
    DateTime Timestamp);

public sealed record RecordReadingResult(SensorReading Reading, bool AlreadyProcessed, Alert? Alert);

#endregion
