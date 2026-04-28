# TrumpStockAlert Collector Function

Azure Functions isolated worker app that runs every 5 minutes and calls the backend collector endpoint.

Required Function App configuration:

```text
BackendBaseUrl
CollectorApiKey
```

`CollectorApiKey` must match the backend App Service setting `Collector__ApiKey`. Do not commit real secrets.

For local development, copy `local.settings.sample.json` to `local.settings.json` and replace the placeholder values.
