#!/usr/bin/env bash

set -euo pipefail

if [ "$#" -ne 4 ]; then
    echo "Usage: $0 <template> <version> <commit> <output>" >&2
    exit 1
fi

template="$1"
version="$2"
commit="$3"
output="$4"

sed \
    -e "s|__VERSION__|${version}|g" \
    -e "s|__COMMIT__|${commit}|g" \
    "$template" > "$output"
