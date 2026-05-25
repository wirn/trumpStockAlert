# Manual OpenAI Analyzer Test

Mock remains the committed/default analyzer. Use this runbook only when you want to temporarily verify the real OpenAI provider on the self-hosted Docker server.

Do not put real API keys in `.env`, source control, shell history, or logs.

## 1. Pause scheduled runs

Stop the scheduler before enabling OpenAI so it cannot trigger real API calls while you test manually.

```bash
docker compose stop collector-scheduler
```

## 2. Start the API with OpenAI enabled temporarily

These shell variables override `.env` for this command without changing committed config.

```bash
read -rsp "OpenAI API key: " OPENAI_API_KEY
echo
export OPENAI_API_KEY
export ANALYZER_PROVIDER=OpenAI
export OPENAI_MODEL=gpt-5.1-mini
export OPENAI_TIMEOUT_SECONDS=30

docker compose up -d --no-deps --build --force-recreate api
docker compose logs api --tail=50
```

Confirm the container sees OpenAI mode without printing the key:

```bash
docker compose exec api printenv Analyzer__Provider
docker compose exec api printenv OpenAI__Model
```

## 3. Ensure there is one unanalyzed real-content post

If the collector has already saved unanalyzed posts, you can skip this. Otherwise insert one temporary post through the API:

```bash
TEST_EXTERNAL_ID="manual-openai-test-$(date +%s)"

curl -sS -X POST "http://100.92.230.97:8080/api/truth-posts" \
  -H "Content-Type: application/json" \
  -d "{
    \"source\": \"manual_test\",
    \"author\": \"manual\",
    \"externalId\": \"$TEST_EXTERNAL_ID\",
    \"url\": \"https://example.com/$TEST_EXTERNAL_ID\",
    \"content\": \"Tariffs on China and Fed rate policy may affect equities, Treasuries, and USD.\",
    \"createdAt\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",
    \"collectedAt\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",
    \"raw\": {}
  }"
```

## 4. Run analysis manually

```bash
curl -sS -X POST "http://100.92.230.97:8080/api/analyses/run" \
  -H "X-TrumpStockAlert-Scheduler-Key: $SCHEDULER_API_KEY" | jq
```

Expected response fields:

```json
{
  "analyzedCount": 1,
  "skippedCount": 0,
  "skippedAlreadyAnalyzedCount": 0,
  "skippedNoTextContentCount": 0,
  "errorCount": 0,
  "message": "Analyzed 1 posts, skipped 0, failed 0."
}
```

Counts may differ if other unanalyzed posts exist.

## 5. Verify persisted analysis

```bash
docker compose exec postgres psql -U trumpuser -d trumpstockalert -c '
SELECT
  p."ExternalId",
  p."Content",
  a."MarketImpactScore",
  a."Confidence",
  a."Direction",
  a."Reasoning",
  a."AffectedAssetsJson",
  a."AnalyzerVersion",
  a."CreatedAt"
FROM post_analyses a
JOIN truth_posts p ON p."Id" = a."PostId"
ORDER BY a."CreatedAt" DESC
LIMIT 10;
'
```

For a real OpenAI result, `AnalyzerVersion` should start with `openai-`, and `Direction` should be one of `positive`, `negative`, `neutral`, or `mixed`.

## 6. Verify skip rules still hold

Placeholder content should be skipped and should not create a `post_analyses` row:

```bash
PLACEHOLDER_EXTERNAL_ID="manual-placeholder-test-$(date +%s)"

curl -sS -X POST "http://100.92.230.97:8080/api/truth-posts" \
  -H "Content-Type: application/json" \
  -d "{
    \"source\": \"manual_test\",
    \"author\": \"manual\",
    \"externalId\": \"$PLACEHOLDER_EXTERNAL_ID\",
    \"url\": \"https://example.com/$PLACEHOLDER_EXTERNAL_ID\",
    \"content\": \"[No text content]\",
    \"createdAt\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",
    \"collectedAt\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",
    \"raw\": {}
  }"

curl -sS -X POST "http://100.92.230.97:8080/api/analyses/run" \
  -H "X-TrumpStockAlert-Scheduler-Key: $SCHEDULER_API_KEY" | jq

docker compose exec postgres psql -U trumpuser -d trumpstockalert -c "
SELECT COUNT(*) AS placeholder_analysis_count
FROM post_analyses a
JOIN truth_posts p ON p.\"Id\" = a.\"PostId\"
WHERE p.\"ExternalId\" = '$PLACEHOLDER_EXTERNAL_ID';
"
```

`placeholder_analysis_count` should be `0`.

Running `POST /api/analyses/run` again should not re-analyze posts that already have rows in `post_analyses`; they should be counted as already-analyzed skips.

## 7. Switch back to Mock

```bash
unset OPENAI_API_KEY
unset ANALYZER_PROVIDER
unset OPENAI_MODEL
unset OPENAI_TIMEOUT_SECONDS

docker compose up -d --no-deps --build --force-recreate api
docker compose exec api printenv Analyzer__Provider
```

The printed provider should be `Mock` or blank/defaulted to Mock by Compose.

Restart the scheduler only after the API is back on Mock:

```bash
docker compose up -d collector-scheduler
docker compose logs collector-scheduler --tail=50
```

## 8. Verify no real OpenAI calls after switching back

Insert another temporary post and run analysis. The latest `AnalyzerVersion` should start with `mock-keyword-`, not `openai-`.

```bash
MOCK_EXTERNAL_ID="manual-mock-test-$(date +%s)"

curl -sS -X POST "http://100.92.230.97:8080/api/truth-posts" \
  -H "Content-Type: application/json" \
  -d "{
    \"source\": \"manual_test\",
    \"author\": \"manual\",
    \"externalId\": \"$MOCK_EXTERNAL_ID\",
    \"url\": \"https://example.com/$MOCK_EXTERNAL_ID\",
    \"content\": \"Tariffs on China may affect markets.\",
    \"createdAt\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",
    \"collectedAt\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",
    \"raw\": {}
  }"

curl -sS -X POST "http://100.92.230.97:8080/api/analyses/run" \
  -H "X-TrumpStockAlert-Scheduler-Key: $SCHEDULER_API_KEY" | jq

docker compose exec postgres psql -U trumpuser -d trumpstockalert -c "
SELECT a.\"AnalyzerVersion\"
FROM post_analyses a
JOIN truth_posts p ON p.\"Id\" = a.\"PostId\"
WHERE p.\"ExternalId\" = '$MOCK_EXTERNAL_ID';
"
```
