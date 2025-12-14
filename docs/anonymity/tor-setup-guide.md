# Tor Setup Guide for slskdN

This guide covers installing, configuring, and using Tor with slskdN for anonymous peer-to-peer networking.

## Overview

Tor (The Onion Router) provides anonymity by routing your traffic through multiple volunteer-operated nodes. slskdN integrates with Tor via the SOCKS5 proxy interface to route mesh overlay connections through the Tor network.

## Prerequisites

- Linux, macOS, or Windows
- Root/administrator access for system-wide Tor installation
- At least 512MB RAM for Tor daemon
- Network connectivity to Tor directory authorities

## Installation

### Ubuntu/Debian

```bash
# Update package lists
sudo apt update

# Install Tor
sudo apt install tor

# Start and enable Tor service
sudo systemctl enable tor
sudo systemctl start tor

# Verify installation
tor --version
```

### CentOS/RHEL/Fedora

```bash
# Install Tor (EPEL repository may be required)
sudo dnf install tor

# Or for older systems:
sudo yum install tor

# Start and enable Tor service
sudo systemctl enable tor
sudo systemctl start tor
```

### macOS

```bash
# Install using Homebrew
brew install tor

# Start Tor service
brew services start tor

# Or run manually
tor
```

### Windows

Download the Tor Browser Bundle from [torproject.org](https://www.torproject.org/download/) and extract it, or use Chocolatey:

```powershell
# Install Chocolatey if not already installed
# Then install Tor
choco install tor
```

### Docker

```yaml
# Add to docker-compose.yml
services:
  tor:
    image: dperson/torproxy
    ports:
      - "9050:9050"
    environment:
      - TOR_ControlPort=9051
      - TOR_CookieAuthentication=1
    restart: unless-stopped
```

## Basic Configuration

### Tor Configuration File

Edit `/etc/tor/torrc` (Linux) or `torrc` in your Tor installation directory:

```torrc
# Basic SOCKS proxy configuration
SocksPort 9050
SocksListenAddress 127.0.0.1

# Control port for monitoring (optional)
ControlPort 9051
CookieAuthentication 1

# Exit policy - restrict exit nodes for better anonymity
ExitPolicy reject *:*

# Optional: Use bridges if Tor is blocked in your region
# Bridge obfs4 <bridge-line>
# ClientTransportPlugin obfs4 exec /usr/bin/obfs4proxy
```

### slskdN Configuration

Add to your `slskd.yml` configuration:

```yaml
# Enable anonymity layer
anonymity:
  enabled: true
  mode: Tor

  # Tor-specific settings
  tor:
    socksAddress: "127.0.0.1:9050"
    isolateStreams: true
```

## Advanced Configuration

### Stream Isolation

Stream isolation ensures different connections use different Tor circuits, preventing correlation attacks:

```yaml
anonymity:
  tor:
    isolateStreams: true
```

**Benefits:**
- Each peer gets a separate Tor circuit
- Connection metadata cannot be correlated
- Better privacy against traffic analysis

**Tradeoffs:**
- Higher resource usage (more circuits)
- Slower initial connections
- Increased Tor network load

### Bridges

If Tor is blocked in your region, configure bridges:

```torrc
# Use bridges
UseBridges 1

# Add bridge lines (get from https://bridges.torproject.org/)
Bridge obfs4 192.0.2.1:443 1234567890ABCDEF... cert=... iat-mode=0
Bridge obfs4 192.0.2.2:443 1234567890ABCDEF... cert=... iat-mode=0

# Enable obfs4 transport
ClientTransportPlugin obfs4 exec /usr/bin/obfs4proxy
```

Install obfs4proxy:
```bash
# Ubuntu/Debian
sudo apt install obfs4proxy

# macOS
brew install obfs4proxy

# Other systems: Download from torproject.org
```

### Bandwidth Limits

Configure Tor bandwidth to avoid overwhelming your connection:

```torrc
# Bandwidth limits
RelayBandwidthRate 100 KBytes
RelayBandwidthBurst 200 KBytes

# Accounting (optional)
AccountingStart month 1 00:00
AccountingMax 100 GBytes
```

## Testing Tor Setup

### 1. Basic Connectivity Test

```bash
# Test SOCKS proxy
curl --socks5 127.0.0.1:9050 https://check.torproject.org/api/ip
```

Expected response:
```json
{"IsTor":true,"IP":"..."}
```

### 2. slskdN Tor Test

Use the WebGUI to test Tor connectivity:

1. Navigate to Settings → Security → Adversarial
2. Go to the "Overview" tab
3. Click "Test Transport Connectivity"
4. Check Tor status indicator

### 3. Circuit Information

Check your Tor circuits:

```bash
# Via control port
echo "GETINFO circuit-status" | nc 127.0.0.1 9051
```

Or use tools like `nyx` or `arm` for monitoring.

## Troubleshooting

### Common Issues

#### Tor Won't Start

**Problem:** Tor service fails to start
**Solution:**
```bash
# Check logs
sudo journalctl -u tor -n 50

# Test configuration
tor --verify-config

# Check permissions
sudo chown debian-tor:debian-tor /var/lib/tor
```

#### Connection Timeouts

**Problem:** slskdN connections through Tor time out
**Solutions:**
- Increase timeout values in slskdN config
- Check Tor bootstrap status: `tail -f /var/log/tor/log`
- Verify SOCKS proxy is accessible: `telnet 127.0.0.1 9050`

#### High Latency

**Problem:** Tor connections are very slow
**Solutions:**
- Use faster guard nodes: `ExitPolicy reject *:25` (block SMTP)
- Configure `StrictNodes 0` in torrc
- Consider using Tor with bridges in uncensored regions

#### DNS Leaks

**Problem:** DNS queries bypass Tor
**Solution:** Ensure slskdN uses Tor's SOCKS proxy for all connections:

```yaml
anonymity:
  tor:
    # Tor handles DNS resolution automatically via SOCKS
    socksAddress: "127.0.0.1:9050"
```

### Debug Logging

Enable verbose logging in slskdN:

```yaml
logging:
  levels:
    slskd.Common.Security.TorSocksTransport: Debug
    slskd.Mesh.Transport: Debug
```

And in Tor:

```torrc
Log notice file /var/log/tor/notices.log
Log info file /var/log/tor/info.log
```

## Performance Considerations

### Latency Impact

Tor typically adds 200-500ms latency per hop (3 hops = ~1-2 seconds). For mesh networking:

- **Initial connections:** 5-10 seconds
- **Data transfer:** 20-50% slower than direct connections
- **Peer discovery:** Significantly slower due to DHT lookups

### Bandwidth Overhead

Tor adds ~10-20% bandwidth overhead due to encryption and routing.

### Resource Usage

- **Memory:** 100-200MB per Tor process
- **CPU:** Moderate encryption overhead
- **Network:** Additional connections to Tor nodes

## Security Considerations

### Threat Model

Tor protects against:
- ✅ IP address correlation
- ✅ Traffic analysis by local network observers
- ✅ ISP-level surveillance
- ✅ Exit node eavesdropping (when using HTTPS)

Tor does NOT protect against:
- ❌ End-to-end application-level attacks
- ❌ Malicious exit nodes (use HTTPS)
- ❌ Timing attacks (use stream isolation)
- ❌ Tor node compromise

### Best Practices

1. **Always use HTTPS** for any sensitive data
2. **Enable stream isolation** for multi-peer scenarios
3. **Regularly update Tor** to patch security issues
4. **Monitor Tor logs** for unusual activity
5. **Use bridges** in censored environments
6. **Avoid clearnet fallback** when privacy is critical

### Exit Node Risks

Exit nodes can see your traffic in plaintext. Mitigate with:

```yaml
# Force HTTPS-only applications
# Use end-to-end encryption in slskdN
anonymity:
  tor:
    isolateStreams: true  # Different circuits per peer
```

## Integration with slskdN

### Automatic Mode Selection

slskdN automatically selects Tor when:
- `anonymity.enabled = true`
- `anonymity.mode = Tor`
- Tor SOCKS proxy is available

### Fallback Behavior

If Tor is unavailable:
- slskdN logs warnings
- Falls back to direct connections (configurable)
- WebGUI shows connection status

### Monitoring

Monitor Tor usage via:
- WebGUI Security → Adversarial → Overview
- Application logs for transport decisions
- Tor control port for circuit information

## Alternative Tor Configurations

### Multiple Tor Instances

Run multiple Tor processes for different purposes:

```bash
# Tor instance 1 (general browsing)
tor -f /etc/tor/torrc1

# Tor instance 2 (slskdN mesh)
tor -f /etc/tor/torrc2
```

### Tor Hidden Services

For server-side anonymity (advanced):

```torrc
# Hidden service for slskdN control interface
HiddenServiceDir /var/lib/tor/hidden_slskdn/
HiddenServicePort 80 127.0.0.1:5000
```

## Support

For issues:
1. Check slskdN logs: `tail -f /app/logs/slskd.log`
2. Verify Tor status: `systemctl status tor`
3. Test connectivity: Use WebGUI transport test
4. Check Tor logs: `tail -f /var/log/tor/log`

## Further Reading

- [Tor Project Documentation](https://community.torproject.org/)
- [Tor Manual](https://2019.www.torproject.org/docs/tor-manual.html.en)
- [slskdN Security Documentation](../security/threat-model.md)
- [Traffic Analysis Prevention](../security/traffic-analysis.md)
