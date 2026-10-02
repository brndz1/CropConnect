using Farms.Service.Models;
using Farms.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Farms.Service.Application;

public interface IStockApplicationService
{
    #region Reservation lifecycle

    Task<StockReservationResult> ReserveAsync(ReserveStockCommand command, CancellationToken cancellationToken);

    Task<StockReservationResult> ConfirmAsync(Guid orderId, CancellationToken cancellationToken);

    Task<StockReservationResult> ReleaseAsync(Guid orderId, CancellationToken cancellationToken);

    #endregion

    #region Expiry

    Task<int> ExpireDueReservationsAsync(CancellationToken cancellationToken);

    #endregion
}

public sealed class StockApplicationService : IStockApplicationService
{
    #region Dependencies

    private readonly FarmsDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly StockReservationOptions _options;
    private readonly ILogger<StockApplicationService> _logger;

    public StockApplicationService(
        FarmsDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<StockReservationOptions> options,
        ILogger<StockApplicationService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #endregion

    #region Reservation lifecycle

    public async Task<StockReservationResult> ReserveAsync(ReserveStockCommand command, CancellationToken cancellationToken)
    {
        Guard.NotEmpty(command.OrderId, "Order");
        var requested = MergeLines(command.Lines);

        var existing = await _dbContext.StockReservations.AsNoTracking()
            .Where(r => r.OrderId == command.OrderId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            EnsureSameActiveReservation(existing, requested);
            return await BuildResultAsync(command.OrderId, alreadyProcessed: true, cancellationToken);
        }

        var products = await LoadReservableProductsAsync(requested.Keys, cancellationToken);
        var now = Now;
        DateTime? updatedAt = now;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var (productId, quantity) in requested.OrderBy(line => line.Key))
            {
                var affected = await _dbContext.Products
                    .Where(p => p.Id == productId && !p.IsDeleted && p.StockQuantity >= quantity)
                    .ExecuteUpdateAsync(setters => setters
                            .SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity)
                            .SetProperty(p => p.UpdatedAt, updatedAt),
                        cancellationToken);

                if (affected == 0)
                {
                    throw new BusinessRuleException($"Insufficient stock for product '{products[productId].Name}'.");
                }

                _dbContext.StockReservations.Add(
                    StockReservation.Create(command.OrderId, products[productId], quantity, now, _options.TimeToLive));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            throw new ConflictException("A reservation for this order is already being processed.");
        }

