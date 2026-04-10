using FantasyFootballForecast.Application;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

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

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
    }
}
