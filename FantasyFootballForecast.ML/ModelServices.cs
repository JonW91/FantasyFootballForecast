using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using FantasyFootballForecast.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FantasyFootballForecast.ML;

public sealed class PlayerFantasyPointPredictionService : IPlayerFantasyPointPredictor
{
    private readonly FantasyFootballForecastDbContext _db;
    private readonly MLContext _mlContext = new(seed: 42);
    private readonly string _modelPath;
    private readonly object _sync = new();
    private ITransformer? _model;

    public PlayerFantasyPointPredictionService(FantasyFootballForecastDbContext db, IHostEnvironment environment)
    {
        _db = db;
        _modelPath = Path.Combine(environment.ContentRootPath, "App_Data", "models", "player-fantasy", "model.zip");
    }

    public async Task<PlayerFantasyPredictionResult> PredictAsync(PlayerFantasyPredictionInput input, CancellationToken cancellationToken = default)
    {
        var model = await EnsureModelAsync(cancellationToken);
        var engine = _mlContext.Model.CreatePredictionEngine<PlayerFantasyTrainingRow, PlayerFantasyPredictionOutput>(model);
        var output = engine.Predict(ToTrainingRow(input));
        var score = Math.Max(0, output.Score);

        return new PlayerFantasyPredictionResult(
            PredictedFantasyPoints: (decimal)score,
            Score: score,
            Probability: Math.Clamp(score / 15f, 0f, 1f),
            ModelVersion: "player-fantasy",
            Explanation: $"Recent form {input.RecentFormAverage:F1}, availability {input.AvailabilityStatus}, projected score {score:F2}.");
    }

    public async Task<ModelTrainingSummaryDto> TrainAsync(CancellationToken cancellationToken = default)
    {
        var rows = await BuildTrainingRowsAsync(cancellationToken);
        if (rows.Count < 5)
        {
            rows.AddRange(BuildSyntheticRows());
        }

        var data = _mlContext.Data.LoadFromEnumerable(rows);
        var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(PlayerFantasyTrainingRow.MinutesPlayed),
                nameof(PlayerFantasyTrainingRow.RecentPoints),
                nameof(PlayerFantasyTrainingRow.Goals),
                nameof(PlayerFantasyTrainingRow.Assists),
                nameof(PlayerFantasyTrainingRow.CleanSheets),
                nameof(PlayerFantasyTrainingRow.YellowCards),
                nameof(PlayerFantasyTrainingRow.RedCards),
                nameof(PlayerFantasyTrainingRow.OpponentStrength),
                nameof(PlayerFantasyTrainingRow.IsHome),
                nameof(PlayerFantasyTrainingRow.RecentFormAverage),
                nameof(PlayerFantasyTrainingRow.Price),
                nameof(PlayerFantasyTrainingRow.OwnershipPercent),
                nameof(PlayerFantasyTrainingRow.ChanceOfPlayingNextRound),
                nameof(PlayerFantasyTrainingRow.AvailabilityStatusCode))
            .Append(_mlContext.Regression.Trainers.Sdca(labelColumnName: nameof(PlayerFantasyTrainingRow.Label), featureColumnName: "Features"));

        _model = pipeline.Fit(split.TrainSet);
        SaveModel(_model);

        var scored = _model.Transform(split.TestSet);
        var metrics = _mlContext.Regression.Evaluate(scored, labelColumnName: nameof(PlayerFantasyTrainingRow.Label));
        var run = new ModelTrainingRun
        {
            ModelName = "PlayerFantasyPoints",
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow,
            Status = "Completed",
            TrainingSampleCount = rows.Count,
            EvaluationMetricName = "RMSE",
            EvaluationMetricValue = (decimal)metrics.RootMeanSquaredError,
            ModelPath = _modelPath,
            Notes = "Trained from historical player fantasy point rows."
        };

