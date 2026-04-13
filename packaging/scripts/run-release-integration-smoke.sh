#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

UNIT_FILTER='FullyQualifiedName~ApplicationLifecycleTests|FullyQualifiedName~DownloadServiceTests|FullyQualifiedName~ProgramPathNormalizationTests|FullyQualifiedName~ConnectionWatchdogTests'
INTEGRATION_FILTER='FullyQualifiedName~LoadTests|FullyQualifiedName~DisasterModeIntegrationTests|FullyQualifiedName~SoulbeetAdvancedModeTests|FullyQualifiedName~CanonicalSelectionTests|FullyQualifiedName~LibraryHealthTests|FullyQualifiedName~VersionedApiRoutesIntegrationTests|FullyQualifiedName~SecurityRoutesIntegrationTests|FullyQualifiedName~NicotinePlusIntegrationTests'

echo
echo "==> Run backend unit smoke tests"
echo "dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release --filter \"$UNIT_FILTER\" -v minimal"
dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release --filter "$UNIT_FILTER" -v minimal

echo
echo "==> Run backend integration smoke tests"
echo "dotnet test tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -c Release --filter \"$INTEGRATION_FILTER\" -v minimal"
dotnet test tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -c Release --filter "$INTEGRATION_FILTER" -v minimal

echo
echo "Release smoke passed."
