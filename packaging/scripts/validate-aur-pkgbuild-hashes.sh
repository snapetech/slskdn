#!/usr/bin/env bash

set -euo pipefail

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <PKGBUILD> [<PKGBUILD> ...]" >&2
    exit 1
fi

fail() {
    echo "ERROR: $1" >&2
    exit 1
}

get_checksums() {
    local pb="$1"
    local line
    local sum_line

    line=$(grep '^sha256sums=' "$pb" | head -n 1)
    [[ -n "$line" ]] || fail "${pb}: missing sha256sums"

    sum_line="${line#sha256sums=(}"
    sum_line="${sum_line%)}"
    sum_line="${sum_line//\"/}"
    sum_line="${sum_line//\'/}"
    read -r -a result <<< "$sum_line"
    echo "${result[@]}"
}

validate_pkgbuild() {
    local pkgbuild="$1"
    local dir
    local -a sums
    local i
    local expected
    local actual
    local source_file

    [[ -f "$pkgbuild" ]] || fail "PKGBUILD missing: $pkgbuild"
    dir="$(cd "$(dirname "$pkgbuild")" && pwd)"

    read -r -a sums <<< "$(get_checksums "$pkgbuild")"

    if [[ "${#sums[@]}" -lt 4 ]]; then
        fail "${pkgbuild}: sha256sums must include archive + 3 static files"
    fi

    if [[ "${sums[0]}" != "SKIP" ]]; then
        fail "${pkgbuild}: archive checksum must stay SKIP for mutable AUR release assets"
    fi

    local -a release_files=("slskd.service" "slskd.yml" "slskd.sysusers")
    for i in 0 1 2; do
        expected="${sums[$((i + 1))]:-}"
        source_file="${dir}/${release_files[$i]}"

        if [[ -z "$expected" ]]; then
            fail "${pkgbuild}: missing hash for ${release_files[$i]}"
        fi

        if [[ "$expected" == "SKIP" || "$expected" == *"PLACEHOLDER"* ]]; then
            continue
        fi

        [[ -f "$source_file" ]] || fail "${pkgbuild}: expected local source file ${release_files[$i]}"
        actual="$(sha256sum "$source_file" | awk '{print $1}')"
        if [[ "$actual" != "$expected" ]]; then
            fail "${pkgbuild}: checksum mismatch for ${release_files[$i]} (${actual} != ${expected})"
        fi
    done
}

for pkgbuild in "$@"; do
    validate_pkgbuild "$pkgbuild"
done

echo "AUR PKGBUILD checksum validation passed: $*"
