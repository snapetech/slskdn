#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

VERSION="${1:-}"
LINUX_X64_HEX="${2:-}"
LINUX_ARM64_HEX="${3:-}"
MACOS_X64_HEX="${4:-}"
MACOS_ARM64_HEX="${5:-}"
WIN_X64_HEX="${6:-}"
RELEASE_TAG="${7:-$VERSION}"

if [[ -z "$VERSION" || -z "$LINUX_X64_HEX" || -z "$LINUX_ARM64_HEX" || -z "$MACOS_X64_HEX" || -z "$MACOS_ARM64_HEX" || -z "$WIN_X64_HEX" ]]; then
    echo "Usage: $0 <version> <linux-x64-sha256-hex> <linux-arm64-sha256-hex> <macos-x64-sha256-hex> <macos-arm64-sha256-hex> <win-x64-sha256-hex> [release-tag]" >&2
    exit 1
fi

hex_to_sri() {
    local hex="$1"
    printf 'sha256-%s' "$(printf '%s' "$hex" | xxd -r -p | base64 -w0)"
}

LINUX_X64_SRI="$(hex_to_sri "$LINUX_X64_HEX")"
LINUX_ARM64_SRI="$(hex_to_sri "$LINUX_ARM64_HEX")"
MACOS_X64_SRI="$(hex_to_sri "$MACOS_X64_HEX")"
MACOS_ARM64_SRI="$(hex_to_sri "$MACOS_ARM64_HEX")"
WIN_URL="https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-win-x64.zip"
PKGVER_DOTTED="${VERSION//-/.}"

bash packaging/scripts/update-winget-manifests.sh \
    stable \
    "$VERSION" \
    "$WIN_URL" \
    "$WIN_X64_HEX" \
    "$RELEASE_TAG"

cat > Formula/slskdn.rb <<EOF
class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "$VERSION"
  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-osx-arm64.zip"
      sha256 "$MACOS_ARM64_HEX"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-osx-x64.zip"
      sha256 "$MACOS_X64_HEX"
    end
  end
  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-linux-x64.zip"
    sha256 "$LINUX_X64_HEX"
  end
  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end
end
EOF

cat > packaging/homebrew/Formula/slskdn.rb <<EOF
class Slskdn < Formula
  desc "Batteries-included Soulseek web client"
  homepage "https://github.com/snapetech/slskdn"
  license "AGPL-3.0-or-later"
  version "$VERSION"

  on_macos do
    on_arm do
      url "https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-osx-arm64.zip"
      sha256 "$MACOS_ARM64_HEX"
    end
    on_intel do
      url "https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-osx-x64.zip"
      sha256 "$MACOS_X64_HEX"
    end
  end

  on_linux do
    url "https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-linux-x64.zip"
    sha256 "$LINUX_X64_HEX"
  end

  def install
    libexec.install Dir["*"]
    (bin/"slskd").write_exec_script libexec/"slskd"
    (bin/"slskdn").write_exec_script libexec/"slskd"
  end

  test do
    # Simple test to verify version or help output
    assert_match "slskd", shell_output("#{bin}/slskd --help", 1)
  end
end
EOF

sed -i "s/version = \".*\";/version = \"${VERSION}\";/" flake.nix
sed -i "/stableSources = {/,/};/s|sha256 = \".*\"; # x86_64-linux|sha256 = \"${LINUX_X64_SRI}\"; # x86_64-linux|" flake.nix
sed -i "/stableSources = {/,/};/s|sha256 = \".*\"; # aarch64-linux|sha256 = \"${LINUX_ARM64_SRI}\"; # aarch64-linux|" flake.nix
sed -i "/stableSources = {/,/};/s|sha256 = \".*\"; # x86_64-darwin|sha256 = \"${MACOS_X64_SRI}\"; # x86_64-darwin|" flake.nix
sed -i "/stableSources = {/,/};/s|sha256 = \".*\"; # aarch64-darwin|sha256 = \"${MACOS_ARM64_SRI}\"; # aarch64-darwin|" flake.nix

