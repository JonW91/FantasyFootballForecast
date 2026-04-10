using System.Net;
using System.Text.Json;
using FantasyFootballForecast.Application;
using FantasyFootballForecast.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FantasyFootballForecast.Integrations;

public abstract class FootballDataProviderBase : IFootballDataProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly IMemoryCache Cache;
    protected readonly ILogger Logger;

    protected FootballDataProviderBase(HttpClient httpClient, IMemoryCache cache, ILogger logger)
    {
        HttpClient = httpClient;
        Cache = cache;
        Logger = logger;
    }

    public abstract string Name { get; }

    public abstract Task<IReadOnlyList<ProviderTeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default);
    public abstract Task<IReadOnlyList<ProviderPlayerDto>> GetPlayersAsync(CancellationToken cancellationToken = default);
    public abstract Task<IReadOnlyList<ProviderFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken = default);
    public abstract Task<IReadOnlyList<ProviderNewsDto>> GetNewsAsync(CancellationToken cancellationToken = default);
    public abstract Task<IReadOnlyList<ProviderAvailabilityDto>> GetAvailabilityAsync(CancellationToken cancellationToken = default);
    public abstract Task<IReadOnlyList<ProviderPlayerMatchStatDto>> GetPlayerMatchStatsAsync(int playerExternalId, CancellationToken cancellationToken = default);

    protected async Task<T> GetCachedJsonAsync<T>(string cacheKey, string relativeUrl, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        using var response = await HttpClient.GetAsync(relativeUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Logger.LogWarning("Provider {Provider} rate limited on {Url}. Retry-After: {RetryAfter}", Name, relativeUrl, response.Headers.RetryAfter);
            if (Cache.TryGetValue(cacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            throw new HttpRequestException($"Provider {Name} returned 429 for {relativeUrl}.");
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
            ?? throw new InvalidOperationException($"Provider {Name} returned an empty payload for {relativeUrl}.");

        Cache.Set(cacheKey, value, ttl);
        return value;
    }
}

public sealed class FplPublicFootballDataProvider : FootballDataProviderBase
{
    public FplPublicFootballDataProvider(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<FplPublicFootballDataProvider> logger)
        : base(httpClientFactory.CreateClient("fpl"), cache, logger)
    {
    }

    public override string Name => "FPL Public API";

    public override async Task<IReadOnlyList<ProviderTeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = await GetCachedJsonAsync<FplBootstrapStatic>("fpl.bootstrap", "bootstrap-static/", TimeSpan.FromMinutes(10), cancellationToken);
        return bootstrap.Teams
            .Select(team => new ProviderTeamDto(
                team.Id,
                team.Name,
                team.ShortName,
                team.Code.ToString(),
                $"https://resources.premierleague.com/premierleague/badges/50/t{team.Code}.png",
                team.Strength,
                Math.Round(team.Strength / 45m, 2),
                Math.Round((100m - team.Strength) / 45m, 2)))
            .ToList();
    }

    public override async Task<IReadOnlyList<ProviderPlayerDto>> GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = await GetCachedJsonAsync<FplBootstrapStatic>("fpl.bootstrap", "bootstrap-static/", TimeSpan.FromMinutes(10), cancellationToken);
        return bootstrap.Elements.Select(player => new ProviderPlayerDto(
                player.Id,
                player.Team,
                player.WebName,
                player.FirstName,
                player.SecondName,
                player.ElementType switch
                {
                    1 => "GK",
                    2 => "DEF",
                    3 => "MID",
                    4 => "FWD",
                    _ => "UNK"
                },
                player.SquadNumber ?? 0,
                player.NowCost / 10m,
                player.SelectedByPercent,
                player.Form,
                player.Minutes,
                player.EventPoints,
                player.GoalsScored,
                player.Assists,
                player.CleanSheets,
                player.YellowCards,
                player.RedCards,
                MapAvailability(player.Status, player.ChanceOfPlayingNextRound),
                (player.ChanceOfPlayingNextRound ?? 100) / 100m,
                player.News))
            .ToList();
    }

    public override async Task<IReadOnlyList<ProviderFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken = default)
    {
        var fixtures = await GetCachedJsonAsync<List<FplFixture>>("fpl.fixtures", "fixtures/", TimeSpan.FromMinutes(5), cancellationToken);
        return fixtures.Select(fixture => new ProviderFixtureDto(
                fixture.Id,
                fixture.Season.ToString().GetHashCode(),
                fixture.Gameweek ?? 0,
                fixture.TeamH,
                fixture.TeamA,
                fixture.KickoffTime ?? DateTime.UtcNow,
                fixture.TeamHScore,
                fixture.TeamAScore,
                fixture.Venue,
                fixture.IsFinished,
                fixture.IsBlankGameweek,
                fixture.IsDoubleGameweek,
                fixture.Status))
            .ToList();
    }

    public override async Task<IReadOnlyList<ProviderNewsDto>> GetNewsAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = await GetCachedJsonAsync<FplBootstrapStatic>("fpl.bootstrap", "bootstrap-static/", TimeSpan.FromMinutes(10), cancellationToken);
        return bootstrap.Elements
            .Where(player => !string.IsNullOrWhiteSpace(player.News))
            .Select(player => new ProviderNewsDto(
                player.Id,
                player.Team,
                DateTime.UtcNow,
                $"{player.WebName} news",
                player.News,
                Name,
                "https://fantasy.premierleague.com/api/bootstrap-static/",
                player.News ?? string.Empty))
            .ToList();
    }

    public override async Task<IReadOnlyList<ProviderAvailabilityDto>> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = await GetCachedJsonAsync<FplBootstrapStatic>("fpl.bootstrap", "bootstrap-static/", TimeSpan.FromMinutes(10), cancellationToken);
        return bootstrap.Elements.Select(player => new ProviderAvailabilityDto(
                player.Id,
                MapAvailability(player.Status, player.ChanceOfPlayingNextRound),
                player.Status is "i" or "d" or "u",
                player.Status == "s",
                (player.ChanceOfPlayingNextRound ?? 100) / 100m,
                player.News,
                player.News is null or "" ? 0.45m : 0.9m,
                Name,
                "https://fantasy.premierleague.com/api/bootstrap-static/",
                player.News ?? string.Empty,
                DateTime.UtcNow))
            .ToList();
    }

    public override async Task<IReadOnlyList<ProviderPlayerMatchStatDto>> GetPlayerMatchStatsAsync(int playerExternalId, CancellationToken cancellationToken = default)
    {
        var bootstrap = await GetCachedJsonAsync<FplBootstrapStatic>("fpl.bootstrap", "bootstrap-static/", TimeSpan.FromMinutes(10), cancellationToken);
        var teamStrengths = bootstrap.Teams.ToDictionary(team => team.Id, team => team.Strength);
        var summary = await GetCachedJsonAsync<FplElementSummary>($"fpl.summary.{playerExternalId}", $"element-summary/{playerExternalId}/", TimeSpan.FromMinutes(30), cancellationToken);

        return summary.History.Select(history => new ProviderPlayerMatchStatDto(
                PlayerExternalId: playerExternalId,
                OpponentTeamExternalId: history.OpponentTeam,
                FixtureExternalId: history.Fixture,
                GameweekNumber: history.Round,
                KickoffUtc: history.KickoffTime ?? DateTime.UtcNow,
                IsHome: history.WasHome,
                MinutesPlayed: history.Minutes,
                Goals: history.GoalsScored,
                Assists: history.Assists,
                CleanSheets: history.CleanSheets,
                Saves: history.Saves,
                BonusPoints: history.Bonus,
                GoalsConceded: history.GoalsConceded,
                YellowCards: history.YellowCards,
                RedCards: history.RedCards,
                FantasyPoints: history.TotalPoints,
                OpponentStrength: teamStrengths.TryGetValue(history.OpponentTeam, out var strength) ? strength : 50m,
                RollingForm: history.TotalPoints,
                PriceAtKickoff: history.Value / 10m))
            .ToList();
    }

    private static AvailabilityStatus MapAvailability(string? status, int? chanceOfPlayingNextRound) => status switch
    {
        "a" => AvailabilityStatus.Available,
        "d" => AvailabilityStatus.Doubtful,
        "i" => AvailabilityStatus.Injured,
        "s" => AvailabilityStatus.Suspended,
        "u" => AvailabilityStatus.RuledOut,
        _ when (chanceOfPlayingNextRound ?? 100) >= 80 => AvailabilityStatus.Available,
        _ when (chanceOfPlayingNextRound ?? 100) >= 50 => AvailabilityStatus.Doubtful,
        _ => AvailabilityStatus.Unknown
    };
}

