using Farms.Service.Models;

namespace Farms.Service.Application;

public sealed record CreateFarmCommand(
    string Name,
    string? Description,
    string Location,
    double? Latitude = null,
    double? Longitude = null);

public sealed record UpdateFarmCommand(
    Guid FarmId,
    string? Name,
    string? Description,
    string? Location,
    int? ExpectedVersion,
    double? Latitude = null,
    double? Longitude = null);

public sealed record FarmQuery(PageRequest Page, bool IncludeInactive, string? Search, Guid? OwnerId);

public sealed record CreateProductCommand(
    Guid FarmId,
    string Name,
    string? Description,
    string? Unit,
    decimal Price,
    int StockQuantity,
    ProductType Type = ProductType.Product,
    DateTime? AvailableUntil = null);

public sealed record UpdateProductCommand(
    Guid ProductId,
    string? Name,
    string? Description,
    string? Unit,
    decimal? Price,
    bool? IsAvailable,
    int? ExpectedVersion,
    DateTime? AvailableUntil = null);

public sealed record ProductQuery(
    PageRequest Page,
    Guid? FarmId,
    string? Search,
    bool OnlyInStock,
    bool IncludeUnavailable,
    ProductType? Type = null);

public sealed record CreatePickupScheduleCommand(
    Guid FarmId,
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime);

public sealed record UpdatePickupScheduleCommand(
    Guid ScheduleId,
    DayOfWeek? DayOfWeek,
    TimeOnly? OpenTime,
    TimeOnly? CloseTime,
    int? ExpectedVersion);

public sealed record StockLine(Guid ProductId, int Quantity);

public sealed record ReserveStockCommand(Guid OrderId, IReadOnlyList<StockLine> Lines);

public sealed record ReservedLine(
    Guid ProductId,
    string ProductName,
    Guid FarmId,
    Guid FarmOwnerId,
    int Quantity,
    decimal UnitPrice);

public sealed record StockReservationResult(
    Guid OrderId,
    ReservationStatus Status,
    DateTime ExpiresAt,
    bool AlreadyProcessed,
    IReadOnlyList<ReservedLine> Lines);
