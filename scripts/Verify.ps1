#Requires -Version 7.4
param([switch]$Graphics)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    python scripts/check_docs.py
    dotnet restore Platformer.sln
    dotnet build Platformer.sln -c Release --no-restore
    dotnet test Platformer.sln -c Release --no-build --logger 'trx;LogFileName=platformer.trx' --results-directory artifacts/tests
    dotnet run --project src/Platformer.App -c Release --no-build -- --help
    if ($Graphics) {
        dotnet run --project src/Platformer.App -c Release --no-build -- --smoke
    }
    git diff --check
} finally {
    Pop-Location
}
