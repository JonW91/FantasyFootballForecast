using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using FantasyFootballForecast.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FantasyFootballForecast.Tests;

public sealed class RecommendationServiceTests
{
    [Test]
    public async Task GetBestXIAsync_ReturnsAtMostElevenPlayers()
    {
        await using var db = CreateDb();
        var service = new FantasyRecommendationService(db);

        var result = await service.GetBestXIAsync();

        Assert.That(result.Count, Is.LessThanOrEqualTo(11));
    }

    [Test]
    public async Task GetBestXIAsync_RespectsPositionConstraints()
    {
        await using var db = CreateDb();
        var service = new FantasyRecommendationService(db);

        var result = await service.GetBestXIAsync();

        Assert.That(result.Count(p => p.Position == "GK"), Is.LessThanOrEqualTo(1));
        Assert.That(result.Count(p => p.Position == "DEF"), Is.LessThanOrEqualTo(4));
        Assert.That(result.Count(p => p.Position == "MID"), Is.LessThanOrEqualTo(4));
        Assert.That(result.Count(p => p.Position == "FWD"), Is.LessThanOrEqualTo(2));
    }

    [Test]
    public async Task GetBestXIAsync_WithFullSquad_ReturnsElevenPlayers()
    {
        await using var db = CreateDbWithFullSquad();
        var service = new FantasyRecommendationService(db);

        var result = await service.GetBestXIAsync();

        Assert.That(result.Count, Is.EqualTo(11));
        Assert.That(result.Count(p => p.Position == "GK"), Is.EqualTo(1));
        Assert.That(result.Count(p => p.Position == "DEF"), Is.EqualTo(4));
        Assert.That(result.Count(p => p.Position == "MID"), Is.EqualTo(4));
        Assert.That(result.Count(p => p.Position == "FWD"), Is.EqualTo(2));
    }

    [Test]
    public async Task GetTopPicksAsync_ReturnsRequestedCount()
    {
        await using var db = CreateDb();
        var service = new FantasyRecommendationService(db);

        var result = await service.GetTopPicksAsync(3);

        Assert.That(result.Count, Is.LessThanOrEqualTo(3));
    }

    private static FantasyFootballForecastDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FantasyFootballForecastDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var db = new FantasyFootballForecastDbContext(options);
        db.Teams.AddRange(SeedData.Teams);
        db.Players.AddRange(SeedData.Players);
        db.SaveChanges();
        return db;
    }

    private static FantasyFootballForecastDbContext CreateDbWithFullSquad()
    {
        var options = new DbContextOptionsBuilder<FantasyFootballForecastDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var db = new FantasyFootballForecastDbContext(options);

        var team = new Team { Name = "Test FC", ShortName = "TST", Code = "TST", StrengthRating = 75 };
        db.Teams.Add(team);
        db.SaveChanges();

        var players = new List<Player>
        {
            new() { TeamId = team.Id, Name = "GK One", Position = "GK", Price = 5.0m, RecentPoints = 5, Form = 5, ShirtNumber = 1 },
            new() { TeamId = team.Id, Name = "GK Two", Position = "GK", Price = 4.5m, RecentPoints = 4, Form = 4, ShirtNumber = 13 },
            new() { TeamId = team.Id, Name = "DEF One", Position = "DEF", Price = 5.5m, RecentPoints = 8, Form = 7, ShirtNumber = 2 },
            new() { TeamId = team.Id, Name = "DEF Two", Position = "DEF", Price = 5.5m, RecentPoints = 7, Form = 6, ShirtNumber = 3 },
            new() { TeamId = team.Id, Name = "DEF Three", Position = "DEF", Price = 5.0m, RecentPoints = 6, Form = 5, ShirtNumber = 4 },
            new() { TeamId = team.Id, Name = "DEF Four", Position = "DEF", Price = 5.0m, RecentPoints = 5, Form = 4, ShirtNumber = 5 },
            new() { TeamId = team.Id, Name = "MID One", Position = "MID", Price = 9.0m, RecentPoints = 15, Form = 8, ShirtNumber = 7 },
            new() { TeamId = team.Id, Name = "MID Two", Position = "MID", Price = 8.5m, RecentPoints = 12, Form = 7, ShirtNumber = 8 },
            new() { TeamId = team.Id, Name = "MID Three", Position = "MID", Price = 7.0m, RecentPoints = 9, Form = 6, ShirtNumber = 10 },
            new() { TeamId = team.Id, Name = "MID Four", Position = "MID", Price = 6.5m, RecentPoints = 7, Form = 5, ShirtNumber = 11 },
            new() { TeamId = team.Id, Name = "FWD One", Position = "FWD", Price = 9.5m, RecentPoints = 18, Form = 9, ShirtNumber = 9 },
            new() { TeamId = team.Id, Name = "FWD Two", Position = "FWD", Price = 8.0m, RecentPoints = 13, Form = 7, ShirtNumber = 19 },
        };

        db.Players.AddRange(players);
        db.SaveChanges();
        return db;
    }
}
