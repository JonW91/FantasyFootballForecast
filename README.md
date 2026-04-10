# FantasyFootballForecast

Aspire-orchestrated EPL fantasy football starter built with:

- .NET 10 / C#
- Aspire AppHost + ServiceDefaults
- Blazor Web App frontend
- ASP.NET Core Web API backend
- SQL Server + EF Core code-first persistence
- ML.NET model training and inference
- NUnit unit and integration tests

Additional documentation:

- [`docs/Architecture.md`](C:/Users/Jon/source/repos/FantasyFootballForecast/docs/Architecture.md)
- [`docs/Api.md`](C:/Users/Jon/source/repos/FantasyFootballForecast/docs/Api.md)
- [`docs/Operations.md`](C:/Users/Jon/source/repos/FantasyFootballForecast/docs/Operations.md)

## Solution layout

- `FantasyFootballForecast.AppHost` - local orchestration for SQL Server, API, Web, and Worker
- `FantasyFootballForecast.Api` - API endpoints, sync, predictions, and model triggers
- `FantasyFootballForecast.Web` - Blazor UI
- `FantasyFootballForecast.Worker` - optional background ingestion/retraining loop
- `FantasyFootballForecast.Application` - contracts and application services
- `FantasyFootballForecast.Domain` - entities and domain primitives
- `FantasyFootballForecast.Infrastructure` - EF Core, database initialization, sync orchestration
- `FantasyFootballForecast.Integrations` - external football provider adapters
- `FantasyFootballForecast.ML` - ML.NET training and inference services
- `FantasyFootballForecast.Tests` - NUnit unit tests
- `FantasyFootballForecast.IntegrationTests` - API smoke tests

## Local run

1. Restore and build:

```powershell
dotnet restore
dotnet build
```

2. Start Aspire:

```powershell
dotnet run --project .\FantasyFootballForecast.AppHost\FantasyFootballForecast.AppHost.csproj
```

3. Open the Aspire dashboard and start the resources. The API will initialize and seed the local database on first run.

The default local topology is:

- `web` calls `api` through Aspire service discovery
- `api` and `worker` share the SQL Server database resource
- `worker` can run scheduled sync and retraining cycles in the background

## Database

The infrastructure project is configured for SQL Server and supports code-first migrations. The repo includes a local `dotnet-ef` tool manifest.

To create or update migrations:

```powershell
dotnet tool restore
dotnet ef migrations add InitialCreate `
  --project .\FantasyFootballForecast.Infrastructure\FantasyFootballForecast.Infrastructure.csproj `
  --startup-project .\FantasyFootballForecast.Api\FantasyFootballForecast.Api.csproj `
  --context FantasyFootballForecast.Infrastructure.Persistence.FantasyFootballForecastDbContext
```

To apply migrations manually:

```powershell
dotnet ef database update `
  --project .\FantasyFootballForecast.Infrastructure\FantasyFootballForecast.Infrastructure.csproj `
  --startup-project .\FantasyFootballForecast.Api\FantasyFootballForecast.Api.csproj `
  --context FantasyFootballForecast.Infrastructure.Persistence.FantasyFootballForecastDbContext
```

## Configuration

Use `appsettings.Development.json`, user secrets, or environment variables for:

- `ConnectionStrings:fantasyfootballforecast`
- `FootballProviders:TheSportsDb:ApiKey`
- `FootballProviders:ApiFootball:ApiKey`
- `Ingestion:Enabled`
- `Ingestion:IntervalMinutes`
- `Ingestion:Providers`

The FPL provider uses the public Fantasy Premier League endpoints and does not require an API key.

Example user-secrets setup:

```powershell
dotnet user-secrets init --project .\FantasyFootballForecast.Api\FantasyFootballForecast.Api.csproj
dotnet user-secrets set "FootballProviders:ApiFootball:ApiKey" "replace-me" --project .\FantasyFootballForecast.Api\FantasyFootballForecast.Api.csproj
dotnet user-secrets set "FootballProviders:TheSportsDb:ApiKey" "replace-me" --project .\FantasyFootballForecast.Api\FantasyFootballForecast.Api.csproj
```

## API and UI

The API exposes endpoints for:

- teams, players, fixtures, predictions
- player availability and news
- ingestion runs and model training runs
- sync/import and model training triggers
- top picks and best-XI recommendations

The Blazor UI provides:

- dashboard and summary cards
- teams, players, fixtures, and predictions pages
- injury and news views
- model status view
- admin page for sync and retraining operations

## Next steps

- Add a fuller historical import pipeline for fixtures and player match stats.
- Expand TheSportsDB and API-Football adapters behind the shared provider contract.
- Replace the rule-based availability extractor with a richer NLP/LLM enrichment stage if needed.
- Add more baseline models and calibration logic once enough historical data is loaded.
