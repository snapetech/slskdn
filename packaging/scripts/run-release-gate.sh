#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

section() {
    echo
    echo "==> $1"
}

section "Validate packaging metadata"
bash packaging/scripts/validate-packaging-metadata.sh

section "Install frontend dependencies"
npm --prefix src/web ci --legacy-peer-deps

section "Run frontend unit tests"
npm --prefix src/web test

section "Build frontend"
npm --prefix src/web run build

section "Verify built frontend output"
node src/web/scripts/verify-build-output.mjs

section "Smoke built frontend under a subpath"
node src/web/scripts/smoke-subpath-build.mjs

section "Run backend unit tests"
dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release

section "Run backend smoke/regression tests"
dotnet test tests/slskd.Tests/slskd.Tests.csproj -c Release

section "Run backend integration smoke tests"
bash packaging/scripts/run-release-integration-smoke.sh

echo
echo "Release gate passed."
