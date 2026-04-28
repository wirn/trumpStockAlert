# trumpStockAlert Backend API

Step 2 of `trumpStockAlert`: a local .NET 10 Web API that stores normalized Truth Social posts in SQL Server/Azure SQL Database through Entity Framework Core.

This backend only stores posts. It does not include AI market-impact scoring, email alerts, or the React dashboard yet.

## Structure

```text
backend/
  Controllers/           HTTP API endpoints
  Data/                  EF Core DbContext and migrations
  DTOs/                  Request/response contracts
  Models/                EF Core entities
  Services/              Application logic
  appsettings.json       Local development configuration
  Program.cs             Dependency injection and SQL Server setup
```

## Database Provider

The backend uses:

```csharp
options.UseSqlServer(connectionString, sqlServerOptions =>
{
    sqlServerOptions.EnableRetryOnFailure();
});
```

The connection string is read from configuration:

```csharp
builder.Configuration.GetConnectionString("DefaultConnection")
```

No database credentials are hardcoded in source code.

`truth_posts` includes `SavedAtUtc`, which is set by the backend when a row is inserted. This is the UTC timestamp for when the post was persisted locally, distinct from the Truth Social `CreatedAt` timestamp and the collector payload's `CollectedAt` timestamp.

## Local Configuration

`appsettings.json` contains a local development connection string using Windows authentication:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=TrumpStockAlert;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

If you use Docker SQL Server or another local SQL Server instance, replace only the local value in `appsettings.json` or use an environment variable.

## Azure Configuration

For Azure App Service, Azure Functions, or containers, set the connection string as an environment variable or app setting:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

In Azure Portal, use an Application Setting named:

```text
ConnectionStrings__DefaultConnection
```

The production collector endpoint also requires an API key. Configure it in Azure App Service as:

```text
Collector__ApiKey
```

Do not store the real value in source control. Optional collector settings use the same double-underscore convention, for example `Collector__MaxPosts`, `Collector__LookbackMinutes`, and `Collector__BackendBaseUrl`.

Prefer managed identity for production when you wire up Azure SQL authentication later. If using SQL credentials, store them in Azure app settings or Key Vault, never in source control.

## Migrations

EF Core migrations are enabled for SQL Server. The initial migration creates `truth_posts` with a unique index on:

```text
Source, ExternalId
```

Install/restore the local EF tool:

```powershell
dotnet tool restore
```

Create a new migration after model changes:

```powershell
dotnet tool run dotnet-ef migrations add <MigrationName> --project backend\TrumpStockAlert.Api.csproj --startup-project backend\TrumpStockAlert.Api.csproj
```

Apply migrations locally:

```powershell
dotnet tool run dotnet-ef database update --project backend\TrumpStockAlert.Api.csproj --startup-project backend\TrumpStockAlert.Api.csproj
```

Apply migrations against Azure SQL by setting the environment variable first:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
dotnet tool run dotnet-ef database update --project backend\TrumpStockAlert.Api.csproj --startup-project backend\TrumpStockAlert.Api.csproj
```

## Run Locally

From the repository root:

```powershell
cd backend
dotnet restore
dotnet run
```

The default launch profile exposes:

```text
http://localhost:5044
https://localhost:7281
```

Open Swagger UI at:

```text
https://localhost:7281/swagger
```

If HTTPS dev certificates are not configured, use:

```text
http://localhost:5044/swagger
```

## API

### POST `/api/truth-posts`

Creates a new post. If a post with the same `(source, externalId)` already exists, the endpoint returns `200 OK` with the existing row. New rows return `201 Created` and include `savedAtUtc` in the response.

Example:

```powershell
$body = @{
  source = "truthsocial"
  author = "realDonaldTrump"
  externalId = "123456789"
  url = "https://truthsocial.com/@realDonaldTrump/posts/123456789"
  content = "Example post text"
  createdAt = "2026-04-26T12:00:00Z"
  collectedAt = "2026-04-26T12:01:00Z"
  raw = @{
    id = "123456789"
    content = "<p>Example post text</p>"
  }
} | ConvertTo-Json -Depth 10

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:5044/api/truth-posts" `
  -ContentType "application/json" `
  -Body $body
```

### GET `/api/truth-posts`

Returns stored posts sorted by `createdAt` descending.

Optional query parameter:

```text
limit
```

Default limit is `50`; max limit is `500`.

Example:

```powershell
Invoke-RestMethod "http://localhost:5044/api/truth-posts?limit=10"
```

### GET `/api/truth-posts/{id}`

Returns a single post by database ID, or `404 Not Found`.

Example:

```powershell
Invoke-RestMethod "http://localhost:5044/api/truth-posts/1"
```

## Collector Integration

The Python Collector sends newly fetched posts to `POST /api/truth-posts` in both normal mode and `--test` mode. Test mode only changes fetching behavior; it does not skip database persistence.

### POST `/api/collector/run`

Runs the production-safe collector flow. It uses the same Truthbrush-based fetch path as the development test collector, fetches the latest configured number of Truth Social posts, saves new rows to `dbo.truth_posts`, skips duplicates by `ExternalId`, and does not run AI analysis or alert logic.

Required header:

```text
x-api-key: <Collector__ApiKey>
```

This endpoint is intended for scheduled execution, such as an Azure Function Timer Trigger every 5 minutes.

### POST `/api/collector/run-test`

Development-only local test endpoint. It remains separate from the production collector endpoint and does not require the production API key.
