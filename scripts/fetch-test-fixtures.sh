#!/usr/bin/env bash
# Fetches test-fixture binaries from remote sources into test-data/slskdn-test-fixtures.
# Run from repo root or set FIXTURES_DIR. Requires: python3, curl or wget.
# Populated tree is suitable for slskdn shares (point config at the fixtures directory).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FIXTURES="${FIXTURES_DIR:-$REPO_ROOT/test-data/slskdn-test-fixtures}"

if [[ ! -f "$FIXTURES/meta/manifest.json" ]]; then
  echo "ERROR: Fixtures not found at $FIXTURES (missing meta/manifest.json)." >&2
  echo "  Set FIXTURES_DIR if the fixtures tree is elsewhere." >&2
  exit 1
fi

"$FIXTURES/meta/fetch_media.sh"
