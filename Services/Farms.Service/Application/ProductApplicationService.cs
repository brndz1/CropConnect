using Farms.Service.Models;
using Farms.Service.Data;
using Microsoft.EntityFrameworkCore;

namespace Farms.Service.Application;

public interface IProductApplicationService
{
    #region Queries

    Task<Product> GetAsync(Caller caller, Guid productId, CancellationToken cancellationToken);

    Task<PagedResult<Product>> ListAsync(Caller caller, ProductQuery query, CancellationToken cancellationToken);

    #endregion

    #region Commands

    Task<Product> CreateAsync(Caller caller, CreateProductCommand command, CancellationToken cancellationToken);

    Task<Product> UpdateAsync(Caller caller, UpdateProductCommand command, CancellationToken cancellationToken);

    Task<Product> AdjustStockAsync(Caller caller, Guid productId, int delta, CancellationToken cancellationToken);

    Task DeleteAsync(Caller caller, Guid productId, CancellationToken cancellationToken);

    #endregion
}

public sealed class ProductApplicationService : IProductApplicationService
{
    #region Dependencies

    private readonly FarmsDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProductApplicationService> _logger;

    public ProductApplicationService(FarmsDbContext dbContext, TimeProvider timeProvider, ILogger<ProductApplicationService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #endregion

    #region Queries

    public async Task<Product> GetAsync(Caller caller, Guid productId, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.AsNoTracking()
            .Include(p => p.Farm)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted && !p.Farm.IsDeleted, cancellationToken);

        return product is not null && (product.IsVisibleInCatalog || caller.CanManage(product.Farm))
            ? product
            : throw new NotFoundException(ErrorMessages.ProductNotFound);
    }

    public async Task<PagedResult<Product>> ListAsync(Caller caller, ProductQuery query, CancellationToken cancellationToken)
    {
        var products = _dbContext.Products.AsNoTracking()
            .Include(p => p.Farm)
            .Where(p => !p.IsDeleted && !p.Farm.IsDeleted);

        if (query.FarmId is { } farmId)
        {
            products = products.Where(p => p.FarmId == farmId);
        }

        if (query.IncludeUnavailable)
        {
            await EnsureCanListUnavailableAsync(caller, query.FarmId, cancellationToken);
        }
        else
        {
            products = products.Where(p => p.IsAvailable && p.Farm.IsActive);
        }

        if (query.OnlyInStock)
        {
            products = products.Where(p => p.StockQuantity > 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = query.Search.ToContainsPattern();
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern, PersistenceExtensions.LikeEscapeCharacter));
        }

        return await products
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .ToPagedResultAsync(query.Page, cancellationToken);
    }

    #endregion

    #region Commands

    public async Task<Product> CreateAsync(Caller caller, CreateProductCommand command, CancellationToken cancellationToken)
    {
        var farm = await _dbContext.Farms
                       .FirstOrDefaultAsync(f => f.Id == command.FarmId && !f.IsDeleted, cancellationToken)
                   ?? throw new NotFoundException(ErrorMessages.FarmNotFound);

        caller.EnsureCanManage(farm);

        var product = Product.Create(
            farm,
            command.Name,
            command.Description,
            command.Unit,
            command.Price,
            command.StockQuantity,
            Now);

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Product {ProductId} created in farm {FarmId}", product.Id, farm.Id);
        return product;
    }

    public async Task<Product> UpdateAsync(Caller caller, UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var userId = caller.RequireUserId();
        var product = await FindTrackedAsync(command.ProductId, cancellationToken);

        caller.EnsureCanManage(product.Farm);
        product.EnsureVersion(command.ExpectedVersion);
        product.Update(command.Name, command.Description, command.Unit, command.Price, command.IsAvailable, Now);

        await _dbContext.SaveChangesOrConflictAsync(product, cancellationToken);

        _logger.LogInformation("Product {ProductId} updated by {UserId}", product.Id, userId);
        return product;
    }

    public async Task<Product> AdjustStockAsync(Caller caller, Guid productId, int delta, CancellationToken cancellationToken)
    {
        if (delta == 0)
        {
            throw new InvalidInputException("Delta must be different from zero.");
        }

        var product = await FindTrackedAsync(productId, cancellationToken);
        caller.EnsureCanManage(product.Farm);

        var maxStockBeforeIncrease = delta > 0 ? int.MaxValue - delta : int.MaxValue;
        DateTime? updatedAt = Now;

        var affected = await _dbContext.Products
            .Where(p => p.Id == productId
                        && !p.IsDeleted
                        && p.StockQuantity + delta >= 0
                        && p.StockQuantity <= maxStockBeforeIncrease)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity + delta)
                    .SetProperty(p => p.UpdatedAt, updatedAt),
                cancellationToken);

        if (affected == 0)
        {
            throw new BusinessRuleException(delta < 0
                ? "Insufficient stock: the stock quantity cannot become negative."
                : "Stock quantity is too large.");
        }

        await _dbContext.Entry(product).ReloadAsync(cancellationToken);

        _logger.LogInformation("Stock of product {ProductId} adjusted by {Delta} to {StockQuantity}", product.Id, delta, product.StockQuantity);
        return product;
    }

    public async Task DeleteAsync(Caller caller, Guid productId, CancellationToken cancellationToken)
    {
        var userId = caller.RequireUserId();
        var product = await FindTrackedAsync(productId, cancellationToken);

        caller.EnsureCanManage(product.Farm);
        product.Delete(Now);

        await _dbContext.SaveChangesOrConflictAsync(product, cancellationToken);

        _logger.LogInformation("Product {ProductId} deleted by {UserId}", product.Id, userId);
    }

    #endregion

    #region Helpers

    private async Task EnsureCanListUnavailableAsync(Caller caller, Guid? farmId, CancellationToken cancellationToken)
    {
        if (caller.IsAdmin)
        {
            return;
        }

        var userId = caller.UserId;

        var ownsFarm = caller.IsManager
                       && farmId is not null
                       && await _dbContext.Farms.AnyAsync(
                           f => f.Id == farmId && f.OwnerId == userId && !f.IsDeleted,
                           cancellationToken);

        if (!ownsFarm)
        {
            throw new ForbiddenException("Only the farm owner or an administrator can list unavailable products.");
        }
    }

    private async Task<Product> FindTrackedAsync(Guid productId, CancellationToken cancellationToken) =>
        await _dbContext.Products
            .Include(p => p.Farm)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted && !p.Farm.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(ErrorMessages.ProductNotFound);

    #endregion
}
