# TrumpStockAlert Frontend

React + TypeScript + Vite dashboard for viewing saved Truth Social posts and their market-impact analysis.

## Setup

```powershell
npm install
```

Create a local environment file if you want to override the default backend URL:

```powershell
Copy-Item .env.example .env.local
```

Expected local API:

```text
VITE_API_BASE_URL=http://localhost:5044
```

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
5. Use `Run analysis` to call `POST /api/analysis/run`, then refresh the dashboard automatically.
