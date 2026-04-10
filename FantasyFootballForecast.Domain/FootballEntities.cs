namespace FantasyFootballForecast.Domain;

public abstract class EntityBase
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
}

public enum AvailabilityStatus
{
    Unknown = 0,
    Available = 1,
    Doubtful = 2,
    Injured = 3,
    Suspended = 4,
    RuledOut = 5,
    LateFitnessTest = 6,
    Rested = 7
}

public enum PredictionKind
{
    MatchOutcome = 0,
    ExpectedGoals = 1,
    PlayerFantasyPoints = 2,
    PlayerFormTrend = 3,
    FixtureDifficulty = 4,
    LeagueTableProjection = 5,
    BestPickRecommendation = 6
}

public enum AvailabilitySourceType
{
    StructuredFeed = 0,
    OfficialClub = 1,
    OfficialLeague = 2,
    NewsArticle = 3,
    ProviderFeed = 4
}

public sealed class Season : EntityBase
{
    public required string Name { get; set; }
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class Gameweek : EntityBase
{
    public int SeasonId { get; set; }
    public int Number { get; set; }
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class Team : EntityBase
{
    public int? ExternalId { get; set; }
    public required string Name { get; set; }
    public required string ShortName { get; set; }
    public required string Code { get; set; }
    public string? CrestUrl { get; set; }
    public decimal StrengthRating { get; set; }
    public decimal ExpectedGoalsForPerMatch { get; set; }
    public decimal ExpectedGoalsAgainstPerMatch { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Player : EntityBase
{
    public int? ExternalId { get; set; }
    public int TeamId { get; set; }
    public required string Name { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public required string Position { get; set; }
    public int ShirtNumber { get; set; }
    public decimal Price { get; set; }
    public decimal OwnershipPercent { get; set; }
    public decimal Form { get; set; }
    public decimal MinutesPlayed { get; set; }
    public decimal RecentPoints { get; set; }
    public decimal Goals { get; set; }
    public decimal Assists { get; set; }
    public decimal CleanSheets { get; set; }
    public decimal YellowCards { get; set; }
    public decimal RedCards { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.Unknown;
    public decimal ChanceOfPlayingNextRound { get; set; }
    public string? ExpectedReturnText { get; set; }
    public DateTime? LastVerifiedUtc { get; set; }
}

public sealed class Fixture : EntityBase
{
    public int? ExternalId { get; set; }
    public int SeasonId { get; set; }
    public int GameweekId { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime KickoffUtc { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string? Venue { get; set; }
    public bool IsFinished { get; set; }
    public bool IsBlanked { get; set; }
    public bool IsDoubleGameweek { get; set; }
    public string? Status { get; set; }
}

public sealed class PlayerMatchStat : EntityBase
{
    public int FixtureId { get; set; }
    public int PlayerId { get; set; }
    public int MinutesPlayed { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int CleanSheets { get; set; }
    public int Saves { get; set; }
    public int BonusPoints { get; set; }
    public int GoalsConceded { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public decimal FantasyPoints { get; set; }
    public bool IsHome { get; set; }
    public decimal OpponentStrength { get; set; }
    public decimal RollingForm { get; set; }
    public decimal PriceAtKickoff { get; set; }
}

public sealed class TeamMatchStat : EntityBase
{
    public int FixtureId { get; set; }
    public int TeamId { get; set; }
    public int OpponentTeamId { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public decimal ExpectedGoalsFor { get; set; }
    public decimal ExpectedGoalsAgainst { get; set; }
    public decimal ShotsFor { get; set; }
    public decimal ShotsAgainst { get; set; }
    public decimal PossessionPercent { get; set; }
    public decimal HomeStrength { get; set; }
    public decimal AwayStrength { get; set; }
}

public sealed class FantasyPlayerPrice : EntityBase
{
    public int PlayerId { get; set; }
    public int GameweekId { get; set; }
    public decimal Price { get; set; }
    public string? SourceName { get; set; }
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FantasyPlayerOwnership : EntityBase
{
    public int PlayerId { get; set; }
    public int GameweekId { get; set; }
    public decimal OwnershipPercent { get; set; }
    public string? SourceName { get; set; }
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PlayerAvailability : EntityBase
{
    public int PlayerId { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.Unknown;
    public bool InjuryFlag { get; set; }
    public bool SuspensionFlag { get; set; }
    public decimal ChanceOfPlayingNextRound { get; set; }
    public string? ExpectedReturnText { get; set; }
    public decimal AvailabilityConfidence { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime LastVerifiedUtc { get; set; } = DateTime.UtcNow;
    public string? RawNewsText { get; set; }
}

public sealed class InjuryReport : EntityBase
{
    public int? PlayerId { get; set; }
    public DateTime ReportedUtc { get; set; }
    public string? ReportText { get; set; }
    public string? ExpectedReturnText { get; set; }
    public decimal Confidence { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? RawNewsText { get; set; }
}

public sealed class Suspension : EntityBase
{
    public int? PlayerId { get; set; }
    public DateTime StartsUtc { get; set; }
    public DateTime? EndsUtc { get; set; }
    public string? Reason { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime LastVerifiedUtc { get; set; } = DateTime.UtcNow;
    public string? RawNewsText { get; set; }
}

public sealed class NewsItem : EntityBase
{
    public int? PlayerId { get; set; }
    public int? TeamId { get; set; }
    public DateTime PublishedUtc { get; set; }
    public required string Title { get; set; }
    public string? Summary { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? RawNewsText { get; set; }
    public decimal SentimentScore { get; set; }
    public bool InjuryFlag { get; set; }
    public bool SuspensionFlag { get; set; }
    public bool AvailableFlag { get; set; }
    public decimal Confidence { get; set; }
    public AvailabilityStatus ExtractedAvailabilityStatus { get; set; } = AvailabilityStatus.Unknown;
}

public sealed class Prediction : EntityBase
{
    public PredictionKind PredictionKind { get; set; }
    public int? FixtureId { get; set; }
    public int? PlayerId { get; set; }
    public int? TeamId { get; set; }
    public int? GameweekId { get; set; }
    public string? ModelVersion { get; set; }
    public decimal Score { get; set; }
    public decimal Probability { get; set; }
    public decimal PredictedValue { get; set; }
    public decimal? LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public string? Explanation { get; set; }
    public string? InputJson { get; set; }
    public decimal? EvaluationMetric { get; set; }
}

public sealed class ModelTrainingRun : EntityBase
{
    public required string ModelName { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public required string Status { get; set; }
    public string? DatasetDescription { get; set; }
    public int TrainingSampleCount { get; set; }
    public string? EvaluationMetricName { get; set; }
    public decimal EvaluationMetricValue { get; set; }
    public string? ModelPath { get; set; }
    public string? Notes { get; set; }
}

public sealed class DataIngestionRun : EntityBase
{
    public required string SourceName { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public required string Status { get; set; }
    public int ItemsProcessed { get; set; }
    public int ItemsUpserted { get; set; }
    public int ItemsSkipped { get; set; }
    public string? Notes { get; set; }
    public string? ErrorMessage { get; set; }
}
