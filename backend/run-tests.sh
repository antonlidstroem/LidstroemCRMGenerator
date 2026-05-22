#!/usr/bin/env bash
# Runs the full test suite in recommended order.
# Usage: ./run-tests.sh [--fast]   (--fast skips integration tests)
set -e

FAST=false
[[ "$1" == "--fast" ]] && FAST=true

echo "=========================================="
echo " Lidstroem Test Suite"
echo "=========================================="
echo ""

run_tests() {
  local name="$1"
  local project="$2"
  echo "--- $name ---"
  dotnet test "$project" --no-build --logger "console;verbosity=minimal"
  echo ""
}

echo "Building solution..."
dotnet build Lidstroem.sln --configuration Release --verbosity quiet
echo ""

run_tests "Core unit tests"           "Tests/Core/Lidstroem.Tests.Core.csproj"
run_tests "Infrastructure unit tests" "Tests/Infrastructure/Lidstroem.Tests.Infrastructure.csproj"

if [ "$FAST" = false ]; then
  run_tests "Plugin contract + integration" "Tests/Integration/Lidstroem.Tests.Integration.csproj"
  run_tests "Plugin functional tests"       "Tests/Plugins/Lidstroem.Tests.Plugins.csproj"
else
  echo "--- Integration + Plugin tests skipped (--fast mode) ---"
fi

echo "=========================================="
echo " All tests passed."
echo "=========================================="
