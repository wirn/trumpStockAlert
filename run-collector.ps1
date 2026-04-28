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

    & ".\.venv\Scripts\python.exe" -m collector.main @argsToPass
}
finally {
    Set-Location $originalLocation
}
