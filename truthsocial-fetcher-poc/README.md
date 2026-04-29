# Truth Social Fetcher Worker

Isolated Playwright worker for fetching latest public posts from `@realDonaldTrump` without Truthbrush. This does not replace the ASP.NET backend collector or the Azure Function scheduler yet.

The worker opens the public profile in Chromium, captures Truth Social network JSON responses, normalizes posts to the backend contract, and can either write local JSON or post each item to the backend.

## Local Setup

```powershell
cd truthsocial-fetcher-poc
npm install
npm run install-browsers
```

## Environment Variables

Fetch-only variables:

```text
TRUTH_SOCIAL_USERNAME=realDonaldTrump
MAX_POSTS=10
HEADLESS=true
OUTPUT_PATH=output/latest-posts.json
```

Save-to-backend variables:

```text
BackendBaseUrl=http://localhost:5044
Collector__ApiKey=<local or Azure app setting value>
```

Do not commit secrets. Local `.env*`, generated output, Playwright reports, auth/session folders, and `node_modules` are ignored.

## Fetch Only

```powershell
npm run fetch
```

This fetches posts and writes normalized JSON to `output/latest-posts.json`.

## Save To Backend

Start the backend API, then run:

```powershell
$env:BackendBaseUrl = "http://localhost:5044"
$env:Collector__ApiKey = "<same value configured for the backend>"
npm run save
```

`npm run save` fetches posts, writes the local JSON sample, and POSTs each normalized post to:

```text
{BackendBaseUrl}/api/truth-posts
```

It sends the API key as `x-api-key` and never logs the key. Backend `201 Created` responses count as saved. Backend `200 OK` or `409 Conflict` responses count as skipped duplicates. Other responses count as failed, and the worker continues with remaining posts.

Final summary:

```json
{
  "fetchedPosts": 10,
  "savedPosts": 0,
  "skippedPosts": 10,
  "failedPosts": 0
}
```

The worker exits non-zero only when fetching fails or every fetched post fails to save unexpectedly.

## Output Shape

Each post is normalized to:

```json
{
  "source": "truthsocial",
  "author": "realDonaldTrump",
  "externalId": "string",
  "url": "https://truthsocial.com/@realDonaldTrump/...",
  "content": "plain text",
  "createdAt": "ISO-8601 datetime",
  "collectedAt": "ISO-8601 datetime",
  "raw": {}
}
```

## Tests

```powershell
npm test
```

## Azure Hosting Recommendation

If this worker remains reliable, host it separately from the ASP.NET API as a Linux containerized worker or Azure Container Apps job. Build Chromium with Playwright into the image, run the job on a schedule or from a future backend trigger, and keep browser automation out of the API App Service process.
