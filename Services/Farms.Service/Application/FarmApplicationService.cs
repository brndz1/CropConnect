using Farms.Service.Models;
using Farms.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Farms.Service.Application;

public interface IFarmApplicationService
{
    #region Queries

    Task<Farm> GetAsync(Caller caller, Guid farmId, CancellationToken cancellationToken);

    Task<PagedResult<Farm>> ListAsync(Caller caller, FarmQuery query, CancellationToken cancellationToken);

    Task<PagedResult<Farm>> ListMineAsync(Caller caller, PageRequest page, CancellationToken cancellationToken);

    #endregion

    #region Commands

    Task<Farm> CreateAsync(Caller caller, CreateFarmCommand command, CancellationToken cancellationToken);

    Task<Farm> UpdateAsync(Caller caller, UpdateFarmCommand command, CancellationToken cancellationToken);

    Task<Farm> SetActiveAsync(Caller caller, Guid farmId, bool isActive, CancellationToken cancellationToken);

    Task DeleteAsync(Caller caller, Guid farmId, CancellationToken cancellationToken);

    #endregion
}

public sealed class FarmApplicationService : IFarmApplicationService
{
    #region Dependencies

    private readonly FarmsDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly FarmOptions _options;
    private readonly ILogger<FarmApplicationService> _logger;

    public FarmApplicationService(
        FarmsDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<FarmOptions> options,
        ILogger<FarmApplicationService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #endregion

    #region Queries

    public async Task<Farm> GetAsync(Caller caller, Guid farmId, CancellationToken cancellationToken)
    {
        var farm = await _dbContext.Farms.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == farmId && !f.IsDeleted, cancellationToken);

        return farm is not null && (farm.IsActive || caller.CanManage(farm))
            ? farm
            : throw new NotFoundException(ErrorMessages.FarmNotFound);
    }

