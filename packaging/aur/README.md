# slskdN AUR Packages

This directory contains PKGBUILD files for the Arch User Repository (AUR).

## Drop-in Replacement

**slskdN is a drop-in replacement for slskd.** Both packages use identical paths:

| Resource | Path |
|----------|------|
| Binary | `/usr/lib/slskd/slskd` |
| Symlink | `/usr/bin/slskd` |
| Config | `/etc/slskd/slskd.yml` |
| Data | `/var/lib/slskd/` |
| Service | `slskd.service` |
| User | `slskd` |

This means:
- **Your existing slskd config and data are preserved**
- **The systemd service name stays the same** (`systemctl status slskd`)
- **All your settings, downloads, and shares remain intact**

## Packages

### slskdN-bin (Recommended)

Binary package - quick installation, no build dependencies.

```bash
yay -S slskdN-bin
```

### slskdN

Source package - builds from source, requires build dependencies.

```bash
yay -S slskdN
```

## Upgrading from slskd

Simply replace slskd with slskdN:

```bash
# If using pacman/yay
yay -R slskd        # Remove old package
yay -S slskdN-bin   # Install slskdN

# Service continues to work
sudo systemctl restart slskd
```

Your config at `/etc/slskd/slskd.yml` and data at `/var/lib/slskd/` are preserved.

## Fresh Installation

```bash
# Install
yay -S slskdN-bin

# Edit config
sudo nano /etc/slskd/slskd.yml

# Start service
sudo systemctl enable --now slskd

# Access web UI
xdg-open http://localhost:5030
```

## Configuration

The default config is at `/etc/slskd/slskd.yml`. Key settings:

```yaml
soulseek:
  username: your_username
  password: your_password

directories:
  downloads: /var/lib/slskd/downloads
  incomplete: /var/lib/slskd/incomplete

web:
  port: 5030
  authentication:
    username: admin
    password: your_web_password

shares:
  directories:
    - /path/to/your/music
```

## Building Manually

```bash
# Clone
git clone https://aur.archlinux.org/slskdN-bin.git
cd slskdN-bin

# Build and install
makepkg -si
```
