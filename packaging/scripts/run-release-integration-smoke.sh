#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

FILTER='FullyQualifiedName~LoadTests|FullyQualifiedName~DisasterModeIntegrationTests|FullyQualifiedName~SoulbeetAdvancedModeTests|FullyQualifiedName~CanonicalSelectionTests|FullyQualifiedName~LibraryHealthTests'

echo
echo "==> Run backend integration smoke tests"
echo "dotnet test tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -c Release --filter \"$FILTER\""
dotnet test tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -c Release --filter "$FILTER"

echo
echo "Release integration smoke passed."
