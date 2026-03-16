#!/usr/bin/env bash

set -euo pipefail

CHANNEL="${1:-}"
VERSION="${2:-}"
WIN_X64_URL="${3:-}"
WIN_X64_SHA="${4:-}"
RELEASE_TAG="${5:-$VERSION}"

if [ -z "$CHANNEL" ] || [ -z "$VERSION" ] || [ -z "$WIN_X64_URL" ] || [ -z "$WIN_X64_SHA" ]; then
    echo "Usage: $0 <stable|dev> <version> <win-x64-url> <win-x64-sha> [release-tag]"
    exit 1
fi

case "$CHANNEL" in
    stable)
        PACKAGE_IDENTIFIER="snapetech.slskdn"
        FILE_BASENAME="snapetech.slskdn"
        PACKAGE_NAME="slskdN"
        MONIKER="slskdn"
        COMMAND_ALIAS="slskdn"
        SHORT_DESCRIPTION="Batteries-included Soulseek web client"
        DESCRIPTION=$(cat <<'EOF'
slskdN is a batteries-included fork of slskd with advanced download features,
automation, and network enhancements for Soulseek.

Stable features include:
- Multi-source swarm downloads
- DHT mesh networking
- MusicBrainz integration
- Wishlist and background search
- Security hardening
EOF
)
        RELEASE_NOTES=$(cat <<EOF
Stable Release $VERSION

See https://github.com/snapetech/slskdn/releases/tag/$RELEASE_TAG for details.
EOF
)
        RELEASE_NOTES_URL="https://github.com/snapetech/slskdn/releases/tag/$RELEASE_TAG"
        ;;
    dev)
        PACKAGE_IDENTIFIER="snapetech.slskdn-dev"
        FILE_BASENAME="snapetech.slskdn-dev"
        PACKAGE_NAME="slskdN (Development)"
        MONIKER="slskdn-dev"
        COMMAND_ALIAS="slskdn-dev"
        SHORT_DESCRIPTION="Batteries-included Soulseek web client (Development Build)"
        DESCRIPTION=$(cat <<'EOF'
slskdN is an experimental fork of slskd exploring advanced download features,
protocol extensions, and network enhancements for Soulseek.

WARNING: This is an unstable development build.

Features in development builds:
- Multi-source swarm downloads
- DHT mesh network with content verification
- BitTorrent DHT rendezvous for peer discovery
- TLS-secured mesh connections
- Distributed hash database with mesh sync
EOF
)
        RELEASE_NOTES=$(cat <<EOF
Development Build $VERSION

See https://github.com/snapetech/slskdn/releases/tag/$RELEASE_TAG for details.
EOF
)
        RELEASE_NOTES_URL="https://github.com/snapetech/slskdn/releases/tag/$RELEASE_TAG"
        ;;
    *)
        echo "Unknown channel: $CHANNEL"
        exit 1
        ;;
esac

WINGET_VERSION="$(echo "$VERSION" | sed 's/-/./g')"
MANIFEST_DIR="packaging/winget"
INSTALLER_FILE="$MANIFEST_DIR/${FILE_BASENAME}.installer.yaml"
LOCALE_FILE="$MANIFEST_DIR/${FILE_BASENAME}.locale.en-US.yaml"
VERSION_FILE="$MANIFEST_DIR/${FILE_BASENAME}.yaml"
RELEASE_DATE="$(date -u +%Y-%m-%d)"

cat > "$INSTALLER_FILE" <<EOF
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.installer.1.6.0.schema.json

PackageIdentifier: $PACKAGE_IDENTIFIER
PackageVersion: $WINGET_VERSION
InstallerType: zip
InstallerSwitches:
  Silent: ""
  SilentWithProgress: ""
UpgradeBehavior: install
ReleaseDate: $RELEASE_DATE
Installers:
  - Architecture: x64
    InstallerUrl: $WIN_X64_URL
    InstallerSha256: $WIN_X64_SHA
    NestedInstallerType: portable
    NestedInstallerFiles:
      - RelativeFilePath: slskd.exe
        PortableCommandAlias: $COMMAND_ALIAS
ManifestType: installer
ManifestVersion: 1.6.0
EOF

cat > "$LOCALE_FILE" <<EOF
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json

PackageIdentifier: $PACKAGE_IDENTIFIER
PackageVersion: $WINGET_VERSION
PackageLocale: en-US
Publisher: slskdN Team
PublisherUrl: https://github.com/snapetech
PublisherSupportUrl: https://github.com/snapetech/slskdn/issues
PackageName: $PACKAGE_NAME
PackageUrl: https://github.com/snapetech/slskdn
License: AGPL-3.0-or-later
LicenseUrl: https://github.com/snapetech/slskdn/blob/main/LICENSE
ShortDescription: $SHORT_DESCRIPTION
Description: |-
$(printf '%s\n' "$DESCRIPTION" | sed 's/^/  /')
Moniker: $MONIKER
Tags:
  - soulseek
  - p2p
  - music
  - filesharing
ReleaseNotes: |-
$(printf '%s\n' "$RELEASE_NOTES" | sed 's/^/  /')
ReleaseNotesUrl: $RELEASE_NOTES_URL
ManifestType: defaultLocale
ManifestVersion: 1.6.0
EOF

cat > "$VERSION_FILE" <<EOF
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.version.1.6.0.schema.json

PackageIdentifier: $PACKAGE_IDENTIFIER
PackageVersion: $WINGET_VERSION
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
EOF

echo "Updated Winget manifests for $CHANNEL:"
echo "  - $INSTALLER_FILE"
echo "  - $LOCALE_FILE"
echo "  - $VERSION_FILE"
