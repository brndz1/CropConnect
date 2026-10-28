using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sensors.Service.Data;
using Sensors.Service.Models;

namespace Sensors.Service.Application;

public interface IThresholdApplicationService
{
    Task<IReadOnlyList<AlertThreshold>> ListAsync(Caller caller, CancellationToken cancellationToken);

    Task<AlertThreshold> UpdateAsync(Caller caller, UpdateThresholdCommand command, CancellationToken cancellationToken);

    Task<AlertThreshold> GetForEvaluationAsync(SensorType type, CancellationToken cancellationToken);
}

public sealed class ThresholdApplicationService : IThresholdApplicationService
{
    #region Dependencies

    // Every reading is evaluated against its threshold, so they are cached. Another instance
    // may keep an old threshold for up to this long after an update.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private readonly SensorsDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ThresholdApplicationService> _logger;

    public ThresholdApplicationService(
        SensorsDbContext dbContext,
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILogger<ThresholdApplicationService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #endregion

    #region Queries

    public async Task<IReadOnlyList<AlertThreshold>> ListAsync(Caller caller, CancellationToken cancellationToken)
    {
        caller.EnsureCanViewThresholds();

        return await _dbContext.AlertThresholds.AsNoTracking()
            .OrderBy(t => t.Type)
            .ToListAsync(cancellationToken);
    }

    public async Task<AlertThreshold> GetForEvaluationAsync(SensorType type, CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync(CacheKey(type), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _dbContext.AlertThresholds.AsNoTracking()
                       .FirstOrDefaultAsync(t => t.Type == type, cancellationToken)
                   ?? throw new NotFoundException(ErrorMessages.ThresholdNotFound);
        }) ?? throw new NotFoundException(ErrorMessages.ThresholdNotFound);

    #endregion

    #region Commands

    public async Task<AlertThreshold> UpdateAsync(Caller caller, UpdateThresholdCommand command, CancellationToken cancellationToken)
    {
        caller.EnsureAdmin();
        var type = command.Type.Required();

        var threshold = await _dbContext.AlertThresholds.FirstOrDefaultAsync(t => t.Type == type, cancellationToken)
                        ?? throw new NotFoundException(ErrorMessages.ThresholdNotFound);

        threshold.EnsureVersion(command.ExpectedVersion);
        threshold.Update(command.MinValue, command.MaxValue, Now);

        await _dbContext.SaveChangesOrConflictAsync(threshold, cancellationToken);
        _cache.Remove(CacheKey(type));

        _logger.LogInformation(
            "Threshold for {Type} set to [{Min}, {Max}] by {UserId}",
            type, threshold.MinValue, threshold.MaxValue, caller.UserId);

        return threshold;
    }

    #endregion

    #region Helpers

    private static string CacheKey(SensorType type) => $"threshold:{type}";

    #endregion
}
