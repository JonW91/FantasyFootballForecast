using FantasyFootballForecast.Domain;

namespace FantasyFootballForecast.Infrastructure.Persistence;

public static class SeedData
{
    public static IReadOnlyList<Team> Teams { get; } =
    [
        new Team { Name = "Arsenal", ShortName = "ARS", Code = "ARS", StrengthRating = 88, ExpectedGoalsForPerMatch = 1.92m, ExpectedGoalsAgainstPerMatch = 0.92m, CrestUrl = "https://resources.premierleague.com/premierleague/badges/50/t3.png" },
        new Team { Name = "Aston Villa", ShortName = "AVL", Code = "AVL", StrengthRating = 79, ExpectedGoalsForPerMatch = 1.58m, ExpectedGoalsAgainstPerMatch = 1.08m, CrestUrl = "https://resources.premierleague.com/premierleague/badges/50/t7.png" },
        new Team { Name = "Chelsea", ShortName = "CHE", Code = "CHE", StrengthRating = 81, ExpectedGoalsForPerMatch = 1.66m, ExpectedGoalsAgainstPerMatch = 1.02m, CrestUrl = "https://resources.premierleague.com/premierleague/badges/50/t8.png" },
        new Team { Name = "Liverpool", ShortName = "LIV", Code = "LIV", StrengthRating = 92, ExpectedGoalsForPerMatch = 2.11m, ExpectedGoalsAgainstPerMatch = 0.83m, CrestUrl = "https://resources.premierleague.com/premierleague/badges/50/t14.png" },
        new Team { Name = "Manchester City", ShortName = "MCI", Code = "MCI", StrengthRating = 93, ExpectedGoalsForPerMatch = 2.18m, ExpectedGoalsAgainstPerMatch = 0.79m, CrestUrl = "https://resources.premierleague.com/premierleague/badges/50/t43.png" },
        new Team { Name = "Manchester United", ShortName = "MUN", Code = "MUN", StrengthRating = 76, ExpectedGoalsForPerMatch = 1.47m, ExpectedGoalsAgainstPerMatch = 1.21m, CrestUrl = "https://resources.premierleague.com/premierleague/badges/50/t1.png" }
    ];

    public static IReadOnlyList<Player> Players { get; } =
    [
        new Player { TeamId = 1, Name = "Bukayo Saka", FirstName = "Bukayo", LastName = "Saka", Position = "MID", ShirtNumber = 7, Price = 9.0m, OwnershipPercent = 34.5m, Form = 6.8m, MinutesPlayed = 1980, RecentPoints = 38, Goals = 12, Assists = 11, CleanSheets = 14, YellowCards = 3, RedCards = 0, AvailabilityStatus = AvailabilityStatus.Available, ChanceOfPlayingNextRound = 0.98m, ExpectedReturnText = "Available" },
        new Player { TeamId = 4, Name = "Mohamed Salah", FirstName = "Mohamed", LastName = "Salah", Position = "MID", ShirtNumber = 11, Price = 13.5m, OwnershipPercent = 55.8m, Form = 7.6m, MinutesPlayed = 2150, RecentPoints = 44, Goals = 18, Assists = 9, CleanSheets = 12, YellowCards = 1, RedCards = 0, AvailabilityStatus = AvailabilityStatus.Available, ChanceOfPlayingNextRound = 0.99m, ExpectedReturnText = "Available" },
        new Player { TeamId = 5, Name = "Erling Haaland", FirstName = "Erling", LastName = "Haaland", Position = "FWD", ShirtNumber = 9, Price = 14.0m, OwnershipPercent = 67.1m, Form = 8.4m, MinutesPlayed = 2100, RecentPoints = 52, Goals = 28, Assists = 5, CleanSheets = 10, YellowCards = 2, RedCards = 0, AvailabilityStatus = AvailabilityStatus.Available, ChanceOfPlayingNextRound = 0.97m, ExpectedReturnText = "Available" },
        new Player { TeamId = 2, Name = "Ollie Watkins", FirstName = "Ollie", LastName = "Watkins", Position = "FWD", ShirtNumber = 11, Price = 9.0m, OwnershipPercent = 26.4m, Form = 6.2m, MinutesPlayed = 2050, RecentPoints = 41, Goals = 17, Assists = 12, CleanSheets = 8, YellowCards = 4, RedCards = 0, AvailabilityStatus = AvailabilityStatus.Available, ChanceOfPlayingNextRound = 0.96m, ExpectedReturnText = "Available" },
        new Player { TeamId = 3, Name = "Cole Palmer", FirstName = "Cole", LastName = "Palmer", Position = "MID", ShirtNumber = 20, Price = 10.5m, OwnershipPercent = 62.0m, Form = 7.9m, MinutesPlayed = 2020, RecentPoints = 46, Goals = 19, Assists = 13, CleanSheets = 6, YellowCards = 2, RedCards = 0, AvailabilityStatus = AvailabilityStatus.Doubtful, ChanceOfPlayingNextRound = 0.68m, ExpectedReturnText = "Late fitness test", LastVerifiedUtc = DateTime.UtcNow },
        new Player { TeamId = 6, Name = "Bruno Fernandes", FirstName = "Bruno", LastName = "Fernandes", Position = "MID", ShirtNumber = 8, Price = 8.5m, OwnershipPercent = 24.7m, Form = 5.8m, MinutesPlayed = 2230, RecentPoints = 34, Goals = 10, Assists = 11, CleanSheets = 5, YellowCards = 6, RedCards = 0, AvailabilityStatus = AvailabilityStatus.Available, ChanceOfPlayingNextRound = 0.94m, ExpectedReturnText = "Available" }
    ];

