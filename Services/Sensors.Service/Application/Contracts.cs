using Sensors.Service.Models;

namespace Sensors.Service.Application;

#region Thresholds

public sealed record UpdateThresholdCommand(SensorType Type, double MinValue, double MaxValue, int? ExpectedVersion);

#endregion
