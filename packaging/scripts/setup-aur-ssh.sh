#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <private-key-file>" >&2
    exit 1
fi

key_file="$1"

mkdir -p ~/.ssh
cp "$key_file" ~/.ssh/aur
chmod 600 ~/.ssh/aur

cat > ~/.ssh/config <<'EOF'
Host aur.archlinux.org
  IdentityFile ~/.ssh/aur
  User aur
  IdentitiesOnly yes
  StrictHostKeyChecking yes
EOF

for attempt in 1 2 3 4 5; do
    if ssh-keyscan -H aur.archlinux.org >> ~/.ssh/known_hosts 2>/dev/null; then
        exit 0
    fi

    if [[ "$attempt" -eq 5 ]]; then
        echo "ERROR: Failed to collect aur.archlinux.org host keys after ${attempt} attempts" >&2
        exit 1
    fi

    sleep_seconds=$((attempt * 2))
    echo "ssh-keyscan failed; retrying in ${sleep_seconds} seconds..." >&2
    sleep "$sleep_seconds"
done
