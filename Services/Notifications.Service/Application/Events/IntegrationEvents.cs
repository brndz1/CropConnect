namespace Notifications.Service.Application.Events;

// The JSON contracts published on cropconnect.events by Orders and Sensors (camelCase, enums as camelCase strings).
// Enum values are kept as strings so a new status in a producer doesn't break deserialization here.

public sealed record OrderCreatedMessage(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    decimal TotalAmount,
    int ItemCount,
    DateTime OccurredAt);

public sealed record OrderStatusChangedMessage(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    string PreviousStatus,
    string NewStatus,
    DateTime OccurredAt);

public sealed record OrderCancelledMessage(
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    string PreviousStatus,
    string? Reason,
    bool CancelledByCustomer,
    DateTime OccurredAt);

public sealed record SensorAlertRaisedMessage(
    Guid AlertId,
    Guid FarmId,
    Guid FarmOwnerId,
    string FarmName,
    string Type,
    double Value,
    double MinValue,
    double MaxValue,
    string Unit,
    string Description,
    string? WeatherContext,
    DateTime OccurredAt);
