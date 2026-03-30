#!/usr/bin/env bash

set -euo pipefail

if [[ $# -lt 3 || $# -gt 4 ]]; then
    echo "Usage: $0 <repo-dir> <aur-package-name> <commit-message> [branch]" >&2
    exit 1
fi

repo_dir="$1"
package_name="$2"
commit_message="$3"
branch="${4:-master}"

cd "$repo_dir"

git config user.name "slskdn CI"
git config user.email "slskdn@proton.me"

git commit -m "$commit_message" || echo "No changes"

for attempt in 1 2 3 4 5; do
    if git push origin HEAD:"$branch"; then
        echo "Pushed to AUR: ${package_name}"
        exit 0
    fi

    if [[ "$attempt" -eq 5 ]]; then
        echo "ERROR: Failed to push AUR repo ${package_name} after ${attempt} attempts" >&2
        exit 1
    fi

    sleep_seconds=$((attempt * 2))
    echo "Push failed for ${package_name}; rebasing and retrying in ${sleep_seconds} seconds..." >&2
    sleep "$sleep_seconds"

    if git fetch origin "$branch" && git pull --rebase origin "$branch"; then
        continue
    fi

    echo "Fetch/rebase failed for ${package_name}; retrying push on next attempt..." >&2
done
