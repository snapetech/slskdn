#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 2 ]]; then
    echo "Usage: $0 <aur-package-name> <target-dir>" >&2
    exit 1
fi

package_name="$1"
target_dir="$2"
read_url="https://aur.archlinux.org/${package_name}.git"
push_url="ssh://aur@aur.archlinux.org/${package_name}.git"

for attempt in 1 2 3 4 5; do
    rm -rf "$target_dir"

    if git clone "$read_url" "$target_dir"; then
        (
            cd "$target_dir"
            git remote set-url --push origin "$push_url"
        )
        echo "Cloned AUR repo ${package_name} via HTTPS and configured SSH push"
        exit 0
    fi

    if [[ "$attempt" -eq 5 ]]; then
        echo "ERROR: Failed to clone AUR repo ${package_name} after ${attempt} attempts" >&2
        exit 1
    fi

    sleep_seconds=$((attempt * 2))
    echo "Clone failed for ${package_name}; retrying in ${sleep_seconds} seconds..." >&2
    sleep "$sleep_seconds"
done
