
ssh wirn@192.168.50.14
cd ~/projects/trumpStockAlert

docker compose up -d --build api collector-scheduler
docker compose up -d --build api

ssh -N -L 5432:localhost:5432 wirn@gen-eric-server


# TrumpStockAlert

Monitors Truth Social posts and analyzes their potential financial market impact using OpenAI.

## How it works

1. A **collector** fetches recent posts from Truth Social on a schedule.
2. New posts are saved to a **PostgreSQL** database via the backend API.
3. An **AI analyzer** (OpenAI or mock) scores each post for market impact (1–100), direction, and affected assets.
4. A **React frontend** displays posts and their analyses.

---

## Components

| Directory | Technology | Role |
|---|---|---|
| `backend/` | ASP.NET Core 10 | REST API, database, AI analysis |
| `collector/` | Python | Fetches posts from Truth Social |
| `collector-function/` | Azure Functions v4 (.NET) | Timer that triggers the collector every 5 minutes |
| `frontend/` | React 19 + Vite + TypeScript | Admin and dashboard UI |

---

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- Python 3.10+
- PostgreSQL
- (Optional) Azure Functions Core Tools — only needed to run the timer function locally

---

## Backend

### Configuration

Create `backend/appsettings.Development.json` (never committed):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=trumpstockalert;Username=postgres;Password=yourpassword"
  },
  "Analyzer": {
    "Provider": "OpenAI"
  },
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4o-mini"
  },
  "Scheduler": {
    "ApiKey": "a-random-secret-you-choose"
  },
  "Collector": {
    "TruthSocialUsername": "realDonaldTrump",
    "TruthSocialAccountId": ""
  }
}
```

`Scheduler:ApiKey` protects the `POST /api/collector/run` endpoint. The Azure Function and the frontend both send this key.

If `Analyzer:Provider` is omitted or set to anything other than `"OpenAI"`, the mock analyzer is used (no API calls, deterministic fake scores).

### Run

```powershell
cd backend
dotnet run
# API available at http://localhost:5044
# Swagger UI at http://localhost:5044/swagger
```

### Database migrations

```powershell
cd backend
dotnet ef database update          # apply all pending migrations
dotnet ef migrations add <Name>    # create a new migration
```

---

## Frontend

### Configuration

Create `frontend/.env.local`:

```
VITE_API_BASE_URL=http://localhost:5044
VITE_SCHEDULER_API_KEY=a-random-secret-you-choose
```

`VITE_SCHEDULER_API_KEY` must match `Scheduler:ApiKey` in the backend config.

### Run

```powershell
cd frontend
npm install
npm run dev      # http://localhost:5173
```

---

## Collector

The collector fetches posts from Truth Social and posts them to the backend API. It supports two client backends, switchable via the `COLLECTOR_CLIENT_MODE` environment variable.

### Client modes

| `COLLECTOR_CLIENT_MODE` | Description |
|---|---|
| `truthbrush` (default) | Uses the [Truthbrush](https://github.com/mastodon/truthbrush) Python library to call the Truth Social API directly. Fast, no browser required. May be blocked by Truth Social on cloud IPs. |
| `playwright` | Launches a headless Chromium browser with [playwright-stealth](https://github.com/AtuboDad/playwright_stealth) to load the profile page and intercept the API response. Harder to block, especially on residential IPs. Requires extra dependencies. |

### Switching client mode

Set the environment variable before running:

```powershell
# Use Playwright (recommended for local/residential servers)
$env:COLLECTOR_CLIENT_MODE = "playwright"
python -m collector.main

# Use Truthbrush (default)
$env:COLLECTOR_CLIENT_MODE = "truthbrush"
python -m collector.main
```

Or add it to a `.env` file loaded by your shell or process manager.

### Installing Playwright dependencies

Only required when using `COLLECTOR_CLIENT_MODE=playwright`:

```powershell
cd collector
pip install -e ".[playwright]"
playwright install chromium
```

### Setup (all modes)

```powershell
cd collector
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -e ".[dev]"             # base + dev dependencies
pip install -e ".[dev,playwright]"  # include Playwright
```

### Environment variables

| Variable | Default | Description |
|---|---|---|
| `COLLECTOR_CLIENT_MODE` | `truthbrush` | `truthbrush` or `playwright` |
| `COLLECTOR_STORE_MODE` | `api` | `api` (post to backend) or `json` (write to file) |
| `TRUTH_POST_API_BASE_URL` | `http://localhost:5044` | Backend base URL when using `api` store mode |
| `TRUTH_SOCIAL_USERNAME` | `realDonaldTrump` | Username to fetch posts for |
| `MAX_POSTS` | `10` | Maximum posts to fetch per run |
| `LOOKBACK_MINUTES` | `5` | Only keep posts newer than this many minutes |
| `TRUTH_POSTS_FILE_PATH` | `./data/truth-posts.json` | Output file path when using `json` store mode |

### Run

```powershell
cd collector
python -m collector.main                  # normal run with lookback filter
python -m collector.main --test           # fetch 1 post, skip lookback (good for testing)
python -m collector.main --skip-lookback  # fetch latest N posts without time filter
```

### Tests

```powershell
cd collector
pytest
```

---

## Azure Function (collector-function)

The timer function runs every 5 minutes and calls `POST /api/collector/run` on the backend. It does not run the Python collector directly — the backend handles fetching via its own .NET HTTP client.

### Configuration

`collector-function/local.settings.json` (never committed):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "BackendBaseUrl": "http://localhost:5044",
    "Collector__ApiKey": "a-random-secret-you-choose"
  }
}
```

`Collector__ApiKey` must match `Scheduler:ApiKey` in the backend.

### Run locally

```powershell
cd collector-function
dotnet build
func start
```

---

## API endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Health check |
| `GET` | `/api/truth-posts` | List saved posts (with nested analyses) |
| `GET` | `/api/analyses` | List all analyses |
| `GET` | `/api/fetcher-runs/latest` | Recent collector run log |
| `POST` | `/api/collector/run` | Run the collector (requires `X-TrumpStockAlert-Scheduler-Key` header) |
| `POST` | `/api/analysis/run` | Analyze all pending posts |
| `POST` | `/api/analysis/mock-preview` | Preview mock analysis for a given post content |
| `POST` | `/api/analysis/openai-preview` | Preview OpenAI analysis without saving |
| `POST` | `/api/analysis/prompt-preview` | Preview the AI prompt for a given post content |
