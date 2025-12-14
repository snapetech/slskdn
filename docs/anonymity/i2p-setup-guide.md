# I2P Setup Guide for slskdN

This guide covers installing, configuring, and using I2P (Invisible Internet Project) with slskdN for anonymous peer-to-peer networking.

## Overview

I2P is a fully decentralized anonymous network that provides end-to-end encryption and garlic routing. Unlike Tor's onion routing, I2P is designed specifically for peer-to-peer applications and provides better performance for persistent connections.

slskdN integrates with I2P via the SAM (Simple Anonymous Messaging) bridge to create I2P destinations for mesh overlay connections.

## Prerequisites

- Linux, macOS, or Windows
- Java Runtime Environment (JRE) 8 or higher
- At least 512MB RAM for I2P router
- Network connectivity to I2P network

## Installation

### Ubuntu/Debian

```bash
# Update package lists
sudo apt update

# Install I2P
sudo apt install i2p

# Start I2P service (may take several minutes to bootstrap)
sudo systemctl enable i2p
sudo systemctl start i2p

# Wait for bootstrap (check /var/log/i2p.log)
tail -f /var/log/i2p/wrapper.log
```

### CentOS/RHEL/Fedora

```bash
# Enable EPEL repository
sudo dnf install epel-release

# Install I2P
sudo dnf install i2p

# Start I2P service
sudo systemctl enable i2p
sudo systemctl start i2p
```

### macOS

```bash
# Install using Homebrew
brew install i2p

# Start I2P service
brew services start i2p

# Or run manually
i2prouter start
```

### Windows

