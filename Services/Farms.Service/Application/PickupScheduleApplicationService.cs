using Farms.Service.Data;
using Farms.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Farms.Service.Application;

public interface IPickupScheduleApplicationService
{
    Task<PickupSchedule> GetAsync(Caller caller, Guid scheduleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PickupSchedule>> ListByFarmAsync(Caller caller, Guid farmId, CancellationToken cancellationToken);

    Task<PickupSchedule> CreateAsync(Caller caller, CreatePickupScheduleCommand command, CancellationToken cancellationToken);

    Task<PickupSchedule> UpdateAsync(Caller caller, UpdatePickupScheduleCommand command, CancellationToken cancellationToken);

    Task DeleteAsync(Caller caller, Guid scheduleId, CancellationToken cancellationToken);
}

public sealed class PickupScheduleApplicationService : IPickupScheduleApplicationService
{
    #region Dependencies

    private readonly FarmsDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PickupScheduleApplicationService> _logger;

    public PickupScheduleApplicationService(
        FarmsDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<PickupScheduleApplicationService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #endregion

    #region Queries

    public async Task<PickupSchedule> GetAsync(Caller caller, Guid scheduleId, CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.PickupSchedules.AsNoTracking()
            .Include(s => s.Farm)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted && !s.Farm.IsDeleted, cancellationToken);

        return schedule is not null && (schedule.Farm.IsActive || caller.CanManage(schedule.Farm))
            ? schedule
            : throw new NotFoundException(ErrorMessages.PickupScheduleNotFound);
    }

    public async Task<IReadOnlyList<PickupSchedule>> ListByFarmAsync(Caller caller, Guid farmId, CancellationToken cancellationToken)
    {
        var farm = await _dbContext.Farms.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == farmId && !f.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(ErrorMessages.FarmNotFound);

        if (!farm.IsActive && !caller.CanManage(farm))
        {
            throw new NotFoundException(ErrorMessages.FarmNotFound);
        }

        return await _dbContext.PickupSchedules.AsNoTracking()
            .Where(s => s.FarmId == farmId && !s.IsDeleted)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.OpenTime)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Commands

    public async Task<PickupSchedule> CreateAsync(Caller caller, CreatePickupScheduleCommand command, CancellationToken cancellationToken)
    {
        var farm = await _dbContext.Farms
            .FirstOrDefaultAsync(f => f.Id == command.FarmId && !f.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(ErrorMessages.FarmNotFound);

        caller.EnsureCanManage(farm);

        var schedule = PickupSchedule.Create(
            farm,
            command.DayOfWeek,
            command.OpenTime,
            command.CloseTime,
            Now);

        _dbContext.PickupSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Pickup schedule {ScheduleId} created for farm {FarmId} ({DayOfWeek} {OpenTime}-{CloseTime})",
            schedule.Id, farm.Id, schedule.DayOfWeek, schedule.OpenTime, schedule.CloseTime);

        return schedule;
    }

    public async Task<PickupSchedule> UpdateAsync(Caller caller, UpdatePickupScheduleCommand command, CancellationToken cancellationToken)
    {
        var userId = caller.RequireUserId();
        var schedule = await FindTrackedAsync(command.ScheduleId, cancellationToken);

        caller.EnsureCanManage(schedule.Farm);
        schedule.EnsureVersion(command.ExpectedVersion);

        schedule.Update(command.DayOfWeek, command.OpenTime, command.CloseTime, Now);

        await _dbContext.SaveChangesOrConflictAsync(schedule, cancellationToken);

        _logger.LogInformation("Pickup schedule {ScheduleId} updated by {UserId}", schedule.Id, userId);
        return schedule;
    }

    public async Task DeleteAsync(Caller caller, Guid scheduleId, CancellationToken cancellationToken)
    {
        var userId = caller.RequireUserId();
        var schedule = await FindTrackedAsync(scheduleId, cancellationToken);

        caller.EnsureCanManage(schedule.Farm);
        schedule.Delete(Now);

        await _dbContext.SaveChangesOrConflictAsync(schedule, cancellationToken);

        _logger.LogInformation("Pickup schedule {ScheduleId} deleted by {UserId}", schedule.Id, userId);
    }

    #endregion

    #region Helpers

    private async Task<PickupSchedule> FindTrackedAsync(Guid scheduleId, CancellationToken cancellationToken) =>
        await _dbContext.PickupSchedules
            .Include(s => s.Farm)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted && !s.Farm.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(ErrorMessages.PickupScheduleNotFound);

    #endregion
}
