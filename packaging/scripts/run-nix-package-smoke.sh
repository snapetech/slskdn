#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

fail() {
    echo "ERROR: $1" >&2
    exit 1
}

section() {
    echo
    echo "==> $1"
}

if ! command -v nix >/dev/null 2>&1; then
    echo "Skipping Nix package smoke: nix is not installed on this machine."
    exit 0
fi

section "Build flake package"
nix build --no-write-lock-file '.#default'

if [[ ! -x result/bin/slskd ]]; then
    fail "Expected nix build output to expose result/bin/slskd"
fi

section "Smoke packaged binary"
./result/bin/slskd --help >/tmp/slskdn-nix-smoke-help.txt

if [[ ! -s /tmp/slskdn-nix-smoke-help.txt ]]; then
    fail "Packaged slskd --help output was empty"
fi

if [[ "$(uname -s)" != "Linux" ]]; then
    echo
    echo "Skipping NixOS module smoke: current host is not Linux."
    exit 0
fi

section "Evaluate NixOS module contract"
exec_start="$(nix eval --impure --raw --no-write-lock-file --expr "
let
  flake = builtins.getFlake (toString ./.);
  system = builtins.currentSystem;
  nixos = flake.inputs.nixpkgs.lib.nixosSystem {
    inherit system;
    modules = [
      ({ ... }: {
        services.slskd.enable = true;
        services.slskd.domain = \"localhost\";
        services.slskd.environmentFile = \"/etc/slskd.env\";
        services.slskd.settings.shares.directories = [ ];
        services.slskd.package = flake.packages.\${system}.default;
      })
    ];
  };
in nixos.config.systemd.services.slskd.serviceConfig.ExecStart
")"

if [[ -z "$exec_start" ]]; then
    fail "NixOS module smoke returned an empty ExecStart"
fi

case "$exec_start" in
    *"/bin/slskd"*) ;;
    *)
        fail "Expected NixOS module ExecStart to reference /bin/slskd, got: $exec_start"
        ;;
esac

echo
echo "Nix package smoke passed."