    public async Task<PagedResult<Farm>> ListAsync(Caller caller, FarmQuery query, CancellationToken cancellationToken)
    {
        var farms = _dbContext.Farms.AsNoTracking().Where(f => !f.IsDeleted);

        if (query.OwnerId is { } ownerId)
        {
            farms = farms.Where(f => f.OwnerId == ownerId);
        }

        if (query.IncludeInactive)
        {
            var isOwnerListingOwnFarms = caller.IsManager && query.OwnerId is { } owner && owner == caller.UserId;

            if (!caller.IsAdmin && !isOwnerListingOwnFarms)
            {
                throw new ForbiddenException("Only administrators or the owner can list inactive farms.");
            }
        }
        else
        {
            farms = farms.Where(f => f.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = query.Search.ToContainsPattern();
            farms = farms.Where(f => EF.Functions.ILike(f.Name, pattern, PersistenceExtensions.LikeEscapeCharacter));
        }

        return await farms
            .OrderBy(f => f.Name)
            .ThenBy(f => f.Id)
            .ToPagedResultAsync(query.Page, cancellationToken);
    }

    public async Task<PagedResult<Farm>> ListMineAsync(Caller caller, PageRequest page, CancellationToken cancellationToken)
    {
        var userId = caller.RequireUserId();

        return await _dbContext.Farms.AsNoTracking()
            .Where(f => f.OwnerId == userId && !f.IsDeleted)
            .OrderBy(f => f.Name)
            .ThenBy(f => f.Id)
            .ToPagedResultAsync(page, cancellationToken);
    }

    #endregion

    #region Commands

    public async Task<Farm> CreateAsync(Caller caller, CreateFarmCommand command, CancellationToken cancellationToken)
    {
        var ownerId = caller.RequireUserId();

        if (!caller.IsManager)
        {
            throw new ForbiddenException("Only managers can register a farm.");
        }

        var farm = Farm.Create(ownerId, command.Name, command.Description, command.Location, Now);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.Database.AcquireAdvisoryLockAsync(ownerId, cancellationToken);

        var ownedFarms = await _dbContext.Farms.CountAsync(f => f.OwnerId == ownerId && !f.IsDeleted, cancellationToken);

        if (ownedFarms >= _options.MaxFarmsPerOwner)
        {
            throw new BusinessRuleException($"A manager can have at most {_options.MaxFarmsPerOwner} farms.");
        }

        await EnsureNameAvailableAsync(ownerId, farm.Name, null, cancellationToken);

        _dbContext.Farms.Add(farm);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Farm {FarmId} created by owner {OwnerId}", farm.Id, ownerId);
        return farm;
    }

    public async Task<Farm> UpdateAsync(Caller caller, UpdateFarmCommand command, CancellationToken cancellationToken)
    {
        var userId = caller.RequireUserId();
        var farm = await FindTrackedAsync(command.FarmId, cancellationToken);

        caller.EnsureCanManage(farm);
        farm.EnsureVersion(command.ExpectedVersion);

        var previousName = farm.Name;
        farm.Update(command.Name, command.Description, command.Location, Now);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (!string.Equals(previousName, farm.Name, StringComparison.OrdinalIgnoreCase))
        {
            await _dbContext.Database.AcquireAdvisoryLockAsync(farm.OwnerId, cancellationToken);
            await EnsureNameAvailableAsync(farm.OwnerId, farm.Name, farm.Id, cancellationToken);
        }

        await _dbContext.SaveChangesOrConflictAsync(farm, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Farm {FarmId} updated by {UserId}", farm.Id, userId);
        return farm;
    }

    public async Task<Farm> SetActiveAsync(Caller caller, Guid farmId, bool isActive, CancellationToken cancellationToken)
    {
        var userId = caller.RequireUserId();

        if (!caller.IsAdmin)
        {
            throw new ForbiddenException("Only administrators can activate or deactivate farms.");
        }

        var farm = await FindTrackedAsync(farmId, cancellationToken);
        farm.SetActive(isActive, Now);

        await _dbContext.SaveChangesOrConflictAsync(farm, cancellationToken);

        _logger.LogInformation("Farm {FarmId} set to IsActive={IsActive} by {UserId}", farm.Id, isActive, userId);
        return farm;
    }

    public async Task DeleteAsync(Caller caller, Guid farmId, CancellationToken cancellationToken)
    {
        var userId = caller.RequireUserId();
        var farm = await FindTrackedAsync(farmId, cancellationToken);

        caller.EnsureCanManage(farm);

        var now = Now;
        DateTime? updatedAt = now;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var deletedProducts = await _dbContext.Products
            .Where(p => p.FarmId == farmId && !p.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.IsDeleted, true)
                    .SetProperty(p => p.IsAvailable, false)
                    .SetProperty(p => p.Version, p => p.Version + 1)
                    .SetProperty(p => p.UpdatedAt, updatedAt),
                cancellationToken);

        farm.Delete(now);

        await _dbContext.SaveChangesOrConflictAsync(farm, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Farm {FarmId} deleted by {UserId} with {ProductCount} product(s)", farm.Id, userId, deletedProducts);
    }

    #endregion

    #region Helpers

    private async Task EnsureNameAvailableAsync(Guid ownerId, string name, Guid? exceptFarmId, CancellationToken cancellationToken)
    {
        var normalizedName = name.ToLowerInvariant();

        var nameInUse = await _dbContext.Farms.AnyAsync(
            f => f.OwnerId == ownerId
                 && !f.IsDeleted
                 && f.Id != exceptFarmId
                 && f.Name.ToLower() == normalizedName,
            cancellationToken);

        if (nameInUse)
        {
            throw new ConflictException(ErrorMessages.FarmNameInUse);
        }
    }

    private async Task<Farm> FindTrackedAsync(Guid farmId, CancellationToken cancellationToken) =>
        await _dbContext.Farms.FirstOrDefaultAsync(f => f.Id == farmId && !f.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(ErrorMessages.FarmNotFound);

    #endregion
}
