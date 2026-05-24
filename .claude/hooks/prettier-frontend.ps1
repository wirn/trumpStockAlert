$hookInput = $input | ConvertFrom-Json
$filePath = $hookInput.tool_input.file_path

if (-not $filePath) {
  exit 0
}

if ($filePath -match '^frontend[\\/].*\.(ts|tsx|js|jsx|json|css|scss|html|md)$') {
  Push-Location frontend
  try {
    npx prettier --write (Resolve-Path (Join-Path '..' $filePath))
  }
  finally {
    Pop-Location
  }
}