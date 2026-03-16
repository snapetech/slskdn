#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

fail() {
    echo "ERROR: $1" >&2
    exit 1
}

expect_line() {
    local file="$1"
    local pattern="$2"
    grep -Eq "$pattern" "$file" || fail "$file is missing pattern: $pattern"
}

reject_line() {
    local file="$1"
    local pattern="$2"
    if grep -Eq "$pattern" "$file"; then
        fail "$file contains forbidden pattern: $pattern"
    fi
}

expect_line flake.nix 'makeWrapper \$out/libexec/\$\{pname\}/slskd \$out/bin/slskd'
expect_line flake.nix 'ln -s slskd \$out/bin/\$\{pname\}'
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

reject_line .github/workflows/dev-release.yml 'slskdn-dev-windows-x64\.zip'
reject_line .github/workflows/release-packages.yml 'slskdn-\$\{\{ steps\.version\.outputs\.tag \}\}-linux-x64\.zip'

expect_line packaging/chocolatey/slskdn.nuspec '<version>0\.24\.5-slskdn\.52</version>'
expect_line packaging/chocolatey/tools/chocolateyinstall\.ps1 'releases/download/0\.24\.5-slskdn\.52/slskdn-main-win-x64\.zip'

echo "Packaging metadata validation passed."
