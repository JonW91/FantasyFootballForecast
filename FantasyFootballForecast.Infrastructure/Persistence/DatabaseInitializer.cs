using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FantasyFootballForecast.Infrastructure.Persistence;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly FantasyFootballForecastDbContext _dbContext;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(FantasyFootballForecastDbContext dbContext, ILogger<DatabaseInitializer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.GetMigrations().Any())
        {
            _logger.LogInformation("Applying database migrations.");
            await _dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            _logger.LogInformation("No migrations found yet. Creating database schema with EnsureCreated.");
            await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        await SeedDataAsync(cancellationToken);
    }

    private async Task SeedDataAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Seasons.AnyAsync(cancellationToken))
        {
            return;
        }

        var season = new Domain.Season
        {
            Name = "Premier League 2025/26",
            StartYear = 2025,
            EndYear = 2026,
            IsCurrent = true
        };

        _dbContext.Seasons.Add(season);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var teams = SeedData.Teams.ToList();
        foreach (var team in teams)
        {
            _dbContext.Teams.Add(team);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var gameweek = new Domain.Gameweek
        {
            SeasonId = season.Id,
            Number = 1,
            StartsUtc = DateTime.UtcNow.Date,
            EndsUtc = DateTime.UtcNow.Date.AddDays(7),
            IsCurrent = true
        };

        _dbContext.Gameweeks.Add(gameweek);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var player in SeedData.Players)
        {
            _dbContext.Players.Add(player);
        }

        foreach (var fixture in SeedData.Fixtures(teams, season.Id, gameweek.Id))
        {
            _dbContext.Fixtures.Add(fixture);
        }

        _dbContext.NewsItems.AddRange(SeedData.NewsItems());
        _dbContext.PlayerAvailabilities.AddRange(SeedData.Availabilities());
        _dbContext.ModelTrainingRuns.AddRange(SeedData.ModelTrainingRuns());
        _dbContext.DataIngestionRuns.AddRange(SeedData.DataIngestionRuns());

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
