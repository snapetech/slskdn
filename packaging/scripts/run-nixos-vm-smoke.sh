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
    echo "Skipping NixOS VM smoke: nix is not installed on this machine."
    exit 0
fi

if [[ "$(uname -s)" != "Linux" ]]; then
    echo "Skipping NixOS VM smoke: current host is not Linux."
    exit 0
fi

if [[ ! -e /dev/kvm && "${SLSKDN_NIXOS_VM_SMOKE_ALLOW_TCG:-0}" != "1" ]]; then
    echo "Skipping NixOS VM smoke: /dev/kvm is unavailable. Set SLSKDN_NIXOS_VM_SMOKE_ALLOW_TCG=1 to allow slower software emulation."
    exit 0
fi

tmpdir="$(mktemp -d)"
cleanup() {
    rm -rf "$tmpdir"
}
trap cleanup EXIT

vm_expr="$tmpdir/slskdn-nixos-vm.nix"
vm_log="$tmpdir/vm.log"

cat >"$vm_expr" <<'NIX'
let
  flake = builtins.getFlake (toString ./.);
  system = builtins.currentSystem;
in
flake.inputs.nixpkgs.lib.nixosSystem {
  inherit system;
  modules = [
    ({ pkgs, ... }: {
      virtualisation = {
        cores = 1;
        memorySize = 1024;
        diskSize = 1024;
        graphics = false;
      };

      networking.hostName = "slskdn-smoke";
      networking.firewall.allowedTCPPorts = [ 5030 ];

      services.slskd = {
        enable = true;
        domain = "localhost";
        environmentFile = "/etc/slskd.env";
        package = flake.packages.${system}.default;
        settings = {
          soulseek = {
            username = "vm-smoke-user";
            password = "vm-smoke-password";
          };
          shares.directories = [ "/var/lib/slskd/smoke-share" ];
          web = {
            port = 5030;
            authentication.disabled = true;
          };
        };
      };

      systemd.tmpfiles.rules = [
        "d /var/lib/slskd/smoke-share 0755 slskd slskd -"
        "f /etc/slskd.env 0600 root root - SLSKD_NO_LOGO=true"
      ];

      systemd.services.slskdn-smoke-report = {
        wantedBy = [ "multi-user.target" ];
        after = [ "slskd.service" ];
        wants = [ "slskd.service" ];
        script = ''
          for attempt in $(seq 1 60); do
            if ${pkgs.systemd}/bin/systemctl is-active --quiet slskd.service; then
              echo SLSKDN_VM_SMOKE_OK
              poweroff
              exit 0
            fi
            sleep 1
          done
          ${pkgs.systemd}/bin/systemctl status --no-pager slskd.service || true
          echo SLSKDN_VM_SMOKE_FAILED
          poweroff
          exit 1
        '';
      };

      system.stateVersion = "24.11";
    })
  ];
}
NIX

section "Build NixOS VM"
vm_runner="$(nix build --no-link --print-out-paths --impure --no-write-lock-file --expr "(import $vm_expr).config.system.build.vm")"

if [[ ! -x "$vm_runner/bin/run-slskdn-smoke-vm" ]]; then
    fail "Expected VM runner at $vm_runner/bin/run-slskdn-smoke-vm"
fi

section "Boot NixOS VM and wait for slskd"
timeout_seconds="${SLSKDN_NIXOS_VM_SMOKE_TIMEOUT_SECONDS:-180}"
qemu_flags=("-nographic" "-serial" "mon:stdio")
if [[ ! -e /dev/kvm ]]; then
    qemu_flags+=("-machine" "accel=tcg")
fi

set +e
timeout "$timeout_seconds" "$vm_runner/bin/run-slskdn-smoke-vm" "${qemu_flags[@]}" 2>&1 | tee "$vm_log"
status="${PIPESTATUS[0]}"
set -e

if ! grep -q "SLSKDN_VM_SMOKE_OK" "$vm_log"; then
    fail "NixOS VM did not report a healthy slskd service before shutdown"
fi

if [[ "$status" -ne 0 && "$status" -ne 124 ]]; then
    fail "NixOS VM runner exited with status $status"
fi

echo
echo "NixOS VM smoke passed."
