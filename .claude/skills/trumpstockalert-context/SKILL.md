---
name: trumpstockalert-context
description: Project context for TrumpStockAlert. Use when working in this repository, planning changes, debugging, writing documentation, or explaining architecture.
---

# TrumpStockAlert Project Context

TrumpStockAlert monitors Donald Trump's Truth Social posts, stores new posts, analyzes potential market impact with AI, and sends/logs alerts when impact is above a configured threshold.

## Core goals

- Fetch new Truth Social posts regularly.
- Store posts and collector run history.
- Analyze posts for market impact.
- Score each post from 1–100.
- Include likely market direction when relevant.
- Avoid duplicate posts and duplicate alerts.
- Keep hosting cost very low.
- Prefer simple, observable, maintainable solutions.

## Current architecture

The system is self-hosted on a home Ubuntu server and runs mainly through Docker Compose.

Main services:

- `.NET API`
- `PostgreSQL database`
- `Python collector`
- `collector scheduler`

The collector scheduler calls the API endpoint:

```text
POST /api/collector/run
```

The endpoint is protected by the header:

```text
X-TrumpStockAlert-Scheduler-Key
```

The value comes from configuration:

```text
Scheduler:ApiKey
```

In Docker Compose, use environment variable format:

```text
Scheduler__ApiKey
```

## Technologies

- .NET API
- PostgreSQL
- Entity Framework Core
- Python collector
- Docker
- Docker Compose
- AI analysis through an LLM provider
- Swagger for API testing
- Ubuntu server for self-hosting

## Collector

The collector is responsible for fetching Truth Social posts.

Current/known provider options:

- `truthbrush`
- possible future `playwright` provider

When changing collector behavior:

- Keep provider selection configurable.
- Do not remove the existing provider unless explicitly asked.
- Prefer config-driven switching, for example:

```text
COLLECTOR_PROVIDER=truthbrush
COLLECTOR_PROVIDER=playwright
```

- Keep failures isolated.
- Log clear error messages.
- Include counts for fetched, inserted, duplicate, and failed posts.
- Avoid hardcoded credentials, API keys, cookies, or secrets.

## Common endpoints

Known endpoints include:

```text
GET /health
POST /api/collector/run
GET /api/truth-posts
GET /api/analyses
```

Swagger may be available for local or deployed API testing.

## Docker conventions

Prefer using Docker Compose for local/server execution.

Useful commands:

```bash
docker compose ps
docker compose logs api --tail=100
docker compose logs collector-scheduler --tail=100
docker compose logs postgres --tail=100
docker compose up -d --build
```

Keep services separated:

- API
- database
- collector
- collector scheduler

Avoid mixing scheduler logic directly into the API unless explicitly requested.

## Database

PostgreSQL is used for self-hosted runtime.

The database should store at least:

- Truth Social posts
- analysis results
- collector/fetcher run history
- alert notification history, if alerts are enabled

When adding database changes:

- Prefer EF Core migrations.
- Preserve existing data.
- Avoid destructive schema changes unless explicitly requested.
- Add indexes for lookup-heavy fields when useful.
- Keep deduplication reliable.

## Error handling and logging

Use clear, structured logging where possible.

Important events to log:

- collector run started
- collector run finished
- number of fetched posts
- number of inserted posts
- number of duplicate posts
- number of errors
- provider used
- HTTP failures from Truth Social
- API save failures
- AI analysis failures
- alert sending/logging failures

## Truth Social blocking risk

Truth Social may block, rate-limit, or return HTTP 403 for automated requests.

When debugging collector failures:

- Check whether failures are provider-specific.
- Check whether requests are blocked by IP or request pattern.
- Avoid aggressive polling.
- Prefer backoff and jitter.
- Do not assume Playwright will always bypass blocking.
- Keep the collector resilient if only some runs succeed.

## Scheduler

The collector scheduler runs periodically and calls the collector endpoint.

Expected behavior:

- Run on a configurable interval.
- Support jitter.
- Support backoff after failures.
- Log each run.
- Do not crash permanently after a single failed run.

Common environment variables may include:

```text
COLLECTOR_SCHEDULER_ENABLED=true
COLLECTOR_SCHEDULER_URL=http://api:8080/api/collector/run
COLLECTOR_SCHEDULER_HEALTH_URL=http://api:8080/health
COLLECTOR_SCHEDULER_INTERVAL_SECONDS=900
COLLECTOR_SCHEDULER_JITTER_SECONDS=300
COLLECTOR_SCHEDULER_BACKOFF_SECONDS=1800
```

## Development principles

When making changes:

1. Prefer small, safe, incremental changes.
2. Preserve current working behavior.
3. Do not remove functionality unless explicitly requested.
4. Use configuration instead of hardcoding values.
5. Keep secrets out of source code.
6. Keep Docker and local development paths aligned.
7. Add or update README documentation when behavior changes.
8. Include exact commands for testing where useful.

## Debugging workflow

When asked to debug, start by checking:

```bash
docker compose ps
docker compose logs api --tail=100
docker compose logs collector-scheduler --tail=100
docker compose logs postgres --tail=100
```

Then inspect:

- recent collector runs
- API health
- database connectivity
- scheduler configuration
- provider configuration
- recent HTTP errors

Prefer proposing one fix at a time.

## Documentation style

When writing documentation:

- Use English.
- Be concise but complete.
- Prefer Markdown.
- Include practical commands.
- Explain local setup and Docker setup separately when relevant.
- Include troubleshooting for common Docker, database, scheduler, and collector issues.

## Commit message convention

Use this format:

```text
type/(branch-name): message
```

The message starts with a lowercase letter.

Allowed types:

```text
feat
fix
docs
style
refactor
test
chore
```

Example:

```text
fix/collector-provider: handle failed Truth Social fetch gracefully
```

## Important instruction

When working in this repository, always apply this project context unless the user explicitly says otherwise.
