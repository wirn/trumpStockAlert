# TrumpStockAlert Frontend

React + TypeScript + Vite dashboard for viewing saved Truth Social posts and their market-impact analysis.

## Setup

```powershell
npm install
```

Create a local environment file:

```powershell
Copy-Item .env.local.example .env.local
```

Expected local development setup:

```text
Frontend: http://localhost:5173
API:      http://localhost:8080
```

Local `.env.local`:

```text
VITE_API_BASE_URL=http://localhost:8080
VITE_SCHEDULER_API_KEY=<same value as SCHEDULER_API_KEY for the backend>
```

All API calls use `VITE_API_BASE_URL`. The value may include or omit a trailing slash.

`VITE_SCHEDULER_API_KEY` is sent to protected admin endpoints as `X-TrumpStockAlert-Scheduler-Key` when using manual admin actions such as collector and analysis runs. This is acceptable only for a private/protected dashboard, for example behind Tailscale. Do not expose this frontend publicly with `VITE_SCHEDULER_API_KEY`; move admin actions behind real server-side authentication or a protected backend-for-frontend first.

## Run

```powershell
npm run dev
```

Vite normally serves the app at:

```text
http://localhost:5173
```

## Build

```powershell
npm run build
```

## Local Flow

1. Start the backend from `../backend` with `dotnet run`.
2. Start this frontend with `npm run dev`.
3. Open `http://localhost:5173`.
4. Use `Refresh data` to reload posts and analyses.
5. Use `Run Collector Test` to call `POST /api/collector/run`, then refresh saved posts automatically.
6. Use `Run analysis` to call `POST /api/analyses/run`, then refresh the dashboard automatically.

## Azure Deployment

Set `VITE_API_BASE_URL` during the frontend build/deploy for the target environment:

```text
VITE_API_BASE_URL=https://<your-api-app>.azurewebsites.net
```

For a public Azure frontend, leave `VITE_SCHEDULER_API_KEY` unset and hide/avoid protected admin actions until they are moved behind real authentication. Vite variables are client-side configuration, not secrets.
