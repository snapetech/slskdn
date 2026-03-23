#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

fail() {
    echo "ERROR: $1" >&2
    exit 1
}

fail_if_empty() {
    local value="$1"
    local label="$2"
    if [[ -z "$value" ]]; then
        fail "${label} is missing"
    fi
}

expect_line() {
    local file="$1"
    local pattern="$2"
    grep -Eq -- "$pattern" "$file" || fail "$file is missing pattern: $pattern"
}

expect_literal() {
    local file="$1"
    local pattern="$2"
    grep -Fq -- "$pattern" "$file" || fail "$file is missing literal: $pattern"
}

reject_line() {
    local file="$1"
    local pattern="$2"
    if grep -Eq -- "$pattern" "$file"; then
        fail "$file contains forbidden pattern: $pattern"
    fi
}

expect_line flake.nix 'makeWrapper \$out/libexec/\$\{pname\}/slskd \$out/bin/slskd'
expect_line flake.nix 'ln -s slskd \$out/bin/\$\{pname\}'
expect_line flake.nix 'nativeBuildInputs = \[ pkgs\.unzip pkgs\.makeWrapper pkgs\.autoPatchelfHook pkgs\.patchelf \];'
expect_line flake.nix 'pkgs\.libunwind'
expect_line flake.nix 'pkgs\.lttng-ust\.out'
expect_line flake.nix 'dontStrip = true;'
expect_line flake.nix '--replace-needed liblttng-ust\.so\.0 liblttng-ust\.so\.1'
expect_line flake.nix 'pkgs\.util-linux'
reject_line flake.nix 'releases/download/dev/'
expect_line flake.nix 'slskdn-dev = throw "slskdn-dev flake output is temporarily unavailable'

expect_line packaging/winget/snapetech.slskdn.yaml '^PackageIdentifier: snapetech\.slskdn$'
expect_line packaging/winget/snapetech.slskdn.installer.yaml '^PackageIdentifier: snapetech\.slskdn$'
expect_line packaging/winget/snapetech.slskdn.installer.yaml 'PortableCommandAlias: slskdn$'
expect_line packaging/winget/snapetech.slskdn.locale.en-US.yaml '^PackageIdentifier: snapetech\.slskdn$'
expect_line packaging/winget/snapetech.slskdn.locale.en-US.yaml '^Moniker: slskdn$'
expect_line packaging/winget/snapetech.slskdn.locale.en-US.yaml '^PackageName: slskdN$'

expect_line packaging/winget/snapetech.slskdn-dev.yaml '^PackageIdentifier: snapetech\.slskdn-dev$'
expect_line packaging/winget/snapetech.slskdn-dev.installer.yaml '^PackageIdentifier: snapetech\.slskdn-dev$'
expect_line packaging/winget/snapetech.slskdn-dev.installer.yaml 'PortableCommandAlias: slskdn-dev$'
expect_line packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml '^PackageIdentifier: snapetech\.slskdn-dev$'
expect_line packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml '^Moniker: slskdn-dev$'

expect_line packaging/homebrew/Formula/slskdn.rb '^class Slskdn < Formula$'
expect_line packaging/snap/snapcraft.yaml '^name: slskdn$'

reject_line .github/workflows/dev-release.yml 'slskdn-dev-windows-x64\.zip'
expect_line .github/workflows/release-packages.yml 'slskdn-main-linux-x64\.zip'
expect_line .github/workflows/release-packages.yml '\$\{\{ steps\.version\.outputs\.tag \}\}-linux-x64\.zip'

bash packaging/scripts/validate-release-copy.sh

CHOC_VERSION=$(sed -n 's#.*<version>\(.*\)</version>#\1#p' packaging/chocolatey/slskdn.nuspec | head -n 1)
if [[ -z "${CHOC_VERSION}" ]]; then
  fail "Could not extract Chocolatey version from packaging/chocolatey/slskdn.nuspec"
fi

HOMEBREW_VERSION=$(sed -n 's#^  version "\([^"]*\)"#\1#p' packaging/homebrew/Formula/slskdn.rb | head -n 1)
fail_if_empty "$HOMEBREW_VERSION" "Homebrew version"

