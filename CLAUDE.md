# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Components

Four separate components live in the repo root:

| Directory             | Technology                               | Purpose                                            |
| --------------------- | ---------------------------------------- | -------------------------------------------------- |
| `backend/`            | ASP.NET Core 10 (C#)                     | REST API, data persistence, AI analysis            |
| `collector/`          | Python                                   | Fetches posts from Truth Social                    |
| `collector-scheduler/` | Docker + shell                          | Runs collector, analysis, and alerts on a schedule |
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

## Architecture and data flow

### Happy path

1. **`collector-scheduler`** waits for API health, then runs the Python collector container with `docker compose run --rm --no-deps collector`.
2. The **Python Playwright collector** fetches Truth Social posts and saves them through the backend API.
3. If collection succeeds, the scheduler calls `POST /api/analyses/run` with `X-TrumpStockAlert-Scheduler-Key`.
4. If analysis succeeds, the scheduler calls `POST /api/alerts/run` with the same scheduler key.
5. The **frontend** polls the backend REST endpoints to display posts and analyses.

### Analyzer selection

`IMarketImpactAnalyzer` is factory-resolved in `Program.cs` based on `Analyzer:Provider` config:

- `"OpenAI"` → `OpenAiMarketImpactAnalyzer` (calls OpenAI Chat Completions with structured JSON output; up to 3 retries on transient errors)
- anything else → `MockMarketImpactAnalyzer` (returns deterministic fake data)

### Dual collector implementations

There are **two collector paths**:

- `collector/` Python package: used by the Docker scheduler for the scheduled flow.
- `CollectorRunner` + `TruthSocialCollectorClient`: pure .NET HTTP client, reachable through manual/admin API calls.
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
| `Scheduler:ApiKey`                    | Required for protected scheduler/admin endpoints       |
| `Collector:TruthSocialUsername`       | e.g. `realDonaldTrump`                                 |
| `Collector:TruthSocialAccountId`      | Optional; skips account lookup API call                |
| `Collector:MaxPosts`                  | Default 10                                             |
| `Cors:AllowedOrigins`                 | Comma-separated or array; defaults to `localhost:5173` |
