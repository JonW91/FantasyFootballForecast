using FantasyFootballForecast.Application;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Json;

namespace FantasyFootballForecast.IntegrationTests;

public sealed class ApiSmokeTests
{
    [Test]
    public async Task GetTeams_ReturnsSeededTeams()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/teams");

        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    [Test]
    public async Task GetTeamDetails_ReturnsSquadAndFixtures()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/teams/1");

        Assert.That(response.IsSuccessStatusCode, Is.True);

        var detail = await response.Content.ReadFromJsonAsync<TeamDetailDto>();
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Players, Is.Not.Empty);
        Assert.That(detail.UpcomingFixtures, Is.Not.Null);
    }

    [Test]
    public async Task GetPlayerDetails_ReturnsAvailabilityAndNews()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/players/1");

        Assert.That(response.IsSuccessStatusCode, Is.True);

        var detail = await response.Content.ReadFromJsonAsync<PlayerDetailDto>();
        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Player.Name, Is.Not.Empty);
        Assert.That(detail.CurrentAvailability.PlayerName, Is.EqualTo(detail.Player.Name));
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
    }
}