public sealed class TheSportsDbFootballDataProvider : FootballDataProviderBase
{
    private const int PremierLeagueId = 4328;

    public TheSportsDbFootballDataProvider(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<TheSportsDbFootballDataProvider> logger)
        : base(httpClientFactory.CreateClient("thesportsdb"), cache, logger)
    {
    }

    public override string Name => "TheSportsDB";

    public override async Task<IReadOnlyList<ProviderTeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetCachedJsonAsync<TheSportsDbTeamsResponse>("thesportsdb.teams.premierleague", $"search_all_teams.php?l=English_Premier_League", TimeSpan.FromHours(12), cancellationToken);
        return response.Teams
            .Select(team => new ProviderTeamDto(
                ExternalId: ParseInt(team.IdTeam),
                Name: team.StrTeam,
                ShortName: string.IsNullOrWhiteSpace(team.StrTeamShort) ? team.StrTeam[..Math.Min(team.StrTeam.Length, 3)].ToUpperInvariant() : team.StrTeamShort,
                Code: ParseInt(team.IdTeam).ToString(),
                CrestUrl: team.StrTeamBadge,
                StrengthRating: 50m,
                ExpectedGoalsForPerMatch: 1.5m,
                ExpectedGoalsAgainstPerMatch: 1.2m))
            .ToList();
    }

    public override async Task<IReadOnlyList<ProviderPlayerDto>> GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        var teams = await GetTeamsAsync(cancellationToken);
        var rosterTasks = teams.Select(async team =>
        {
            var roster = await GetCachedJsonAsync<TheSportsDbPlayersResponse>($"thesportsdb.players.{team.ExternalId}", $"lookup_all_players.php?id={team.ExternalId}", TimeSpan.FromHours(12), cancellationToken);
            return roster.Players.Select(player => new ProviderPlayerDto(
                ExternalId: ParseInt(player.IdPlayer),
                ExternalTeamId: team.ExternalId,
                Name: player.StrPlayer,
                FirstName: null,
                LastName: player.StrLastName,
                Position: player.StrPosition ?? player.StrStatus ?? "Unknown",
                ShirtNumber: ParseInt(player.StrNumber, defaultValue: 0),
                Price: 0m,
                OwnershipPercent: 0m,
                Form: 0m,
                MinutesPlayed: 0m,
                RecentPoints: 0m,
                Goals: 0m,
                Assists: 0m,
                CleanSheets: 0m,
                YellowCards: 0m,
                RedCards: 0m,
                AvailabilityStatus: MapAvailability(player.StrStatus),
                ChanceOfPlayingNextRound: player.StrStatus == "Active" ? 1m : 0m,
                ExpectedReturnText: player.StrStatus))
            .ToList();
        });

        var rows = await Task.WhenAll(rosterTasks);
        return rows.SelectMany(row => row).ToList();
    }

    public override async Task<IReadOnlyList<ProviderFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetCachedJsonAsync<TheSportsDbEventsResponse>(
            "thesportsdb.fixtures.past.premierleague",
            $"eventspastleague.php?id={PremierLeagueId}",
            TimeSpan.FromHours(6),
            cancellationToken);

        return response.Events
            .Select(fixture => new ProviderFixtureDto(
                ExternalId: ParseInt(fixture.IdEvent),
                SeasonExternalId: fixture.StrSeason?.GetHashCode(StringComparison.Ordinal) ?? 0,
                GameweekNumber: ParseInt(fixture.IntRound, defaultValue: 0),
                HomeTeamExternalId: ParseInt(fixture.IdHomeTeam),
                AwayTeamExternalId: ParseInt(fixture.IdAwayTeam),
                KickoffUtc: ParseKickoffUtc(fixture.DateEvent, fixture.StrTime, fixture.StrTimestamp),
                HomeScore: ParseNullableInt(fixture.IntHomeScore),
                AwayScore: ParseNullableInt(fixture.IntAwayScore),
                Venue: fixture.StrVenue,
                IsFinished: string.Equals(fixture.StrStatus, "Match Finished", StringComparison.OrdinalIgnoreCase),
                IsBlanked: false,
                IsDoubleGameweek: false,
                Status: fixture.StrStatus))
            .ToList();
    }

    public override Task<IReadOnlyList<ProviderNewsDto>> GetNewsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProviderNewsDto>>([]);

    public override Task<IReadOnlyList<ProviderAvailabilityDto>> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProviderAvailabilityDto>>([]);

    public override async Task<IReadOnlyList<ProviderPlayerMatchStatDto>> GetPlayerMatchStatsAsync(int playerExternalId, CancellationToken cancellationToken = default)
    {
        var playerResults = await GetCachedJsonAsync<TheSportsDbPlayerResultsResponse>(
            $"thesportsdb.playerresults.{playerExternalId}",
            $"playerresults.php?id={playerExternalId}",
            TimeSpan.FromHours(6),
            cancellationToken);

        if (playerResults.Results.Count == 0)
        {
            return [];
        }

        var teams = await GetTeamsAsync(cancellationToken);
        var teamStrengths = teams.ToDictionary(team => team.ExternalId, team => team.StrengthRating);

        var rows = new List<ProviderPlayerMatchStatDto>(playerResults.Results.Count);
        foreach (var result in playerResults.Results)
        {
            var eventDetails = await GetCachedJsonAsync<TheSportsDbEventResponse>(
                $"thesportsdb.event.{result.IdEvent}",
                $"lookupevent.php?id={result.IdEvent}",
                TimeSpan.FromHours(12),
                cancellationToken);

            var match = eventDetails.Events.FirstOrDefault();
            if (match is null)
            {
                continue;
            }

            var playerTeamId = ParseInt(result.IdTeam);
            var homeTeamId = ParseNullableInt(match.IdHomeTeam);
            var awayTeamId = ParseNullableInt(match.IdAwayTeam);
            var isHome = homeTeamId == playerTeamId;
            var opponentTeamId = isHome ? awayTeamId : homeTeamId;
            if (opponentTeamId is null)
            {
                continue;
            }

            rows.Add(new ProviderPlayerMatchStatDto(
                PlayerExternalId: ParseInt(result.IdPlayer),
                OpponentTeamExternalId: opponentTeamId.Value,
                FixtureExternalId: ParseInt(result.IdEvent),
                GameweekNumber: ParseNullableInt(match.IntRound) ?? 0,
                KickoffUtc: ParseKickoffUtc(match.DateEvent, match.StrTime, match.StrTimestamp),
                IsHome: isHome,
                MinutesPlayed: 0,
                Goals: 0,
                Assists: 0,
                CleanSheets: 0,
                Saves: 0,
                BonusPoints: 0,
                GoalsConceded: 0,
                YellowCards: 0,
                RedCards: 0,
                FantasyPoints: ParseDecimal(result.IntPoints),
                OpponentStrength: teamStrengths.TryGetValue(opponentTeamId.Value, out var strength) ? strength : 50m,
                RollingForm: ParseDecimal(result.IntPoints),
                PriceAtKickoff: 0m));
        }

        return rows;
    }

    private static AvailabilityStatus MapAvailability(string? status) => status?.ToLowerInvariant() switch
    {
        "active" => AvailabilityStatus.Available,
        "injured" => AvailabilityStatus.Injured,
        "suspended" => AvailabilityStatus.Suspended,
        "out of squad" => AvailabilityStatus.RuledOut,
        _ => AvailabilityStatus.Unknown
    };

    private static int ParseInt(string? value, int defaultValue = 0)
        => int.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static int? ParseNullableInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static DateTime ParseKickoffUtc(string? dateEvent, string? time, string? timestamp)
    {
        if (!string.IsNullOrWhiteSpace(timestamp) && DateTime.TryParse(timestamp, out var parsedTimestamp))
        {
            return DateTime.SpecifyKind(parsedTimestamp, DateTimeKind.Utc);
        }

        var combined = string.Join(" ", new[] { dateEvent, time }.Where(item => !string.IsNullOrWhiteSpace(item)));
        if (DateTime.TryParse(combined, out var parsedCombined))
        {
            return DateTime.SpecifyKind(parsedCombined, DateTimeKind.Utc);
        }

        return DateTime.UtcNow;
    }

    private static decimal ParseDecimal(string? value, decimal defaultValue = 0m)
        => decimal.TryParse(value, out var parsed) ? parsed : defaultValue;
}

