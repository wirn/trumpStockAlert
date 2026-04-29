# TrumpStockAlert Collector Function

Azure Functions isolated worker app that runs every 5 minutes, executes the Python Truthbrush collector directly, and saves posts through the backend `POST /api/truth-posts` endpoint.

Required Function App configuration:

```text
BackendBaseUrl
```

Optional Function App configuration:

```text
Collector__PythonExecutable
Collector__CollectorDirectory
TruthSocialUsername
MaxPosts
OutputMode
```

`PythonExecutable` and `CollectorDirectory` are still supported as legacy flat keys, but prefer the explicit `Collector__...` names for new local settings and Azure App Settings.

The function publish output includes `collector/pyproject.toml` and `collector/collector/**/*.py`. It does not include `.venv`, `__pycache__`, local databases, local JSON data, or secrets.

The Function runtime must have Python 3.10+ and `truthbrush>=0.2.5` available to the configured Python executable. For Azure, use a runtime/container that provides Python and install dependencies from `collector/pyproject.toml`, or set `Collector__PythonExecutable` to the deployed Python path.

For local development, copy `local.settings.sample.json` to `local.settings.json` and replace the placeholder values.

## Local Python dependency setup

From the repository root:

```powershell
.\setup-collector-python.ps1
```

This creates `collector\.venv`, installs the collector package and its dependencies from `collector\pyproject.toml`, and verifies that `truthbrush` can be imported by the venv interpreter. The venv is ignored by git and must not be committed.

Then make sure `collector-function\local.settings.json` contains:

```json
"Collector__PythonExecutable": "..\\collector\\.venv\\Scripts\\python.exe",
"Collector__CollectorDirectory": "..\\collector"
```

Start the backend API, then run the Function from `collector-function` with Azure Functions Core Tools. The timer trigger will launch the collector with that venv Python.

## Azure deployment

Do not deploy `.venv` or installed packages from local development. Provision Python 3.10+ in the Function runtime or deployment image, install dependencies from `collector\pyproject.toml` during deployment, and set `Collector__PythonExecutable` to the Python interpreter that has `truthbrush` installed. Store secrets only in Azure App Settings or local ignored settings.
