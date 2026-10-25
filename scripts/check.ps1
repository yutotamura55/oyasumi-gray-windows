$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $repositoryRoot 'OyasumiGray.sln'

function Invoke-Step([string] $name, [scriptblock] $command) {
    Write-Host "==> $name"
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$name failed with exit code $LASTEXITCODE" }
}

Invoke-Step 'Format' { dotnet format $solution --verify-no-changes }
Invoke-Step 'Build and analyze' { dotnet build $solution -c Release }
Invoke-Step 'Core tests' { dotnet run --project (Join-Path $repositoryRoot 'tests/OyasumiGray.Core.Tests') -c Release --no-build }

# The settings screen test needs the Windows Desktop runtime, which is unavailable on Linux.
if ($env:OS -eq 'Windows_NT') {
    Invoke-Step 'Windows tests' { dotnet run --project (Join-Path $repositoryRoot 'tests/OyasumiGray.Windows.Tests') -c Release --no-build }
}
else {
    Write-Warning 'Skipped Windows tests: they run only on Windows.'
}

Write-Host 'All checks passed.'