public sealed class ApiFootballDataProvider : FootballDataProviderBase
{
    public ApiFootballDataProvider(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<ApiFootballDataProvider> logger)
        : base(httpClientFactory.CreateClient("api-football"), cache, logger)
    {
    }

    public override string Name => "API-Football";

    public override Task<IReadOnlyList<ProviderTeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProviderTeamDto>>([]);

    public override Task<IReadOnlyList<ProviderPlayerDto>> GetPlayersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProviderPlayerDto>>([]);

    public override Task<IReadOnlyList<ProviderFixtureDto>> GetFixturesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProviderFixtureDto>>([]);

    public override Task<IReadOnlyList<ProviderNewsDto>> GetNewsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProviderNewsDto>>([]);

    public override Task<IReadOnlyList<ProviderAvailabilityDto>> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProviderAvailabilityDto>>([]);

    public override Task<IReadOnlyList<ProviderPlayerMatchStatDto>> GetPlayerMatchStatsAsync(int playerExternalId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProviderPlayerMatchStatDto>>([]);
}

file sealed class FplBootstrapStatic
{
    [System.Text.Json.Serialization.JsonPropertyName("teams")]
    public List<FplTeam> Teams { get; set; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("elements")]
    public List<FplPlayer> Elements { get; set; } = [];
}

file sealed class FplElementSummary
{
    [System.Text.Json.Serialization.JsonPropertyName("history")]
    public List<FplPlayerHistory> History { get; set; } = [];
}

file sealed record FplTeam(
    int Id,
    string Name,
    string ShortName,
    int Code,
    decimal Strength);

file sealed record FplPlayer(
    int Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("web_name")] string WebName,
    [property: System.Text.Json.Serialization.JsonPropertyName("first_name")] string FirstName,
    [property: System.Text.Json.Serialization.JsonPropertyName("second_name")] string SecondName,
    [property: System.Text.Json.Serialization.JsonPropertyName("element_type")] int ElementType,
    [property: System.Text.Json.Serialization.JsonPropertyName("team")] int Team,
    [property: System.Text.Json.Serialization.JsonPropertyName("squad_number")] int? SquadNumber,
    [property: System.Text.Json.Serialization.JsonPropertyName("now_cost")] decimal NowCost,
    [property: System.Text.Json.Serialization.JsonPropertyName("selected_by_percent")] decimal SelectedByPercent,
    [property: System.Text.Json.Serialization.JsonPropertyName("form")] decimal Form,
    [property: System.Text.Json.Serialization.JsonPropertyName("minutes")] decimal Minutes,
    [property: System.Text.Json.Serialization.JsonPropertyName("event_points")] decimal EventPoints,
    [property: System.Text.Json.Serialization.JsonPropertyName("goals_scored")] decimal GoalsScored,
    [property: System.Text.Json.Serialization.JsonPropertyName("assists")] decimal Assists,
    [property: System.Text.Json.Serialization.JsonPropertyName("clean_sheets")] decimal CleanSheets,
    [property: System.Text.Json.Serialization.JsonPropertyName("yellow_cards")] decimal YellowCards,
    [property: System.Text.Json.Serialization.JsonPropertyName("red_cards")] decimal RedCards,
    [property: System.Text.Json.Serialization.JsonPropertyName("chance_of_playing_next_round")] int? ChanceOfPlayingNextRound,
    [property: System.Text.Json.Serialization.JsonPropertyName("status")] string? Status,
    [property: System.Text.Json.Serialization.JsonPropertyName("news")] string? News);

