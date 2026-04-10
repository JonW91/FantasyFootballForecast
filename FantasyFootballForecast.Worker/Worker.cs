using FantasyFootballForecast.Application;
using FantasyFootballForecast.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace FantasyFootballForecast.Worker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IngestionOptions _options;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IOptions<IngestionOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunMaintenanceCycleAsync(stoppingToken);

        if (!_options.Enabled)
        {
            _logger.LogInformation("Background ingestion is disabled. Startup maintenance cycle completed, recurring execution skipped.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.IntervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunMaintenanceCycleAsync(stoppingToken);
        }
    }

    private async Task RunMaintenanceCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        var sync = scope.ServiceProvider.GetRequiredService<IFootballSyncService>();
        var training = scope.ServiceProvider.GetRequiredService<IModelTrainingService>();

        _logger.LogInformation("Starting maintenance cycle.");
        await initializer.InitializeAsync(cancellationToken);

        if (_options.Providers.Length == 0)
        {
            await sync.SyncAllAsync(cancellationToken);
        }
        else
        {
            foreach (var provider in _options.Providers.Where(provider => !string.IsNullOrWhiteSpace(provider)))
            {
                await sync.SyncFromProviderAsync(provider, cancellationToken);
            }
        }

        await training.RetrainAllAsync(cancellationToken);
        _logger.LogInformation("Maintenance cycle completed.");
    }
}
