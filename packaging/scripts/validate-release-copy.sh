#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

fail() {
    echo "ERROR: $1" >&2
    exit 1
}

expect_contains() {
    local file="$1"
    local pattern="$2"
    grep -qi -- "$pattern" "$file" || fail "$file is missing required copy: $pattern"
}

expect_max_chars() {
    local file="$1"
    local max_chars="$2"
    local chars
    chars=$(wc -m < "$file")
    if [ "$chars" -gt "$max_chars" ]; then
        fail "$file is ${chars} chars, exceeding budget ${max_chars}"
    fi
}

# GitHub releases do not document a tight body cap, but we keep these under an
# internal readability budget so release pages stay scannable and don't bloat.
expect_max_chars .github/release-notes/main.md.tmpl 4800
expect_max_chars .github/release-notes/dev.md.tmpl 4200

expect_contains .github/release-notes/main.md.tmpl 'SongID'
expect_contains .github/release-notes/main.md.tmpl 'Discovery Graph'
expect_contains .github/release-notes/dev.md.tmpl 'SongID'
expect_contains .github/release-notes/dev.md.tmpl 'Discovery Graph'

# Winget schema limits come from https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json
short_desc_main=$(sed -n 's/^ShortDescription: //p' packaging/winget/snapetech.slskdn.locale.en-US.yaml)
short_desc_dev=$(sed -n 's/^ShortDescription: //p' packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml)

if [ "${#short_desc_main}" -gt 256 ]; then
    fail "stable winget ShortDescription exceeds 256 chars"
fi

if [ "${#short_desc_dev}" -gt 256 ]; then
    fail "dev winget ShortDescription exceeds 256 chars"
fi

expect_contains packaging/winget/snapetech.slskdn.locale.en-US.yaml 'SongID'
expect_contains packaging/winget/snapetech.slskdn.locale.en-US.yaml 'Discovery Graph'
expect_contains packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml 'SongID'
expect_contains packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml 'Discovery Graph'

echo "Release copy validation passed."
