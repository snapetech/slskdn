#!/usr/bin/env bash
# Verify release artifacts: download, compute SHA256, optionally run binary for version.
# Usage:
#   ./scripts/verify-release-artifacts.sh [TAG]
#   TAG defaults to latest dev tag (build-dev-*). Use e.g. build-dev-0.24.1.dev.91769607746
set -e

REPO="${REPO:-snapetech/slskdn}"
TAG="${1:-}"

if [ -z "$TAG" ]; then
  echo "Fetching latest dev release tag..."
  # gh release list columns: title, type, tag, date (tab-separated)
  TAG=$(gh release list --repo "$REPO" --limit 20 | awk -F'\t' '$3 ~ /^build-dev-/ {print $3; exit}')
  if [ -z "$TAG" ]; then
    echo "No build-dev-* release found. Pass TAG explicitly, e.g. build-dev-0.24.1.dev.91769607746"
    exit 1
  fi
  echo "Using tag: $TAG"
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
cd "$WORK_DIR"

echo "Downloading assets from $REPO @ $TAG..."
gh release download "$TAG" --repo "$REPO" --dir . || { echo "Download failed (gh auth? tag exists?)"; exit 1; }

echo ""
echo "=== SHA256 checksums (computed from downloaded files) ==="
for f in *.zip; do
  [ -f "$f" ] || continue
  sha=$(sha256sum "$f" | awk '{print $1}')
  echo "$sha  $f"
done

echo ""
echo "=== Version check (Linux x64 binary) ==="
LINUX_ZIP=""
for f in slskdn-dev-linux-x64.zip slskdn-main-linux-x64.zip slskdn-*-linux-x64.zip; do
  if [ -f "$f" ]; then LINUX_ZIP="$f"; break; fi
done
if [ -n "$LINUX_ZIP" ]; then
  unzip -q -o "$LINUX_ZIP" -d extracted
  if [ -f extracted/slskd ]; then
    chmod +x extracted/slskd
    ./extracted/slskd --version 2>/dev/null || ./extracted/slskd -v 2>/dev/null || echo "(binary did not print version)"
  else
    echo "No extracted/slskd found; listing:"
    ls -la extracted/
  fi
else
  echo "No Linux x64 zip found to run version check."
fi

echo ""
echo "To compare with published hashes:"
echo "  - Chocolatey/Winget/release notes: SHA256 hex (as above)."
echo "  - Nix flake.nix: uses SRI/base32 (nix-hash --type sha256 --flat --base32 <file>)."
