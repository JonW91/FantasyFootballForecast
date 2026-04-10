using FantasyFootballForecast.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyFootballForecast.Integrations;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddHttpClient("fpl", client =>
        {
            client.BaseAddress = new Uri(configuration["Providers:Fpl:BaseUrl"] ?? "https://fantasy.premierleague.com/api/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FantasyFootballForecast/1.0");
        });

        services.AddHttpClient("thesportsdb", client =>
        {
            client.BaseAddress = new Uri(configuration["Providers:TheSportsDb:BaseUrl"] ?? "https://www.thesportsdb.com/api/v1/json/");
        });

        services.AddHttpClient("api-football", client =>
        {
            client.BaseAddress = new Uri(configuration["Providers:ApiFootball:BaseUrl"] ?? "https://v3.football.api-sports.io/");
        });

        services.AddSingleton<IFootballDataProvider, FplPublicFootballDataProvider>();
        services.AddSingleton<IFootballDataProvider, TheSportsDbFootballDataProvider>();
        services.AddSingleton<IFootballDataProvider, ApiFootballDataProvider>();

        return services;
    }
}
