# Operations

## Local Development

Build:

```powershell
dotnet tool restore
dotnet build FantasyFootballForecast.slnx
```

Run through Aspire:

```powershell
dotnet run --project .\FantasyFootballForecast.AppHost\FantasyFootballForecast.AppHost.csproj
```

## Background Worker

The worker supports configuration through `Ingestion` settings:

- `Ingestion:Enabled`
  - Enables or disables recurring cycles.
- `Ingestion:IntervalMinutes`
  - Delay between cycles.
- `Ingestion:Providers`
  - Optional list of provider names to sync. If omitted, the worker syncs all providers.

Example:

```json
{
  "Ingestion": {
    "Enabled": true,
    "IntervalMinutes": 360,
    "Providers": [ "FPL Public API" ]
  }
}
```

Each cycle performs:

1. database initialization
2. provider sync
3. full model retraining

During sync, the current implementation also records:

- current price and ownership snapshots for each player
- current-season player match history from the FPL `element-summary` endpoint
- team match rows derived from completed fixtures

The admin console also exposes a historical backfill action that skips the live-news and availability feeds and focuses on fixtures plus player match history for deeper retrospective coverage.

## Migrations

Add a migration:

```powershell
dotnet tool restore
dotnet ef migrations add AddSomething `
  --project .\FantasyFootballForecast.Infrastructure\FantasyFootballForecast.Infrastructure.csproj `
  --startup-project .\FantasyFootballForecast.Api\FantasyFootballForecast.Api.csproj `
  --context FantasyFootballForecast.Infrastructure.Persistence.FantasyFootballForecastDbContext
```

Update the database:

```powershell
dotnet ef database update `
  --project .\FantasyFootballForecast.Infrastructure\FantasyFootballForecast.Infrastructure.csproj `
  --startup-project .\FantasyFootballForecast.Api\FantasyFootballForecast.Api.csproj `
  --context FantasyFootballForecast.Infrastructure.Persistence.FantasyFootballForecastDbContext
```

## Tests

Run unit tests:

```powershell
dotnet test .\FantasyFootballForecast.Tests\FantasyFootballForecast.Tests.csproj
```

Run integration tests:

```powershell
dotnet test .\FantasyFootballForecast.IntegrationTests\FantasyFootballForecast.IntegrationTests.csproj
```

## Current Operational Caveats

- Only the FPL provider currently returns live data.
- The worker retrains both models after each sync cycle.
- The current UI is intended as a starter operator console, not a full production dashboard yet.
