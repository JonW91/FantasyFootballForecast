using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithDataVolume("football-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = sql.AddDatabase("fantasyfootballforecast");

var api = builder.AddProject<Projects.FantasyFootballForecast_Api>("api")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.FantasyFootballForecast_Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.AddProject<Projects.FantasyFootballForecast_Worker>("worker")
    .WithReference(database)
    .WithReference(api)
    .WaitFor(api)
    .WaitFor(database);

builder.Build().Run();
