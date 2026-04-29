param(
    [switch]$Test,
    [switch]$SkipLookback
)

$originalLocation = Get-Location

try {
    Set-Location "$PSScriptRoot\collector"

    if ([string]::IsNullOrWhiteSpace($env:TRUTH_POST_API_BASE_URL)) {
        $env:TRUTH_POST_API_BASE_URL = "http://localhost:5044"
    }

    $argsToPass = @()

    if ($Test) {
        $argsToPass += "--test"
    }

    if ($SkipLookback) {
        $argsToPass += "--skip-lookback"
    }

    $pythonExecutable = $env:PYTHON_EXECUTABLE

    if ([string]::IsNullOrWhiteSpace($pythonExecutable)) {
        $localVenvPython = Join-Path (Get-Location) ".venv\Scripts\python.exe"
        if (Test-Path $localVenvPython) {
            $pythonExecutable = $localVenvPython
        }
        else {
            $pythonExecutable = "python"
        }
    }

    & $pythonExecutable -m collector.main @argsToPass
}
finally {
    Set-Location $originalLocation
}