sed -i "s/^version: '.*'/version: '${VERSION}'/" packaging/snap/snapcraft.yaml
sed -i "s|^    source: .*|    source: https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-linux-x64.zip|" packaging/snap/snapcraft.yaml
sed -i "s|^    source-checksum: .*|    source-checksum: sha256/${LINUX_X64_HEX}|" packaging/snap/snapcraft.yaml

sed -i "s|# slskdN .* Linux x64|# slskdN ${VERSION} Linux x64|" packaging/flatpak/io.github.slskd.slskdn.yml
sed -i "s|url: https://github.com/snapetech/slskdn/releases/download/.*/slskdn-main-linux-x64.zip|url: https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-linux-x64.zip|" packaging/flatpak/io.github.slskd.slskdn.yml
sed -i "s|sha256: .*|sha256: ${LINUX_X64_HEX}|" packaging/flatpak/io.github.slskd.slskdn.yml

sed -i "s/appVersion: \".*\"/appVersion: \"${VERSION}\"/" packaging/truenas-scale/charts/slskdn/Chart.yaml
sed -i "s/appVersion: \".*\"/appVersion: \"${VERSION}\"/" packaging/helm/slskdn/Chart.yaml
sed -i "s/--set image.tag=.*/--set image.tag=${VERSION}/" packaging/helm/slskdn/README.md

sed -i "s#<version>.*</version>#<version>${VERSION}</version>#" packaging/chocolatey/slskdn.nuspec
sed -i "s#^\\\$url.*#\\\$url        = \"https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-win-x64.zip\"#" packaging/chocolatey/tools/chocolateyinstall.ps1
sed -i "s#^\\\$checksum.*#\\\$checksum   = \"${WIN_X64_HEX}\"#" packaging/chocolatey/tools/chocolateyinstall.ps1

sed -i "s|^Source0:.*|Source0:        https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-linux-x64.zip|" packaging/rpm/slskdn.spec
sed -i "s|^Version:.*|Version:        ${PKGVER_DOTTED}|" packaging/rpm/slskdn.spec
sed -i "s|0\\.24\\.5-slskdn\\.[0-9]\\+ (slskdn-main-linux-x64.zip)|${VERSION} (slskdn-main-linux-x64.zip)|" packaging/rpm/slskdn.spec

sed -i "1s|^slskdn (.*)|slskdn (${PKGVER_DOTTED}-1) stable; urgency=medium|" packaging/debian/changelog
sed -i "s|stable release 0\\.24\\.5-slskdn\\.[0-9]\\+|stable release ${VERSION}|" packaging/debian/changelog
sed -i "s|SLSKDN_VERSION=0\\.24\\.5-slskdn\\.[0-9]\\+|SLSKDN_VERSION=${VERSION}|" packaging/proxmox-lxc/README.md

sed -i "s|^pkgver=.*|pkgver=${PKGVER_DOTTED}|" packaging/aur/PKGBUILD
sed -i "s|^pkgver=.*|pkgver=${PKGVER_DOTTED}|" packaging/aur/PKGBUILD-bin

sed -i "s|https://github.com/snapetech/slskdn/releases/download/0\\.24\\.5-slskdn\\.[0-9]\\+/slskdn-main-linux-x64.zip|https://github.com/snapetech/slskdn/releases/download/${RELEASE_TAG}/slskdn-main-linux-x64.zip|g" packaging/flatpak/FLATHUB_SUBMISSION.md
sed -i "s|for \`0\\.24\\.5-slskdn\\.[0-9]\\+\`|for \`${VERSION}\`|" packaging/flatpak/FLATHUB_SUBMISSION.md

echo "Updated stable release metadata to ${VERSION} (tag ${RELEASE_TAG})."