HOME_URL_VERSIONS_COUNT=0
while IFS= read -r homebrew_release; do
  ((HOME_URL_VERSIONS_COUNT += 1))
  if [[ "$homebrew_release" != "$HOMEBREW_VERSION" ]]; then
    fail "Homebrew formula URL release (${homebrew_release}) does not match version ${HOMEBREW_VERSION}"
  fi
done < <(sed -n "s#.*releases/download/\([^/]*\)/.*#\1#p" packaging/homebrew/Formula/slskdn.rb | grep -v '^$')

if [[ "$HOME_URL_VERSIONS_COUNT" -ne 3 ]]; then
  fail "Homebrew formula should have exactly 3 release URLs, found $HOME_URL_VERSIONS_COUNT"
fi

SNAP_VERSION=$(sed -n "s#^version: '\([^']*\)'#\1#p" packaging/snap/snapcraft.yaml | head -n 1)
fail_if_empty "$SNAP_VERSION" "Snap version"
if ! grep -q "releases/download/${SNAP_VERSION}/" packaging/snap/snapcraft.yaml; then
  fail "Snapcraft source URL should contain version ${SNAP_VERSION}"
fi

validate_winget() {
    local installer_file="$1"
    local locale_file="$2"

    local manifest_version
    manifest_version=$(sed -n 's/^PackageVersion: \(.*\)/\1/p' "$installer_file" | head -n1)
    fail_if_empty "$manifest_version" "Winget PackageVersion in ${installer_file}"

    local installer_url
    installer_url=$(sed -n 's/^ *InstallerUrl: \(.*\)/\1/p' "$installer_file" | head -n1)
    fail_if_empty "$installer_url" "Winget InstallerUrl in ${installer_file}"

    local expected_release_tag
    if [[ "$installer_url" == *"build-dev-"* ]]; then
        expected_release_tag="build-dev-${manifest_version}"
    else
        expected_release_tag="${manifest_version/.slskdn./-slskdn.}"
    fi

    if [[ "$installer_url" != *"${expected_release_tag}"* ]]; then
        fail "Winget manifest ${installer_file} url does not match release tag ${expected_release_tag}"
    fi

    if [[ -f "$locale_file" ]]; then
        local release_notes_url
        release_notes_url=$(sed -n 's/^ReleaseNotesUrl: \(.*\)/\1/p' "$locale_file" | head -n1)
        if [[ -n "$release_notes_url" && "$release_notes_url" != *"${expected_release_tag}"* ]]; then
            fail "Winget manifest ${locale_file} release notes url does not match release tag ${expected_release_tag}"
        fi
    fi
}

validate_winget packaging/winget/snapetech.slskdn.installer.yaml packaging/winget/snapetech.slskdn.locale.en-US.yaml
validate_winget packaging/winget/snapetech.slskdn-dev.installer.yaml packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml

expect_literal packaging/chocolatey/slskdn.nuspec "<version>${CHOC_VERSION}</version>"
expect_literal packaging/chocolatey/tools/chocolateyinstall.ps1 "releases/download/${CHOC_VERSION}/slskdn-main-win-x64.zip"

CHOC_CHECKSUM=$(sed -n 's#^\$checksum\s*=\s*\"\([^\"]*\)\"#\1#p' packaging/chocolatey/tools/chocolateyinstall.ps1 | head -n 1)
if [[ -z "${CHOC_CHECKSUM}" ]]; then
  fail "Could not extract checksum from packaging/chocolatey/tools/chocolateyinstall.ps1"
fi

if [[ "${#CHOC_CHECKSUM}" -ne 64 ]]; then
  fail "Chocolatey checksum in packaging/chocolatey/tools/chocolateyinstall.ps1 must be a 64-char SHA-256"
fi

if [[ ! "$CHOC_CHECKSUM" =~ ^[a-fA-F0-9]{64}$ ]]; then
  fail "Chocolatey checksum in packaging/chocolatey/tools/chocolateyinstall.ps1 must be hex"
fi

echo "Packaging metadata validation passed."