file sealed record FplFixture(
    int Id,
    int Season,
    [property: System.Text.Json.Serialization.JsonPropertyName("event")] int? Gameweek,
    [property: System.Text.Json.Serialization.JsonPropertyName("team_h")] int TeamH,
    [property: System.Text.Json.Serialization.JsonPropertyName("team_a")] int TeamA,
    [property: System.Text.Json.Serialization.JsonPropertyName("kickoff_time")] DateTime? KickoffTime,
    [property: System.Text.Json.Serialization.JsonPropertyName("team_h_score")] int? TeamHScore,
    [property: System.Text.Json.Serialization.JsonPropertyName("team_a_score")] int? TeamAScore,
    [property: System.Text.Json.Serialization.JsonPropertyName("venue")] string? Venue,
    [property: System.Text.Json.Serialization.JsonPropertyName("finished")] bool IsFinished,
    [property: System.Text.Json.Serialization.JsonPropertyName("is_bgw")] bool IsBlankGameweek,
    [property: System.Text.Json.Serialization.JsonPropertyName("is_dgw")] bool IsDoubleGameweek,
    [property: System.Text.Json.Serialization.JsonPropertyName("status")] string? Status);

file sealed record FplPlayerHistory(
    [property: System.Text.Json.Serialization.JsonPropertyName("fixture")] int Fixture,
    [property: System.Text.Json.Serialization.JsonPropertyName("round")] int Round,
    [property: System.Text.Json.Serialization.JsonPropertyName("opponent_team")] int OpponentTeam,
    [property: System.Text.Json.Serialization.JsonPropertyName("kickoff_time")] DateTime? KickoffTime,
    [property: System.Text.Json.Serialization.JsonPropertyName("was_home")] bool WasHome,
    [property: System.Text.Json.Serialization.JsonPropertyName("minutes")] int Minutes,
    [property: System.Text.Json.Serialization.JsonPropertyName("goals_scored")] int GoalsScored,
    [property: System.Text.Json.Serialization.JsonPropertyName("assists")] int Assists,
    [property: System.Text.Json.Serialization.JsonPropertyName("clean_sheets")] int CleanSheets,
    [property: System.Text.Json.Serialization.JsonPropertyName("goals_conceded")] int GoalsConceded,
    [property: System.Text.Json.Serialization.JsonPropertyName("saves")] int Saves,
    [property: System.Text.Json.Serialization.JsonPropertyName("bonus")] int Bonus,
    [property: System.Text.Json.Serialization.JsonPropertyName("yellow_cards")] int YellowCards,
    [property: System.Text.Json.Serialization.JsonPropertyName("red_cards")] int RedCards,
    [property: System.Text.Json.Serialization.JsonPropertyName("total_points")] decimal TotalPoints,
    [property: System.Text.Json.Serialization.JsonPropertyName("value")] decimal Value);

