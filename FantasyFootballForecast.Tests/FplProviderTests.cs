using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using FantasyFootballForecast.Integrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace FantasyFootballForecast.Tests;

public sealed class FplProviderTests
{
    [Test]
    public async Task GetTeamsAsync_ParsesBootstrapPayload()
    {
        var provider = CreateProvider(bootstrapJson: SampleBootstrapJson);

        var teams = await provider.GetTeamsAsync();

        Assert.That(teams, Is.Not.Empty);
        Assert.That(teams[0].Name, Is.EqualTo("Arsenal"));
    }

    [Test]
    public async Task GetAvailabilityAsync_UsesPlayerStatusFields()
    {
        var provider = CreateProvider(bootstrapJson: SampleBootstrapJson);

        var availability = await provider.GetAvailabilityAsync();

        Assert.That(availability, Is.Not.Empty);
        Assert.That(availability[0].Status, Is.EqualTo(AvailabilityStatus.Available));
    }

    [Test]
    public async Task GetPlayerMatchStatsAsync_ParsesHistoryPayload()
    {
        var provider = CreateProvider(bootstrapJson: SampleBootstrapJson, summaryJson: SampleSummaryJson);

        var stats = await provider.GetPlayerMatchStatsAsync(1);

        Assert.That(stats, Has.Count.EqualTo(2));
        Assert.That(stats[0].FixtureExternalId, Is.EqualTo(10));
        Assert.That(stats[0].Goals, Is.EqualTo(1));
        Assert.That(stats[1].OpponentStrength, Is.EqualTo(92));
    }

    private static FplPublicFootballDataProvider CreateProvider(string bootstrapJson, string? summaryJson = null)
    {
        var handler = new StubHttpMessageHandler(bootstrapJson, summaryJson);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fantasy.premierleague.com/api/")
        };

        var factory = new StubHttpClientFactory(httpClient);
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new FplPublicFootballDataProvider(factory, cache, NullLogger<FplPublicFootballDataProvider>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _bootstrapJson;
        private readonly string? _summaryJson;

        public StubHttpMessageHandler(string bootstrapJson, string? summaryJson)
        {
            _bootstrapJson = bootstrapJson;
            _summaryJson = summaryJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var json = path.Contains("element-summary", StringComparison.OrdinalIgnoreCase)
                ? _summaryJson ?? "{}"
                : path.Contains("fixtures", StringComparison.OrdinalIgnoreCase)
                    ? "[]"
                    : _bootstrapJson;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private const string SampleBootstrapJson = """
    {
      "teams": [
        { "id": 1, "name": "Arsenal", "short_name": "ARS", "code": 3, "strength": 90 },
        { "id": 2, "name": "Liverpool", "short_name": "LIV", "code": 14, "strength": 92 },
        { "id": 3, "name": "Manchester City", "short_name": "MCI", "code": 43, "strength": 92 }
      ],
      "elements": [
        {
          "id": 1,
          "web_name": "Saka",
          "first_name": "Bukayo",
          "second_name": "Saka",
          "element_type": 3,
          "team": 1,
          "squad_number": 7,
          "now_cost": 90,
          "selected_by_percent": 10.5,
          "form": 6.1,
          "minutes": 900,
          "event_points": 38,
          "goals_scored": 5,
          "assists": 4,
          "clean_sheets": 8,
          "yellow_cards": 1,
          "red_cards": 0,
          "chance_of_playing_next_round": 100,
          "status": "a",
          "news": "Available"
        }
      ]
    }
    """;

    private const string SampleSummaryJson = """
    {
      "history": [
        {
          "fixture": 10,
          "round": 1,
          "opponent_team": 2,
          "kickoff_time": "2025-08-15T19:00:00Z",
          "was_home": true,
          "minutes": 90,
          "goals_scored": 1,
          "assists": 0,
          "clean_sheets": 1,
          "goals_conceded": 0,
          "saves": 0,
          "bonus": 3,
          "yellow_cards": 0,
          "red_cards": 0,
          "total_points": 9,
          "value": 90
        },
        {
          "fixture": 11,
          "round": 2,
          "opponent_team": 3,
          "kickoff_time": "2025-08-22T19:00:00Z",
          "was_home": false,
          "minutes": 88,
          "goals_scored": 0,
          "assists": 1,
          "clean_sheets": 0,
          "goals_conceded": 2,
          "saves": 0,
          "bonus": 1,
          "yellow_cards": 1,
          "red_cards": 0,
          "total_points": 4,
          "value": 91
        }
      ]
    }
    """;
}
