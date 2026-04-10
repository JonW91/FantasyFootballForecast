using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using FantasyFootballForecast.Infrastructure.Persistence;
using FantasyFootballForecast.ML;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace FantasyFootballForecast.Tests;

public sealed class ModelServiceTests
{
    [Test]
    public async Task PlayerModelPredictsAValue()
    {
        await using var db = CreateDb();
        var environment = new TestHostEnvironment(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var service = new PlayerFantasyPointPredictionService(db, environment);

        var result = await service.PredictAsync(new PlayerFantasyPredictionInput(1, 1, 2, 900, 35, 10, 8, 6, 1, 0, 50, true, 6.0m, 9.0m, 20m, 0.95m, AvailabilityStatus.Available));

        Assert.That(result.PredictedFantasyPoints, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task TeamModelPredictsAProbability()
    {
        await using var db = CreateDb();
        var environment = new TestHostEnvironment(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var service = new TeamMatchPredictionService(db, environment);

        var result = await service.PredictAsync(new TeamMatchPredictionInput(1, 2, 88, 79, 1.8m, 1.3m, 0.9m, 1.2m, true, 6.2m, 5.4m));

        Assert.That(result.HomeWinProbability, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.HomeWinProbability, Is.LessThanOrEqualTo(1));
    }

    private static FantasyFootballForecastDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FantasyFootballForecastDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var db = new FantasyFootballForecastDbContext(options);
        db.Teams.AddRange(SeedData.Teams);
        db.Players.AddRange(SeedData.Players);
        db.Seasons.Add(new Season { Name = "Premier League 2025/26", StartYear = 2025, EndYear = 2026, IsCurrent = true });
        db.Gameweeks.Add(new Gameweek { SeasonId = 1, Number = 1, StartsUtc = DateTime.UtcNow, EndsUtc = DateTime.UtcNow.AddDays(7), IsCurrent = true });
        db.SaveChanges();
        return db;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath) => ContentRootPath = contentRootPath;
        public string ApplicationName { get; set; } = "FantasyFootballForecast.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
