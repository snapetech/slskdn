#!/usr/bin/env bash
# Run this ON 192.168.50.48 (e.g. ssh 192.168.50.48 'bash -s' < fix-aur-build-192.168.50.48.sh)
# Fixes AUR slskdn build: npm peer conflict (react-scripts vs typescript@5) by using --legacy-peer-deps.

set -e
CACHE="${HOME}/.cache/yay/slskdn"
if [[ ! -d "$CACHE" ]]; then
  echo "Running yay to fetch slskdn first..."
  yay -G slskdn 2>/dev/null || true
  CACHE="${HOME}/.cache/yay/slskdn"
fi
if [[ ! -f "$CACHE/PKGBUILD" ]]; then
  echo "No $CACHE/PKGBUILD found. Run: yay -S slskdn (let it fail), then run this script again."
  exit 1
fi

echo "Patching PKGBUILD in $CACHE"
sed -i 's/npm ci$/npm ci --legacy-peer-deps/' "$CACHE/PKGBUILD"
sed -i 's/npm run build$/DISABLE_ESLINT_PLUGIN=true npm run build/' "$CACHE/PKGBUILD"
grep -E 'npm ci|npm run build' "$CACHE/PKGBUILD" || true

echo "Building and installing..."
cd "$CACHE"
makepkg -si
