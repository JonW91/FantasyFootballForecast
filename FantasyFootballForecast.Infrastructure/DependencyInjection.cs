using FantasyFootballForecast.Application;
using FantasyFootballForecast.Infrastructure.Persistence;
using FantasyFootballForecast.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyFootballForecast.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FantasyFootballForecastDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("fantasyfootballforecast")
                ?? configuration["ConnectionStrings:fantasyfootballforecast"]
                ?? "Server=(localdb)\\MSSQLLocalDB;Database=FantasyFootballForecast;Trusted_Connection=True;TrustServerCertificate=True";

            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            });
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<FantasyFootballForecastDbContext>());
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IFantasyRecommendationService, FantasyRecommendationService>();
        services.AddScoped<IAvailabilityEnrichmentService, RuleBasedAvailabilityEnrichmentService>();
        services.AddScoped<IFootballSyncService, FootballSyncService>();
        return services;
    }
}
