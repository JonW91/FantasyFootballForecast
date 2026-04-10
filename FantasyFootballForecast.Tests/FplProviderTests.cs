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

    private static FplPublicFootballDataProvider CreateProvider(string bootstrapJson)
    {
        var handler = new StubHttpMessageHandler(bootstrapJson);
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

        public StubHttpMessageHandler(string bootstrapJson) => _bootstrapJson = bootstrapJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath.Contains("fixtures", StringComparison.OrdinalIgnoreCase)
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
        { "id": 2, "name": "Liverpool", "short_name": "LIV", "code": 14, "strength": 92 }
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
}
