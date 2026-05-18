# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Components

Four separate components live in the repo root:

| Directory             | Technology                               | Purpose                                            |
| --------------------- | ---------------------------------------- | -------------------------------------------------- |
| `backend/`            | ASP.NET Core 10 (C#)                     | REST API, data persistence, AI analysis            |
| `collector/`          | Python                                   | Fetches posts from Truth Social                    |
| `collector-function/` | Azure Functions v4 (C#, isolated worker) | Timer trigger that calls the backend on a schedule |
| `frontend/`           | React 19, Vite, TypeScript, SCSS         | Admin/dashboard SPA                                |

## Commands

### Backend

```powershell
cd backend
dotnet run                         # start API on http://localhost:5044
dotnet build
dotnet ef database update          # apply EF Core migrations
dotnet ef migrations add <Name>    # create a new migration
```

The backend requires a PostgreSQL connection string in `appsettings.Development.json` or user secrets:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=...;Username=...;Password=..."
  }
}
```

### Frontend

```powershell
cd frontend
npm install
npm run dev      # Vite dev server on http://localhost:5173
npm run build
npm run lint
```

Create `frontend/.env.local` for local overrides:

```
VITE_API_BASE_URL=http://localhost:5044
VITE_SCHEDULER_API_KEY=<your-scheduler-api-key>
```

when using typescript, always use strict mode

### Python collector

```powershell
cd collector
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -e ".[dev]"
python -m collector.main --test      # fetch 1 post, no lookback filter
python -m collector.main             # normal run with lookback window
pytest                               # run tests
```

### Azure Function

```powershell
cd collector-function
dotnet build
func start     # requires Azure Functions Core Tools
```

## Architecture and data flow

### Happy path

1. **Azure Function** (`collector-function/CollectorTimerFunction.cs`) fires every 5 minutes and calls `POST /api/collector/run` with an `x-api-key` header.
2. **`CollectorController`** verifies the `X-TrumpStockAlert-Scheduler-Key` header against `Scheduler:ApiKey` config (timing-safe compare).
3. **`CollectorRunner`** calls `TruthSocialCollectorClient` directly against the Truth Social Mastodon-compatible API (`/api/v1/accounts/{id}/statuses`) and saves new posts via `TruthPostService`.
4. **`POST /api/analysis/run`** triggers `PostAnalysisRunner`, which finds all posts without a `PostAnalysis`, passes each to `IMarketImpactAnalyzer`, and saves results.
5. The **frontend** polls the backend REST endpoints to display posts and analyses.

### Analyzer selection

`IMarketImpactAnalyzer` is factory-resolved in `Program.cs` based on `Analyzer:Provider` config:

- `"OpenAI"` → `OpenAiMarketImpactAnalyzer` (calls OpenAI Chat Completions with structured JSON output; up to 3 retries on transient errors)
- anything else → `MockMarketImpactAnalyzer` (returns deterministic fake data)

### Dual collector implementations

There are **two parallel collector paths**:

- `CollectorRunner` + `TruthSocialCollectorClient`: pure .NET HTTP client, used for the scheduled/production flow.
- `CollectorProcessRunner`: spawns a PowerShell subprocess to run the Python `collector/` package, used by `CollectorController.RunCollectorTestMode` in Development only.

The Python collector (`collector/`) is a standalone package. It supports `--test` (1 post, no lookback) and `--skip-lookback` flags, and can write to a JSON file or call the backend API (`COLLECTOR_STORE_MODE=api`).

### Database schema

PostgreSQL via EF Core. Four tables (snake_case names):

- `truth_posts` — raw post content + metadata; unique index on `(source, external_id)`
- `post_analyses` — one-to-one with `truth_posts`; `MarketImpactScore` and `Confidence` constrained 1–100
- `alerts` — many-to-one with `truth_posts` and `post_analyses`
- `fetcher_runs` — audit log for each collector invocation

Migrations live in `backend/Data/Migrations/`.

## Key configuration

All backend config follows ASP.NET Core conventions (appsettings → env vars with `__` separator):

| Key                                   | Notes                                                  |
| ------------------------------------- | ------------------------------------------------------ |
| `ConnectionStrings:DefaultConnection` | PostgreSQL                                             |
| `Analyzer:Provider`                   | `"OpenAI"` or omit for mock                            |
| `OpenAI:ApiKey`                       | Store in user secrets locally                          |
| `OpenAI:Model`                        | e.g. `gpt-4o-mini`                                     |
| `Scheduler:ApiKey`                    | Required for `POST /api/collector/run`                 |
| `Collector:TruthSocialUsername`       | e.g. `realDonaldTrump`                                 |
| `Collector:TruthSocialAccountId`      | Optional; skips account lookup API call                |
| `Collector:MaxPosts`                  | Default 10                                             |
| `Cors:AllowedOrigins`                 | Comma-separated or array; defaults to `localhost:5173` |

For the Azure Function, set `BackendBaseUrl` and `Collector:ApiKey` in `local.settings.json` or Azure App Settings.
