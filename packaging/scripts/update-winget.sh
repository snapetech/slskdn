#!/bin/bash
set -e
TAG=$1
SHA=$2
URL="https://github.com/snapetech/slskdn/releases/download/${TAG}/slskdn-${TAG}-win-x64.zip"
VERSION=$TAG

# Assumes we are in the winget-pkgs repo root
BRANCH="update-slskdn-${VERSION}"
git checkout -b $BRANCH

PKG_DIR="manifests/s/snapetech/slskdn/${VERSION}"
mkdir -p $PKG_DIR

cat > $PKG_DIR/snapetech.slskdn.installer.yaml <<YAML
PackageIdentifier: snapetech.slskdn
PackageVersion: $VERSION
InstallerLocale: en-US
MinimumOSVersion: 10.0.0.0
InstallerType: zip
Installers:
  - Architecture: x64
    InstallerUrl: $URL
    InstallerSha256: $SHA
    NestedInstallerType: portable
    NestedInstallerFiles:
      - RelativeFilePath: slskd/slskd.exe
        PortableCommandAlias: slskdn
ManifestType: installer
ManifestVersion: 1.5.0
YAML

cat > $PKG_DIR/snapetech.slskdn.locale.en-US.yaml <<YAML
PackageIdentifier: snapetech.slskdn
PackageVersion: $VERSION
PackageLocale: en-US
Publisher: snapetech
PublisherUrl: https://github.com/snapetech/slskdn
PublisherSupportUrl: https://github.com/snapetech/slskdn/issues
PackageName: slskdn
PackageUrl: https://github.com/snapetech/slskdn
License: AGPL-3.0-or-later
LicenseUrl: https://github.com/snapetech/slskdn/blob/master/LICENSE
ShortDescription: Batteries-included Soulseek web client
Tags:
  - soulseek
  - p2p
  - file-sharing
  - music
ManifestType: defaultLocale
ManifestVersion: 1.5.0
YAML

cat > $PKG_DIR/snapetech.slskdn.yaml <<YAML
PackageIdentifier: snapetech.slskdn
PackageVersion: $VERSION
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.5.0
YAML

git add .
git commit -m "New version: snapetech.slskdn version $VERSION"
git push -u origin $BRANCH

gh pr create --repo microsoft/winget-pkgs \
  --title "Update snapetech.slskdn to $VERSION" \
  --body "Updates slskdn to version $VERSION. Release notes: https://github.com/snapetech/slskdn/releases/tag/$TAG" \
  --base master \
  --head snapetech:$BRANCH
