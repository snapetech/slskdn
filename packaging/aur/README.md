# slskdn AUR Packages

This directory contains PKGBUILD files for the Arch User Repository (AUR).

## Drop-in Replacement

**slskdn is a drop-in replacement for slskd.** Both packages use identical paths:

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

### slskdn-bin (Recommended)

Binary package - quick installation, no build dependencies.

```bash
yay -S slskdn-bin
```

### slskdn

Source package - builds from source, requires build dependencies.

```bash
yay -S slskdn
```

## Upgrading from slskd

Simply replace slskd with slskdn:

```bash
# If using pacman/yay
yay -R slskd        # Remove old package
yay -S slskdn-bin   # Install slskdn

# Service continues to work
sudo systemctl restart slskd
```

Your config at `/etc/slskd/slskd.yml` and data at `/var/lib/slskd/` are preserved.

## Fresh Installation

```bash
# Install
yay -S slskdn-bin

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

## CI / Release

- **slskdn-dev**: `PKGBUILD-dev` uses `RELEASE_TAG_PLACEHOLDER` in the zip source URL. The workflow (`.github/workflows/build-on-tag.yml` AUR dev job) replaces it with the actual release tag (e.g. `build-dev-0.24.1.dev.91769637539`) before pushing to AUR. Do not remove this placeholder; CI must substitute it so the package points at the correct GitHub release.
- **Checksums**: Dev zip uses `sha256sums=('SKIP' ...)` (binary changes each build). Static files (slskd.service, slskd.yml, slskd.sysusers) use real hashes.

## Building Manually

```bash
# Clone
git clone https://aur.archlinux.org/slskdn-bin.git
cd slskdn-bin

# Build and install
makepkg -si
```
