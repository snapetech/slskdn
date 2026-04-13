#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

MODE="${1:-staged}"
BASE_REF="${2:-}"
HEAD_REF="${3:-HEAD}"
CHANGELOG_PATH="docs/CHANGELOG.md"

err() {
  echo "changelog check: $*" >&2
  exit 1
}

extract_unreleased_from_stream() {
  awk '
    $0 == "## [Unreleased]" { in_section = 1; next }
    in_section && /^## \[/ { exit }
    in_section && /^---$/ { next }
    in_section { print }
  '
}

trim_blank_edges() {
  awk '
    { lines[NR] = $0 }
    END {
      start = 1
      while (start <= NR && lines[start] ~ /^[[:space:]]*$/) start++
      end = NR
      while (end >= start && lines[end] ~ /^[[:space:]]*$/) end--
      for (i = start; i <= end; i++) print lines[i]
    }
  '
}

is_placeholder_unreleased() {
  local section="$1"
  [[ -z "$section" ]] && return 0
  [[ "$section" == "_Add release notes here while developing; move only shipped bullets into a dated version section when a release is cut._" ]] && return 0
  [[ "$section" == "- *(none)*" ]] && return 0
  return 1
}

has_changelog_bullets() {
  local section="$1"
  printf '%s\n' "$section" | rg -q '^[[:space:]]*-[[:space:]]+\S'
}

is_release_worthy_path() {
  local path="$1"

  case "$path" in
    docs/CHANGELOG.md|docs/*|memory-bank/*|tests/*|.githooks/*)
      return 1
      ;;
    src/slskd/*|src/web/src/*|src/web/public/*|config/*|bin/*)
      return 0
      ;;
    packaging/aur/*|packaging/proxmox-lxc/*|packaging/snap/*|packaging/debian/*|packaging/rpm/*|packaging/nix/*|packaging/winget/*|packaging/homebrew/*)
      return 0
      ;;
    .github/workflows/ci.yml|.github/workflows/build-on-tag.yml|.github/workflows/dev-release.yml)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

collect_changed_paths() {
  case "$MODE" in
    staged)
      git diff --cached --name-only --diff-filter=ACMR
      ;;
    range)
      [[ -n "$BASE_REF" ]] || err "range mode requires <base-ref> <head-ref>"
      git diff --name-only --diff-filter=ACMR "${BASE_REF}...${HEAD_REF}"
      ;;
    *)
      err "usage: $0 [staged|range <base-ref> <head-ref>]"
      ;;
  esac
}

read_changelog_at_ref() {
  local ref="$1"

  if [[ "$ref" == ":index" ]]; then
    git show ":${CHANGELOG_PATH}" 2>/dev/null || true
    return 0
  fi

  if [[ -n "$ref" ]] && git rev-parse -q --verify "$ref" >/dev/null 2>&1; then
    git show "${ref}:${CHANGELOG_PATH}" 2>/dev/null || true
    return 0
  fi

  if [[ -f "$CHANGELOG_PATH" ]]; then
    cat "$CHANGELOG_PATH"
  fi
}

needs_changelog=0
while IFS= read -r path; do
  [[ -n "$path" ]] || continue
  if is_release_worthy_path "$path"; then
    needs_changelog=1
    break
  fi
done < <(collect_changed_paths)

if [[ "$needs_changelog" -eq 0 ]]; then
  exit 0
fi

before_ref="HEAD"
after_ref=":index"
if [[ "$MODE" == "range" ]]; then
  before_ref="$BASE_REF"
  after_ref="$HEAD_REF"
fi

before_section="$(
  read_changelog_at_ref "$before_ref" | extract_unreleased_from_stream | trim_blank_edges || true
)"
after_section="$(
  read_changelog_at_ref "$after_ref" | extract_unreleased_from_stream | trim_blank_edges || true
)"

if is_placeholder_unreleased "$after_section"; then
  err "release-worthy changes require a real bullet under ${CHANGELOG_PATH} -> ## [Unreleased]."
fi

if ! has_changelog_bullets "$after_section"; then
  err "${CHANGELOG_PATH} -> ## [Unreleased] must contain at least one bullet for release-worthy changes."
fi

if [[ "$before_section" == "$after_section" ]]; then
  err "release-worthy changes were detected, but ${CHANGELOG_PATH} -> ## [Unreleased] was not updated."
fi

exit 0
