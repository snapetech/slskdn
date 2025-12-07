# slskdn Announcement Posts

## Short Version (Reddit/Discord)

---

**slskdn - Feature-rich Soulseek web client (slskd fork with batteries included)**

Hey everyone! I've been working on **slskdn**, a fork of the excellent slskd Soulseek client that adds all the features I wished were built-in instead of requiring external scripts.

**What's different from slskd?**
- 🔄 **Auto-replace stuck downloads** - Automatically finds alternative sources when downloads fail
- ⭐ **Wishlist/background search** - Save searches that run periodically with auto-download
- 🧠 **Smart source ranking** - Ranks results by speed, queue depth, and your download history
- 📊 **User download history badges** - See who you've successfully downloaded from before
- 🚫 **Block users from results** - Hide problem users permanently
- 📁 **Multiple download destinations** - Choose where each download goes
- 🗑️ **Delete files from UI** - Remove failed downloads from disk
- 🔔 **Push notifications** - Ntfy, Pushover, Pushbullet support
- And more...

**No scripts. No external tools. Everything built-in.**

📦 **Install:**
- Docker: `ghcr.io/snapetech/slskdn`
- Unraid: Add repo `https://github.com/snapetech/slskdn` in Docker settings
- AUR: `yay -S slskdn-bin`
- Fedora/RHEL: COPR available
- Ubuntu/Debian: PPA available

🔗 **Links:**
- GitHub: https://github.com/snapetech/slskdn
- Releases: https://github.com/snapetech/slskdn/releases

Happy to answer questions!

---

## Long Version (Unraid Forums / Blog)

---

### Introducing slskdn - A Feature-Rich Soulseek Web Client

Hi everyone!

I'd like to share a project I've been working on: **slskdn**, a feature-rich fork of [slskd](https://github.com/slskd/slskd).

#### Why Fork?

slskd is an excellent headless Soulseek client with a clean API. However, many feature requests get closed with "this can be done via the API with a script." That's fine for developers, but I wanted something that just *works* out of the box—like the desktop clients (Nicotine+, SoulseekQt) but with a modern web UI.

**slskdn takes the opposite approach: batteries included.**

#### Features

| Feature | Description |
|---------|-------------|
| 🔄 **Auto-Replace** | Stuck download? slskdn automatically searches for and downloads from an alternative source |
| ⭐ **Wishlist** | Save searches that run in the background. Auto-download when matches are found |
| 🧠 **Smart Ranking** | Results ranked by upload speed, queue depth, free slots, and your past download success |
| 📊 **History Badges** | Green/blue/orange badges show how many successful downloads you've had from each user |
| 🚫 **Block Users** | Hide specific users from all search results permanently |
| 📝 **User Notes** | Add personal notes to any user (e.g., "great quality", "slow uploads") |
| 📁 **Multiple Destinations** | Configure multiple download folders, choose per-download |
| 🗑️ **Delete from Disk** | Remove failed/unwanted downloads from your filesystem via the UI |
| 🔔 **Push Notifications** | Get notified via Ntfy, Pushover, or Pushbullet |
| 🗂️ **Tabbed Browsing** | Browse multiple users simultaneously in tabs (like browser tabs) |
| 🧹 **Clear All Searches** | One-click cleanup of search history |

#### Installation

**Docker (recommended):**
```bash
docker pull ghcr.io/snapetech/slskdn
```

**Unraid:**
1. Settings → Docker → Template Repositories
2. Add: `https://github.com/snapetech/slskdn`
3. Apps → Search "slskdn" → Install

**Arch Linux (AUR):**
```bash
yay -S slskdn-bin
```

**Fedora/RHEL/openSUSE (COPR):**
```bash
sudo dnf copr enable snapetech/slskdn
sudo dnf install slskdn
```

**Ubuntu/Debian (PPA):**
```bash
sudo add-apt-repository ppa:snapetech/slskdn
sudo apt update && sudo apt install slskdn
```

#### Links

- 🏠 **GitHub:** https://github.com/snapetech/slskdn
- 📦 **Releases:** https://github.com/snapetech/slskdn/releases
- 🐛 **Issues:** https://github.com/snapetech/slskdn/issues
- 🐳 **Docker:** https://ghcr.io/snapetech/slskdn

#### Compatibility

slskdn maintains API compatibility with slskd, so existing integrations (like tubifarry for Lidarr) should work. The database schema is also compatible if you're migrating.

#### Screenshots

*(Add screenshots here)*

---

I'm actively developing this and welcome feedback, bug reports, and feature requests. Happy downloading!

---

## Subreddit Suggestions

- r/selfhosted
- r/unraid
- r/homelab
- r/musichoarder
- r/DataHoarder
- r/Soulseek (if exists)
- r/seedboxes

## Forum Suggestions

- Unraid Forums (Plugin Support)
- LinuxServer.io Discord
- Self-Hosted Discord/Matrix

---

## Twitter/Mastodon Version

---

🎵 Introducing slskdn - a feature-rich Soulseek web client

✅ Auto-replace stuck downloads
✅ Wishlist with auto-download  
✅ Smart source ranking
✅ User notes & history badges
✅ Push notifications

No scripts needed. Everything built-in.

🐳 Docker: ghcr.io/snapetech/slskdn
📦 AUR, COPR, PPA available

https://github.com/snapetech/slskdn

---

