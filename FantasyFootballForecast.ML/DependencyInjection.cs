using FantasyFootballForecast.Application;
using FantasyFootballForecast.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyFootballForecast.ML;

public static class DependencyInjection
{
    public static IServiceCollection AddMachineLearning(this IServiceCollection services)
    {
        services.AddScoped<PlayerFantasyPointPredictionService>();
        services.AddScoped<TeamMatchPredictionService>();
        services.AddScoped<IPlayerFantasyPointPredictor>(provider => provider.GetRequiredService<PlayerFantasyPointPredictionService>());
        services.AddScoped<ITeamMatchPredictor>(provider => provider.GetRequiredService<TeamMatchPredictionService>());
        services.AddScoped<IModelTrainingService, ModelTrainingService>();
        return services;
    }
}
