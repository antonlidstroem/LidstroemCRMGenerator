# run-tests.ps1
# Windows equivalent of run-tests.sh
# Usage:
#   .\run-tests.ps1           — runs full suite
#   .\run-tests.ps1 -Fast     — skips integration and plugin tests
#
# If you see "running scripts is disabled", run this first (once, as admin):
#   Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

param(
    [switch]$Fast
)

$ErrorActionPreference = 'Stop'

function Run-Tests {
    param([string]$Name, [string]$Project)
    Write-Host "--- $Name ---"
    dotnet test $Project --no-build --logger "console;verbosity=minimal"
    Write-Host ""
}

Write-Host ""
Write-Host "=========================================="
Write-Host " Lidstroem Test Suite"
Write-Host "=========================================="
Write-Host ""

Write-Host "Building solution..."
dotnet build Lidstroem.sln --configuration Release --verbosity quiet
Write-Host ""

Run-Tests "Core unit tests"           "Tests\Core\Lidstroem.Tests.Core.csproj"
Run-Tests "Infrastructure unit tests" "Tests\Infrastructure\Lidstroem.Tests.Infrastructure.csproj"

if (-not $Fast) {
    Run-Tests "Plugin contract + integration" "Tests\Integration\Lidstroem.Tests.Integration.csproj"
    Run-Tests "Plugin functional tests"       "Tests\Plugins\Lidstroem.Tests.Plugins.csproj"
} else {
    Write-Host "--- Integration + Plugin tests skipped (-Fast mode) ---"
    Write-Host ""
}

Write-Host "=========================================="
Write-Host " All tests passed."
Write-Host "=========================================="
Write-Host ""
