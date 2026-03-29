#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

EXPECTED_PATH=".githooks"
CURRENT_PATH="$(git config --local --get core.hooksPath || true)"
MODE="${1:-install}"

print_status() {
  echo "git hooks: core.hooksPath=${1}"
}

case "$MODE" in
  install)
    git config --local core.hooksPath "$EXPECTED_PATH"
    print_status "$EXPECTED_PATH"
    ;;
  --check|check)
    if [[ "$CURRENT_PATH" != "$EXPECTED_PATH" ]]; then
      echo "git hooks: expected core.hooksPath=${EXPECTED_PATH}, found ${CURRENT_PATH:-<unset>}" >&2
      exit 1
    fi

    print_status "$CURRENT_PATH"
    ;;
  *)
    echo "usage: $0 [install|--check]" >&2
    exit 1
    ;;
esac
