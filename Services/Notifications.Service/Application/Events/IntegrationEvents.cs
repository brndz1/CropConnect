namespace Notifications.Service.Application.Events;

// The JSON contracts published on cropconnect.events by Orders, Sensors and Auth (camelCase, enums as camelCase strings).
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

public sealed record ReviewCreatedMessage(
    Guid ReviewId,
    Guid OrderId,
    Guid CustomerId,
    Guid FarmId,
    Guid FarmOwnerId,
    int Rating,
    string? Comment,
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

// Auth: an email with a one-time link (verification, password reset, invitation).
public sealed record AccountTokenMessage(
    Guid UserId,
    string Email,
    string Username,
    string Role,
    string Token,
    DateTime ExpiresAt,
    DateTime OccurredAt);

// Auth: an email that only informs (welcome, password changed, account deactivated or reactivated).
public sealed record AccountMessage(
    Guid UserId,
    string Email,
    string Username,
    string Role,
    DateTime OccurredAt);
