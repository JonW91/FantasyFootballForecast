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

        group.MapPost("/sync/import", async (IFootballSyncService syncService, string? provider, CancellationToken cancellationToken) =>
        {
            var run = string.IsNullOrWhiteSpace(provider)
                ? await syncService.SyncAllAsync(cancellationToken)
                : await syncService.SyncFromProviderAsync(provider, cancellationToken);

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

        return group;
    }
}
