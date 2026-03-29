#!/usr/bin/env bash
# Repo-backed GitHub Release notes (same idea as iptvTunerr).
# Prefers docs/CHANGELOG.md section for the logical version, then Unreleased,
# then commit summaries for the range since the previous tag.
#
# Usage:
#   ./scripts/generate-release-notes.sh <logical-version> <out-path> [git-ref]
#
# <logical-version> should match a heading in docs/CHANGELOG.md:
#   ## [<logical-version>] — YYYY-MM-DD   or   ## [<logical-version>]
#
# <git-ref> defaults to refs/tags/<logical-version> if that tag exists,
# otherwise GITHUB_SHA or HEAD (for releases where the version tag is created
# by the release action after this script runs).

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

err() {
  echo "[generate-release-notes] ERROR: $*" >&2
  exit 1
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

CHANGELOG_PATH="${CHANGELOG_PATH:-docs/CHANGELOG.md}"

extract_changelog_section() {
  local heading="$1"
  [[ -f "$CHANGELOG_PATH" ]] || return 0
  awk -v heading="$heading" '
    $0 == heading { in_section = 1; next }
    in_section && /^## \[/ { exit }
    in_section && /^---$/ { next }
    in_section { print }
  ' "$CHANGELOG_PATH" | trim_blank_edges
}

normalize_remote_url() {
  local raw="$1"
  case "$raw" in
    https://github.com/*)
      printf '%s\n' "${raw%.git}"
      ;;
    git@github.com:*)
      raw="${raw#git@github.com:}"
      printf 'https://github.com/%s\n' "${raw%.git}"
      ;;
    *)
      printf '%s\n' "${raw%.git}"
      ;;
  esac
}

subject_highlight() {
  local subject="$1"
  subject="$(printf '%s' "$subject" | sed -E 's/^[a-z]+(\([^)]+\))?:[[:space:]]*//')"
  printf '%s' "$subject" | awk '
    {
      line = $0
      if (length(line) == 0) next
      first = substr(line, 1, 1)
      rest = substr(line, 2)
      line = toupper(first) rest
      if (line !~ /[.!?]$/) line = line "."
      print line
    }
  '
}

is_release_hygiene_subject() {
  local subject="$1"
  [[ "$subject" =~ ^docs:\ Add\ gotcha\ for\  ]] && return 0
  [[ "$subject" =~ ^docs:\ add\ release\ notes\  ]] && return 0
  [[ "$subject" =~ ^chore\(release\):\ update\ stable\ metadata\  ]] && return 0
  return 1
}

LOGICAL_VERSION="${1:-}"
OUT_PATH="${2:-$ROOT_DIR/dist/release-notes.md}"
GIT_REF_ARG="${3:-}"

[[ -n "$LOGICAL_VERSION" ]] || err "usage: $0 <logical-version> <output-path> [git-ref]"

REMOTE_URL="$(git config --get remote.origin.url || true)"
REPO_URL="$(normalize_remote_url "$REMOTE_URL")"

# Resolve anchor ref for dates + commit range
if [[ -n "$GIT_REF_ARG" ]]; then
  GIT_REF="$GIT_REF_ARG"
elif git rev-parse -q --verify "refs/tags/${LOGICAL_VERSION}" >/dev/null 2>&1; then
  GIT_REF="refs/tags/${LOGICAL_VERSION}"
else
  GIT_REF="${GITHUB_SHA:-HEAD}"
fi

git rev-parse -q --verify "$GIT_REF" >/dev/null || err "git ref not found: $GIT_REF"

RELEASE_DATE="$(git log -1 --format=%cs "$GIT_REF")"

# Previous tag: stable slskdn semver tags first when this release uses that scheme
logical_is_release_version() {
  [[ "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+-slskdn\.[0-9]+$ ]]
}

if logical_is_release_version "$LOGICAL_VERSION"; then
  PREV_TAG="$(
    git tag --sort=-v:refname |
      grep -E '^[0-9]+\.[0-9]+\.[0-9]+-slskdn\.[0-9]+$' |
      awk -v cur="$LOGICAL_VERSION" '$0 != cur { print; exit }' || true
  )"
  if [[ -z "$PREV_TAG" ]]; then
    PREV_TAG="$(
      git tag --sort=-v:refname | awk -v cur="$LOGICAL_VERSION" '$0 != cur { print; exit }' || true
    )"
  fi
else
  PREV_TAG="$(
    git tag --sort=-v:refname | awk -v cur="$LOGICAL_VERSION" '$0 != cur { print; exit }' || true
  )"
fi

