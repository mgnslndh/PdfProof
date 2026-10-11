#!/usr/bin/env pwsh
# Runs the build (build/Build, Cake.Sdk); arguments pass through, e.g. --target=Test or --configuration=Debug.
# build.sh does the same without PowerShell.
$ErrorActionPreference = 'Stop'
# Run from the repository root so dotnet uses this repository's global.json (SDK pin), wherever the caller is.
# A .ps1 runs in the caller's session, so the finally block always restores the caller's directory, even on failure.
Push-Location $PSScriptRoot
try {
    dotnet run --project "build/Build/Build.csproj" -- @args
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $exitCode
