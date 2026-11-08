using Microsoft.Extensions.Options;
using Analytics.Ingestor.Storage;

namespace Analytics.Ingestor.Catalog;

// Takes a catalog snapshot every interval. The time of the last one is read from MongoDB, so a restart does not
// take an extra snapshot, and a snapshot missed while the service was down is taken as soon as it starts.
public sealed class CatalogSnapshotWorker : BackgroundService
{
    #region Dependencies

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRawStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly CatalogSnapshotOptions _options;
    private readonly ILogger<CatalogSnapshotWorker> _logger;

    public CatalogSnapshotWorker(
        IServiceScopeFactory scopeFactory,
        IRawStore store,
        TimeProvider timeProvider,
        IOptions<CatalogSnapshotOptions> options,
        ILogger<CatalogSnapshotWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    #endregion

    #region Execution

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var last = await _store.GetLatestSnapshotTimeAsync(stoppingToken);
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var wait = CatalogSnapshotService.NextDue(last, now, _options.Interval) - now;

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, _timeProvider, stoppingToken);
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<CatalogSnapshotService>().TakeAsync(stoppingToken);
                _logger.LogInformation("Catalog snapshot stored: {Count} farms and products", count);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Cannot take the catalog snapshot; retrying in {Delay}", _options.RetryDelay);
                await Task.Delay(_options.RetryDelay, _timeProvider, stoppingToken);
            }
        }
    }

    #endregion
}
