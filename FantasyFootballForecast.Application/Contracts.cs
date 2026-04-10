using FantasyFootballForecast.Domain;

namespace FantasyFootballForecast.Application;

public interface IApplicationDbContext
{
    IQueryable<Season> Seasons { get; }
    IQueryable<Gameweek> Gameweeks { get; }
    IQueryable<Team> Teams { get; }
    IQueryable<Player> Players { get; }
    IQueryable<Fixture> Fixtures { get; }
    IQueryable<PlayerMatchStat> PlayerMatchStats { get; }
    IQueryable<TeamMatchStat> TeamMatchStats { get; }
    IQueryable<FantasyPlayerPrice> FantasyPlayerPrices { get; }
    IQueryable<FantasyPlayerOwnership> FantasyPlayerOwnerships { get; }
    IQueryable<PlayerAvailability> PlayerAvailabilities { get; }
    IQueryable<InjuryReport> InjuryReports { get; }
    IQueryable<Suspension> Suspensions { get; }
    IQueryable<NewsItem> NewsItems { get; }
    IQueryable<Prediction> Predictions { get; }
    IQueryable<ModelTrainingRun> ModelTrainingRuns { get; }
    IQueryable<DataIngestionRun> DataIngestionRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IFootballDataProvider
{
    string Name { get; }

    Task<IReadOnlyList<ProviderTeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderPlayerDto>> GetPlayersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderNewsDto>> GetNewsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderAvailabilityDto>> GetAvailabilityAsync(CancellationToken cancellationToken = default);
}

public interface IAvailabilityEnrichmentService
{
    AvailabilityEnrichmentResult Enrich(string sourceName, string? sourceUrl, string rawText, DateTimeOffset? publishedUtc = null);
}

public interface IPlayerFantasyPointPredictor
{
    Task<PlayerFantasyPredictionResult> PredictAsync(PlayerFantasyPredictionInput input, CancellationToken cancellationToken = default);
}

public interface ITeamMatchPredictor
{
    Task<TeamMatchPredictionResult> PredictAsync(TeamMatchPredictionInput input, CancellationToken cancellationToken = default);
}

public interface IModelTrainingService
{
    Task<ModelTrainingSummaryDto> TrainPlayerFantasyModelAsync(CancellationToken cancellationToken = default);
    Task<ModelTrainingSummaryDto> TrainTeamMatchModelAsync(CancellationToken cancellationToken = default);
    Task<ModelTrainingSummaryDto> RetrainAllAsync(CancellationToken cancellationToken = default);
}

public interface IFootballSyncService
{
    Task<DataIngestionRunDto> SyncFromProviderAsync(string providerName, CancellationToken cancellationToken = default);
    Task<DataIngestionRunDto> SyncAllAsync(CancellationToken cancellationToken = default);
}

public interface IFantasyRecommendationService
{
    Task<IReadOnlyList<FantasyPickDto>> GetTopPicksAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FantasyPickDto>> GetBestXIAsync(CancellationToken cancellationToken = default);
}

public sealed record ProviderTeamDto(
    int ExternalId,
    string Name,
    string ShortName,
    string Code,
    string? CrestUrl,
    decimal StrengthRating,
    decimal ExpectedGoalsForPerMatch,
    decimal ExpectedGoalsAgainstPerMatch);

public sealed record ProviderPlayerDto(
    int ExternalId,
    int ExternalTeamId,
    string Name,
    string? FirstName,
    string? LastName,
    string Position,
    int ShirtNumber,
    decimal Price,
    decimal OwnershipPercent,
    decimal Form,
    decimal MinutesPlayed,
    decimal RecentPoints,
    decimal Goals,
    decimal Assists,
    decimal CleanSheets,
    decimal YellowCards,
    decimal RedCards,
    AvailabilityStatus AvailabilityStatus,
    decimal ChanceOfPlayingNextRound,
    string? ExpectedReturnText);

public sealed record ProviderFixtureDto(
    int ExternalId,
    int SeasonExternalId,
    int GameweekNumber,
    int HomeTeamExternalId,
    int AwayTeamExternalId,
    DateTime KickoffUtc,
    int? HomeScore,
    int? AwayScore,
    string? Venue,
    bool IsFinished,
    bool IsBlanked,
    bool IsDoubleGameweek,
    string? Status);

public sealed record ProviderNewsDto(
    int? PlayerExternalId,
    int? TeamExternalId,
    DateTime PublishedUtc,
    string Title,
    string? Summary,
    string? SourceName,
    string? SourceUrl,
    string RawText);

public sealed record ProviderAvailabilityDto(
    int? PlayerExternalId,
    AvailabilityStatus Status,
    bool InjuryFlag,
    bool SuspensionFlag,
    decimal ChanceOfPlayingNextRound,
    string? ExpectedReturnText,
    decimal Confidence,
    string? SourceName,
    string? SourceUrl,
    string RawText,
    DateTime LastVerifiedUtc);

public sealed record AvailabilityEnrichmentResult(
    AvailabilityStatus Status,
    bool InjuryFlag,
    bool SuspensionFlag,
    decimal ChanceOfPlayingNextRound,
    string? ExpectedReturnText,
    decimal Confidence,
    string? DetectedKeywords,
    string RawText);

public sealed record PlayerFantasyPredictionInput(
    int PlayerId,
    int TeamId,
    int OpponentTeamId,
    decimal MinutesPlayed,
    decimal RecentPoints,
    decimal Goals,
    decimal Assists,
    decimal CleanSheets,
    decimal YellowCards,
    decimal RedCards,
    decimal OpponentStrength,
    bool IsHome,
    decimal RecentFormAverage,
    decimal Price,
    decimal OwnershipPercent,
    decimal ChanceOfPlayingNextRound,
    AvailabilityStatus AvailabilityStatus);

public sealed record PlayerFantasyPredictionResult(
    decimal PredictedFantasyPoints,
    float Score,
    float Probability,
    string ModelVersion,
    string Explanation);

public sealed record TeamMatchPredictionInput(
    int HomeTeamId,
    int AwayTeamId,
    decimal HomeStrength,
    decimal AwayStrength,
    decimal HomeExpectedGoalsFor,
    decimal AwayExpectedGoalsFor,
    decimal HomeExpectedGoalsAgainst,
    decimal AwayExpectedGoalsAgainst,
    bool IsHomeFixture,
    decimal HomeRecentForm,
    decimal AwayRecentForm);

public sealed record TeamMatchPredictionResult(
    decimal HomeWinProbability,
    decimal DrawProbability,
    decimal AwayWinProbability,
    decimal ExpectedHomeGoals,
    decimal ExpectedAwayGoals,
    string ModelVersion,
    string Explanation);

public sealed record FantasyPickDto(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string Position,
    decimal PredictedPoints,
    decimal Price,
    decimal Value,
    decimal AvailabilityChance,
    bool IsInjuryConcern,
    bool IsSuspended,
    string RecommendationReason);

public sealed record TeamDto(
    int Id,
    string Name,
    string ShortName,
    string Code,
    decimal StrengthRating,
    decimal ExpectedGoalsForPerMatch,
    decimal ExpectedGoalsAgainstPerMatch,
    string? CrestUrl);

public sealed record PlayerDto(
    int Id,
    int TeamId,
    string TeamName,
    string Name,
    string Position,
    int ShirtNumber,
    decimal Price,
    decimal OwnershipPercent,
    decimal Form,
    decimal MinutesPlayed,
    decimal RecentPoints,
    decimal Goals,
    decimal Assists,
    decimal CleanSheets,
    AvailabilityStatus AvailabilityStatus,
    decimal ChanceOfPlayingNextRound,
    string? ExpectedReturnText,
    DateTime? LastVerifiedUtc);

public sealed record FixtureDto(
    int Id,
    int HomeTeamId,
    string HomeTeamName,
    int AwayTeamId,
    string AwayTeamName,
    DateTime KickoffUtc,
    int? HomeScore,
    int? AwayScore,
    string? Venue,
    bool IsFinished,
    bool IsDoubleGameweek,
    string? Status);

public sealed record TeamMatchStatDto(
    int FixtureId,
    DateTime KickoffUtc,
    string OpponentTeamName,
    bool IsHome,
    int GoalsFor,
    int GoalsAgainst,
    decimal ExpectedGoalsFor,
    decimal ExpectedGoalsAgainst,
    decimal ShotsFor,
    decimal ShotsAgainst,
    decimal PossessionPercent,
    decimal HomeStrength,
    decimal AwayStrength);

public sealed record PlayerMatchStatDto(
    int FixtureId,
    DateTime KickoffUtc,
    string OpponentTeamName,
    bool IsHome,
    int MinutesPlayed,
    int Goals,
    int Assists,
    int CleanSheets,
    int Saves,
    int BonusPoints,
    int GoalsConceded,
    int YellowCards,
    int RedCards,
    decimal FantasyPoints,
    decimal OpponentStrength,
    decimal RollingForm,
    decimal PriceAtKickoff);

public sealed record AvailabilityDto(
    int PlayerId,
    string PlayerName,
    string TeamName,
    AvailabilityStatus AvailabilityStatus,
    bool InjuryFlag,
    bool SuspensionFlag,
    decimal ChanceOfPlayingNextRound,
    string? ExpectedReturnText,
    decimal AvailabilityConfidence,
    string? SourceName,
    string? SourceUrl,
    DateTime LastVerifiedUtc);

public sealed record NewsItemDto(
    int Id,
    DateTime PublishedUtc,
    string Title,
    string? Summary,
    string? SourceName,
    string? SourceUrl,
    decimal SentimentScore,
    bool InjuryFlag,
    bool SuspensionFlag,
    bool AvailableFlag,
    decimal Confidence,
    AvailabilityStatus ExtractedAvailabilityStatus);

public sealed record TeamDetailDto(
    TeamDto Team,
    IReadOnlyList<PlayerDto> Players,
    IReadOnlyList<FixtureDto> UpcomingFixtures,
    IReadOnlyList<TeamMatchStatDto> RecentMatchStats);

public sealed record PlayerDetailDto(
    PlayerDto Player,
    TeamDto Team,
    AvailabilityDto CurrentAvailability,
    IReadOnlyList<PlayerMatchStatDto> RecentMatchStats,
    IReadOnlyList<NewsItemDto> RecentNews,
    IReadOnlyList<PredictionDto> RecentPredictions);

public sealed record PredictionDto(
    int Id,
    PredictionKind PredictionKind,
    int? FixtureId,
    int? PlayerId,
    int? TeamId,
    int? GameweekId,
    DateTime CreatedUtc,
    string? ModelVersion,
    decimal Score,
    decimal Probability,
    decimal PredictedValue,
    decimal? LowerBound,
    decimal? UpperBound,
    string? Explanation,
    decimal? EvaluationMetric);

public sealed record ModelTrainingRunDto(
    int Id,
    string ModelName,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    string Status,
    int TrainingSampleCount,
    string? EvaluationMetricName,
    decimal EvaluationMetricValue,
    string? ModelPath,
    string? Notes);

public sealed record DataIngestionRunDto(
    int Id,
    string SourceName,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    string Status,
    int ItemsProcessed,
    int ItemsUpserted,
    int ItemsSkipped,
    string? Notes,
    string? ErrorMessage);

public sealed record ModelTrainingSummaryDto(
    string ModelName,
    string Status,
    int TrainingSampleCount,
    string? MetricName,
    decimal MetricValue,
    string? ModelPath,
    string? Notes);
