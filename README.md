
# slskdn

[Releases](https://github.com/snapetech/slskdn/releases) · [Issues](https://github.com/snapetech/slskdn/issues) · [Quick Start](#quick-start)

CI ![CI](https://github.com/snapetech/slskdn/actions/workflows/ci.yml/badge.svg) · AUR ![AUR](https://img.shields.io/aur/version/slskdn-bin?logo=archlinux&label=AUR) · Docker `ghcr.io/snapetech/slskdn` · Base: slskd 0.24.1

**slskdn** is the batteries-included fork of slskd. Same core, but with built-in wishlist, smart ranking, tabbed browsing, advanced filters, user notes, notifications, and packaged builds.

## Highlights
- Wishlist/background search with optional auto-download.
- Unified smart source ranking (manual, auto-replace, wishlist) + history badges + per-user notes.
- Advanced search filters GUI, save/clear defaults, max size/min size, configurable page size, block users.
- Tabbed browse with per-user cache, refresh button, persistent tabs, checkbox tree + multi-folder download, delete-on-disk.
- Notifications (Ntfy/Pushover), PWA/mobile friendly, chat room context menu, inline user notes.
- Packages: Docker (GHCR), Arch AUR (`slskdn-bin`/`slskdn`), release zips (with .deb/.rpm workflows), drop-in for existing slskd config.

## Quick Start
### Docker
```bash
docker run -d \
  -p 5030:5030 -p 50300:50300 \
  -e SLSKD_SLSK_USERNAME=your_username \
  -e SLSKD_SLSK_PASSWORD=your_password \
  -v /path/to/downloads:/downloads \
  -v /path/to/app:/app \
  --name slskdn \
  ghcr.io/snapetech/slskdn:latest
```

### Arch Linux (AUR)
```bash
yay -S slskdn-bin   # binary, recommended
# or
yay -S slskdn       # build from source
sudo systemctl enable --now slskd
```

### Release Zip (Linux/Windows)
```bash
curl -LO https://github.com/snapetech/slskdn/releases/latest/download/slskdn-<version>-linux-x64.zip
unzip slskdn-*-linux-x64.zip
./slskd
```

### From Source (dev)
```bash
dotnet run --project src/slskd/slskd.csproj
cd src/web && npm install && npm start
```

## Key UI Tips
- **Advanced Filters**: Click “Advanced Filters” in Search. Set bitrate/size/length/include/exclude; save or clear defaults. Text syntax still works (`minbr:320 maxfs:500mb -live`).
- **User Notes**: Pencil icon in search/browse/chat → add note + color + high priority. Badges show on users.
- **Smart Ranking**: Default sort; used for manual downloads, auto-replace, wishlist. Purple score badge; history badges (green/blue/orange) reflect your success rate.
- **Tabbed Browse**: “+” adds tab; names update after first search; tabs persist (localStorage). Refresh icon re-fetches and refreshes cache. Checkbox tree → Download Selected.
- **Delete on Disk**: Downloads page → Remove and Delete. Cleans empty directories too.
- **Notifications**: Configure ntfy/pushover in config/env; get PM/mention alerts.
- **PWA**: Add to Home Screen; works well on mobile.

## Configuration (minimal)
```yaml
soulseek:
  username: your_username
  password: your_password
  listen_port: 50300
directories:
  downloads: /downloads
  incomplete: /downloads/incomplete
shares:
  directories:
    - /music
web:
  port: 5030
  authentication:
    username: admin
    password: change_me
# slskdn extras
global:
  download:
    auto_replace_stuck: true
  wishlist:
    enabled: true
```
Environment variables override config (e.g., `SLSKD_SLSK_USERNAME`, `SLSKD_SLSK_PASSWORD`, `SLSKD_HTTP_AUTH_DISABLED=true` for local testing).

## Differences vs slskd (quick)
- Wishlist/background search + auto-download.
- Auto-replace stuck downloads.
- Tabbed browse with cached trees + multi-folder download + delete-on-disk.
- Advanced filter GUI, saved defaults, max filesize, page size, block users.
- User notes, smart ranking with history badges, unified scoring for manual/API/auto.
- Notifications (Ntfy/Pushover), PWA, room user context menu.

## Versioning
`<upstream>-slskdn.<n>` (e.g., `0.24.1-slskdn.8`) — same upstream base with a fork suffix.

## License
[AGPL-3.0](LICENSE) (same as slskd).
