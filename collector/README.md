# trumpStockAlert Collector

Step 1 of `trumpStockAlert`: a small Python Collector that fetches recent public Truth Social posts from Donald Trump's account using [Truthbrush](https://github.com/stanfordio/truthbrush), normalizes them, filters duplicates, and prints new posts as formatted JSON.

This is intentionally only the Collector. It does not include AI scoring, email alerts, database storage, a .NET API, Azure hosting, or a React dashboard yet.

## Project Structure

```text
collector/
  config.py              Environment configuration
  main.py                Entry point
  models.py              Normalized post dataclass
  normalizer.py          Truthbrush raw post normalization
  service.py             Collector orchestration
  truth_post_store.py    Local JSON truth_posts-style storage
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
$env:TRUTH_POSTS_FILE_PATH="./data/truth-posts.json"
$env:OUTPUT_MODE="console"
```

Defaults are already set for the values above, so you only need to set them when overriding behavior.

## Run Locally

Normal mode fetches only posts created within the last 5 minutes using UTC time comparisons. This matches the intended future schedule where the Collector runs every 5 minutes.

```powershell
python -m collector.main
```

Test mode ignores the 5-minute window and fetches exactly the latest 1 post, regardless of when it was posted:

```powershell
python -m collector.main --test
```

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

If no new posts are found, the Collector logs that zero new posts were detected and prints no JSON payload.

## Local MVP Storage

Collected posts are stored locally in:

```text
./data/truth-posts.json
```

This file is the MVP stand-in for the future database table `truth_posts`. It contains a JSON array of normalized posts, including `collectedAt` and the original Truthbrush `raw` payload for debugging and later migration.

Duplicate detection mimics the future database unique constraint by using the pair `(source, externalId)`. If no new posts are found, the file is not rewritten.

## Tests

```powershell
python -m pytest
```

## Future Hosting Notes

The Collector is designed so it can later be wrapped by a scheduler or low-cost Azure host, such as Azure Functions timer trigger or Azure Container Apps Jobs. Hosting is not implemented in this step.
