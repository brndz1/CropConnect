using Farms.Service.Models;

namespace Farms.Service.Application;

public sealed record CreateFarmCommand(string Name, string? Description, string Location);

public sealed record UpdateFarmCommand(
    Guid FarmId,
    string? Name,
    string? Description,
    string? Location,
    int? ExpectedVersion);

public sealed record FarmQuery(PageRequest Page, bool IncludeInactive, string? Search, Guid? OwnerId);

public sealed record CreateProductCommand(
    Guid FarmId,
    string Name,
    string? Description,
    string? Unit,
    decimal Price,
    int StockQuantity);

public sealed record UpdateProductCommand(
    Guid ProductId,
    string? Name,
    string? Description,
    string? Unit,
    decimal? Price,
    bool? IsAvailable,
    int? ExpectedVersion);

public sealed record ProductQuery(
    PageRequest Page,
    Guid? FarmId,
    string? Search,
    bool OnlyInStock,
    bool IncludeUnavailable);

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