        _logger.LogInformation("Stock reserved for order {OrderId} with {LineCount} line(s)", command.OrderId, requested.Count);
        return await BuildResultAsync(command.OrderId, alreadyProcessed: false, cancellationToken);
    }

    public async Task<StockReservationResult> ConfirmAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var now = Now;
        var changed = false;

        await using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var reservations = await LockReservationsAsync(orderId, cancellationToken);
            var expired = await ExpireLockedAsync(reservations, now, cancellationToken);

            if (expired > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Expired stock reservations of {OrderCount} order(s)", 1);
                throw new BusinessRuleException("The reservation for this order has expired.");
            }

            foreach (var reservation in reservations)
            {
                changed |= reservation.Confirm(now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (changed)
        {
            _logger.LogInformation("Stock reservation for order {OrderId} confirmed", orderId);
        }

        return await BuildResultAsync(orderId, alreadyProcessed: !changed, cancellationToken);
    }

    public async Task<StockReservationResult> ReleaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var now = Now;
        var released = new List<StockReservation>();

        await using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var reservations = await LockReservationsAsync(orderId, cancellationToken);

            foreach (var reservation in reservations)
            {
                if (reservation.Release(now))
                {
                    released.Add(reservation);
                }
            }

            await RestoreStockAsync(released, now, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (released.Count > 0)
        {
            _logger.LogInformation("Stock released for order {OrderId} with {LineCount} line(s)", orderId, released.Count);
        }

        return await BuildResultAsync(orderId, alreadyProcessed: released.Count == 0, cancellationToken);
    }

    #endregion

    #region Expiry

    public async Task<int> ExpireDueReservationsAsync(CancellationToken cancellationToken)
    {
        var now = Now;

        var orderIds = await _dbContext.StockReservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Reserved && r.ExpiresAt <= now)
            .GroupBy(r => r.OrderId)
            .Select(group => new { OrderId = group.Key, ExpiresAt = group.Min(r => r.ExpiresAt) })
            .OrderBy(order => order.ExpiresAt)
            .ThenBy(order => order.OrderId)
            .Take(_options.SweepBatchSize)
            .Select(order => order.OrderId)
            .ToListAsync(cancellationToken);

        var expiredOrders = 0;

        foreach (var orderId in orderIds)
        {
            int expired;

            await using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                var reservations = await LockReservationsAsync(orderId, cancellationToken);
                expired = await ExpireLockedAsync(reservations, now, cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            _dbContext.ChangeTracker.Clear();

            if (expired > 0)
            {
                expiredOrders++;
            }
        }

        if (expiredOrders > 0)
        {
            _logger.LogInformation("Expired stock reservations of {OrderCount} order(s)", expiredOrders);
        }

        return expiredOrders;
    }

    private async Task<int> ExpireLockedAsync(List<StockReservation> reservations, DateTime now, CancellationToken cancellationToken)
    {
        var expired = new List<StockReservation>();

        foreach (var reservation in reservations)
        {
            if (reservation.Expire(now))
            {
                expired.Add(reservation);
            }
        }

        await RestoreStockAsync(expired, now, cancellationToken);
        return expired.Count;
    }

    #endregion

    #region Helpers

    private Dictionary<Guid, int> MergeLines(IReadOnlyList<StockLine> lines)
    {
        if (lines.Count == 0)
        {
            throw new InvalidInputException("The order must contain at least one item.");
        }

        if (lines.Count > _options.MaxLinesPerOrder)
        {
            throw new InvalidInputException($"An order can contain at most {_options.MaxLinesPerOrder} items.");
        }

        var merged = new Dictionary<Guid, int>();

        foreach (var line in lines)
        {
            Guard.NotEmpty(line.ProductId, "Product");
            Guard.Positive(line.Quantity, "Quantity");

            var total = (long)merged.GetValueOrDefault(line.ProductId) + line.Quantity;

            if (total > int.MaxValue)
            {
                throw new InvalidInputException("Requested quantity is too large.");
            }

            merged[line.ProductId] = (int)total;
        }

        return merged;
    }

    private static void EnsureSameActiveReservation(List<StockReservation> existing, Dictionary<Guid, int> requested)
    {
        if (!existing.TrueForAll(r => r.HoldsStock))
        {
            throw new BusinessRuleException("The reservation for this order is no longer active.");
        }

        var sameLines = existing.Count == requested.Count
                        && existing.TrueForAll(r => requested.TryGetValue(r.ProductId, out var quantity) && quantity == r.Quantity);

        if (!sameLines)
        {
            throw new ConflictException("A different reservation already exists for this order.");
        }
    }

    private async Task<Dictionary<Guid, Product>> LoadReservableProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.ToList();

        var products = await _dbContext.Products.AsNoTracking()
            .Include(p => p.Farm)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var productId in ids)
        {
            if (!products.TryGetValue(productId, out var product) || product.IsDeleted || product.Farm.IsDeleted)
            {
                throw new NotFoundException($"Product {productId} not found.");
            }

            if (!product.IsVisibleInCatalog)
            {
                throw new BusinessRuleException($"Product '{product.Name}' is not available.");
            }
        }

        return products;
    }

    private async Task<List<StockReservation>> LockReservationsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var reservations = await _dbContext.StockReservations
            .FromSql($"SELECT * FROM \"StockReservations\" WHERE \"OrderId\" = {orderId} ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);

        return reservations.Count > 0
            ? reservations
            : throw new NotFoundException(ErrorMessages.ReservationNotFound);
    }

    private async Task RestoreStockAsync(IEnumerable<StockReservation> reservations, DateTime now, CancellationToken cancellationToken)
    {
        DateTime? updatedAt = now;

        foreach (var reservation in reservations.OrderBy(r => r.ProductId))
        {
            var quantity = reservation.Quantity;

            await _dbContext.Products
                .Where(p => p.Id == reservation.ProductId)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity)
                        .SetProperty(p => p.UpdatedAt, updatedAt),
                    cancellationToken);
        }
    }

    private async Task<StockReservationResult> BuildResultAsync(Guid orderId, bool alreadyProcessed, CancellationToken cancellationToken)
    {
        var reservations = await _dbContext.StockReservations.AsNoTracking()
            .Include(r => r.Product)
            .ThenInclude(p => p.Farm)
            .Where(r => r.OrderId == orderId)
            .OrderBy(r => r.ProductId)
            .ToListAsync(cancellationToken);

        if (reservations.Count == 0)
        {
            throw new NotFoundException(ErrorMessages.ReservationNotFound);
        }

        var lines = reservations
            .Select(r => new ReservedLine(
                r.ProductId,
                r.Product.Name,
                r.Product.FarmId,
                r.Product.Farm.OwnerId,
                r.Quantity,
                r.UnitPrice))
            .ToList();

        return new StockReservationResult(
            orderId,
            reservations[0].Status,
            reservations[0].ExpiresAt,
            alreadyProcessed,
            lines);
    }

    #endregion
}
