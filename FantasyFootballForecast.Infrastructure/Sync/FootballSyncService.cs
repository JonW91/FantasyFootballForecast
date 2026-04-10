using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using FantasyFootballForecast.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FantasyFootballForecast.Infrastructure.Sync;

public sealed class FootballSyncService : IFootballSyncService
{
    private readonly FantasyFootballForecastDbContext _db;
    private readonly IEnumerable<IFootballDataProvider> _providers;
    private readonly IAvailabilityEnrichmentService _enrichmentService;
    private readonly ILogger<FootballSyncService> _logger;

    public FootballSyncService(
        FantasyFootballForecastDbContext db,
        IEnumerable<IFootballDataProvider> providers,
        IAvailabilityEnrichmentService enrichmentService,
        ILogger<FootballSyncService> logger)
    {
        _db = db;
        _providers = providers;
        _enrichmentService = enrichmentService;
        _logger = logger;
    }

    public Task<DataIngestionRunDto> SyncAllAsync(CancellationToken cancellationToken = default)
        => RunSyncAsync("all", includeOperationalFeeds: true, includeSnapshots: true, includeHistoricalStats: true, cancellationToken);

    public async Task<DataIngestionRunDto> SyncFromProviderAsync(string providerName, CancellationToken cancellationToken = default)
        => await RunSyncAsync(providerName, includeOperationalFeeds: true, includeSnapshots: true, includeHistoricalStats: true, cancellationToken);

    public async Task<DataIngestionRunDto> SyncHistoricalAsync(string providerName = "FPL Public API", CancellationToken cancellationToken = default)
        => await RunSyncAsync(providerName, includeOperationalFeeds: false, includeSnapshots: false, includeHistoricalStats: true, cancellationToken);

