# slskdn Unraid Template

This template allows you to easily install slskdn on Unraid via Community Applications.

## Quick Install (Manual)

If slskdn isn't in the official Community Apps yet, you can add it manually:

1. In Unraid, go to **Settings → Docker → Template Repositories**
2. Add this URL:
   ```
   https://github.com/snapetech/slskdn
   ```
3. Click **Save**
4. Go to **Apps** and search for "slskdn"
5. Click **Install**

## Default Paths

| Setting | Container Path | Default Host Path |
|---------|---------------|-------------------|
| App Data | `/app` | `/mnt/user/appdata/slskdn` |
| Downloads | `/downloads` | `/mnt/user/downloads/slskdn` |
| Music Library | `/music` | `/mnt/user/media/music` |

## Default Ports

| Port | Purpose |
|------|---------|
| 5030 | Web UI (HTTP) |
| 5031 | Web UI (HTTPS) |
| 50300 | Soulseek incoming connections |

**Important:** Port 50300 must be forwarded in your router for optimal connectivity.

## First Run

1. Access the web UI at `http://YOUR_UNRAID_IP:5030`
2. Default login: `slskd` / `slskd`
3. Go to **System** → Configure your Soulseek username/password
4. Configure your shared folders under **System** → **Shares**

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `PUID` | User ID for file permissions | 1000 |
| `PGID` | Group ID for file permissions | 1000 |
| `TZ` | Timezone | America/Chicago |
| `SLSKD_SLSK_USERNAME` | Soulseek username | (set in UI) |
| `SLSKD_SLSK_PASSWORD` | Soulseek password | (set in UI) |

## Support

- **Issues:** https://github.com/snapetech/slskdn/issues
- **Documentation:** https://github.com/snapetech/slskdn