file sealed record TheSportsDbTeamsResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("teams")] List<TheSportsDbTeam> Teams);

file sealed record TheSportsDbPlayersResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("player")] List<TheSportsDbPlayer> Players);

file sealed record TheSportsDbEventsResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("events")] List<TheSportsDbEvent> Events);

file sealed record TheSportsDbEventResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("events")] List<TheSportsDbEvent> Events);

file sealed record TheSportsDbPlayerResultsResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("results")] List<TheSportsDbPlayerResult> Results);

file sealed record TheSportsDbTeam(
    [property: System.Text.Json.Serialization.JsonPropertyName("idTeam")] string IdTeam,
    [property: System.Text.Json.Serialization.JsonPropertyName("strTeam")] string StrTeam,
    [property: System.Text.Json.Serialization.JsonPropertyName("strTeamShort")] string? StrTeamShort,
    [property: System.Text.Json.Serialization.JsonPropertyName("strTeamBadge")] string? StrTeamBadge);

file sealed record TheSportsDbPlayer(
    [property: System.Text.Json.Serialization.JsonPropertyName("idPlayer")] string IdPlayer,
    [property: System.Text.Json.Serialization.JsonPropertyName("strPlayer")] string StrPlayer,
    [property: System.Text.Json.Serialization.JsonPropertyName("strLastName")] string? StrLastName,
    [property: System.Text.Json.Serialization.JsonPropertyName("strNumber")] string? StrNumber,
    [property: System.Text.Json.Serialization.JsonPropertyName("strPosition")] string? StrPosition,
    [property: System.Text.Json.Serialization.JsonPropertyName("strStatus")] string? StrStatus);

