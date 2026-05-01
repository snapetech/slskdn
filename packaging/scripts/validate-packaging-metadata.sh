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

reject_literal() {
    local file="$1"
    local pattern="$2"
    if grep -Fq -- "$pattern" "$file"; then
        fail "$file contains forbidden literal: $pattern"
    fi
}

extract_release_from_url() {
    local url="$1"
    printf '%s\n' "${url##*/releases/download/}" | sed 's#/.*##'
}

extract_release_from_formula() {
    local file="$1"
    awk '/^  version "/ {gsub(/"/, "", $2); print substr($2, 1); exit}' "$file"
}

extract_stable_linux_sha() {
    local file="$1"
    awk '
      /on_linux do/ {in_linux=1; next}
      in_linux && $1=="sha256" {gsub(/"/, "", $2); print $2; exit}
      in_linux && /^  end/ {in_linux=0}
    ' "$file"
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
reject_literal flake.nix 'slskdn-dev'

expect_line packaging/winget/snapetech.slskdn.yaml '^PackageIdentifier: snapetech\.slskdn$'
expect_line packaging/winget/snapetech.slskdn.installer.yaml '^PackageIdentifier: snapetech\.slskdn$'
expect_line packaging/winget/snapetech.slskdn.installer.yaml 'PortableCommandAlias: slskdn$'
expect_line packaging/winget/snapetech.slskdn.locale.en-US.yaml '^PackageIdentifier: snapetech\.slskdn$'
expect_line packaging/winget/snapetech.slskdn.locale.en-US.yaml '^Moniker: slskdn$'
expect_line packaging/winget/snapetech.slskdn.locale.en-US.yaml '^PackageName: slskdN$'

expect_line packaging/homebrew/Formula/slskdn.rb '^class Slskdn < Formula$'

expect_line .github/workflows/release-packages.yml 'slskdn-main-linux-glibc-x64\.zip'
expect_literal .github/workflows/build-on-tag.yml 'cp packaging/linux/install-from-release.sh release/install-linux-release.sh'
expect_literal .github/workflows/build-on-tag.yml 'sha256sum *.zip slskd.service slskd.yml slskd.sysusers install-linux-release.sh > SHA256SUMS.txt'
expect_literal .github/workflows/build-on-tag.yml 'cp packaging/aur/slskd.service packaging/aur/slskd.yml packaging/aur/slskd.sysusers release/'

expect_line .github/workflows/release-packages.yml '\$\{\{ steps\.version\.outputs\.tag \}\}-linux-x64\.zip'

expect_literal Dockerfile 'gosu'
expect_literal Dockerfile 'COPY packaging/docker/slskdn-container-start /usr/local/bin/slskdn-container-start'
expect_literal Dockerfile 'SLSKD_DOCKER_REVISION=$REVISION'
reject_literal Dockerfile 'groupadd --gid 1000'
reject_literal Dockerfile 'useradd --uid 1000'
reject_literal Dockerfile 'SLSKD_DOCKER_REVISON'
reject_literal Dockerfile 'VOLUME /app'
test -x packaging/docker/slskdn-container-start || fail 'packaging/docker/slskdn-container-start must be executable'

expect_line packaging/aur/PKGBUILD '^source=\($'
expect_literal packaging/aur/PKGBUILD-bin '"slskdn-${pkgver}-main-linux-glibc-x64.zip::https://github.com/snapetech/slskdn/releases/download/${pkgver//.slskdn/-slskdn}/slskdn-main-linux-glibc-x64.zip"'
expect_literal packaging/aur/PKGBUILD-bin 'noextract=("slskdn-${pkgver}-main-linux-glibc-x64.zip")'
expect_line packaging/aur/PKGBUILD-bin '^install=slskd\.install$'
expect_line packaging/aur/PKGBUILD '^install=slskd\.install$'
expect_line packaging/aur/slskd.service '^ExecStart=/usr/lib/slskd/slskd --config /etc/slskd/slskd\.yml$'
expect_literal packaging/aur/PKGBUILD-bin 'local release_root="${app_root}/releases/${pkgver}"'
expect_literal packaging/aur/PKGBUILD-bin 'local archive="${srcdir}/slskdn-${pkgver}-main-linux-glibc-x64.zip"'
expect_literal packaging/aur/PKGBUILD-bin 'unzip -q "${archive}" -d "${stage_root}"'
expect_literal packaging/aur/PKGBUILD-bin 'Microsoft.AspNetCore.Diagnostics.Abstractions.dll'
expect_literal packaging/aur/PKGBUILD-bin 'chmod -R u=rwX,go=rX "${release_root}"'
expect_literal packaging/aur/PKGBUILD-bin 'chmod 755 "${release_root}/slskd"'
expect_literal packaging/aur/PKGBUILD-bin 'exec /usr/lib/slskd/current/slskd "$@"'
expect_literal packaging/aur/PKGBUILD 'local release_root="${app_root}/releases/${pkgver}"'
expect_literal packaging/aur/PKGBUILD '_dotnet_version="0.0.0-slskdn.${BASH_REMATCH[1]}.${BASH_REMATCH[2]}"'
expect_literal packaging/aur/PKGBUILD '-p:Version="$_dotnet_version"'
expect_literal packaging/aur/PKGBUILD '-p:PackageVersion="$_dotnet_version"'
reject_literal packaging/aur/PKGBUILD '_assembly_ver="${pkgver%.slskdn.*}.${pkgver##*.}"'
reject_literal packaging/aur/PKGBUILD '-p:Version="$_assembly_ver"'
expect_literal packaging/aur/PKGBUILD 'chmod -R u=rwX,go=rX "${release_root}"'
expect_literal packaging/aur/PKGBUILD 'chmod 755 "${release_root}/slskd"'
expect_literal packaging/aur/PKGBUILD 'exec /usr/lib/slskd/current/slskd "$@"'
test -f packaging/aur/slskd.install || fail 'packaging/aur/slskd.install is missing'
reject_line packaging/aur/PKGBUILD-bin 'slskdn-\$\{pkgver\}-linux-x64\.zip::https://github\.com/snapetech/slskdn/releases/download/\${pkgver//\.slskdn/-slskdn}/slskdn-\$\{pkgver\}-linux-x64\.zip'

bash packaging/scripts/validate-release-copy.sh

STABLE_FORMULA_VERSION=$(extract_release_from_formula Formula/slskdn.rb)
fail_if_empty "$STABLE_FORMULA_VERSION" "Stable Formula version"

STABLE_FORMULA_LINUX_SHA=$(extract_stable_linux_sha Formula/slskdn.rb)
fail_if_empty "$STABLE_FORMULA_LINUX_SHA" "Stable Formula Linux SHA"

HOMEBREW_VERSION=$(sed -n 's#^  version "\([^"]*\)"#\1#p' packaging/homebrew/Formula/slskdn.rb | head -n 1)
fail_if_empty "$HOMEBREW_VERSION" "Homebrew version"
if [[ "$HOMEBREW_VERSION" != "$STABLE_FORMULA_VERSION" ]]; then
  fail "Homebrew formula version ${HOMEBREW_VERSION} does not match stable Formula version ${STABLE_FORMULA_VERSION}"
fi

HOME_URL_VERSIONS_COUNT=0
while IFS= read -r homebrew_release; do
  ((HOME_URL_VERSIONS_COUNT += 1))
  if [[ "$homebrew_release" != "$STABLE_FORMULA_VERSION" ]]; then
    fail "Homebrew formula URL release (${homebrew_release}) does not match version ${HOMEBREW_VERSION}"
  fi
done < <(sed -n "s#.*releases/download/\([^/]*\)/.*#\1#p" packaging/homebrew/Formula/slskdn.rb | grep -v '^$')

if [[ "$HOME_URL_VERSIONS_COUNT" -ne 3 ]]; then
  fail "Homebrew formula should have exactly 3 release URLs, found $HOME_URL_VERSIONS_COUNT"
fi

FLATPAK_URL=$(sed -n "s#^[[:space:]]*url: \\(.*\\)#\\1#p" packaging/flatpak/io.github.slskd.slskdn.yml | grep '/releases/download/' | head -n 1)
if [[ -z "$FLATPAK_URL" ]]; then
  fail "Could not extract Flatpak release URL"
fi
FLATPAK_RELEASE=$(extract_release_from_url "$FLATPAK_URL")
if [[ "$FLATPAK_RELEASE" != "$STABLE_FORMULA_VERSION" ]]; then
  fail "Flatpak release ${FLATPAK_RELEASE} does not match stable Formula version ${STABLE_FORMULA_VERSION}"
fi
if ! grep -q "sha256: ${STABLE_FORMULA_LINUX_SHA}" packaging/flatpak/io.github.slskd.slskdn.yml; then
  fail "Flatpak source SHA should match stable Linux SHA ${STABLE_FORMULA_LINUX_SHA}"
fi

TRUENAS_CHART_VERSION=$(sed -n 's/^appVersion: "\(.*\)"$/\1/p' packaging/truenas-scale/charts/slskdn/Chart.yaml)
fail_if_empty "$TRUENAS_CHART_VERSION" "TrueNAS appVersion"
if [[ "$TRUENAS_CHART_VERSION" != "$STABLE_FORMULA_VERSION" ]]; then
  fail "TrueNAS chart appVersion ${TRUENAS_CHART_VERSION} does not match stable Formula version ${STABLE_FORMULA_VERSION}"
fi

HELM_CHART_VERSION=$(sed -n 's/^appVersion: "\(.*\)"$/\1/p' packaging/helm/slskdn/Chart.yaml)
fail_if_empty "$HELM_CHART_VERSION" "Helm appVersion"
if [[ "$HELM_CHART_VERSION" != "$STABLE_FORMULA_VERSION" ]]; then
  fail "Helm chart appVersion ${HELM_CHART_VERSION} does not match stable Formula version ${STABLE_FORMULA_VERSION}"
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

    local expected_release_tag="${manifest_version/.slskdn./-slskdn.}"

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

if [[ "${VALIDATE_WINGET_RELEASE_METADATA:-false}" == "true" ]]; then
  validate_winget packaging/winget/snapetech.slskdn.installer.yaml packaging/winget/snapetech.slskdn.locale.en-US.yaml

  WINGET_STABLE_VERSION=$(sed -n 's/^PackageVersion: \(.*\)$/\1/p' packaging/winget/snapetech.slskdn.installer.yaml | head -n1)
  if [[ "${WINGET_STABLE_VERSION/.slskdn./-slskdn.}" != "$STABLE_FORMULA_VERSION" ]]; then
    fail "Winget stable PackageVersion ${WINGET_STABLE_VERSION} does not match stable Formula version ${STABLE_FORMULA_VERSION}"
  fi
else
  echo "Skipping Winget release-version metadata validation; set VALIDATE_WINGET_RELEASE_METADATA=true to enforce it."
fi

CHOC_VERSION=$(sed -n 's#.*<version>\(.*\)</version>#\1#p' packaging/chocolatey/slskdn.nuspec | head -n 1)
if [[ -z "${CHOC_VERSION}" ]]; then
  fail "Could not extract Chocolatey version from packaging/chocolatey/slskdn.nuspec"
fi
if [[ "$CHOC_VERSION" != "$STABLE_FORMULA_VERSION" ]]; then
  fail "Chocolatey version ${CHOC_VERSION} does not match stable Formula version ${STABLE_FORMULA_VERSION}"
fi

CHOC_URL=$(sed -n 's#^\$url[[:space:]]*= "\([^"]*\)"#\1#p' packaging/chocolatey/tools/chocolateyinstall.ps1 | head -n 1)
if [[ -z "$CHOC_URL" ]]; then
  fail "Could not extract Chocolatey URL"
fi
CHOC_RELEASE=$(extract_release_from_url "$CHOC_URL")
if [[ "$CHOC_RELEASE" != "$STABLE_FORMULA_VERSION" ]]; then
  fail "Chocolatey installer URL release ${CHOC_RELEASE} does not match stable Formula version ${STABLE_FORMULA_VERSION}"
fi

if [[ "$CHOC_URL" != "https://github.com/snapetech/slskdn/releases/download/${CHOC_VERSION}/slskdn-main-win-x64.zip" ]]; then
  fail "Chocolatey installer URL must match version ${CHOC_VERSION}"
fi

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
