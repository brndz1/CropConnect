using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sensors.Service.Application.Clients;
using Sensors.Service.Application.Events;
using Sensors.Service.Data;
using Sensors.Service.Models;

namespace Sensors.Service.Application;

public interface IReadingIngestionService
{
    Task<RecordReadingResult> RecordAsync(RecordReadingCommand command, CancellationToken cancellationToken);
}

public sealed class ReadingIngestionService : IReadingIngestionService
{
    #region Dependencies

    private readonly SensorsDbContext _dbContext;
    private readonly IFarmDirectoryClient _farms;
    private readonly IThresholdApplicationService _thresholds;
    private readonly IAlertEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly SensorReadingOptions _options;
    private readonly ILogger<ReadingIngestionService> _logger;

    public ReadingIngestionService(
        SensorsDbContext dbContext,
        IFarmDirectoryClient farms,
        IThresholdApplicationService thresholds,
        IAlertEventPublisher eventPublisher,
        TimeProvider timeProvider,
        IOptions<SensorReadingOptions> options,
        ILogger<ReadingIngestionService> logger)
    {
        _dbContext = dbContext;
        _farms = farms;
        _thresholds = thresholds;
        _eventPublisher = eventPublisher;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #endregion

    #region Ingestion

    // RF16 store the reading, RF17 compare it with the global threshold, RF21 alert the manager.
    public async Task<RecordReadingResult> RecordAsync(RecordReadingCommand command, CancellationToken cancellationToken)
    {
        if (await FindProcessedAsync(command.MessageId, cancellationToken) is { } processed)
        {
            return processed;
        }

        var now = Now;
        var reading = SensorReading.Create(
            command.MessageId,
            command.FarmId,
            command.SensorId,
            command.Type,
            command.Value,
            command.Timestamp,
            now,
            _options.MaxClockSkew);

        var farm = await _farms.GetOwnershipAsync(reading.FarmId, cancellationToken);

        if (!farm.IsActive)
        {
            throw new BusinessRuleException($"Farm {farm.FarmId} is inactive; its readings are not recorded.");
        }

        var threshold = await _thresholds.GetForEvaluationAsync(reading.Type, cancellationToken);
        var alert = await ShouldRaiseAlertAsync(reading, threshold, now, cancellationToken)
            ? Alert.Create(reading, threshold, farm.OwnerId, now)
            : null;

        _dbContext.SensorReadings.Add(reading);

        if (alert is not null)
        {
            _dbContext.Alerts.Add(alert);
            StageAlertRaised(alert, farm);
        }

        // The reading, its alert and the alert event are committed together.
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // The same message was delivered twice concurrently and the other delivery won.
            _dbContext.ChangeTracker.Clear();
            return await FindProcessedAsync(command.MessageId, cancellationToken)
                   ?? throw new ConflictException("The reading is already being processed.");
        }

        if (alert is not null)
        {
            _logger.LogInformation("Alert {AlertId} raised for farm {FarmId}: {Description}", alert.Id, alert.FarmId, alert.Description);
        }

        return new RecordReadingResult(reading, AlreadyProcessed: false, alert);
    }

    #endregion

    #region Helpers

    private async Task<RecordReadingResult?> FindProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var reading = await _dbContext.SensorReadings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.MessageId == messageId, cancellationToken);

        if (reading is null)
        {
            return null;
        }

        var alert = await _dbContext.Alerts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ReadingId == reading.Id, cancellationToken);

        return new RecordReadingResult(reading, AlreadyProcessed: true, alert);
    }

    private async Task<bool> ShouldRaiseAlertAsync(
        SensorReading reading,
        AlertThreshold threshold,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (threshold.IsWithin(reading.Value))
        {
            return false;
        }

        var cooldownStart = now - _options.AlertCooldown;

        return !await _dbContext.Alerts.AnyAsync(
            a => a.FarmId == reading.FarmId && a.Type == reading.Type && a.CreatedAt > cooldownStart,
            cancellationToken);
    }

    private void StageAlertRaised(Alert alert, FarmOwnership farm) =>
        _eventPublisher.StageAlertRaised(
            new SensorAlertRaisedEvent(
                alert.Id,
                alert.FarmId,
                alert.FarmOwnerId,
                farm.Name,
                alert.Type,
                alert.Value,
                alert.MinValue,
                alert.MaxValue,
                alert.Type.Unit(),
                alert.Description,
                alert.WeatherContext,
                alert.CreatedAt));

    #endregion
}
