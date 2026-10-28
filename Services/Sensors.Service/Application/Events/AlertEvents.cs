using Sensors.Service.Models;

namespace Sensors.Service.Application.Events;

public sealed record SensorAlertRaisedEvent(
    Guid AlertId,
    Guid FarmId,
    Guid FarmOwnerId,
    string FarmName,
    SensorType Type,
    double Value,
    double MinValue,
    double MaxValue,
    string Unit,
    string Description,
    string? WeatherContext,
    DateTime OccurredAt);