file sealed record TheSportsDbEvent(
    [property: System.Text.Json.Serialization.JsonPropertyName("idEvent")] string IdEvent,
    [property: System.Text.Json.Serialization.JsonPropertyName("strTimestamp")] string? StrTimestamp,
    [property: System.Text.Json.Serialization.JsonPropertyName("dateEvent")] string? DateEvent,
    [property: System.Text.Json.Serialization.JsonPropertyName("strTime")] string? StrTime,
    [property: System.Text.Json.Serialization.JsonPropertyName("strEvent")] string StrEvent,
    [property: System.Text.Json.Serialization.JsonPropertyName("strHomeTeam")] string StrHomeTeam,
    [property: System.Text.Json.Serialization.JsonPropertyName("strAwayTeam")] string StrAwayTeam,
    [property: System.Text.Json.Serialization.JsonPropertyName("intHomeScore")] string? IntHomeScore,
    [property: System.Text.Json.Serialization.JsonPropertyName("intAwayScore")] string? IntAwayScore,
    [property: System.Text.Json.Serialization.JsonPropertyName("intRound")] string? IntRound,
    [property: System.Text.Json.Serialization.JsonPropertyName("strSeason")] string? StrSeason,
    [property: System.Text.Json.Serialization.JsonPropertyName("strVenue")] string? StrVenue,
    [property: System.Text.Json.Serialization.JsonPropertyName("strStatus")] string? StrStatus,
    [property: System.Text.Json.Serialization.JsonPropertyName("idHomeTeam")] string IdHomeTeam,
    [property: System.Text.Json.Serialization.JsonPropertyName("idAwayTeam")] string IdAwayTeam);

