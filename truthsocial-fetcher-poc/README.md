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

## Docker

The production image uses the official Microsoft Playwright image, which includes Chromium and browser OS dependencies.

Build locally:

```powershell
cd truthsocial-fetcher-poc
docker build -t truthsocial-fetcher-worker:local .
```

Run locally against a backend:

```powershell
docker run --rm `
  -e BackendBaseUrl="https://<backend-host>" `
  -e Collector__ApiKey="<secret>" `
  -e TRUTH_SOCIAL_USERNAME="realDonaldTrump" `
  -e MAX_POSTS="10" `
  truthsocial-fetcher-worker:local
```

For local host backend access from Docker Desktop on Windows:

```powershell
docker run --rm `
  -e BackendBaseUrl="http://host.docker.internal:5044" `
  -e Collector__ApiKey="<same value configured for the backend>" `
  truthsocial-fetcher-worker:local
```

The container command is:

```text
npm run save
```

## Azure Container Registry

Create a registry:

```powershell
az group create `
  --name <resource-group> `
  --location <region>

az acr create `
  --resource-group <resource-group> `
  --name <acr-name> `
  --sku Basic `
  --admin-enabled false
```

Build and push with ACR Tasks:

```powershell
az acr build `
  --registry <acr-name> `
  --image truthsocial-fetcher-worker:latest `
  .
```

Run that command from `truthsocial-fetcher-poc/`.

## Azure Container Apps Job

Create or reuse a Container Apps environment:

```powershell
az containerapp env create `
  --name <containerapps-env> `
  --resource-group <resource-group> `
  --location <region>
```

Create the scheduled job. The cron expression below runs every 5 minutes:

```powershell
az containerapp job create `
  --name truthsocial-fetcher-job `
  --resource-group <resource-group> `
  --environment <containerapps-env> `
  --trigger-type Schedule `
  --cron-expression "*/5 * * * *" `
  --replica-timeout 600 `
  --replica-retry-limit 1 `
  --parallelism 1 `
  --replica-completion-count 1 `
  --image <acr-name>.azurecr.io/truthsocial-fetcher-worker:latest `
  --registry-server <acr-name>.azurecr.io `
  --secrets collector-api-key="<collector-api-key>" `
  --env-vars `
    BackendBaseUrl="https://<backend-host>" `
    Collector__ApiKey=secretref:collector-api-key `
    TRUTH_SOCIAL_USERNAME="realDonaldTrump" `
    MAX_POSTS="10" `
    HEADLESS="true"
```

Use managed identity and ACR pull permissions for production deployments where possible:

```powershell
az containerapp job identity assign `
  --name truthsocial-fetcher-job `
  --resource-group <resource-group> `
  --system-assigned

az role assignment create `
  --assignee <job-principal-id> `
  --role AcrPull `
  --scope $(az acr show --name <acr-name> --query id -o tsv)
```

Required runtime settings:

```text
BackendBaseUrl
Collector__ApiKey
```

Optional runtime settings:

```text
TRUTH_SOCIAL_USERNAME=realDonaldTrump
MAX_POSTS=10
HEADLESS=true
OUTPUT_PATH=/tmp/truthsocial-fetcher/latest-posts.json
```

Store `Collector__ApiKey` as a Container Apps secret. Do not put it in the image, Dockerfile, README command history, or source control.

## Verification

On a machine with Docker:

```powershell
cd truthsocial-fetcher-poc
docker build -t truthsocial-fetcher-worker:local .
docker run --rm `
  -e BackendBaseUrl="http://host.docker.internal:5044" `
  -e Collector__ApiKey="<same value configured for the backend>" `
  -e MAX_POSTS="1" `
  truthsocial-fetcher-worker:local
```

Expected output includes:

```json
{
  "fetchedPosts": 1,
  "savedPosts": 0,
  "skippedPosts": 1,
  "failedPosts": 0
}
```

Counts vary depending on whether the post already exists.

## Azure Hosting Recommendation

If this worker remains reliable, host it separately from the ASP.NET API as a Linux containerized worker or Azure Container Apps job. Build Chromium with Playwright into the image, run the job on a schedule or from a future backend trigger, and keep browser automation out of the API App Service process.
