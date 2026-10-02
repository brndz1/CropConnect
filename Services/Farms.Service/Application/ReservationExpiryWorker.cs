using Microsoft.Extensions.Options;

namespace Farms.Service.Application;

public sealed class ReservationExpiryWorker : BackgroundService
{
    #region Dependencies

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly StockReservationOptions _options;
    private readonly ILogger<ReservationExpiryWorker> _logger;

    public ReservationExpiryWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<StockReservationOptions> options,
        ILogger<ReservationExpiryWorker> logger)
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
        if (!_options.EnableExpiryWorker)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.SweepInterval, _timeProvider);

        do
        {
            await SweepAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var stock = scope.ServiceProvider.GetRequiredService<IStockApplicationService>();

            int expired;

            do
            {
                expired = await stock.ExpireDueReservationsAsync(stoppingToken);
            }
            while (expired >= _options.SweepBatchSize && !stoppingToken.IsCancellationRequested);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Stock reservation expiry sweep failed");
        }
    }

    #endregion
}
