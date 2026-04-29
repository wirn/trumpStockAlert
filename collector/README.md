# trumpStockAlert Collector

Step 1 of `trumpStockAlert`: a small Python Collector that fetches recent public Truth Social posts from Donald Trump's account using [Truthbrush](https://github.com/stanfordio/truthbrush), normalizes them, sends them to the backend API, and prints newly saved posts as formatted JSON.

This is intentionally only the Collector. It does not include AI scoring, email alerts, Azure hosting, or a React dashboard yet.

## Project Structure

```text
collector/
  config.py              Environment configuration
  main.py                Entry point
  models.py              Normalized post dataclass
  normalizer.py          Truthbrush raw post normalization
  api_truth_post_store.py Backend API persistence
  service.py             Collector orchestration
  truth_post_store.py    Optional local JSON fallback storage
  truth_social_client.py Truthbrush integration
tests/
  test_normalizer.py
  test_service.py
  test_truth_post_store.py
data/
  truth-posts.json       Created when the first new post is stored
```

## Setup

Truthbrush requires Python 3.10 or newer.

From the repository root, the Function-friendly setup is:

```powershell
.\setup-collector-python.ps1 -Dev
```

That creates `collector\.venv`, installs this package in editable mode with development dependencies, and verifies that `truthbrush` imports from the venv.

If you are already in the `collector` directory, the equivalent manual setup is:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -e ".[dev]"
```

The Collector uses Truthbrush in unauthenticated public mode, so Truth Social login credentials are not required.

## Collector Configuration

The Collector uses these environment variables:

```powershell
$env:TRUTH_SOCIAL_USERNAME="realDonaldTrump"
$env:MAX_POSTS="10"
$env:LOOKBACK_MINUTES="5"
$env:COLLECTOR_STORE_MODE="api"
$env:TRUTH_POST_API_BASE_URL="http://localhost:5044"
$env:TRUTH_POSTS_FILE_PATH="./data/truth-posts.json"
$env:OUTPUT_MODE="console"
```

Defaults are already set for the values above, so you only need to set them when overriding behavior. `COLLECTOR_STORE_MODE=api` persists through the .NET backend. Set `COLLECTOR_STORE_MODE=json` only if you want the old local JSON fallback. The local backend URL is HTTP on port `5044`; do not use `https://localhost:5044`.

## Run Locally

Normal mode fetches only posts created within the last 5 minutes using UTC time comparisons. This matches the intended future schedule where the Collector runs every 5 minutes.

```powershell
python -m collector.main
```

Test mode ignores the 5-minute window and fetches exactly the latest 1 post, regardless of when it was posted:

```powershell
python -m collector.main --test
```

Test mode still persists to the backend database. The backend handles duplicates by `(source, externalId)`, so rerunning test mode with the same latest post returns it as already existing instead of inserting it again.

When new posts are found, the Collector prints them as formatted JSON:

```json
[
  {
    "source": "truthsocial",
    "author": "realDonaldTrump",
    "externalId": "123456789",
    "url": "https://truthsocial.com/@realDonaldTrump/posts/123456789",
    "content": "Post text",
    "createdAt": "2026-04-26T12:00:00.000Z",
    "collectedAt": "2026-04-26T12:01:00.000000+00:00",
    "raw": {}
  }
]
```

If no new posts are saved, the Collector logs how many fetched posts already existed and prints no JSON payload.

## Persistence

By default, collected posts are sent to:

```text
http://localhost:5044/api/truth-posts
```

The backend stores them in SQL Server table `truth_posts`. The API records `savedAtUtc` when the row is inserted into the database.

Duplicate detection uses `(source, externalId)`, which maps to the external Truth Social post id rather than post text/content.

## Tests

```powershell
python -m pytest
```

## Future Hosting Notes

The Collector is designed so it can later be wrapped by a scheduler or low-cost Azure host, such as Azure Functions timer trigger or Azure Container Apps Jobs. Hosting is not implemented in this step.
