param(
    [string]$Python = "python",
    [switch]$Dev
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$collectorDirectory = Join-Path $repoRoot "collector"
$venvDirectory = Join-Path $collectorDirectory ".venv"
$venvPython = Join-Path $venvDirectory "Scripts\python.exe"
$pipTempDirectory = Join-Path $repoRoot ".build-validation\pip-temp"

function Invoke-NativeStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}

if (-not (Test-Path (Join-Path $collectorDirectory "pyproject.toml"))) {
    throw "Could not find collector\pyproject.toml from $repoRoot."
}

New-Item -ItemType Directory -Force -Path $pipTempDirectory | Out-Null

$originalTemp = $env:TEMP
$originalTmp = $env:TMP
$originalTmpDir = $env:TMPDIR

try {
    $env:TEMP = $pipTempDirectory
    $env:TMP = $pipTempDirectory
    $env:TMPDIR = $pipTempDirectory

    if (-not (Test-Path $venvPython)) {
        Invoke-NativeStep $Python @("-m", "venv", $venvDirectory)
    }

    Invoke-NativeStep $venvPython @("-m", "pip", "install", "--upgrade", "pip")

    if ($Dev) {
        Invoke-NativeStep $venvPython @("-m", "pip", "install", "-e", "$collectorDirectory[dev]")
    }
    else {
        Invoke-NativeStep $venvPython @("-m", "pip", "install", "-e", $collectorDirectory)
    }

    Invoke-NativeStep $venvPython @("-c", "import sys, truthbrush; print(sys.executable); print(truthbrush.__file__)")

    Write-Host ""
    Write-Host "Collector Python setup complete."
    Write-Host "Use this interpreter for the Azure Function:"
    Write-Host "  Collector__PythonExecutable=$venvPython"
    Write-Host "  Collector__CollectorDirectory=$collectorDirectory"
}
finally {
    $env:TEMP = $originalTemp
    $env:TMP = $originalTmp
    $env:TMPDIR = $originalTmpDir
}
