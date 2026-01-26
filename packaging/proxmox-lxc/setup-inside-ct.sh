#!/bin/bash
# setup-inside-ct.sh — run as root inside a Debian 12 or Ubuntu 22.04 LXC
# Installs .NET 8, slskdn from GitHub release, systemd unit, and minimal config.
# Does not start the service (edit /etc/slskd/slskd.yml first).

set -e

SLSKDN_VERSION="${SLSKDN_VERSION:-0.24.1-slskdn.40}"
ZIP="slskdn-main-linux-x64.zip"
URL="https://github.com/snapetech/slskdn/releases/download/${SLSKDN_VERSION}/${ZIP}"
DEST="/opt/slskdn"
USER="slskd"
DATA_DIR="/var/lib/slskd"
CONFIG_DIR="/etc/slskd"
CONFIG_FILE="${CONFIG_DIR}/slskd.yml"

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root (e.g. inside LXC: pct exec <vmid> -- bash -c './setup-inside-ct.sh')"
  exit 1
fi

# Detect OS for .NET install
if [ -f /etc/os-release ]; then
  . /etc/os-release
  case "${ID}" in
    debian)  OS=debian;  VERSION="${VERSION_ID:-12}" ;;
    ubuntu)  OS=ubuntu;  VERSION="${VERSION_ID:-22.04}" ;;
    *)       OS=debian;  VERSION="12" ;;
  esac
else
  OS=debian
  VERSION="12"
fi

echo "[1/7] Installing .NET 8 (Microsoft repo)…"
apt-get update -qq
apt-get install -y -qq wget ca-certificates unzip lsb-release
wget -qO /etc/apt/trusted.gpg.d/microsoft.asc https://packages.microsoft.com/keys/microsoft.asc
# Debian: /debian/12/prod; Ubuntu: /ubuntu/22.04/prod
REPO="https://packages.microsoft.com/${ID}/${VERSION_ID}/prod"
CODENAME=$(lsb_release -cs)
echo "deb [arch=amd64] ${REPO} ${CODENAME} main" > /etc/apt/sources.list.d/microsoft.list
apt-get update -qq
apt-get install -y -qq aspnetcore-runtime-8.0

echo "[2/7] Downloading slskdn ${SLSKDN_VERSION}…"
mkdir -p /tmp/slskdn-setup
cd /tmp/slskdn-setup
wget -q -O "$ZIP" "$URL" || { echo "Download failed: $URL"; exit 1; }

echo "[3/7] Extracting to ${DEST}…"
mkdir -p "$DEST"
unzip -o -q "$ZIP" -d "$DEST"
# Some zips have a top-level folder; flatten if slskd.dll is one level down
if [ -d "${DEST}/slskdn-main" ] && [ ! -f "${DEST}/slskd.dll" ]; then
  mv "${DEST}"/slskdn-main/* "${DEST}/"
  rmdir "${DEST}/slskdn-main" 2>/dev/null || true
fi
if [ ! -f "${DEST}/slskd.dll" ]; then
  echo "Expected slskd.dll in ${DEST}; contents: $(ls -la ${DEST})"
  exit 1
fi
cd /
rm -rf /tmp/slskdn-setup

echo "[4/7] Creating slskd user and directories…"
id -u "$USER" >/dev/null 2>&1 || useradd -r -s /usr/sbin/nologin -d "$DATA_DIR" "$USER"
mkdir -p "$DATA_DIR" "$DATA_DIR/downloads" "$DATA_DIR/incomplete" "$CONFIG_DIR"
chown -R "${USER}:${USER}" "$DATA_DIR"

echo "[5/7] Installing minimal config…"
if [ ! -f "$CONFIG_FILE" ]; then
  cat > "$CONFIG_FILE" << 'CFG'
# slskd configuration — edit: soulseek.username, soulseek.password, shares.directories
# See https://github.com/snapetech/slskdn for full options

soulseek:
  username:
  password:
  description: slskd user

directories:
  downloads: /var/lib/slskd/downloads
  incomplete: /var/lib/slskd/incomplete

web:
  port: 5030
  # authentication:
  #   username: admin
  #   password: changeme

# shares:
#   directories:
#     - /path/to/music

# serilog:
#   minimum_level: Information
CFG
  chown "${USER}:${USER}" "$CONFIG_FILE"
  echo "Created $CONFIG_FILE — please set soulseek.username and soulseek.password"
else
  echo "Config already exists: $CONFIG_FILE"
fi

echo "[6/7] Installing systemd unit…"
cat > /etc/systemd/system/slskd.service << 'SVC'
[Unit]
Description=slskd - Soulseek client/server
Documentation=https://github.com/snapetech/slskdn
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=slskd
Group=slskd
ExecStart=/usr/bin/dotnet /opt/slskdn/slskd.dll --config /etc/slskd/slskd.yml
WorkingDirectory=/var/lib/slskd
Environment="HOME=/var/lib/slskd"
Environment="DOTNET_ROOT=/usr/share/dotnet"
Restart=on-failure
RestartSec=10

NoNewPrivileges=yes
ProtectSystem=full
PrivateTmp=yes
ReadWritePaths=/var/lib/slskd /etc/slskd

[Install]
WantedBy=multi-user.target
SVC
systemctl daemon-reload

echo "[7/7] Done."
echo ""
echo "Next:"
echo "  1. Edit config:  nano $CONFIG_FILE"
echo "     Set soulseek.username, soulseek.password, and shares.directories"
echo "  2. Start:       systemctl enable --now slskd"
echo "  3. Web UI:      http://<container-ip>:5030"
echo "  4. Soulseek:    forward port 50300 on your router"
echo ""
