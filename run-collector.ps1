param(
    [switch]$Test
)

$originalLocation = Get-Location

try {
    Set-Location "$PSScriptRoot\collector"

    $argsToPass = @()

    if ($Test) {
        $argsToPass += "--test"
    }

    & ".\.venv\Scripts\python.exe" -m collector.main @argsToPass
}
finally {
    Set-Location $originalLocation
}