Download the I2P installer from [geti2p.net](https://geti2p.net/en/download) and run the installer.

### Docker

```yaml
# Add to docker-compose.yml
services:
  i2p:
    image: geti2p/i2p
    ports:
      - "4444:4444"    # HTTP proxy
      - "4445:4445"    # HTTPS proxy
      - "6668:6668"    # IRC proxy
      - "7656:7656"    # SAM bridge
      - "7657:7657"    # I2CP
      - "7659:7659"    # UPnP
    volumes:
      - i2p-data:/home/i2p/.i2p
    restart: unless-stopped

volumes:
  i2p-data:
```

## Basic Configuration

### I2P Router Configuration

Access the I2P web console at http://127.0.0.1:7657 and configure:

1. **Bandwidth Settings:**
   - Set appropriate bandwidth limits
   - Choose speed setting (V. Fast, Fast, etc.)

2. **Network Settings:**
   - Enable UPnP if behind NAT
   - Configure port forwarding if needed

3. **Advanced Settings:**
   - Enable SAM bridge: `sam.enabled=true`
   - Set SAM listen address: `sam.host=127.0.0.1`
   - Set SAM port: `sam.port=7656`

### slskdN Configuration

Add to your `slskd.yml` configuration:

```yaml
# Enable anonymity layer
anonymity:
  enabled: true
  mode: I2P

  # I2P-specific settings
  i2p:
    samAddress: "127.0.0.1:7656"
```

## Advanced Configuration

### SAM Bridge Configuration

The SAM (Simple Anonymous Messaging) bridge allows applications to create I2P destinations:

```yaml
# I2P configuration in slskdN
anonymity:
  i2p:
    samAddress: "127.0.0.1:7656"

    # Optional: Custom destination options
    destinationOptions:
      - "inbound.length=3"
      - "outbound.length=3"
      - "inbound.lengthVariance=1"
      - "outbound.lengthVariance=1"
      - "inbound.backupQuantity=2"
      - "outbound.backupQuantity=2"
```

### Tunnel Configuration

Configure I2P tunnels for better performance:

1. **Client Tunnels** (for outbound connections):
   ```
   Name: slskdN-outbound
   Type: Client
   Destination: 127.0.0.1:7656 (SAM)
   ```

2. **Server Tunnels** (for inbound connections):
   ```
   Name: slskdN-inbound
   Type: Server
   Target: 127.0.0.1:5000 (slskdN port)
   ```

### Bandwidth Management

Optimize I2P bandwidth settings:

```yaml
# In slskdN config (when implemented)
anonymity:
  i2p:
    inboundBandwidth: "100000"    # 100 KB/s
    outboundBandwidth: "100000"   # 100 KB/s
    sharePercentage: 80           # Share 80% of bandwidth
```

## Testing I2P Setup

### 1. Basic Connectivity Test

```bash
# Test SAM bridge connectivity
echo "HELLO VERSION MIN=3.1 MAX=3.1" | nc 127.0.0.1 7656
```

Expected response:
```
HELLO REPLY RESULT=OK VERSION=3.1
```

### 2. I2P Router Status

Check I2P router status:

```bash
# Via web interface: http://127.0.0.1:7657
# Check:
# - Router uptime
# - Participating tunnels
# - Bandwidth usage
# - Peer connections
```

### 3. slskdN I2P Test

Use the WebGUI to test I2P connectivity:

1. Navigate to Settings → Security → Adversarial
2. Go to the "Overview" tab
3. Click "Test Transport Connectivity"
4. Check I2P status indicator

## Troubleshooting

### Common Issues

#### I2P Won't Start

**Problem:** I2P router fails to start
**Solutions:**
```bash
# Check Java installation
java -version

# Check logs
tail -f /var/log/i2p/wrapper.log

# Try starting manually
i2prouter start

# Check firewall
sudo ufw allow 7656/tcp
```

#### SAM Bridge Not Responding

**Problem:** slskdN cannot connect to SAM bridge
**Solutions:**
- Verify I2P web console shows SAM enabled
- Check SAM port: `netstat -tlnp | grep 7656`
- Test connectivity: `telnet 127.0.0.1 7656`
- Restart I2P router

#### Slow Connections

**Problem:** I2P connections are very slow
**Solutions:**
- Wait for I2P to build more tunnels (can take 30+ minutes)
- Increase bandwidth allocation in I2P console
- Check network configuration and firewall rules
- Reduce tunnel length for faster but less secure connections

#### High CPU Usage

**Problem:** I2P uses excessive CPU
**Solutions:**
- Reduce tunnel quantity in I2P settings
- Lower bandwidth limits
- Check for I2P updates
- Consider JVM tuning: `JAVA_OPTS="-Xmx256m -Xms128m"`

### Debug Logging

Enable verbose logging:

```yaml
# slskdN logging
logging:
  levels:
    slskd.Common.Security.I2PTransport: Debug
    slskd.Mesh.Transport: Debug
```

I2P debug logging:
- Access http://127.0.0.1:7657/logs
- Enable debug logging for SAM bridge

## Performance Considerations

### Latency

I2P typically has lower latency than Tor:
- **Initial connections:** 2-5 seconds (vs Tor's 5-10 seconds)
- **Data transfer:** 10-30% slower than direct
- **Persistent connections:** Much better performance

### Bandwidth

I2P has lower overhead than Tor:
- **Protocol overhead:** ~5-10% (vs Tor's 10-20%)
- **CPU usage:** Moderate encryption overhead
- **Memory:** 200-400MB per router

### Scalability

I2P advantages for mesh networking:
- **Persistent connections:** Optimized for P2P
- **Multiplexing:** Single tunnel handles multiple streams
- **Garlic routing:** Batches messages for efficiency

## Security Considerations

### Threat Model

I2P protects against:
- ✅ Network-level surveillance
- ✅ Traffic analysis
- ✅ Exit node attacks (no exit nodes)
- ✅ Correlation attacks (garlic routing)

I2P limitations:
- ❌ Smaller network (fewer participants)
- ❌ Less researched than Tor
- ❌ Java dependency

### Best Practices

1. **Wait for full integration** - I2P needs time to build connections
2. **Use reasonable bandwidth limits** - Don't overwhelm your connection
3. **Monitor router status** - Keep I2P updated and healthy
4. **Enable UPnP** if behind NAT for better connectivity
5. **Use with Tor as fallback** for maximum privacy

### Network Health

Contribute to I2P network health:
- Share bandwidth generously
- Keep router running 24/7 when possible
- Participate in tunnel building

## Comparison: Tor vs I2P

| Aspect | Tor | I2P |
|--------|-----|-----|
| **Routing** | Onion routing | Garlic routing |
| **Latency** | 1-2 seconds | 0.5-1 second |
| **Network Size** | Millions of users | Thousands of users |
| **Use Case** | General anonymity | P2P applications |
| **Exit Nodes** | Yes (can see traffic) | No (fully internal) |
| **Java Required** | No | Yes |
| **Bootstrap Time** | Fast | Slow (30+ minutes) |
| **Mobile Support** | Good | Limited |

## Integration with slskdN

### Automatic Mode Selection

slskdN selects I2P when:
- `anonymity.enabled = true`
- `anonymity.mode = I2P`
- I2P SAM bridge is available and responding

### Fallback Behavior

If I2P is unavailable:
- slskdN logs warnings
- Falls back to direct connections or Tor
- WebGUI shows connection status

### Monitoring

Monitor I2P usage via:
- WebGUI Security → Adversarial → Overview
- I2P web console: http://127.0.0.1:7657
- Application logs for transport decisions

## Advanced I2P Features

### Custom Destinations

Create persistent I2P destinations:

```yaml
# Future slskdN feature
anonymity:
  i2p:
    persistentDestination: true
    destinationKeyFile: "/app/config/i2p.keys.dat"
```

### Multiple Routers

Run multiple I2P routers for load balancing:

```yaml
# Router 1
anonymity:
  i2p:
    samAddress: "127.0.0.1:7656"

# Router 2 (different instance)
anonymity:
  i2p:
    samAddress: "127.0.0.1:7658"
```

### I2P Hidden Services

For server-side anonymity (advanced):

```yaml
# Configure hidden service tunnels in I2P console
# Point to slskdN's listen address
# Get .i2p address from tunnel configuration
```

## Support

For issues:
1. Check slskdN logs: `tail -f /app/logs/slskd.log`
2. Check I2P logs: `tail -f /var/log/i2p/wrapper.log`
3. Verify I2P status: Open http://127.0.0.1:7657
4. Test SAM bridge: Use WebGUI transport test
5. Check network connectivity and firewall rules

## Community Resources

- [I2P Project Website](https://geti2p.net/)
- [I2P Documentation](https://geti2p.net/en/docs)
- [I2P Forums](https://forum.i2p2.de/)
- [Privacy Comparison: Tor vs I2P](https://www.privacytools.io/providers/tor-i2p/)
- [slskdN Security Documentation](../security/threat-model.md)

## Future Development

I2P integration in slskdN is currently in early stages. Future enhancements include:

- Persistent destination management
- Advanced tunnel configuration
- I2P-only mesh networks
- Bandwidth optimization
- Mobile I2P router support

