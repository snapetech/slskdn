# slskdn AUR Packages

This directory contains PKGBUILD files for the Arch User Repository (AUR).

## Drop-in Replacement

**slskdn is a drop-in replacement for slskd.** Both packages use identical paths:

| Resource | Path |
|----------|------|
| Binary launcher | `/usr/lib/slskd/slskd` |
| Bundled release payload | `/usr/lib/slskd/current/` |
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
The package bundles the published self-contained .NET 10 runtime from the
GitHub release zip, so it should not depend on distro `aspnet-runtime` packages.

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

AUR packages keep the drop-in launcher path at `/usr/lib/slskd/slskd`, but the bundled release payload now lives under `/usr/lib/slskd/current/` (backed by a versioned `releases/` directory). That keeps the compatibility path stable while avoiding pacman upgrade collisions with stale root-level payload files from older/manual installs.

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

### Optional AUR extras for SongID workflows

`slskdn` installs with only the core runtime dependencies. If you want full SongID workflows, install any of these optional packages separately.

- `ffmpeg` — audio decoding and media handling used by SongID/AcoustID pipelines
- `python` — Python runtime for optional SongID tools
- `python-torchaudio` — optional advanced Python fingerprint and analysis features
- `docker` — containerized deployment (optional)

Why this is optional:
- Slskdn works without these for normal transfer/search/download behavior.
- SongID uses them only when advanced audio analysis is enabled in configuration.
- Keeping them optional avoids blocking installs on systems that do not need SongID extras or are behind Python package availability delays.

To avoid every optional dependency prompt (or accidental “all”), install like this:

```bash
yay -S --noconfirm slskdn-bin
```

### Optional fix for `python-torchaudio` download failures

If `python-torchaudio` fails with:

> `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`

it is a GitHub download issue in that package build, not a slskdn dependency problem.

Use the local helper script in this repo (Arch Linux / AUR only):

```bash
cd /path/to/slskdn
bash ./scripts/fix-python-torchaudio-no-resume.sh
```

Why this works:
- the helper uses `wget -O` (no resume mode), which avoids `curl` resume errors,
- it refreshes a clean AUR source checkout,
- it builds and installs `python-torchaudio` without the interactive `pacman` prompt.

If you are not on Arch Linux (or not using AUR), you do not need this script.

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
  https:
    disabled: true
  authentication:
    username: admin
    password: your_web_password

shares:
  directories:
    - /path/to/your/music
```

## CI / Release

- **slskdn-dev**: `PKGBUILD-dev` uses `RELEASE_TAG_PLACEHOLDER` in the zip source URL. The workflow (`.github/workflows/build-on-tag.yml` AUR dev job) replaces it with the actual release tag (e.g. `build-dev-0.24.1.dev.91769637539`) before pushing to AUR. Do not remove this placeholder; CI must substitute it so the package points at the correct GitHub release.
- **Checksums**: Both `slskdn-bin` and `slskdn-dev` keep the GitHub-hosted binary zip on `sha256sums=('SKIP' ...)`. GitHub release assets are not treated as immutable here, so only the repo-owned static packaging files (`slskd.service`, `slskd.yml`, `slskd.sysusers`) use real hashes. The binary package source filenames include `${pkgver}` so makepkg cannot reuse an older cached zip for a newer package version.

## Building Manually

```bash
# Clone
git clone https://aur.archlinux.org/slskdn-bin.git
cd slskdn-bin

# Build and install
makepkg -si
```
