# Playwright Collector

The Python collector supports two client modes. Playwright is the preferred mode for self-hosting because it does not require a Truth Social account.

| Mode | Env var value | Notes |
|------|--------------|-------|
| Playwright (default) | `COLLECTOR_CLIENT_MODE=playwright` | Headless Chromium, no login required |
| Truthbrush | `COLLECTOR_CLIENT_MODE=truthbrush` | Requires Truth Social credentials |

## Required env vars (`.env`)

| Variable | Default | Description |
|----------|---------|-------------|
| `COLLECTOR_CLIENT_MODE` | `playwright` | Client implementation to use |
| `TRUTH_SOCIAL_USERNAME` | `realDonaldTrump` | Profile to scrape (no `@`) |
| `COLLECTOR_MAX_POSTS` | `10` | Posts fetched per run |
| `COLLECTOR_LOOKBACK_MINUTES` | `5` | Lookback window (used in normal mode) |
| `COLLECTOR_STORE_MODE` | `api` | Always `api` in Docker |
| `TRUTH_POST_API_BASE_URL` | `http://api:8080` | Backend URL (set in docker-compose.yml) |

## Run the Playwright collector manually

```bash
# One-shot run against the live backend (from project root)
docker compose run --rm --build collector

# Fetch exactly 1 post, no lookback filter (quick smoke test)
docker compose run --rm --build collector python -m collector.main --test

# Local Python run (requires backend on localhost:5044)
cd collector
pip install ".[playwright]"
playwright install chromium
COLLECTOR_CLIENT_MODE=playwright \
TRUTH_POST_API_BASE_URL=http://localhost:5044 \
python -m collector.main --test
```

## How the scheduler triggers the collector

The `collector-scheduler` container runs a shell loop that calls:

```sh
docker compose run --rm --build collector
```

It connects to the host Docker daemon via a mounted socket (`/var/run/docker.sock`) and reads `docker-compose.yml` from the project directory (mounted read-only at `/workspace`). The project name is determined by the `name: trumpstockalert` field in `docker-compose.yml`.

Flow:
1. Scheduler waits for `GET /health` to return 200.
2. Every `COLLECTOR_SCHEDULER_INTERVAL_SECONDS` ± jitter seconds, it runs `docker compose run --rm --build collector`.
3. The one-shot collector container starts, fetches posts via Playwright, saves them through `POST /api/truth-posts`, then exits.
4. If the run fails (non-zero exit), a backoff of `COLLECTOR_SCHEDULER_BACKOFF_SECONDS` is applied before the next attempt.
5. Duplicate posts are silently skipped by the backend (unique constraint on `(source, external_id)`).

### Security note

Mounting `/var/run/docker.sock` into a container grants it full Docker daemon access, equivalent to root on the host. This is an accepted trade-off for self-hosted home servers. Do not expose the scheduler externally.

### Alternative: host cron / systemd timer

If you prefer not to mount the Docker socket, disable the `collector-scheduler` container and set up a host-level timer instead.

**Cron (every 5 minutes with random jitter via `flock`):**
```cron
*/5 * * * * cd /path/to/trumpStockAlert && docker compose run --rm --build collector >> /var/log/trump-collector.log 2>&1
```

**Systemd timer** (`/etc/systemd/system/trump-collector.service`):
```ini
[Unit]
Description=TrumpStockAlert collector run
After=docker.service

[Service]
Type=oneshot
WorkingDirectory=/path/to/trumpStockAlert
ExecStart=docker compose run --rm --build collector
```

`/etc/systemd/system/trump-collector.timer`:
```ini
[Unit]
Description=TrumpStockAlert collector timer

[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
RandomizedDelaySec=120

[Install]
WantedBy=timers.target
```

```bash
sudo systemctl enable --now trump-collector.timer
sudo systemctl status trump-collector.timer
```

To disable the Docker-socket scheduler while using host cron:
```bash
# In .env
COLLECTOR_SCHEDULER_ENABLED=false
docker compose up -d collector-scheduler
```

## Duplicate handling

Deduplication is enforced by the backend database, not the collector. The `truth_posts` table has a unique index on `(source, external_id)`. If the collector sends a post that already exists, the API returns `200` or `409` and the collector logs it as a skip. No duplicate rows are ever inserted, regardless of how many times the same post is fetched.

## Troubleshooting

**Chromium fails to launch:**
- Run `docker compose run --rm --build collector python -c "from playwright.sync_api import sync_playwright; p = sync_playwright().start(); b = p.chromium.launch(); b.close(); p.stop(); print('OK')"` to isolate the issue.
- Check that the image was built with `docker compose build collector`.

**No posts captured (empty response):**
- Truth Social may have changed its page structure or is blocking the request.
- Check container logs: `docker compose logs collector --tail=50`.
- The profile URL being loaded is `https://truthsocial.com/@<TRUTH_SOCIAL_USERNAME>`.

**API save failures:**
- Check backend logs: `docker compose logs api --tail=50`.
- Verify `TRUTH_POST_API_BASE_URL=http://api:8080` is set in the collector service.