TAG_SECTION="$(extract_changelog_section "## [$LOGICAL_VERSION] — $RELEASE_DATE" || true)"
if [[ -z "$TAG_SECTION" && -f "$CHANGELOG_PATH" ]]; then
  TAG_SECTION="$(
    awk -v ver="$LOGICAL_VERSION" '
      $0 ~ "^## \\[" ver "\\]" { in_section = 1; next }
      in_section && /^## \[/ { exit }
      in_section { print }
    ' "$CHANGELOG_PATH" | trim_blank_edges || true
  )"
fi
UNRELEASED_SECTION="$(extract_changelog_section "## [Unreleased]" || true)"

if [[ "$UNRELEASED_SECTION" == "- *(none)*" ]]; then
  UNRELEASED_SECTION=""
fi

if [[ -n "$PREV_TAG" ]]; then
  RANGE="${PREV_TAG}..${GIT_REF}"
  COMPARE_URL="$REPO_URL/compare/${PREV_TAG}...${LOGICAL_VERSION}"
else
  RANGE="${GIT_REF}^!"
  COMPARE_URL=""
fi

COMMITS="$(git log --reverse --no-merges --format='%H%x09%s' "$RANGE" 2>/dev/null || true)"
if [[ -z "$COMMITS" ]]; then
  COMMITS="$(git log --reverse --format='%H%x09%s' "$RANGE" 2>/dev/null || true)"
fi

DISPLAY_COMMITS=""
if [[ -n "$COMMITS" ]]; then
  while IFS=$'\t' read -r sha subject; do
    [[ -n "$sha" ]] || continue
    if is_release_hygiene_subject "$subject"; then
      continue
    fi

    DISPLAY_COMMITS+="${sha}"$'\t'"${subject}"$'\n'
  done <<<"$COMMITS"

  DISPLAY_COMMITS="$(printf '%s' "$DISPLAY_COMMITS" | trim_blank_edges || true)"
fi

PRODUCT_NAME="${RELEASE_NOTES_PRODUCT_NAME:-}"
if [[ -z "$PRODUCT_NAME" ]]; then
  PRODUCT_NAME="$(basename "$(git config --get remote.origin.url || echo slskdn)" .git)"
fi

mkdir -p "$(dirname "$OUT_PATH")"

{
  printf '# %s %s\n\n' "$PRODUCT_NAME" "$LOGICAL_VERSION"
  printf 'Released: %s\n\n' "$RELEASE_DATE"

  if [[ -n "$PREV_TAG" && -n "$REPO_URL" ]]; then
    printf 'Compare: [%s...%s](%s)\n\n' "$PREV_TAG" "$LOGICAL_VERSION" "$COMPARE_URL"
  elif [[ -n "$PREV_TAG" ]]; then
    printf 'Compare: `%s...%s`\n\n' "$PREV_TAG" "$LOGICAL_VERSION"
  fi

  printf '## What Changed\n\n'

  if [[ -n "$TAG_SECTION" ]]; then
    printf '%s\n\n' "$TAG_SECTION"
    printf '_Source: `%s` section for `%s`._\n\n' "$CHANGELOG_PATH" "$LOGICAL_VERSION"
  elif [[ -n "$UNRELEASED_SECTION" ]]; then
    printf '%s\n\n' "$UNRELEASED_SECTION"
    printf '_Source: `%s` `Unreleased` section at release time._\n\n' "$CHANGELOG_PATH"
  else
    if [[ -z "$DISPLAY_COMMITS" ]]; then
      printf '%s\n\n' "- No recorded changes found for \`${LOGICAL_VERSION}\`."
    else
      while IFS=$'\t' read -r sha subject; do
        highlight="$(subject_highlight "$subject")"
        if [[ -n "$highlight" ]]; then
          printf '%s\n' "- ${highlight}"
        fi
      done <<<"$DISPLAY_COMMITS"
      printf '\n'
    fi
  fi

  printf '## Included Commits\n\n'
  if [[ -z "$DISPLAY_COMMITS" ]]; then
    printf '%s\n' "- No product commits listed for \`${LOGICAL_VERSION}\` after filtering release-hygiene docs commits."
  else
    while IFS=$'\t' read -r sha subject; do
      short_sha="${sha:0:7}"
      if [[ -n "$REPO_URL" ]]; then
        # Subject may contain '%'; avoid printf format parsing on the message body.
        printf '%s\n' "- \`${short_sha}\` ${subject} ([commit](${REPO_URL}/commit/${sha}))"
      else
        printf '%s\n' "- \`${short_sha}\` ${subject}"
      fi
    done <<<"$DISPLAY_COMMITS"
  fi
} >"$OUT_PATH"

printf 'Wrote %s\n' "$OUT_PATH"
