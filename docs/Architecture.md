# Architecture

## Overview

`FantasyFootballForecast` is a modular Aspire solution for English Premier League fantasy forecasting.

The current runtime topology is:

- `FantasyFootballForecast.AppHost` orchestrates all local resources.
- `FantasyFootballForecast.Web` provides the Blazor UI.
- `FantasyFootballForecast.Api` exposes application and admin endpoints.
- `FantasyFootballForecast.Worker` performs scheduled data sync and model retraining.
- SQL Server stores operational, historical, and model metadata.

## Project Responsibilities

- `FantasyFootballForecast.Domain`
  - Entity definitions and shared enums.
- `FantasyFootballForecast.Application`
  - Contracts, DTOs, prediction abstractions, and application services.
- `FantasyFootballForecast.Infrastructure`
  - EF Core persistence, schema configuration, seeding, and sync orchestration.
- `FantasyFootballForecast.Integrations`
  - External data provider adapters behind a common provider contract.
- `FantasyFootballForecast.ML`
  - ML.NET training, inference, evaluation logging, and model persistence.
- `FantasyFootballForecast.Api`
  - HTTP surface for read models, sync triggers, and training triggers.
- `FantasyFootballForecast.Web`
  - UI composition and API consumption.
- `FantasyFootballForecast.Worker`
  - Background ingestion and retraining loop.

## Data Flow

1. Provider adapters fetch football, fantasy, availability, and news data.
2. `FootballSyncService` maps provider DTOs into database entities.
3. Availability/news enrichment produces explainable structured availability signals.
4. The API exposes persisted data and prediction results.
5. ML services train from persisted historical rows and write training-run metadata.
6. The Web app reads API endpoints and renders dashboards and admin workflows.

## Provider Strategy

The solution is designed around `IFootballDataProvider`.

Current state:

- `FplPublicFootballDataProvider`
  - Real implementation against public FPL endpoints.
- `TheSportsDbFootballDataProvider`
  - Scaffolded adapter.
- `ApiFootballDataProvider`
  - Scaffolded adapter.

This lets the sync layer stay stable while providers evolve independently.

## Availability and News

Availability is intentionally not NLP-only. The current source-of-truth approach is:

- structured availability data where available
- provider-sourced availability status
- rule-based extraction from reliable news text
- stored source name, source URL, confidence, and raw text

This keeps the system explainable and auditable while leaving room for future NLP or LLM enrichment.

## ML Design

Current models:

- player fantasy point regression
- team match outcome classification

Both models:

- can train on persisted data with synthetic fallback rows for local startup
- save model artifacts to `App_Data/models`
- persist metrics and run records to SQL Server
- can be retrained manually or by the worker

## Extension Points

Recommended next engineering slices:

- historical player match-stat ingestion
- provider-specific news and suspension feeds
- model feature store / richer rolling features
- explicit read-model endpoints for team detail and player detail pages
