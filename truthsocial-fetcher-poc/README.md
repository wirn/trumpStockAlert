# Truth Social Fetcher POC

Browser-capable proof of concept for fetching latest public posts from `@realDonaldTrump` without Truthbrush and without changing the production backend or Azure Function scheduler.

## Local Run

```powershell
cd truthsocial-fetcher-poc
npm install
npm run install-browsers
npm run fetch
```

The script writes normalized JSON to `output/latest-posts.json` and prints the same JSON to the console.

Optional environment variables:

```text
TRUTH_SOCIAL_USERNAME=realDonaldTrump
MAX_POSTS=10
HEADLESS=true
OUTPUT_PATH=output/latest-posts.json
```

No secrets are required. Do not commit files under `output/` or `.auth/`.

## Output Shape

Each post is normalized to the backend create contract:

```json
{
  "source": "truthsocial",
  "author": "realDonaldTrump",
  "externalId": "string",
  "url": "https://truthsocial.com/@realDonaldTrump/posts/...",
  "content": "plain text",
  "createdAt": "ISO-8601 datetime",
  "collectedAt": "ISO-8601 datetime",
  "raw": {}
}
```

## Azure Hosting Recommendation

If this POC works reliably, host it separately from the ASP.NET API as a Linux containerized worker or Azure Container Apps job with Playwright Chromium installed at image build time. Trigger it from the existing scheduler/backend contract later, but keep browser automation out of the API App Service process.
