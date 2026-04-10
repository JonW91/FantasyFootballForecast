using FantasyFootballForecast.Application;
using FantasyFootballForecast.Api;
using FantasyFootballForecast.Infrastructure;
using FantasyFootballForecast.Infrastructure.Persistence;
using FantasyFootballForecast.Integrations;
using FantasyFootballForecast.ML;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIntegrations(builder.Configuration);
builder.Services.AddMachineLearning();

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapDefaultEndpoints();
await InitializeDatabaseAsync(app);

var api = app.MapGroup("/api");
api.MapFantasyFootballEndpoints();

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync();
}

public partial class Program;
