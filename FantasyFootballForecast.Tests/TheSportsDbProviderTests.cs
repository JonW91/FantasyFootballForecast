using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using FantasyFootballForecast.Integrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace FantasyFootballForecast.Tests;

public sealed class TheSportsDbProviderTests
{
    [Test]
    public async Task GetTeamsAsync_ParsesLeagueTeams()
    {
        var provider = CreateProvider();

        var teams = await provider.GetTeamsAsync();

        Assert.That(teams, Has.Count.EqualTo(1));
        Assert.That(teams[0].Name, Is.EqualTo("Arsenal"));
        Assert.That(teams[0].CrestUrl, Is.EqualTo("https://example.com/arsenal.png"));
    }

    [Test]
    public async Task GetPlayersAsync_ParsesTeamRosters()
    {
        var provider = CreateProvider();

        var players = await provider.GetPlayersAsync();

        Assert.That(players, Has.Count.EqualTo(1));
        Assert.That(players[0].Name, Is.EqualTo("Bukayo Saka"));
        Assert.That(players[0].Position, Is.EqualTo("Midfield"));
    }

    [Test]
    public async Task GetFixturesAsync_ParsesHistoricalFixtures()
    {
        var provider = CreateProvider();

        var fixtures = await provider.GetFixturesAsync();

        Assert.That(fixtures, Has.Count.EqualTo(1));
        Assert.That(fixtures[0].HomeScore, Is.EqualTo(2));
        Assert.That(fixtures[0].AwayScore, Is.EqualTo(1));
        Assert.That(fixtures[0].IsFinished, Is.True);
    }

    [Test]
    public async Task GetPlayerMatchStatsAsync_ParsesPlayerResults()
    {
        var provider = CreateProvider();

        var stats = await provider.GetPlayerMatchStatsAsync(12345);

        Assert.That(stats, Has.Count.EqualTo(1));
        Assert.That(stats[0].FixtureExternalId, Is.EqualTo(2267381));
        Assert.That(stats[0].OpponentTeamExternalId, Is.EqualTo(133720));
        Assert.That(stats[0].FantasyPoints, Is.EqualTo(6));
    }

    private static TheSportsDbFootballDataProvider CreateProvider()
    {
        var handler = new StubHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.thesportsdb.com/api/v1/json/123/")
        };

        var factory = new StubHttpClientFactory(httpClient);
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new TheSportsDbFootballDataProvider(factory, cache, NullLogger<TheSportsDbFootballDataProvider>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var json = path.Contains("search_all_teams", StringComparison.OrdinalIgnoreCase)
                ? TeamsJson
                : path.Contains("lookup_all_players", StringComparison.OrdinalIgnoreCase)
                    ? PlayersJson
                    : path.Contains("playerresults", StringComparison.OrdinalIgnoreCase)
                        ? PlayerResultsJson
                        : path.Contains("lookupevent", StringComparison.OrdinalIgnoreCase)
                            ? EventJson
                    : FixturesJson;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private const string TeamsJson = """
    {
      "teams": [
        {
          "idTeam": "133604",
          "strTeam": "Arsenal",
          "strTeamShort": "ARS",
          "strTeamBadge": "https://example.com/arsenal.png"
        }
      ]
    }
    """;

    private const string PlayersJson = """
    {
      "player": [
        {
          "idPlayer": "12345",
          "strPlayer": "Bukayo Saka",
          "strLastName": "Saka",
          "strNumber": "7",
          "strPosition": "Midfield",
          "strStatus": "Active"
        }
      ]
    }
    """;

    private const string FixturesJson = """
    {
      "events": [
        {
          "idEvent": "2267381",
          "strTimestamp": "2026-03-22T14:15:00",
          "dateEvent": "2026-03-22",
          "strTime": "14:15:00",
          "strEvent": "Tottenham Hotspur vs Nottingham Forest",
          "strHomeTeam": "Tottenham Hotspur",
          "strAwayTeam": "Nottingham Forest",
          "intHomeScore": "2",
          "intAwayScore": "1",
          "intRound": "31",
          "strSeason": "2025-2026",
          "strVenue": "Tottenham Hotspur Stadium",
          "strStatus": "Match Finished",
          "idHomeTeam": "133616",
          "idAwayTeam": "133720"
        }
      ]
    }
    """;

    private const string PlayerResultsJson = """
    {
      "results": [
        {
          "idResult": "9001",
          "idPlayer": "12345",
          "strPlayer": "Bukayo Saka",
          "idTeam": "133604",
          "idEvent": "2267381",
          "strEvent": "Tottenham Hotspur vs Nottingham Forest",
          "strResult": "W",
          "intPosition": "1",
          "intPoints": "6",
          "strDetail": "Scored",
          "dateEvent": "2026-03-22",
          "strSeason": "2025-2026",
          "strCountry": "England",
          "strSport": "Soccer"
        }
      ]
    }
    """;

    private const string EventJson = """
    {
      "events": [
        {
          "idEvent": "2267381",
          "strTimestamp": "2026-03-22T14:15:00",
          "dateEvent": "2026-03-22",
          "strTime": "14:15:00",
          "strEvent": "Arsenal vs Nottingham Forest",
          "strHomeTeam": "Arsenal",
          "strAwayTeam": "Nottingham Forest",
          "intHomeScore": "2",
          "intAwayScore": "1",
          "intRound": "31",
          "strSeason": "2025-2026",
          "strVenue": "Emirates Stadium",
          "strStatus": "Match Finished",
          "idHomeTeam": "133604",
          "idAwayTeam": "133720"
        }
      ]
    }
    """;
}
