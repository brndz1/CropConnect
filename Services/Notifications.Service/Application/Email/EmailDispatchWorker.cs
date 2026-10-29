using Microsoft.Extensions.Options;

namespace Notifications.Service.Application.Email;

// Emails are sent apart from event handling: a slow or failing provider never blocks the consumer,
// and the notification is already in the user's inbox (RNF availability).
public sealed class EmailDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailDispatchWorker> _logger;

    public EmailDispatchWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<EmailOptions> options,
        ILogger<EmailDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableDispatcher)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.PollInterval, _timeProvider);

        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IEmailDispatchService>();
                var sent = await dispatcher.DispatchDueAsync(stoppingToken);

                if (sent > 0)
                {
                    _logger.LogInformation("Sent {Count} notification email(s) via {Provider}", sent, _options.Provider);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Email dispatch failed; it will be retried");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
