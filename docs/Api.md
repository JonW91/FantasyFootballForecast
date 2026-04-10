# API

Base route: `/api`

## Read Endpoints

`GET /api/teams`

- Returns teams ordered by name.

`GET /api/teams/{teamId}`

- Returns a team detail payload with squad, fixtures, and recent match stats.

`GET /api/players?search={text}&teamId={id}`

- Returns players filtered by optional search text and team.

`GET /api/players/{playerId}`

- Returns a player detail payload with team context, availability, news, predictions, and recent match log.

`GET /api/fixtures?upcomingOnly=true`

- Returns fixtures, optionally limited to unfinished fixtures.

`GET /api/predictions?kind={kind}`

- Returns stored predictions, optionally filtered by prediction kind.

`GET /api/availability`

- Returns player availability rows joined with player and team names.

`GET /api/news`

- Returns news items ordered by publish date descending.

`GET /api/model-runs`

- Returns ML training-run history.

`GET /api/ingestion-runs`

- Returns data-ingestion run history.

`GET /api/top-picks?count=10`

- Returns fantasy recommendations ranked by current score.

`GET /api/best-xi`

- Returns a simple best-XI prototype based on current recommendations.

## Mutation / Admin Endpoints

`POST /api/sync/import`

- Triggers sync for all registered providers.

`POST /api/sync/import?provider=FPL%20Public%20API`

- Triggers sync for a single provider by exact provider name.

Supported provider names in the current build:

- `FPL Public API`
- `TheSportsDB`
- `API-Football`
- `all`

`POST /api/models/train`

- Triggers player model training by default.

`POST /api/models/train?model=team`

- Triggers team model training only.

`POST /api/models/train?model=all`

- Triggers full retraining through the shared training service.

`POST /api/models/retrain`

- Retrains all currently configured models.

## Notes

- Health endpoints are added through ServiceDefaults and `MapDefaultEndpoints()`.
- The API seeds and initializes the local database on startup if needed.
- Query shape is intentionally simple for local development and can be expanded into paginated read models later.
