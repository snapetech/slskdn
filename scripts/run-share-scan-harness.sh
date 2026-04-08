#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-${SLSKDN_SHARE_SCAN_ROOT:-}}"
WORKERS="${SLSKDN_SHARE_SCAN_WORKERS:-1}"

if [[ -z "${ROOT}" ]]; then
  echo "Usage: scripts/run-share-scan-harness.sh /path/to/share/root" >&2
  echo "   or: SLSKDN_SHARE_SCAN_ROOT=/path/to/share/root scripts/run-share-scan-harness.sh" >&2
  exit 1
fi

export SLSKDN_SHARE_SCAN_ROOT="${ROOT}"
export SLSKDN_SHARE_SCAN_WORKERS="${WORKERS}"

dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj --filter "FullyQualifiedName~ShareScannerHarnessTests" -v minimal
