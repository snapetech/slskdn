# Getting Started with slskdN

Welcome to slskdN! This guide will help you get up and running quickly.

## What is slskdN?

**slskdN(OT)** is a feature-rich fork of [slskd](https://github.com/slskd/slskd), the modern web-based Soulseek client. While slskd focuses on being a lean, API-first daemon, **slskdN includes everything built-in**—no external scripts required.

### Key Features

- **Auto-Replace Stuck Downloads**: Automatically finds alternatives when downloads get stuck
- **Wishlist / Background Search**: Save searches that run automatically in the background
- **Multiple Download Destinations**: Configure multiple download folders
- **Smart Search Result Ranking**: Intelligent sorting that considers multiple factors
- **Swarm Downloads**: Multi-source parallel downloads for faster transfers
- **Scene ↔ Pod Bridging**: Unified search across Pod/Mesh and Soulseek Scene networks
- **Collections & Sharing**: Share collections with other users
- **Streaming**: Stream content while downloading
- **And much more!**

## Installation

### Quick Install (Recommended)

#### Linux

**Arch Linux (AUR):**
```bash
yay -S slskdn-bin
# or
paru -S slskdn-bin
```

**Fedora/RHEL/openSUSE (COPR):**
```bash
sudo dnf copr enable slskdn/slskdn
sudo dnf install slskdn
```

**Ubuntu/Debian (PPA):**
```bash
sudo add-apt-repository ppa:snapetech/slskdn
sudo apt update
sudo apt install slskdn
```

**NixOS/Nix:**
```bash
nix-env -iA nixos.slskdn
# or add to configuration.nix
```

**Snap:**
```bash
sudo snap install slskdn
```

#### macOS

**Homebrew:**
```bash
brew install snapetech/slskdn/slskdn
```

#### Windows

**Chocolatey:**
```powershell
choco install slskdn
```

**Winget:**
```powershell
winget install snapetech.slskdn
```

**Scoop:**
```powershell
scoop bucket add snapetech https://github.com/snapetech/scoop-bucket
scoop install slskdn
```

### Docker

```bash
docker pull ghcr.io/snapetech/slskdn:latest
docker run -d \
  --name slskdn \
  -p 5000:5000 \
  -v /path/to/config:/app/config \
  -v /path/to/downloads:/app/downloads \
  -v /path/to/shares:/app/shares \
  ghcr.io/snapetech/slskdn:latest
```

### Manual Installation

1. **Download the latest release** from [GitHub Releases](https://github.com/snapetech/slskdn/releases)
2. **Extract the archive** to your desired location
3. **Run the executable**:
   - Linux/macOS: `./slskdn`
   - Windows: `slskdn.exe`

### Building from Source

See [Building from Source](build.md) for detailed instructions.

## Initial Configuration

### First Run

1. **Start slskdN** using your preferred installation method
2. **Open your web browser** and navigate to `http://localhost:5000` (default port)
3. **Log in** with the default credentials:
   - Username: `admin`
   - Password: `admin` (⚠️ **Change this immediately!**)

### Essential Settings

#### 1. Change Default Password

**Settings → Security → Authentication:**
- Click "Change Password"
- Enter a strong password
- Save changes

#### 2. Configure Download Directory

**Settings → Downloads:**
- Set your download directory
- Optionally configure multiple download destinations

#### 3. Configure Share Directories

**Settings → Shares:**
- Add directories you want to share
- Set share options (public/private, etc.)

#### 4. Configure Soulseek Credentials

**Settings → Soulseek:**
- Enter your Soulseek username and password
- Configure connection options

### Configuration File

Advanced configuration can be done via `config/slskd.yml`. See [Configuration Reference](config.md) for all available options.

**Example minimal configuration:**
```yaml
soulseek:
  username: "your-username"
  password: "your-password"

directories:
  downloads: "/path/to/downloads"
  incomplete: "/path/to/incomplete"
  shares:
    - "/path/to/shares"
```

## Basic Usage

### Searching for Files

1. **Navigate to Search** in the sidebar
2. **Enter your search query** (e.g., "artist album")
3. **Select search sources**:
   - ☑ Pod/Mesh (for mesh network content)
   - ☑ Soulseek Scene (for Soulseek network content)
4. **Click Search** or press Enter
5. **Browse results** and click **Download** on desired files

### Advanced Search Filters

Click the **⚙️ Advanced Filters** button to access:
- **Quality Presets**: "High Quality (320kbps+)", "Lossless Only", "Clear Quality"
- **Minimum Bitrate**: Filter by bitrate (kbps)
- **Minimum Bit Depth**: Filter by bit depth (bits)
- **Minimum Sample Rate**: Filter by sample rate (Hz)
- **File Extensions**: Filter by file type (e.g., `flac mp3 wav`)

### Downloading Files

1. **Click Download** on any search result
2. **Monitor progress** in the Downloads section
3. **Files are saved** to your configured download directory

**Auto-Replace Stuck Downloads:**
- Enable the **"Auto-Replace"** toggle in Downloads header
- slskdN will automatically find alternatives for stuck downloads

### Swarm Downloads

For files with multiple sources, slskdN can download from multiple peers simultaneously:

1. **Start a swarm download** (automatic when multiple sources available)
2. **Monitor progress** in Downloads or System → Jobs
3. **View detailed visualization** by clicking "View Details" on active swarm jobs

### Wishlist / Background Search

1. **Navigate to Wishlist** in the sidebar
2. **Click "Add Search"**
3. **Enter search query and filters**
4. **Configure options**:
   - Auto-download: Automatically download matches
   - Interval: How often to run the search
   - Max results: Maximum results per run
5. **Save** and the search will run automatically in the background

## Advanced Features

### Collections & Sharing

**Create a Collection:**
1. Navigate to **Collections** in the sidebar
2. Click **"Create Collection"**
3. Add items (files, albums, etc.)
4. Configure sharing options

**Share with Others:**
1. Open your collection
2. Click **"Share"**
3. Configure share policy (who can access, download, etc.)
4. Share the collection link or grant access to specific users

### Scene ↔ Pod Bridging

Unified search across Pod/Mesh and Soulseek Scene networks:

1. **Enable the feature** in Settings → Features → Scene Pod Bridge
2. **Search results** will show badges indicating source (POD, SCENE, or POD+SCENE)
3. **Download/Stream** buttons work based on result source

### Streaming

Stream content while downloading:

1. **Start a download** (swarm or regular)
2. **Click "Stream"** button (available for Pod/Mesh content)
3. **Content streams** while download continues in background

### Job Management

Monitor all background jobs:

1. Navigate to **System → Jobs**
2. **View analytics**: Total, active, completed jobs
3. **Filter and sort** by type, status, date
4. **View swarm visualizations** for active downloads

## Troubleshooting

### Common Issues

#### Can't Connect to Soulseek

**Symptoms:** "Connection failed" or "Login failed"

**Solutions:**
1. **Check credentials**: Verify username and password in Settings → Soulseek
2. **Check firewall**: Ensure port 2234 (Soulseek) is not blocked
3. **Check network**: Verify internet connectivity
4. **Check Soulseek status**: Soulseek servers may be down

#### Downloads Stuck or Slow

**Symptoms:** Downloads not progressing or very slow

**Solutions:**
1. **Enable Auto-Replace**: Toggle "Auto-Replace" in Downloads header
2. **Check peer status**: User may be offline or have no free slots
3. **Check queue position**: Some users have long queues
4. **Try swarm download**: Multiple sources can improve speed

#### Web Interface Not Loading

**Symptoms:** Can't access web UI at `http://localhost:5000`

**Solutions:**
1. **Check if service is running**: `ps aux | grep slskdn` (Linux/macOS) or check Task Manager (Windows)
2. **Check port**: Verify port 5000 is not in use by another service
3. **Check firewall**: Ensure port 5000 is not blocked
4. **Check logs**: Look for error messages in log files

#### High CPU or Memory Usage

**Symptoms:** slskdN using excessive resources

**Solutions:**
1. **Reduce concurrent downloads**: Settings → Downloads → Max Concurrent Downloads
2. **Disable background features**: Disable wishlist, auto-replace if not needed
3. **Check for stuck processes**: Restart slskdN
4. **Review logs**: Check for errors or warnings

#### Search Not Finding Results

**Symptoms:** Searches return no results

**Solutions:**
1. **Check search sources**: Ensure Pod/Mesh or Soulseek Scene is enabled
2. **Try different search terms**: Be more or less specific
3. **Check filters**: Advanced filters may be too restrictive
4. **Wait longer**: Some searches take time to gather results

### Getting Help

1. **Check Logs**: 
   - Linux/macOS: `tail -f ~/.config/slskd/logs/slskd.log`
   - Windows: Check `%APPDATA%\slskd\logs\slskd.log`
   - Docker: `docker logs slskdn`

2. **Check Known Issues**: See [Known Issues](known_issues.md)

3. **Community Support**:
   - [Discord](https://discord.gg/NRzj8xycQZ)
   - [GitHub Issues](https://github.com/snapetech/slskdn/issues)

4. **Documentation**:
   - [Configuration Reference](config.md)
   - [Features Overview](FEATURES.md)
   - [How It Works](HOW-IT-WORKS.md)

## Next Steps

- **Explore Features**: Check out [Features Overview](FEATURES.md) for all available features
- **Configure Advanced Settings**: See [Configuration Reference](config.md) for detailed options
- **Learn About Architecture**: Read [How It Works](HOW-IT-WORKS.md) for technical details
- **Join the Community**: [Discord](https://discord.gg/NRzj8xycQZ)

## Security Best Practices

1. **Change default password** immediately after first login
2. **Use strong authentication**: Enable 2FA if available
3. **Configure firewall**: Only expose necessary ports
4. **Review share settings**: Ensure you're only sharing what you intend
5. **Keep updated**: Regularly update to latest version
6. **Review logs**: Periodically check logs for suspicious activity

See [Security Guidelines](SECURITY-GUIDELINES.md) for detailed security information.

---

**Need more help?** Check the [Documentation Index](README.md) or join our [Discord](https://discord.gg/NRzj8xycQZ)!