        _db.ModelTrainingRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return new ModelTrainingSummaryDto(run.ModelName, run.Status, run.TrainingSampleCount, run.EvaluationMetricName, run.EvaluationMetricValue, run.ModelPath, run.Notes);
    }

    private async Task<ITransformer> EnsureModelAsync(CancellationToken cancellationToken)
    {
        if (_model is not null)
        {
            return _model;
        }

        lock (_sync)
        {
            if (_model is not null)
            {
                return _model;
            }

            if (File.Exists(_modelPath))
            {
                using var stream = File.OpenRead(_modelPath);
                _model = _mlContext.Model.Load(stream, out _);
                return _model;
            }
        }

        await TrainAsync(cancellationToken);
        return _model ?? throw new InvalidOperationException("Player fantasy model is not available.");
    }

    private Task<List<PlayerFantasyTrainingRow>> BuildTrainingRowsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_db.PlayerMatchStats
            .Join(_db.Players, stat => stat.PlayerId, player => player.Id, (stat, player) => new PlayerFantasyTrainingRow
            {
                MinutesPlayed = stat.MinutesPlayed,
                RecentPoints = (float)stat.FantasyPoints,
                Goals = stat.Goals,
                Assists = stat.Assists,
                CleanSheets = stat.CleanSheets,
                YellowCards = stat.YellowCards,
                RedCards = stat.RedCards,
                OpponentStrength = (float)stat.OpponentStrength,
                IsHome = stat.IsHome ? 1f : 0f,
                RecentFormAverage = (float)stat.RollingForm,
                Price = (float)stat.PriceAtKickoff,
                OwnershipPercent = (float)player.OwnershipPercent,
                ChanceOfPlayingNextRound = (float)player.ChanceOfPlayingNextRound,
                AvailabilityStatusCode = (float)player.AvailabilityStatus,
                Label = (float)stat.FantasyPoints
            })
            .ToList());
    }

    private static List<PlayerFantasyTrainingRow> BuildSyntheticRows()
    {
        return SeedData.Players.Select(player => new PlayerFantasyTrainingRow
        {
            MinutesPlayed = (float)player.MinutesPlayed,
            RecentPoints = (float)player.RecentPoints,
            Goals = (float)player.Goals,
            Assists = (float)player.Assists,
            CleanSheets = (float)player.CleanSheets,
            YellowCards = (float)player.YellowCards,
            RedCards = (float)player.RedCards,
            OpponentStrength = 50,
            IsHome = 1,
            RecentFormAverage = (float)player.Form,
            Price = (float)player.Price,
            OwnershipPercent = (float)player.OwnershipPercent,
            ChanceOfPlayingNextRound = (float)player.ChanceOfPlayingNextRound,
            AvailabilityStatusCode = (float)player.AvailabilityStatus,
            Label = (float)(player.RecentPoints * 0.6m + player.Goals * 4 + player.Assists * 3)
        }).ToList();
    }

    private void SaveModel(ITransformer model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
        using var stream = File.Create(_modelPath);
        _mlContext.Model.Save(model, default, stream);
    }

    private static PlayerFantasyTrainingRow ToTrainingRow(PlayerFantasyPredictionInput input) => new()
    {
        MinutesPlayed = (float)input.MinutesPlayed,
        RecentPoints = (float)input.RecentPoints,
        Goals = (float)input.Goals,
        Assists = (float)input.Assists,
        CleanSheets = (float)input.CleanSheets,
        YellowCards = (float)input.YellowCards,
        RedCards = (float)input.RedCards,
        OpponentStrength = (float)input.OpponentStrength,
        IsHome = input.IsHome ? 1f : 0f,
        RecentFormAverage = (float)input.RecentFormAverage,
        Price = (float)input.Price,
        OwnershipPercent = (float)input.OwnershipPercent,
        ChanceOfPlayingNextRound = (float)input.ChanceOfPlayingNextRound,
        AvailabilityStatusCode = (float)input.AvailabilityStatus
    };

    private sealed class PlayerFantasyTrainingRow
    {
        public float MinutesPlayed { get; set; }
        public float RecentPoints { get; set; }
        public float Goals { get; set; }
        public float Assists { get; set; }
        public float CleanSheets { get; set; }
        public float YellowCards { get; set; }
        public float RedCards { get; set; }
        public float OpponentStrength { get; set; }
        public float IsHome { get; set; }
        public float RecentFormAverage { get; set; }
        public float Price { get; set; }
        public float OwnershipPercent { get; set; }
        public float ChanceOfPlayingNextRound { get; set; }
        public float AvailabilityStatusCode { get; set; }
        public float Label { get; set; }
    }

    private sealed class PlayerFantasyPredictionOutput
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }
}

