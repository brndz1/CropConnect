using Microsoft.Extensions.Options;
using Orders.Service.Data;

namespace Orders.Service.Application;

public sealed class StockReleaseWorker : BackgroundService
{
    #region Dependencies

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly OrderOptions _options;
    private readonly ILogger<StockReleaseWorker> _logger;

    public StockReleaseWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<OrderOptions> options,
        ILogger<StockReleaseWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    #endregion

    #region Execution

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableStockReleaseWorker)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.StockReleaseRetryInterval, _timeProvider);

        do
        {
            await RetryAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RetryAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            await using var workerLock = await WorkerLock.TryAcquireAsync(dbContext, WorkerLock.StockRelease, stoppingToken);

            if (workerLock is null)
            {
                return;
            }

            var orders = scope.ServiceProvider.GetRequiredService<IOrderApplicationService>();

            var released = await orders.ReleasePendingStockAsync(stoppingToken);

            if (released > 0)
            {
                _logger.LogInformation("Released stock of {OrderCount} cancelled order(s) on retry", released);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Stock release retry failed");
        }
    }

    #endregion
}
