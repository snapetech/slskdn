#!/usr/bin/env bash
set -euo pipefail

ROOT=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
MODE=${1:-install}

require_root() {
  if [[ ${EUID} -ne 0 ]]; then
    echo "Run as root: sudo $0" >&2
    exit 1
  fi
}

install_file() {
  local mode=$1 src=$2 dst=$3
  install -D -m "$mode" "$src" "$dst"
}

require_root

case "$MODE" in
  install|--install)
    ;;
  check|--check|verify|--verify)
    dotnet run --project "$ROOT/slskdN-vpn-agent.csproj" -- verify
    exit $?
    ;;
  *)
    echo "Usage: $0 [install|--check]" >&2
    exit 64
    ;;
esac

BACKEND=${VPN_PORT_FORWARD_BACKEND:-natpmp}
TUNNEL_TYPE=${SLSKDN_VPN_TUNNEL_TYPE:-wireguard}

required=(dotnet jq curl ip iptables systemctl)
if [[ "$TUNNEL_TYPE" == "wireguard" ]]; then
  required+=(wg wg-quick)
fi

for cmd in "${required[@]}"; do
  command -v "$cmd" >/dev/null || {
    echo "Missing required command: $cmd" >&2
    exit 2
  }
done
if [[ "$BACKEND" == "natpmp" ]]; then
  command -v natpmpc >/dev/null || {
    echo "Missing required command for ${BACKEND}: natpmpc" >&2
    exit 2
  }
fi

if [[ "$TUNNEL_TYPE" == "wireguard" ]]; then
  [[ -s /etc/wireguard/slskdN-vpn.conf ]] || {
    echo "Missing /etc/wireguard/slskdN-vpn.conf" >&2
    exit 3
  }

  shopt -s nullglob
  ingress_configs=(/etc/wireguard/slskdN-vpn-ingress/*.conf)
  (( ${#ingress_configs[@]} > 0 )) || {
    echo "Missing /etc/wireguard/slskdN-vpn-ingress/*.conf" >&2
    exit 4
  }
fi

dotnet publish "$ROOT/slskdN-vpn-agent.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /usr/local/lib/slskdN-vpn-agent >/dev/null
ln -sfn /usr/local/lib/slskdN-vpn-agent/slskdN-vpn-agent /usr/local/bin/slskdN-vpn-agent

install_file 0644 "$ROOT/systemd/slskdN-vpn-split.service" /etc/systemd/system/slskdN-vpn-split.service
install_file 0644 "$ROOT/systemd/slskdN-vpn-ingress.service" /etc/systemd/system/slskdN-vpn-ingress.service
install_file 0644 "$ROOT/systemd/slskdN-vpn-ingress-renew.service" /etc/systemd/system/slskdN-vpn-ingress-renew.service
install_file 0644 "$ROOT/systemd/slskdN-vpn-ingress-renew.timer" /etc/systemd/system/slskdN-vpn-ingress-renew.timer
install_file 0644 "$ROOT/systemd/slskdN-vpn-gluetun-compat.service" /etc/systemd/system/slskdN-vpn-gluetun-compat.service
install_file 0644 "$ROOT/systemd/slskdN-vpn-watchdog.service" /etc/systemd/system/slskdN-vpn-watchdog.service
install_file 0644 "$ROOT/systemd/slskdN-vpn-watchdog.timer" /etc/systemd/system/slskdN-vpn-watchdog.timer

install -d -m 0755 /var/lib/slskdN-vpn

systemctl daemon-reload
if [[ "$TUNNEL_TYPE" == "wireguard" ]]; then
  systemctl enable --now wg-quick@slskdN-vpn.service
fi
systemctl enable --now slskdN-vpn-split.service
systemctl enable --now slskdN-vpn-gluetun-compat.service
systemctl enable --now slskdN-vpn-ingress-renew.timer
systemctl enable --now slskdN-vpn-watchdog.timer
systemctl restart slskdN-vpn-ingress.service

systemctl --no-pager --full status slskdN-vpn-ingress.service || true
echo "Installed. Configure slskdN integrations.vpn and restart the slskdN service if needed."