file sealed record TheSportsDbPlayerResult(
    [property: System.Text.Json.Serialization.JsonPropertyName("idResult")] string IdResult,
    [property: System.Text.Json.Serialization.JsonPropertyName("idPlayer")] string IdPlayer,
    [property: System.Text.Json.Serialization.JsonPropertyName("strPlayer")] string StrPlayer,
    [property: System.Text.Json.Serialization.JsonPropertyName("idTeam")] string IdTeam,
    [property: System.Text.Json.Serialization.JsonPropertyName("idEvent")] string IdEvent,
    [property: System.Text.Json.Serialization.JsonPropertyName("strEvent")] string StrEvent,
    [property: System.Text.Json.Serialization.JsonPropertyName("strResult")] string? StrResult,
    [property: System.Text.Json.Serialization.JsonPropertyName("intPosition")] string? IntPosition,
    [property: System.Text.Json.Serialization.JsonPropertyName("intPoints")] string? IntPoints,
    [property: System.Text.Json.Serialization.JsonPropertyName("strDetail")] string? StrDetail,
    [property: System.Text.Json.Serialization.JsonPropertyName("dateEvent")] string? DateEvent,
    [property: System.Text.Json.Serialization.JsonPropertyName("strSeason")] string? StrSeason,
    [property: System.Text.Json.Serialization.JsonPropertyName("strCountry")] string? StrCountry,
    [property: System.Text.Json.Serialization.JsonPropertyName("strSport")] string? StrSport);