    private async Task<DataIngestionRunDto> RunSyncAsync(string providerName, bool includeOperationalFeeds, bool includeSnapshots, bool includeHistoricalStats, CancellationToken cancellationToken)
    {
        var run = new DataIngestionRun
        {
            SourceName = providerName,
            StartedUtc = DateTime.UtcNow,
            Status = "Running"
        };

        _db.DataIngestionRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var providers = providerName.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? _providers
                : _providers.Where(provider => provider.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

            var processed = 0;
            var upserted = 0;

            foreach (var provider in providers)
            {
                var teams = await provider.GetTeamsAsync(cancellationToken);
                var players = await provider.GetPlayersAsync(cancellationToken);
                var fixtures = await provider.GetFixturesAsync(cancellationToken);

                upserted += await UpsertTeamsAsync(teams, cancellationToken);
                upserted += await UpsertPlayersAsync(players, cancellationToken);
                upserted += await UpsertFixturesAsync(fixtures, cancellationToken);
                var news = includeOperationalFeeds ? await provider.GetNewsAsync(cancellationToken) : [];
                var availability = includeOperationalFeeds ? await provider.GetAvailabilityAsync(cancellationToken) : [];
                var snapshotCount = includeSnapshots ? await UpsertPlayerSnapshotsAsync(players, cancellationToken) : 0;
                var historicalMatchCount = includeHistoricalStats ? await UpsertHistoricalPlayerMatchStatsAsync(provider, players, cancellationToken) : 0;
                var teamMatchCount = await UpsertTeamMatchStatsAsync(fixtures, cancellationToken);

                if (includeOperationalFeeds)
                {
                    upserted += await UpsertNewsAsync(news, cancellationToken);
                    upserted += await UpsertAvailabilityAsync(availability, cancellationToken);
                    processed += news.Count + availability.Count;
                }

                upserted += snapshotCount + historicalMatchCount + teamMatchCount;
                processed += teams.Count + players.Count + fixtures.Count + snapshotCount + historicalMatchCount + teamMatchCount;
            }

            run.Status = "Completed";
            run.CompletedUtc = DateTime.UtcNow;
            run.ItemsProcessed = processed;
            run.ItemsUpserted = upserted;
            run.ItemsSkipped = Math.Max(0, processed - upserted);
            run.Notes = $"Synced from {providerName}.";
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for provider {Provider}", providerName);
            run.Status = "Failed";
            run.CompletedUtc = DateTime.UtcNow;
            run.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        return ToDto(run);
    }

    private async Task<int> UpsertTeamsAsync(IReadOnlyList<ProviderTeamDto> teams, CancellationToken cancellationToken)
    {
        var affected = 0;
        foreach (var team in teams)
        {
            var existing = await _db.Teams.FirstOrDefaultAsync(t => t.ExternalId == team.ExternalId, cancellationToken);
            if (existing is null)
            {
                existing = new Team
                {
                    ExternalId = team.ExternalId,
                    Name = team.Name,
                    ShortName = team.ShortName,
                    Code = team.Code,
                    CrestUrl = team.CrestUrl,
                    StrengthRating = team.StrengthRating,
                    ExpectedGoalsForPerMatch = team.ExpectedGoalsForPerMatch,
                    ExpectedGoalsAgainstPerMatch = team.ExpectedGoalsAgainstPerMatch,
                    LastUpdatedUtc = DateTime.UtcNow
                };
                _db.Teams.Add(existing);
            }
            else
            {
                existing.Name = team.Name;
                existing.ShortName = team.ShortName;
                existing.Code = team.Code;
                existing.CrestUrl = team.CrestUrl;
                existing.StrengthRating = team.StrengthRating;
                existing.ExpectedGoalsForPerMatch = team.ExpectedGoalsForPerMatch;
                existing.ExpectedGoalsAgainstPerMatch = team.ExpectedGoalsAgainstPerMatch;
                existing.LastUpdatedUtc = DateTime.UtcNow;
            }

            affected++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return affected;
    }

    private async Task<int> UpsertPlayersAsync(IReadOnlyList<ProviderPlayerDto> players, CancellationToken cancellationToken)
    {
        var affected = 0;
        foreach (var player in players)
        {
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.ExternalId == player.ExternalTeamId, cancellationToken);
            if (team is null)
            {
                continue;
            }

            var existing = await _db.Players.FirstOrDefaultAsync(p => p.ExternalId == player.ExternalId, cancellationToken);
            if (existing is null)
            {
                existing = new Player
                {
                    ExternalId = player.ExternalId,
                    TeamId = team.Id,
                    Name = player.Name,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    Position = player.Position,
                    ShirtNumber = player.ShirtNumber,
                    Price = player.Price,
                    OwnershipPercent = player.OwnershipPercent,
                    Form = player.Form,
                    MinutesPlayed = player.MinutesPlayed,
                    RecentPoints = player.RecentPoints,
                    Goals = player.Goals,
                    Assists = player.Assists,
                    CleanSheets = player.CleanSheets,
                    YellowCards = player.YellowCards,
                    RedCards = player.RedCards,
                    AvailabilityStatus = player.AvailabilityStatus,
                    ChanceOfPlayingNextRound = player.ChanceOfPlayingNextRound,
                    ExpectedReturnText = player.ExpectedReturnText,
                    LastVerifiedUtc = DateTime.UtcNow
                };
                _db.Players.Add(existing);
            }
            else
            {
                existing.TeamId = team.Id;
                existing.Name = player.Name;
                existing.FirstName = player.FirstName;
                existing.LastName = player.LastName;
                existing.Position = player.Position;
                existing.ShirtNumber = player.ShirtNumber;
                existing.Price = player.Price;
                existing.OwnershipPercent = player.OwnershipPercent;
                existing.Form = player.Form;
                existing.MinutesPlayed = player.MinutesPlayed;
                existing.RecentPoints = player.RecentPoints;
                existing.Goals = player.Goals;
                existing.Assists = player.Assists;
                existing.CleanSheets = player.CleanSheets;
                existing.YellowCards = player.YellowCards;
                existing.RedCards = player.RedCards;
                existing.AvailabilityStatus = player.AvailabilityStatus;
                existing.ChanceOfPlayingNextRound = player.ChanceOfPlayingNextRound;
                existing.ExpectedReturnText = player.ExpectedReturnText;
                existing.LastVerifiedUtc = DateTime.UtcNow;
            }

            affected++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return affected;
    }

    private async Task<int> UpsertFixturesAsync(IReadOnlyList<ProviderFixtureDto> fixtures, CancellationToken cancellationToken)
    {
        var affected = 0;
        foreach (var fixture in fixtures)
        {
            var homeTeam = await _db.Teams.FirstOrDefaultAsync(team => team.ExternalId == fixture.HomeTeamExternalId, cancellationToken);
            var awayTeam = await _db.Teams.FirstOrDefaultAsync(team => team.ExternalId == fixture.AwayTeamExternalId, cancellationToken);
            var season = await _db.Seasons.FirstOrDefaultAsync(season => season.IsCurrent, cancellationToken);
            var gameweek = await _db.Gameweeks.FirstOrDefaultAsync(gw => gw.Number == fixture.GameweekNumber && gw.SeasonId == season!.Id, cancellationToken);

            if (homeTeam is null || awayTeam is null || season is null || gameweek is null)
            {
                continue;
            }

            var existing = await _db.Fixtures.FirstOrDefaultAsync(f => f.ExternalId == fixture.ExternalId, cancellationToken);
            if (existing is null)
            {
                existing = new Fixture
                {
                    ExternalId = fixture.ExternalId,
                    SeasonId = season.Id,
                    GameweekId = gameweek.Id,
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id,
                    KickoffUtc = fixture.KickoffUtc,
                    HomeScore = fixture.HomeScore,
                    AwayScore = fixture.AwayScore,
                    Venue = fixture.Venue,
                    IsFinished = fixture.IsFinished,
                    IsBlanked = fixture.IsBlanked,
                    IsDoubleGameweek = fixture.IsDoubleGameweek,
                    Status = fixture.Status
                };
                _db.Fixtures.Add(existing);
            }
            else
            {
                existing.SeasonId = season.Id;
                existing.GameweekId = gameweek.Id;
                existing.HomeTeamId = homeTeam.Id;
                existing.AwayTeamId = awayTeam.Id;
                existing.KickoffUtc = fixture.KickoffUtc;
                existing.HomeScore = fixture.HomeScore;
                existing.AwayScore = fixture.AwayScore;
                existing.Venue = fixture.Venue;
                existing.IsFinished = fixture.IsFinished;
                existing.IsBlanked = fixture.IsBlanked;
                existing.IsDoubleGameweek = fixture.IsDoubleGameweek;
                existing.Status = fixture.Status;
            }

            affected++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return affected;
    }

    private async Task<int> UpsertNewsAsync(IReadOnlyList<ProviderNewsDto> newsItems, CancellationToken cancellationToken)
    {
        var affected = 0;
        foreach (var item in newsItems)
        {
            var enriched = _enrichmentService.Enrich(item.SourceName ?? "Unknown", item.SourceUrl, item.RawText, item.PublishedUtc);
            var news = new NewsItem
            {
                PlayerId = await ResolvePlayerIdAsync(item.PlayerExternalId, cancellationToken),
                TeamId = await ResolveTeamIdAsync(item.TeamExternalId, cancellationToken),
                PublishedUtc = item.PublishedUtc,
                Title = item.Title,
                Summary = item.Summary,
                SourceName = item.SourceName,
                SourceUrl = item.SourceUrl,
                RawNewsText = item.RawText,
                SentimentScore = 0.5m,
                InjuryFlag = enriched.InjuryFlag,
                SuspensionFlag = enriched.SuspensionFlag,
                AvailableFlag = enriched.Status == AvailabilityStatus.Available,
                Confidence = enriched.Confidence,
                ExtractedAvailabilityStatus = enriched.Status
            };

            _db.NewsItems.Add(news);
            affected++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return affected;
    }

    private async Task<int> UpsertAvailabilityAsync(IReadOnlyList<ProviderAvailabilityDto> availabilityItems, CancellationToken cancellationToken)
    {
        var affected = 0;
        foreach (var item in availabilityItems)
        {
            var playerId = await ResolvePlayerIdAsync(item.PlayerExternalId, cancellationToken);
            if (playerId is null)
            {
                continue;
            }

            var existing = await _db.PlayerAvailabilities.FirstOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);
            if (existing is null)
            {
                existing = new PlayerAvailability
                {
                    PlayerId = playerId.Value,
                    AvailabilityStatus = item.Status,
                    InjuryFlag = item.InjuryFlag,
                    SuspensionFlag = item.SuspensionFlag,
                    ChanceOfPlayingNextRound = item.ChanceOfPlayingNextRound,
                    ExpectedReturnText = item.ExpectedReturnText,
                    AvailabilityConfidence = item.Confidence,
                    SourceName = item.SourceName,
                    SourceUrl = item.SourceUrl,
                    RawNewsText = item.RawText,
                    LastVerifiedUtc = item.LastVerifiedUtc
                };
                _db.PlayerAvailabilities.Add(existing);
            }
            else
            {
                existing.AvailabilityStatus = item.Status;
                existing.InjuryFlag = item.InjuryFlag;
                existing.SuspensionFlag = item.SuspensionFlag;
                existing.ChanceOfPlayingNextRound = item.ChanceOfPlayingNextRound;
                existing.ExpectedReturnText = item.ExpectedReturnText;
                existing.AvailabilityConfidence = item.Confidence;
                existing.SourceName = item.SourceName;
                existing.SourceUrl = item.SourceUrl;
                existing.RawNewsText = item.RawText;
                existing.LastVerifiedUtc = item.LastVerifiedUtc;
            }

            affected++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return affected;
    }

    private async Task<int> UpsertPlayerSnapshotsAsync(IReadOnlyList<ProviderPlayerDto> players, CancellationToken cancellationToken)
    {
        var currentGameweek = await _db.Gameweeks
            .OrderByDescending(gameweek => gameweek.Number)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentGameweek is null)
        {
            return 0;
        }

        var affected = 0;
        foreach (var player in players)
        {
            var playerId = await ResolvePlayerIdAsync(player.ExternalId, cancellationToken);
            if (playerId is null)
            {
                continue;
            }

            var priceSnapshot = await _db.FantasyPlayerPrices.FirstOrDefaultAsync(x => x.PlayerId == playerId && x.GameweekId == currentGameweek.Id, cancellationToken);
            if (priceSnapshot is null)
            {
                _db.FantasyPlayerPrices.Add(new FantasyPlayerPrice
                {
                    PlayerId = playerId.Value,
                    GameweekId = currentGameweek.Id,
                    Price = player.Price,
                    SourceName = "Provider snapshot",
                    CapturedUtc = DateTime.UtcNow
                });
            }
            else
            {
                priceSnapshot.Price = player.Price;
                priceSnapshot.SourceName = "Provider snapshot";
                priceSnapshot.CapturedUtc = DateTime.UtcNow;
            }

            var ownershipSnapshot = await _db.FantasyPlayerOwnerships.FirstOrDefaultAsync(x => x.PlayerId == playerId && x.GameweekId == currentGameweek.Id, cancellationToken);
            if (ownershipSnapshot is null)
            {
                _db.FantasyPlayerOwnerships.Add(new FantasyPlayerOwnership
                {
                    PlayerId = playerId.Value,
                    GameweekId = currentGameweek.Id,
                    OwnershipPercent = player.OwnershipPercent,
                    SourceName = "Provider snapshot",
                    CapturedUtc = DateTime.UtcNow
                });
            }
            else
            {
                ownershipSnapshot.OwnershipPercent = player.OwnershipPercent;
                ownershipSnapshot.SourceName = "Provider snapshot";
                ownershipSnapshot.CapturedUtc = DateTime.UtcNow;
            }

            affected += 2;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return affected;
    }

    private async Task<int> UpsertHistoricalPlayerMatchStatsAsync(IFootballDataProvider provider, IReadOnlyList<ProviderPlayerDto> players, CancellationToken cancellationToken)
    {
        var affected = 0;

        foreach (var player in players)
        {
            var playerId = await ResolvePlayerIdAsync(player.ExternalId, cancellationToken);
            if (playerId is null)
            {
                continue;
            }

            var historyRows = await provider.GetPlayerMatchStatsAsync(player.ExternalId, cancellationToken);
            foreach (var history in historyRows)
            {
                var fixtureId = await _db.Fixtures.Where(fixture => fixture.ExternalId == history.FixtureExternalId).Select(fixture => (int?)fixture.Id).FirstOrDefaultAsync(cancellationToken);
                if (fixtureId is null)
                {
                    continue;
                }

                var existing = await _db.PlayerMatchStats.FirstOrDefaultAsync(x => x.PlayerId == playerId && x.FixtureId == fixtureId, cancellationToken);
                if (existing is null)
                {
                    existing = new PlayerMatchStat
                    {
                        PlayerId = playerId.Value,
                        FixtureId = fixtureId.Value
                    };
                    _db.PlayerMatchStats.Add(existing);
                }

                existing.MinutesPlayed = history.MinutesPlayed;
                existing.Goals = history.Goals;
                existing.Assists = history.Assists;
                existing.CleanSheets = history.CleanSheets;
                existing.Saves = history.Saves;
                existing.BonusPoints = history.BonusPoints;
                existing.GoalsConceded = history.GoalsConceded;
                existing.YellowCards = history.YellowCards;
                existing.RedCards = history.RedCards;
                existing.FantasyPoints = history.FantasyPoints;
                existing.IsHome = history.IsHome;
                existing.OpponentStrength = history.OpponentStrength;
                existing.RollingForm = history.RollingForm;
                existing.PriceAtKickoff = history.PriceAtKickoff;

                affected++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return affected;
    }

    private async Task<int> UpsertTeamMatchStatsAsync(IReadOnlyList<ProviderFixtureDto> fixtures, CancellationToken cancellationToken)
    {
        var affected = 0;
        foreach (var fixture in fixtures.Where(fixture => fixture.IsFinished && fixture.HomeScore.HasValue && fixture.AwayScore.HasValue))
        {
            var fixtureId = await _db.Fixtures.Where(item => item.ExternalId == fixture.ExternalId).Select(item => (int?)item.Id).FirstOrDefaultAsync(cancellationToken);
            if (fixtureId is null)
            {
                continue;
            }

            var homeTeam = await _db.Teams.FirstOrDefaultAsync(team => team.ExternalId == fixture.HomeTeamExternalId, cancellationToken);
            var awayTeam = await _db.Teams.FirstOrDefaultAsync(team => team.ExternalId == fixture.AwayTeamExternalId, cancellationToken);
            if (homeTeam is null || awayTeam is null)
            {
                continue;
            }

            await UpsertTeamMatchStatAsync(
                fixtureId.Value,
                homeTeam.Id,
                awayTeam.Id,
                fixture.HomeScore!.Value,
                fixture.AwayScore!.Value,
                true,
                homeTeam.StrengthRating,
                awayTeam.StrengthRating,
                cancellationToken);

            await UpsertTeamMatchStatAsync(
                fixtureId.Value,
                awayTeam.Id,
                homeTeam.Id,
                fixture.AwayScore!.Value,
                fixture.HomeScore!.Value,
                false,
                awayTeam.StrengthRating,
                homeTeam.StrengthRating,
                cancellationToken);

            affected += 2;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return affected;
    }

    private async Task UpsertTeamMatchStatAsync(
        int fixtureId,
        int teamId,
        int opponentTeamId,
        int goalsFor,
        int goalsAgainst,
        bool isHome,
        decimal homeStrength,
        decimal awayStrength,
        CancellationToken cancellationToken)
    {
        var existing = await _db.TeamMatchStats.FirstOrDefaultAsync(x => x.TeamId == teamId && x.FixtureId == fixtureId, cancellationToken);
        if (existing is null)
        {
            existing = new TeamMatchStat
            {
                TeamId = teamId,
                FixtureId = fixtureId,
                OpponentTeamId = opponentTeamId
            };
            _db.TeamMatchStats.Add(existing);
        }

        existing.GoalsFor = goalsFor;
        existing.GoalsAgainst = goalsAgainst;
        existing.ExpectedGoalsFor = Math.Max(0, goalsFor) + 0.15m;
        existing.ExpectedGoalsAgainst = Math.Max(0, goalsAgainst) + 0.15m;
        existing.ShotsFor = goalsFor * 3 + 4;
        existing.ShotsAgainst = goalsAgainst * 3 + 4;
        existing.PossessionPercent = Math.Clamp(50m + (isHome ? 4m : -4m) + (homeStrength - awayStrength) / 10m, 35m, 65m);
        existing.HomeStrength = homeStrength;
        existing.AwayStrength = awayStrength;
    }

    private async Task<int?> ResolvePlayerIdAsync(int? externalId, CancellationToken cancellationToken)
        => externalId is null
            ? null
            : await _db.Players.Where(player => player.ExternalId == externalId).Select(player => (int?)player.Id).FirstOrDefaultAsync(cancellationToken);

    private async Task<int?> ResolveTeamIdAsync(int? externalId, CancellationToken cancellationToken)
        => externalId is null
            ? null
            : await _db.Teams.Where(team => team.ExternalId == externalId).Select(team => (int?)team.Id).FirstOrDefaultAsync(cancellationToken);

    private static DataIngestionRunDto ToDto(DataIngestionRun run) => new(
        run.Id,
        run.SourceName,
        run.StartedUtc,
        run.CompletedUtc,
        run.Status,
        run.ItemsProcessed,
        run.ItemsUpserted,
        run.ItemsSkipped,
        run.Notes,
        run.ErrorMessage);
}