public sealed class TeamMatchPredictionService : ITeamMatchPredictor
{
    private readonly FantasyFootballForecastDbContext _db;
    private readonly MLContext _mlContext = new(seed: 42);
    private readonly string _modelPath;
    private readonly object _sync = new();
    private ITransformer? _model;

    public TeamMatchPredictionService(FantasyFootballForecastDbContext db, IHostEnvironment environment)
    {
        _db = db;
        _modelPath = Path.Combine(environment.ContentRootPath, "App_Data", "models", "team-match", "model.zip");
    }

    public async Task<TeamMatchPredictionResult> PredictAsync(TeamMatchPredictionInput input, CancellationToken cancellationToken = default)
    {
        var model = await EnsureModelAsync(cancellationToken);
        var engine = _mlContext.Model.CreatePredictionEngine<TeamMatchTrainingRow, TeamMatchPredictionOutput>(model);
        var output = engine.Predict(ToTrainingRow(input));
        var homeProbability = Math.Clamp(output.Probability, 0f, 1f);

        return new TeamMatchPredictionResult(
            HomeWinProbability: (decimal)homeProbability,
            DrawProbability: 0.25m,
            AwayWinProbability: Math.Clamp(1m - (decimal)homeProbability - 0.25m, 0m, 1m),
            ExpectedHomeGoals: Math.Max(0, input.HomeExpectedGoalsFor),
            ExpectedAwayGoals: Math.Max(0, input.AwayExpectedGoalsFor),
            ModelVersion: "team-match",
            Explanation: $"Home strength {input.HomeStrength:F1} vs away strength {input.AwayStrength:F1}.");
    }

