using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using FantasyFootballForecast.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FantasyFootballForecast.Api;

public static class Endpoints
{
    public static RouteGroupBuilder MapFantasyFootballEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/teams", async (IApplicationDbContext db, CancellationToken cancellationToken) =>
        {
            var teams = await db.Teams
                .OrderBy(team => team.Name)
                .Select(team => new TeamDto(
                    team.Id,
                    team.Name,
                    team.ShortName,
                    team.Code,
                    team.StrengthRating,
                    team.ExpectedGoalsForPerMatch,
                    team.ExpectedGoalsAgainstPerMatch,
                    team.CrestUrl))
                .ToListAsync(cancellationToken);

            return Results.Ok(teams);
        });

        group.MapGet("/teams/{teamId:int}", async (IApplicationDbContext db, int teamId, CancellationToken cancellationToken) =>
        {
            var team = await db.Teams
                .AsNoTracking()
                .Where(item => item.Id == teamId)
                .Select(item => new TeamDto(
                    item.Id,
                    item.Name,
                    item.ShortName,
                    item.Code,
                    item.StrengthRating,
                    item.ExpectedGoalsForPerMatch,
                    item.ExpectedGoalsAgainstPerMatch,
                    item.CrestUrl))
                .FirstOrDefaultAsync(cancellationToken);

            if (team is null)
            {
                return Results.NotFound();
            }

            var players = await db.Players
                .AsNoTracking()
                .Where(player => player.TeamId == teamId)
                .OrderByDescending(player => player.RecentPoints)
                .Take(15)
                .Select(player => new PlayerDto(
                    player.Id,
                    player.TeamId,
                    team.Name,
                    player.Name,
                    player.Position,
                    player.ShirtNumber,
                    player.Price,
                    player.OwnershipPercent,
                    player.Form,
                    player.MinutesPlayed,
                    player.RecentPoints,
                    player.Goals,
                    player.Assists,
                    player.CleanSheets,
                    player.AvailabilityStatus,
                    player.ChanceOfPlayingNextRound,
                    player.ExpectedReturnText,
                    player.LastVerifiedUtc))
                .ToListAsync(cancellationToken);

            var upcomingFixtureRows = await db.Fixtures
                .AsNoTracking()
                .Where(fixture => !fixture.IsFinished && (fixture.HomeTeamId == teamId || fixture.AwayTeamId == teamId))
                .Join(db.Teams.AsNoTracking(), fixture => fixture.HomeTeamId, homeTeam => homeTeam.Id, (fixture, homeTeam) => new { fixture, homeTeam })
                .Join(db.Teams.AsNoTracking(), joined => joined.fixture.AwayTeamId, awayTeam => awayTeam.Id, (joined, awayTeam) => new
                {
                    joined.fixture.Id,
                    joined.fixture.HomeTeamId,
                    HomeTeamName = joined.homeTeam.Name,
                    joined.fixture.AwayTeamId,
                    AwayTeamName = awayTeam.Name,
                    joined.fixture.KickoffUtc,
                    joined.fixture.HomeScore,
                    joined.fixture.AwayScore,
                    joined.fixture.Venue,
                    joined.fixture.IsFinished,
                    joined.fixture.IsDoubleGameweek,
                    joined.fixture.Status
                })
                .OrderBy(fixture => fixture.KickoffUtc)
                .Take(5)
                .ToListAsync(cancellationToken);

            var upcomingFixtures = upcomingFixtureRows
                .Select(item => new FixtureDto(
                    item.Id,
                    item.HomeTeamId,
                    item.HomeTeamName,
                    item.AwayTeamId,
                    item.AwayTeamName,
                    item.KickoffUtc,
                    item.HomeScore,
                    item.AwayScore,
                    item.Venue,
                    item.IsFinished,
                    item.IsDoubleGameweek,
                    item.Status))
                .ToList();

            var recentMatchStatRows = await db.TeamMatchStats
                .AsNoTracking()
                .Where(stat => stat.TeamId == teamId)
                .Join(db.Fixtures.AsNoTracking(), stat => stat.FixtureId, fixture => fixture.Id, (stat, fixture) => new { stat, fixture })
                .Join(db.Teams.AsNoTracking(), joined => joined.stat.OpponentTeamId, opponent => opponent.Id, (joined, opponent) => new
                {
                    joined.stat.FixtureId,
                    joined.fixture.KickoffUtc,
                    OpponentTeamName = opponent.Name,
                    IsHome = joined.stat.TeamId == joined.fixture.HomeTeamId,
                    joined.stat.GoalsFor,
                    joined.stat.GoalsAgainst,
                    joined.stat.ExpectedGoalsFor,
                    joined.stat.ExpectedGoalsAgainst,
                    joined.stat.ShotsFor,
                    joined.stat.ShotsAgainst,
                    joined.stat.PossessionPercent,
                    joined.stat.HomeStrength,
                    joined.stat.AwayStrength
                })
                .OrderByDescending(item => item.KickoffUtc)
                .Take(5)
                .ToListAsync(cancellationToken);

            var recentMatchStats = recentMatchStatRows
                .Select(item => new TeamMatchStatDto(
                    item.FixtureId,
                    item.KickoffUtc,
                    item.OpponentTeamName,
                    item.IsHome,
                    item.GoalsFor,
                    item.GoalsAgainst,
                    item.ExpectedGoalsFor,
                    item.ExpectedGoalsAgainst,
                    item.ShotsFor,
                    item.ShotsAgainst,
                    item.PossessionPercent,
                    item.HomeStrength,
                    item.AwayStrength))
                .ToList();

            return Results.Ok(new TeamDetailDto(team, players, upcomingFixtures, recentMatchStats));
        });

        group.MapGet("/players", async (
            IApplicationDbContext db,
            int? teamId,
            string? search,
            CancellationToken cancellationToken) =>
        {
            var query = db.Players.Join(db.Teams, player => player.TeamId, team => team.Id, (player, team) => new { player, team });

            if (teamId is not null)
            {
                query = query.Where(item => item.player.TeamId == teamId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item => item.player.Name.Contains(search) || item.team.Name.Contains(search));
            }

            var players = await query
                .OrderByDescending(item => item.player.RecentPoints)
                .Select(item => new PlayerDto(
                    item.player.Id,
                    item.player.TeamId,
                    item.team.Name,
                    item.player.Name,
                    item.player.Position,
                    item.player.ShirtNumber,
                    item.player.Price,
                    item.player.OwnershipPercent,
                    item.player.Form,
                    item.player.MinutesPlayed,
                    item.player.RecentPoints,
                    item.player.Goals,
                    item.player.Assists,
                    item.player.CleanSheets,
                    item.player.AvailabilityStatus,
                    item.player.ChanceOfPlayingNextRound,
                    item.player.ExpectedReturnText,
                    item.player.LastVerifiedUtc))
                .ToListAsync(cancellationToken);

            return Results.Ok(players);
        });

        group.MapGet("/players/{playerId:int}", async (IApplicationDbContext db, int playerId, CancellationToken cancellationToken) =>
        {
            var playerEntity = await db.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == playerId, cancellationToken);

            if (playerEntity is null)
            {
                return Results.NotFound();
            }

            var team = await db.Teams
                .AsNoTracking()
                .Where(item => item.Id == playerEntity.TeamId)
                .Select(item => new TeamDto(
                    item.Id,
                    item.Name,
                    item.ShortName,
                    item.Code,
                    item.StrengthRating,
                    item.ExpectedGoalsForPerMatch,
                    item.ExpectedGoalsAgainstPerMatch,
                    item.CrestUrl))
                .FirstOrDefaultAsync(cancellationToken);

            if (team is null)
            {
                return Results.NotFound();
            }

            var player = new PlayerDto(
                playerEntity.Id,
                playerEntity.TeamId,
                team.Name,
                playerEntity.Name,
                playerEntity.Position,
                playerEntity.ShirtNumber,
                playerEntity.Price,
                playerEntity.OwnershipPercent,
                playerEntity.Form,
                playerEntity.MinutesPlayed,
                playerEntity.RecentPoints,
                playerEntity.Goals,
                playerEntity.Assists,
                playerEntity.CleanSheets,
                playerEntity.AvailabilityStatus,
                playerEntity.ChanceOfPlayingNextRound,
                playerEntity.ExpectedReturnText,
                playerEntity.LastVerifiedUtc);
            var playerTeamId = player.TeamId;

            var availability = await db.PlayerAvailabilities
                .AsNoTracking()
                .Where(item => item.PlayerId == playerId)
                .OrderByDescending(item => item.LastVerifiedUtc)
                .Select(item => new AvailabilityDto(
                    playerId,
                    player.Name,
                    team.Name,
                    item.AvailabilityStatus,
                    item.InjuryFlag,
                    item.SuspensionFlag,
                    item.ChanceOfPlayingNextRound,
                    item.ExpectedReturnText,
                    item.AvailabilityConfidence,
                    item.SourceName,
                    item.SourceUrl,
                    item.LastVerifiedUtc))
                .FirstOrDefaultAsync(cancellationToken)
                ?? new AvailabilityDto(
                    player.Id,
                    player.Name,
                    team.Name,
                    player.AvailabilityStatus,
                    player.AvailabilityStatus is AvailabilityStatus.Injured or AvailabilityStatus.RuledOut or AvailabilityStatus.LateFitnessTest,
                    player.AvailabilityStatus == AvailabilityStatus.Suspended,
                    player.ChanceOfPlayingNextRound,
                    player.ExpectedReturnText,
                    player.ChanceOfPlayingNextRound,
                    "Player record",
                    null,
                    player.LastVerifiedUtc ?? DateTime.UtcNow);

            var recentMatchStatRows = await db.PlayerMatchStats
                .AsNoTracking()
                .Where(stat => stat.PlayerId == playerId)
                .Join(db.Fixtures.AsNoTracking(), stat => stat.FixtureId, fixture => fixture.Id, (stat, fixture) => new { stat, fixture })
                .Join(db.Teams.AsNoTracking(), joined => joined.fixture.HomeTeamId, homeTeam => homeTeam.Id, (joined, homeTeam) => new { joined.stat, joined.fixture, homeTeam })
                .Join(db.Teams.AsNoTracking(), joined => joined.fixture.AwayTeamId, awayTeam => awayTeam.Id, (joined, awayTeam) => new
                {
                    joined.stat.FixtureId,
                    joined.fixture.KickoffUtc,
                    OpponentTeamName = joined.fixture.HomeTeamId == playerTeamId ? awayTeam.Name : joined.homeTeam.Name,
                    IsHome = joined.fixture.HomeTeamId == playerTeamId,
                    joined.stat.MinutesPlayed,
                    joined.stat.Goals,
                    joined.stat.Assists,
                    joined.stat.CleanSheets,
                    joined.stat.Saves,
                    joined.stat.BonusPoints,
                    joined.stat.GoalsConceded,
                    joined.stat.YellowCards,
                    joined.stat.RedCards,
                    joined.stat.FantasyPoints,
                    joined.stat.OpponentStrength,
                    joined.stat.RollingForm,
                    joined.stat.PriceAtKickoff
                })
                .OrderByDescending(item => item.KickoffUtc)
                .Take(5)
                .ToListAsync(cancellationToken);

            var recentMatchStats = recentMatchStatRows
                .Select(item => new PlayerMatchStatDto(
                    item.FixtureId,
                    item.KickoffUtc,
                    item.OpponentTeamName,
                    item.IsHome,
                    item.MinutesPlayed,
                    item.Goals,
                    item.Assists,
                    item.CleanSheets,
                    item.Saves,
                    item.BonusPoints,
                    item.GoalsConceded,
                    item.YellowCards,
                    item.RedCards,
                    item.FantasyPoints,
                    item.OpponentStrength,
                    item.RollingForm,
                    item.PriceAtKickoff))
                .ToList();

            var recentNews = await db.NewsItems
                .AsNoTracking()
                .Where(item => item.PlayerId == playerId)
                .OrderByDescending(item => item.PublishedUtc)
                .Take(5)
                .Select(item => new NewsItemDto(
                    item.Id,
                    item.PublishedUtc,
                    item.Title,
                    item.Summary,
                    item.SourceName,
                    item.SourceUrl,
                    item.SentimentScore,
                    item.InjuryFlag,
                    item.SuspensionFlag,
                    item.AvailableFlag,
                    item.Confidence,
                    item.ExtractedAvailabilityStatus))
                .ToListAsync(cancellationToken);

            var recentPredictions = await db.Predictions
                .AsNoTracking()
                .Where(item => item.PlayerId == playerId)
                .OrderByDescending(item => item.CreatedUtc)
                .Take(3)
                .Select(item => new PredictionDto(
                    item.Id,
                    item.PredictionKind,
                    item.FixtureId,
                    item.PlayerId,
                    item.TeamId,
                    item.GameweekId,
                    item.CreatedUtc,
                    item.ModelVersion,
                    item.Score,
                    item.Probability,
                    item.PredictedValue,
                    item.LowerBound,
                    item.UpperBound,
                    item.Explanation,
                    item.EvaluationMetric))
                .ToListAsync(cancellationToken);

            return Results.Ok(new PlayerDetailDto(player, team, availability, recentMatchStats, recentNews, recentPredictions));
        });

        group.MapGet("/fixtures", async (IApplicationDbContext db, bool? upcomingOnly, CancellationToken cancellationToken) =>
        {
            var query = db.Fixtures.Join(db.Teams, fixture => fixture.HomeTeamId, team => team.Id, (fixture, homeTeam) => new { fixture, homeTeam })
                .Join(db.Teams, joined => joined.fixture.AwayTeamId, team => team.Id, (joined, awayTeam) => new { joined.fixture, joined.homeTeam, awayTeam });

            if (upcomingOnly == true)
            {
                query = query.Where(item => !item.fixture.IsFinished);
            }

            var fixtures = await query
                .OrderBy(item => item.fixture.KickoffUtc)
                .Select(item => new FixtureDto(
                    item.fixture.Id,
                    item.fixture.HomeTeamId,
                    item.homeTeam.Name,
                    item.fixture.AwayTeamId,
                    item.awayTeam.Name,
                    item.fixture.KickoffUtc,
                    item.fixture.HomeScore,
                    item.fixture.AwayScore,
                    item.fixture.Venue,
                    item.fixture.IsFinished,
                    item.fixture.IsDoubleGameweek,
                    item.fixture.Status))
                .ToListAsync(cancellationToken);

            return Results.Ok(fixtures);
        });

        group.MapGet("/predictions", async (IApplicationDbContext db, PredictionKind? kind, CancellationToken cancellationToken) =>
        {
            var query = db.Predictions.AsQueryable();
            if (kind is not null)
            {
                query = query.Where(prediction => prediction.PredictionKind == kind);
            }

            var predictions = await query
                .OrderByDescending(prediction => prediction.CreatedUtc)
                .Select(prediction => new PredictionDto(
                    prediction.Id,
                    prediction.PredictionKind,
                    prediction.FixtureId,
                    prediction.PlayerId,
                    prediction.TeamId,
                    prediction.GameweekId,
                    prediction.CreatedUtc,
                    prediction.ModelVersion,
                    prediction.Score,
                    prediction.Probability,
                    prediction.PredictedValue,
                    prediction.LowerBound,
                    prediction.UpperBound,
                    prediction.Explanation,
                    prediction.EvaluationMetric))
                .ToListAsync(cancellationToken);

            return Results.Ok(predictions);
        });

        group.MapGet("/availability", async (IApplicationDbContext db, CancellationToken cancellationToken) =>
        {
            var availability = await db.PlayerAvailabilities
                .Join(db.Players, item => item.PlayerId, player => player.Id, (item, player) => new { item, player })
                .Join(db.Teams, joined => joined.player.TeamId, team => team.Id, (joined, team) => new AvailabilityDto(
                    joined.player.Id,
                    joined.player.Name,
                    team.Name,
                    joined.item.AvailabilityStatus,
                    joined.item.InjuryFlag,
                    joined.item.SuspensionFlag,
                    joined.item.ChanceOfPlayingNextRound,
                    joined.item.ExpectedReturnText,
                    joined.item.AvailabilityConfidence,
                    joined.item.SourceName,
                    joined.item.SourceUrl,
                    joined.item.LastVerifiedUtc))
                .OrderByDescending(item => item.AvailabilityConfidence)
                .ToListAsync(cancellationToken);

            return Results.Ok(availability);
        });

        group.MapGet("/news", async (IApplicationDbContext db, CancellationToken cancellationToken) =>
        {
            var news = await db.NewsItems
                .OrderByDescending(item => item.PublishedUtc)
                .Select(item => new NewsItemDto(
                    item.Id,
                    item.PublishedUtc,
                    item.Title,
                    item.Summary,
                    item.SourceName,
                    item.SourceUrl,
                    item.SentimentScore,
                    item.InjuryFlag,
                    item.SuspensionFlag,
                    item.AvailableFlag,
                    item.Confidence,
                    item.ExtractedAvailabilityStatus))
                .ToListAsync(cancellationToken);

            return Results.Ok(news);
        });

        group.MapGet("/model-runs", async (IApplicationDbContext db, CancellationToken cancellationToken) =>
        {
            var runs = await db.ModelTrainingRuns
                .OrderByDescending(item => item.StartedUtc)
                .Select(item => new ModelTrainingRunDto(
                    item.Id,
                    item.ModelName,
                    item.StartedUtc,
                    item.CompletedUtc,
                    item.Status,
                    item.TrainingSampleCount,
                    item.EvaluationMetricName,
                    item.EvaluationMetricValue,
                    item.ModelPath,
                    item.Notes))
                .ToListAsync(cancellationToken);

            return Results.Ok(runs);
        });

        group.MapGet("/ingestion-runs", async (IApplicationDbContext db, CancellationToken cancellationToken) =>
        {
            var runs = await db.DataIngestionRuns
                .OrderByDescending(item => item.StartedUtc)
                .Select(item => new DataIngestionRunDto(
                    item.Id,
                    item.SourceName,
                    item.StartedUtc,
                    item.CompletedUtc,
                    item.Status,
                    item.ItemsProcessed,
                    item.ItemsUpserted,
                    item.ItemsSkipped,
                    item.Notes,
                    item.ErrorMessage))
                .ToListAsync(cancellationToken);

            return Results.Ok(runs);
        });

        group.MapGet("/top-picks", async (IFantasyRecommendationService recommendations, int count, CancellationToken cancellationToken) =>
        {
            var picks = await recommendations.GetTopPicksAsync(count == 0 ? 10 : count, cancellationToken);
            return Results.Ok(picks);
        });

        group.MapGet("/best-xi", async (IFantasyRecommendationService recommendations, CancellationToken cancellationToken) =>
        {
            var picks = await recommendations.GetBestXIAsync(cancellationToken);
            return Results.Ok(picks);
        });

        group.MapGet("/dashboard-summary", async (IApplicationDbContext db, CancellationToken cancellationToken) =>
        {
            var latestModelRun = await db.ModelTrainingRuns
                .AsNoTracking()
                .OrderByDescending(item => item.StartedUtc)
                .Select(item => new ModelTrainingRunDto(
                    item.Id,
                    item.ModelName,
                    item.StartedUtc,
                    item.CompletedUtc,
                    item.Status,
                    item.TrainingSampleCount,
                    item.EvaluationMetricName,
                    item.EvaluationMetricValue,
                    item.ModelPath,
                    item.Notes))
                .FirstOrDefaultAsync(cancellationToken);

            var latestIngestionRun = await db.DataIngestionRuns
                .AsNoTracking()
                .OrderByDescending(item => item.StartedUtc)
                .Select(item => new DataIngestionRunDto(
                    item.Id,
                    item.SourceName,
                    item.StartedUtc,
                    item.CompletedUtc,
                    item.Status,
                    item.ItemsProcessed,
                    item.ItemsUpserted,
                    item.ItemsSkipped,
                    item.Notes,
                    item.ErrorMessage))
                .FirstOrDefaultAsync(cancellationToken);

            var summary = new DashboardSummaryDto(
                await db.Teams.AsNoTracking().CountAsync(cancellationToken),
                await db.Players.AsNoTracking().CountAsync(cancellationToken),
                await db.Fixtures.AsNoTracking().CountAsync(cancellationToken),
                await db.PlayerAvailabilities.AsNoTracking().CountAsync(item => item.AvailabilityStatus == AvailabilityStatus.Available, cancellationToken),
                await db.PlayerAvailabilities.AsNoTracking().CountAsync(item => item.AvailabilityStatus != AvailabilityStatus.Available, cancellationToken),
                latestModelRun,
                latestIngestionRun);

            return Results.Ok(summary);
        });

        group.MapPost("/sync/import", async (IFootballSyncService syncService, string? provider, CancellationToken cancellationToken) =>
        {
            var run = string.IsNullOrWhiteSpace(provider)
                ? await syncService.SyncAllAsync(cancellationToken)
                : await syncService.SyncFromProviderAsync(provider, cancellationToken);

            return Results.Ok(run);
        });

        group.MapPost("/sync/historical", async (IFootballSyncService syncService, string? provider, CancellationToken cancellationToken) =>
        {
            var run = await syncService.SyncHistoricalAsync(string.IsNullOrWhiteSpace(provider) ? "FPL Public API" : provider, cancellationToken);
            return Results.Ok(run);
        });

        group.MapPost("/models/train", async (IModelTrainingService trainingService, string? model, CancellationToken cancellationToken) =>
        {
            var summary = model?.ToLowerInvariant() switch
            {
                "team" => await trainingService.TrainTeamMatchModelAsync(cancellationToken),
                "all" => await trainingService.RetrainAllAsync(cancellationToken),
                _ => await trainingService.TrainPlayerFantasyModelAsync(cancellationToken)
            };

            return Results.Ok(summary);
        });

        group.MapPost("/models/retrain", async (IModelTrainingService trainingService, CancellationToken cancellationToken) =>
        {
            var summary = await trainingService.RetrainAllAsync(cancellationToken);
            return Results.Ok(summary);
        });

        group.MapGet("/fixture-difficulty", async (IApplicationDbContext db, int? teamId, CancellationToken cancellationToken) =>
        {
            var query = db.Fixtures
                .AsNoTracking()
                .Where(fixture => !fixture.IsFinished)
                .Join(db.Teams.AsNoTracking(), fixture => fixture.HomeTeamId, home => home.Id, (fixture, home) => new { fixture, home })
                .Join(db.Teams.AsNoTracking(), joined => joined.fixture.AwayTeamId, away => away.Id, (joined, away) => new
                {
                    joined.fixture,
                    joined.home,
                    away
                });

            if (teamId is not null)
            {
                query = query.Where(item => item.fixture.HomeTeamId == teamId || item.fixture.AwayTeamId == teamId);
            }

            var rows = await query
                .OrderBy(item => item.fixture.KickoffUtc)
                .ToListAsync(cancellationToken);

            var result = new List<FixtureDifficultyDto>(rows.Count * 2);
            foreach (var row in rows)
            {
                result.Add(new FixtureDifficultyDto(
                    row.fixture.Id,
                    row.home.Id,
                    row.home.Name,
                    row.away.Id,
                    row.away.Name,
                    row.fixture.KickoffUtc,
                    IsHome: true,
                    CalculateDifficulty(row.away.StrengthRating, isHome: true),
                    row.away.StrengthRating));

                result.Add(new FixtureDifficultyDto(
                    row.fixture.Id,
                    row.away.Id,
                    row.away.Name,
                    row.home.Id,
                    row.home.Name,
                    row.fixture.KickoffUtc,
                    IsHome: false,
                    CalculateDifficulty(row.home.StrengthRating, isHome: false),
                    row.home.StrengthRating));
            }

            if (teamId is not null)
            {
                result = result.Where(item => item.TeamId == teamId).ToList();
            }

            return Results.Ok(result.OrderBy(item => item.KickoffUtc).ThenBy(item => item.TeamName).ToList());
        });

        return group;
    }

    private static int CalculateDifficulty(decimal opponentStrength, bool isHome)
    {
        var baseDifficulty = opponentStrength switch
        {
            < 60 => 1,
            < 70 => 2,
            < 80 => 3,
            < 90 => 4,
            _ => 5
        };
        var modifier = isHome ? -1 : 1;
        return Math.Clamp(baseDifficulty + modifier, 1, 5);
    }
}
