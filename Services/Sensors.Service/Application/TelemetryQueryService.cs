using Microsoft.EntityFrameworkCore;
using Sensors.Service.Application.Clients;
using Sensors.Service.Data;
using Sensors.Service.Models;

namespace Sensors.Service.Application;

public interface ITelemetryQueryService
{
    Task<PagedResult<SensorReading>> ListReadingsAsync(Caller caller, ReadingQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<SensorReading>> GetLatestReadingsAsync(Caller caller, Guid farmId, CancellationToken cancellationToken);

    Task<PagedResult<Alert>> ListAlertsAsync(Caller caller, AlertQuery query, CancellationToken cancellationToken);

    Task<Alert> AcknowledgeAlertAsync(Caller caller, Guid alertId, CancellationToken cancellationToken);
}

public sealed class TelemetryQueryService : ITelemetryQueryService
{
    #region Dependencies

    private readonly SensorsDbContext _dbContext;
    private readonly IFarmDirectoryClient _farms;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TelemetryQueryService> _logger;

    public TelemetryQueryService(
        SensorsDbContext dbContext,
        IFarmDirectoryClient farms,
        TimeProvider timeProvider,
        ILogger<TelemetryQueryService> logger)
    {
        _dbContext = dbContext;
        _farms = farms;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion

    #region Readings

    public async Task<PagedResult<SensorReading>> ListReadingsAsync(Caller caller, ReadingQuery query, CancellationToken cancellationToken)
    {
        await EnsureCanReadFarmAsync(caller, query.FarmId, cancellationToken);
        EnsureValidPeriod(query.From, query.To);

        var readings = _dbContext.SensorReadings.AsNoTracking().Where(r => r.FarmId == query.FarmId);

        if (query.Type is { } type)
        {
            readings = readings.Where(r => r.Type == type);
        }

        if (query.From is { } from)
        {
            readings = readings.Where(r => r.Timestamp >= from);
        }

        if (query.To is { } to)
        {
            readings = readings.Where(r => r.Timestamp < to);
        }

        return await readings
            .OrderByDescending(r => r.Timestamp)
            .ThenBy(r => r.Id)
            .ToPagedResultAsync(query.Page, cancellationToken);
    }

    public async Task<IReadOnlyList<SensorReading>> GetLatestReadingsAsync(Caller caller, Guid farmId, CancellationToken cancellationToken)
    {
        await EnsureCanReadFarmAsync(caller, farmId, cancellationToken);

        var latest = new List<SensorReading>();

        foreach (var type in Enum.GetValues<SensorType>())
        {
            var reading = await _dbContext.SensorReadings.AsNoTracking()
                .Where(r => r.FarmId == farmId && r.Type == type)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (reading is not null)
            {
                latest.Add(reading);
            }
        }

        return latest;
    }

    #endregion

    #region Alerts

    public async Task<PagedResult<Alert>> ListAlertsAsync(Caller caller, AlertQuery query, CancellationToken cancellationToken)
    {
        await EnsureCanReadFarmAsync(caller, query.FarmId, cancellationToken);
        EnsureValidPeriod(query.From, query.To);

        var alerts = _dbContext.Alerts.AsNoTracking().Where(a => a.FarmId == query.FarmId);

        if (query.OnlyOpen)
        {
            alerts = alerts.Where(a => a.AcknowledgedAt == null);
        }

        if (query.From is { } from)
        {
            alerts = alerts.Where(a => a.CreatedAt >= from);
        }

        if (query.To is { } to)
        {
            alerts = alerts.Where(a => a.CreatedAt < to);
        }

        return await alerts
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToPagedResultAsync(query.Page, cancellationToken);
    }

    public async Task<Alert> AcknowledgeAlertAsync(Caller caller, Guid alertId, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken)
                    ?? throw new NotFoundException(ErrorMessages.AlertNotFound);

        caller.EnsureCanReadTelemetry(alert.FarmOwnerId);

        if (alert.Acknowledge(caller.RequireUserId(), _timeProvider.GetUtcNow().UtcDateTime))
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Alert {AlertId} acknowledged by {UserId}", alert.Id, caller.UserId);
        }

        return alert;
    }

    #endregion

    #region Helpers

    private async Task EnsureCanReadFarmAsync(Caller caller, Guid farmId, CancellationToken cancellationToken)
    {
        if (!caller.IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        var farm = await _farms.GetOwnershipAsync(farmId, cancellationToken);
        caller.EnsureCanReadTelemetry(farm.OwnerId);
    }

    private static void EnsureValidPeriod(DateTime? from, DateTime? to)
    {
        if (from is { } start && to is { } end && start >= end)
        {
            throw new InvalidInputException("The start of the period must be before its end.");
        }
    }

    #endregion
}