    public static IReadOnlyList<Fixture> Fixtures(IReadOnlyList<Team> teams, int seasonId, int gameweekId) =>
    [
        new Fixture { SeasonId = seasonId, GameweekId = gameweekId, HomeTeamId = teams[0].Id, AwayTeamId = teams[1].Id, KickoffUtc = DateTime.UtcNow.Date.AddDays(2).AddHours(14), Status = "Scheduled" },
        new Fixture { SeasonId = seasonId, GameweekId = gameweekId, HomeTeamId = teams[3].Id, AwayTeamId = teams[4].Id, KickoffUtc = DateTime.UtcNow.Date.AddDays(2).AddHours(16), Status = "Scheduled", IsDoubleGameweek = true },
        new Fixture { SeasonId = seasonId, GameweekId = gameweekId, HomeTeamId = teams[2].Id, AwayTeamId = teams[5].Id, KickoffUtc = DateTime.UtcNow.Date.AddDays(3).AddHours(12), Status = "Scheduled" }
    ];

    public static IReadOnlyList<NewsItem> NewsItems() =>
    [
        new NewsItem
        {
            PublishedUtc = DateTime.UtcNow.AddHours(-4),
            Title = "Cole Palmer late fitness test ahead of weekend deadline",
            Summary = "Chelsea attacker faces a late fitness test after a minor knock.",
            SourceName = "Official Club Update",
            SourceUrl = "https://example.com/club-update",
            RawNewsText = "Cole Palmer is a late fitness test and remains doubtful for the next match.",
            Confidence = 0.84m,
            InjuryFlag = true,
            SuspensionFlag = false,
            AvailableFlag = false,
            ExtractedAvailabilityStatus = AvailabilityStatus.Doubtful
        },
        new NewsItem
        {
            PublishedUtc = DateTime.UtcNow.AddHours(-2),
            Title = "Erling Haaland available after full training",
            Summary = "Manchester City striker is available for selection.",
            SourceName = "Club Press Conference",
            SourceUrl = "https://example.com/press-conference",
            RawNewsText = "Erling Haaland is available and has returned to training.",
            Confidence = 0.91m,
            InjuryFlag = false,
            SuspensionFlag = false,
            AvailableFlag = true,
            ExtractedAvailabilityStatus = AvailabilityStatus.Available
        }
    ];

    public static IReadOnlyList<PlayerAvailability> Availabilities() =>
    [
        new PlayerAvailability
        {
            PlayerId = 5,
            AvailabilityStatus = AvailabilityStatus.Doubtful,
            InjuryFlag = true,
            SuspensionFlag = false,
            ChanceOfPlayingNextRound = 0.68m,
            ExpectedReturnText = "Late fitness test",
            AvailabilityConfidence = 0.84m,
            SourceName = "Official Club Update",
            SourceUrl = "https://example.com/club-update",
            RawNewsText = "Cole Palmer is a late fitness test and remains doubtful for the next match.",
            LastVerifiedUtc = DateTime.UtcNow
        },
        new PlayerAvailability
        {
            PlayerId = 3,
            AvailabilityStatus = AvailabilityStatus.Available,
            InjuryFlag = false,
            SuspensionFlag = false,
            ChanceOfPlayingNextRound = 0.97m,
            ExpectedReturnText = "Available",
            AvailabilityConfidence = 0.95m,
            SourceName = "Structured FPL Feed",
            SourceUrl = "https://fantasy.premierleague.com/api/bootstrap-static/",
            RawNewsText = "Erling Haaland available after training.",
            LastVerifiedUtc = DateTime.UtcNow
        }
    ];

    public static IReadOnlyList<ModelTrainingRun> ModelTrainingRuns() =>
    [
        new ModelTrainingRun
        {
            ModelName = "PlayerFantasyPoints",
            Status = "Completed",
            StartedUtc = DateTime.UtcNow.AddDays(-1),
            CompletedUtc = DateTime.UtcNow.AddDays(-1).AddMinutes(12),
            TrainingSampleCount = 1820,
            EvaluationMetricName = "RMSE",
            EvaluationMetricValue = 3.87m,
            ModelPath = "App_Data/models/player-fantasy.zip",
            Notes = "Starter model seeded for local development."
        },
        new ModelTrainingRun
        {
            ModelName = "TeamMatchPrediction",
            Status = "Completed",
            StartedUtc = DateTime.UtcNow.AddDays(-1).AddMinutes(30),
            CompletedUtc = DateTime.UtcNow.AddDays(-1).AddMinutes(43),
            TrainingSampleCount = 1420,
            EvaluationMetricName = "Accuracy",
            EvaluationMetricValue = 0.61m,
            ModelPath = "App_Data/models/team-match.zip",
            Notes = "Starter binary outcome model seeded for local development."
        }
    ];

    public static IReadOnlyList<DataIngestionRun> DataIngestionRuns() =>
    [
        new DataIngestionRun
        {
            SourceName = "FPL Public API",
            Status = "Completed",
            StartedUtc = DateTime.UtcNow.AddHours(-6),
            CompletedUtc = DateTime.UtcNow.AddHours(-6).AddMinutes(3),
            ItemsProcessed = 6,
            ItemsUpserted = 6,
            ItemsSkipped = 0,
            Notes = "Seeded data refresh."
        },
        new DataIngestionRun
        {
            SourceName = "Official News Feed",
            Status = "Completed",
            StartedUtc = DateTime.UtcNow.AddHours(-5),
            CompletedUtc = DateTime.UtcNow.AddHours(-5).AddMinutes(2),
            ItemsProcessed = 2,
            ItemsUpserted = 2,
            ItemsSkipped = 0,
            Notes = "Sample news and availability feed."
        }
    ];
}
