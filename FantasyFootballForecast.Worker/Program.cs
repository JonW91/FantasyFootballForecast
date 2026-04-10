using FantasyFootballForecast.Worker;
using FantasyFootballForecast.Infrastructure;
using FantasyFootballForecast.Integrations;
using FantasyFootballForecast.ML;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIntegrations(builder.Configuration);
builder.Services.AddMachineLearning();
builder.Services.AddOptions<IngestionOptions>()
    .Bind(builder.Configuration.GetSection(IngestionOptions.SectionName))
    .Validate(options => options.IntervalMinutes > 0, "Ingestion interval must be greater than zero.");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
