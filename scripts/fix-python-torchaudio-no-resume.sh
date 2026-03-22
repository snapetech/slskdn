#!/usr/bin/env bash
# Run this when `python-torchaudio` fails with: "HTTP server does not seem to support byte ranges. Cannot resume."

set -euo pipefail

if [[ ! -x /usr/bin/makepkg ]]; then
  echo "makepkg is required to build AUR packages."
  exit 1
fi

if [[ ! -x /usr/bin/wget ]]; then
  echo "wget is required for this script. Install wget first."
  exit 1
fi

aur_helper=
if command -v yay >/dev/null 2>&1; then
  aur_helper=$(command -v yay)
elif command -v paru >/dev/null 2>&1; then
  aur_helper=$(command -v paru)
else
  echo "An AUR helper is required for this workflow. Install yay or paru first."
  exit 1
fi

cache_root="${HOME}/.cache/$(basename "$aur_helper")"
cache_dir="${cache_root}/python-torchaudio"

if [[ ! -d "$cache_root" ]]; then
  mkdir -p "$cache_root"
fi

echo "Refreshing python-torchaudio AUR sources with ${aur_helper}..."
rm -rf "$cache_dir"
mkdir -p "$cache_root"
cd "$cache_root"
"$aur_helper" -G python-torchaudio >/dev/null

if [[ ! -d "$cache_dir" ]]; then
  echo "Could not locate AUR cache dir: $cache_dir"
  exit 1
fi

echo "Cleaning partial python-torchaudio sources in $cache_dir"
find "$cache_dir" -maxdepth 4 -type f \
  \( -name '*.part' -o -name '*.tmp' -o -name '*.tar.gz' -o -name '*.tar.xz' \) \
  -delete

rm -rf "$cache_dir/src"

makepkg_conf="$(mktemp)"
trap 'rm -f "$makepkg_conf"' EXIT

cat > "$makepkg_conf" <<'EOF'
source /etc/makepkg.conf

# GitHub archive endpoints used by some AUR PKGBUILDs do not support byte-range downloads.
# Use wget without resume support for this build to avoid "curl ... Cannot resume".
DLAGENTS=(
    'file::/usr/bin/cp -f %u %o'
    'ftp::/usr/bin/wget --passive-ftp --show-progress -O %o %u'
    'http::/usr/bin/wget --passive-ftp --show-progress -O %o %u'
    'https::/usr/bin/wget --passive-ftp --show-progress -O %o %u'
    'rsync::/usr/bin/rsync -L %u %o'
)
EOF

cd "$cache_dir"

echo "Building python-torchaudio from cached AUR sources with no-resume downloader"
makepkg --config "$makepkg_conf" -s --noconfirm

pkg_file=("$PWD"/python-torchaudio-*.pkg.tar.*)
if [[ ! -f ${pkg_file[0]} ]]; then
  echo "Built package not found in $PWD"
  echo "Expected pattern: python-torchaudio-*.pkg.tar.*"
  exit 1
fi

echo "Installing ${pkg_file[0]} (non-interactive)"
yes | sudo pacman -U --needed "${pkg_file[0]}"
