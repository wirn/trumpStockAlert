# TrumpStockAlert Collector Function

Azure Functions isolated worker app that runs every 5 minutes and calls the backend collector endpoint:

```text
POST {BackendBaseUrl}/api/collector/run
```

Required Function App configuration:

```text
BackendBaseUrl
Collector__ApiKey
```

`Collector__ApiKey` is sent as the `x-api-key` header and must match the backend `Collector__ApiKey` setting. Do not commit real secrets.

The Function does not start Python directly and does not deploy the collector `.venv` or Python packages. Python dependency setup belongs to the backend host that runs the collector.

For local development, copy `local.settings.sample.json` to `local.settings.json`, set `BackendBaseUrl` to the local backend API, and set `Collector__ApiKey` to the same value used by the backend.
