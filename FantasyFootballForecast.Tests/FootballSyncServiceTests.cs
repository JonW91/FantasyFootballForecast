using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using FantasyFootballForecast.Infrastructure.Persistence;
using FantasyFootballForecast.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FantasyFootballForecast.Tests;

public sealed class FootballSyncServiceTests
{
    [Test]
    public async Task SyncHistoricalAsync_ImportsHistoricalFixturesAndPlayerStats()
    {
        await using var db = CreateDb();
        var provider = new StubProvider();
        var service = new FootballSyncService(
            db,
            [provider],
            NoopAvailabilityEnrichmentService.Instance,
            NullLogger<FootballSyncService>.Instance);

        var run = await service.SyncHistoricalAsync();

        Assert.That(run.Status, Is.EqualTo("Completed"));
        Assert.That(db.Fixtures.Count(), Is.EqualTo(1));
        Assert.That(db.PlayerMatchStats.Count(), Is.EqualTo(1));
        Assert.That(db.TeamMatchStats.Count(), Is.EqualTo(2));
        Assert.That(db.NewsItems.Count(), Is.EqualTo(0));
    }

    private static FantasyFootballForecastDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FantasyFootballForecastDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var db = new FantasyFootballForecastDbContext(options);
        db.Seasons.Add(new Season { Name = "Premier League 2025/26", StartYear = 2025, EndYear = 2026, IsCurrent = true });
        db.Gameweeks.Add(new Gameweek { SeasonId = 1, Number = 1, StartsUtc = DateTime.UtcNow.AddDays(-7), EndsUtc = DateTime.UtcNow, IsCurrent = true });
        db.SaveChanges();
        return db;
    }

    private sealed class StubProvider : IFootballDataProvider
    {
        public string Name => "FPL Public API";

        public Task<IReadOnlyList<ProviderTeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderTeamDto>>([
                new ProviderTeamDto(1, "Arsenal", "ARS", "3", null, 88, 1.9m, 0.9m),
                new ProviderTeamDto(2, "Chelsea", "CHE", "8", null, 81, 1.7m, 1.0m)
            ]);

        public Task<IReadOnlyList<ProviderPlayerDto>> GetPlayersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderPlayerDto>>([
                new ProviderPlayerDto(11, 1, "Bukayo Saka", "Bukayo", "Saka", "MID", 7, 9.0m, 34.5m, 6.8m, 1980m, 38m, 12m, 11m, 14m, 3m, 0m, AvailabilityStatus.Available, 0.98m, "Available")
            ]);

        public Task<IReadOnlyList<ProviderFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderFixtureDto>>([
                new ProviderFixtureDto(101, 2025, 1, 1, 2, DateTime.UtcNow.AddDays(-1), 2, 1, "Emirates Stadium", true, false, false, "Finished")
            ]);

        public Task<IReadOnlyList<ProviderNewsDto>> GetNewsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderNewsDto>>([]);

        public Task<IReadOnlyList<ProviderAvailabilityDto>> GetAvailabilityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderAvailabilityDto>>([]);

        public Task<IReadOnlyList<ProviderPlayerMatchStatDto>> GetPlayerMatchStatsAsync(int playerExternalId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderPlayerMatchStatDto>>([
                new ProviderPlayerMatchStatDto(
                    11,
                    2,
                    101,
                    1,
                    DateTime.UtcNow.AddDays(-1),
                    true,
                    90,
                    1,
                    0,
                    1,
                    0,
                    3,
                    0,
                    0,
                    0,
                    9,
                    81,
                    8.4m,
                    9.0m)
            ]);
    }

    private sealed class NoopAvailabilityEnrichmentService : IAvailabilityEnrichmentService
    {
        public static readonly NoopAvailabilityEnrichmentService Instance = new();

        public AvailabilityEnrichmentResult Enrich(string sourceName, string? sourceUrl, string rawText, DateTimeOffset? publishedUtc = null)
            => new(AvailabilityStatus.Unknown, false, false, 0m, null, 0m, null, rawText);
    }
}