    public async Task<ModelTrainingSummaryDto> TrainAsync(CancellationToken cancellationToken = default)
    {
        var rows = await BuildTrainingRowsAsync(cancellationToken);
        if (rows.Count < 5)
        {
            rows.AddRange(BuildSyntheticRows());
        }

        var data = _mlContext.Data.LoadFromEnumerable(rows);
        var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(TeamMatchTrainingRow.HomeStrength),
                nameof(TeamMatchTrainingRow.AwayStrength),
                nameof(TeamMatchTrainingRow.HomeExpectedGoalsFor),
                nameof(TeamMatchTrainingRow.AwayExpectedGoalsFor),
                nameof(TeamMatchTrainingRow.HomeExpectedGoalsAgainst),
                nameof(TeamMatchTrainingRow.AwayExpectedGoalsAgainst),
                nameof(TeamMatchTrainingRow.IsHomeFixture),
                nameof(TeamMatchTrainingRow.HomeRecentForm),
                nameof(TeamMatchTrainingRow.AwayRecentForm))
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: nameof(TeamMatchTrainingRow.Label), featureColumnName: "Features"));

        _model = pipeline.Fit(split.TrainSet);
        SaveModel(_model);

        var scored = _model.Transform(split.TestSet);
        var metrics = _mlContext.BinaryClassification.Evaluate(scored, labelColumnName: nameof(TeamMatchTrainingRow.Label));
        var run = new ModelTrainingRun
        {
            ModelName = "TeamMatchPrediction",
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow,
            Status = "Completed",
            TrainingSampleCount = rows.Count,
            EvaluationMetricName = "Accuracy",
            EvaluationMetricValue = (decimal)metrics.Accuracy,
            ModelPath = _modelPath,
            Notes = "Trained from fixture and strength signals."
        };

        _db.ModelTrainingRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return new ModelTrainingSummaryDto(run.ModelName, run.Status, run.TrainingSampleCount, run.EvaluationMetricName, run.EvaluationMetricValue, run.ModelPath, run.Notes);
    }

    private async Task<ITransformer> EnsureModelAsync(CancellationToken cancellationToken)
    {
        if (_model is not null)
        {
            return _model;
        }

        lock (_sync)
        {
            if (_model is not null)
            {
                return _model;
            }

            if (File.Exists(_modelPath))
            {
                using var stream = File.OpenRead(_modelPath);
                _model = _mlContext.Model.Load(stream, out _);
                return _model;
            }
        }

        await TrainAsync(cancellationToken);
        return _model ?? throw new InvalidOperationException("Team match model is not available.");
    }

    private Task<List<TeamMatchTrainingRow>> BuildTrainingRowsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_db.TeamMatchStats
            .Join(_db.Fixtures, stat => stat.FixtureId, fixture => fixture.Id, (stat, fixture) => new { stat, fixture })
            .Join(_db.Teams, joined => joined.stat.TeamId, team => team.Id, (joined, team) => new { joined.stat, joined.fixture, team })
            .Join(_db.Teams, joined => joined.stat.OpponentTeamId, opponent => opponent.Id, (joined, opponent) => new TeamMatchTrainingRow
            {
                HomeStrength = joined.stat.TeamId == joined.fixture.HomeTeamId ? (float)joined.stat.HomeStrength : (float)joined.stat.AwayStrength,
                AwayStrength = joined.stat.TeamId == joined.fixture.HomeTeamId ? (float)joined.stat.AwayStrength : (float)joined.stat.HomeStrength,
                HomeExpectedGoalsFor = joined.stat.TeamId == joined.fixture.HomeTeamId ? (float)joined.stat.ExpectedGoalsFor : (float)joined.stat.ExpectedGoalsAgainst,
                AwayExpectedGoalsFor = joined.stat.TeamId == joined.fixture.HomeTeamId ? (float)joined.stat.ExpectedGoalsAgainst : (float)joined.stat.ExpectedGoalsFor,
                HomeExpectedGoalsAgainst = joined.stat.TeamId == joined.fixture.HomeTeamId ? (float)joined.stat.ExpectedGoalsAgainst : (float)joined.stat.ExpectedGoalsFor,
                AwayExpectedGoalsAgainst = joined.stat.TeamId == joined.fixture.HomeTeamId ? (float)joined.stat.ExpectedGoalsFor : (float)joined.stat.ExpectedGoalsAgainst,
                IsHomeFixture = joined.stat.TeamId == joined.fixture.HomeTeamId ? 1f : 0f,
                HomeRecentForm = (float)joined.team.StrengthRating / 10f,
                AwayRecentForm = (float)opponent.StrengthRating / 10f,
                Label = joined.stat.GoalsFor > joined.stat.GoalsAgainst
            })
            .ToList());
    }

    private static List<TeamMatchTrainingRow> BuildSyntheticRows()
    {
        var balancedRows = SeedData.Teams.Zip(SeedData.Teams.Skip(1), (home, away) =>
        {
            var homeWinRow = new TeamMatchTrainingRow
            {
                HomeStrength = (float)home.StrengthRating,
                AwayStrength = (float)away.StrengthRating,
                HomeExpectedGoalsFor = (float)home.ExpectedGoalsForPerMatch,
                AwayExpectedGoalsFor = (float)away.ExpectedGoalsForPerMatch,
                HomeExpectedGoalsAgainst = (float)home.ExpectedGoalsAgainstPerMatch,
                AwayExpectedGoalsAgainst = (float)away.ExpectedGoalsAgainstPerMatch,
                IsHomeFixture = 1f,
                HomeRecentForm = (float)(home.StrengthRating / 10m),
                AwayRecentForm = (float)(away.StrengthRating / 10m),
                Label = true
            };

            var awayWinRow = new TeamMatchTrainingRow
            {
                HomeStrength = (float)away.StrengthRating,
                AwayStrength = (float)home.StrengthRating,
                HomeExpectedGoalsFor = (float)away.ExpectedGoalsForPerMatch,
                AwayExpectedGoalsFor = (float)home.ExpectedGoalsForPerMatch,
                HomeExpectedGoalsAgainst = (float)away.ExpectedGoalsAgainstPerMatch,
                AwayExpectedGoalsAgainst = (float)home.ExpectedGoalsAgainstPerMatch,
                IsHomeFixture = 1f,
                HomeRecentForm = (float)(away.StrengthRating / 10m),
                AwayRecentForm = (float)(home.StrengthRating / 10m),
                Label = false
            };

            return new[] { homeWinRow, awayWinRow };
        }).SelectMany(rows => rows).ToList();

        return balancedRows;
    }

    private void SaveModel(ITransformer model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
        using var stream = File.Create(_modelPath);
        _mlContext.Model.Save(model, default, stream);
    }

    private static TeamMatchTrainingRow ToTrainingRow(TeamMatchPredictionInput input) => new()
    {
        HomeStrength = (float)input.HomeStrength,
        AwayStrength = (float)input.AwayStrength,
        HomeExpectedGoalsFor = (float)input.HomeExpectedGoalsFor,
        AwayExpectedGoalsFor = (float)input.AwayExpectedGoalsFor,
        HomeExpectedGoalsAgainst = (float)input.HomeExpectedGoalsAgainst,
        AwayExpectedGoalsAgainst = (float)input.AwayExpectedGoalsAgainst,
        IsHomeFixture = input.IsHomeFixture ? 1f : 0f,
        HomeRecentForm = (float)input.HomeRecentForm,
        AwayRecentForm = (float)input.AwayRecentForm
    };

    private sealed class TeamMatchTrainingRow
    {
        public float HomeStrength { get; set; }
        public float AwayStrength { get; set; }
        public float HomeExpectedGoalsFor { get; set; }
        public float AwayExpectedGoalsFor { get; set; }
        public float HomeExpectedGoalsAgainst { get; set; }
        public float AwayExpectedGoalsAgainst { get; set; }
        public float IsHomeFixture { get; set; }
        public float HomeRecentForm { get; set; }
        public float AwayRecentForm { get; set; }
        public bool Label { get; set; }
    }

    private sealed class TeamMatchPredictionOutput
    {
        [ColumnName("Probability")]
        public float Probability { get; set; }
    }
}

public sealed class ModelTrainingService : IModelTrainingService
{
    private readonly PlayerFantasyPointPredictionService _playerService;
    private readonly TeamMatchPredictionService _teamService;

    public ModelTrainingService(PlayerFantasyPointPredictionService playerService, TeamMatchPredictionService teamService)
    {
        _playerService = playerService;
        _teamService = teamService;
    }

    public Task<ModelTrainingSummaryDto> TrainPlayerFantasyModelAsync(CancellationToken cancellationToken = default)
        => _playerService.TrainAsync(cancellationToken);

    public Task<ModelTrainingSummaryDto> TrainTeamMatchModelAsync(CancellationToken cancellationToken = default)
        => _teamService.TrainAsync(cancellationToken);

    public async Task<ModelTrainingSummaryDto> RetrainAllAsync(CancellationToken cancellationToken = default)
    {
        var player = await TrainPlayerFantasyModelAsync(cancellationToken);
        var team = await TrainTeamMatchModelAsync(cancellationToken);
        return new ModelTrainingSummaryDto("AllModels", "Completed", player.TrainingSampleCount + team.TrainingSampleCount, "Composite", player.MetricValue + team.MetricValue, null, "Both models retrained.");
    }
}
