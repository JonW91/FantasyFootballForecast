using System.Net.Http.Json;
using FantasyFootballForecast.Application;

namespace FantasyFootballForecast.Web.Services;

public sealed class FootballApiClient
{
    private readonly HttpClient _httpClient;

    public FootballApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<List<TeamDto>?> GetTeamsAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<TeamDto>>("api/teams", cancellationToken);

    public Task<TeamDetailDto?> GetTeamAsync(int teamId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TeamDetailDto>($"api/teams/{teamId}", cancellationToken);

    public Task<List<PlayerDto>?> GetPlayersAsync(string? search = null, int? teamId = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<PlayerDto>>(BuildUrl("api/players", ("search", search), ("teamId", teamId?.ToString())), cancellationToken);

    public Task<PlayerDetailDto?> GetPlayerAsync(int playerId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PlayerDetailDto>($"api/players/{playerId}", cancellationToken);

    public Task<List<FixtureDto>?> GetFixturesAsync(bool upcomingOnly = false, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<FixtureDto>>(BuildUrl("api/fixtures", ("upcomingOnly", upcomingOnly ? "true" : null)), cancellationToken);

    public Task<List<PredictionDto>?> GetPredictionsAsync(string? kind = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<PredictionDto>>(BuildUrl("api/predictions", ("kind", kind)), cancellationToken);

    public Task<List<AvailabilityDto>?> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<AvailabilityDto>>("api/availability", cancellationToken);

    public Task<List<NewsItemDto>?> GetNewsAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<NewsItemDto>>("api/news", cancellationToken);

    public Task<List<ModelTrainingRunDto>?> GetModelRunsAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<ModelTrainingRunDto>>("api/model-runs", cancellationToken);

    public Task<List<DataIngestionRunDto>?> GetIngestionRunsAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<DataIngestionRunDto>>("api/ingestion-runs", cancellationToken);

    public Task<List<FantasyPickDto>?> GetTopPicksAsync(int count = 10, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<FantasyPickDto>>(BuildUrl("api/top-picks", ("count", count.ToString())), cancellationToken);

    public Task<List<FantasyPickDto>?> GetBestXiAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<FantasyPickDto>>("api/best-xi", cancellationToken);

    public Task<ModelTrainingSummaryDto?> TrainAsync(string? model = null, CancellationToken cancellationToken = default)
        => PostAsync<ModelTrainingSummaryDto>(BuildUrl("api/models/train", ("model", model)), cancellationToken);

    public Task<ModelTrainingSummaryDto?> RetrainAsync(CancellationToken cancellationToken = default)
        => PostAsync<ModelTrainingSummaryDto>("api/models/retrain", cancellationToken);

    public Task<DataIngestionRunDto?> SyncAsync(string? provider = null, CancellationToken cancellationToken = default)
        => PostAsync<DataIngestionRunDto>(BuildUrl("api/sync/import", ("provider", provider)), cancellationToken);

    private async Task<T?> PostAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(url, content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private static string BuildUrl(string baseUrl, params (string Key, string? Value)[] parameters)
    {
        var query = string.Join("&", parameters.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!)}"));
        return string.IsNullOrWhiteSpace(query) ? baseUrl : $"{baseUrl}?{query}";
    }
}
