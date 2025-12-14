# Obfuscated Transports User Guide

This guide explains how to configure and use slskdN's obfuscated transport layer for bypassing DPI (Deep Packet Inspection) censorship.

## Overview

Obfuscated transports make slskdN traffic appear as normal web traffic to avoid detection and blocking by network filters. These transports work alongside the anonymity layer (Tor/I2P) to provide both privacy and censorship resistance.

## Available Transports

### WebSocket Transport

**Purpose**: Makes mesh traffic appear as WebSocket connections (common for web applications)

**Configuration**:
```yaml
transport:
  primaryTransport: WebSocket
  webSocket:
    serverUrl: "wss://websocket-server.example.com/tunnel"
    subProtocol: "slskd-tunnel"
    maxPooledConnections: 5
    customHeaders:
      "X-Custom-Header": "value"
```

**Use Cases**:
- Networks that block unknown protocols but allow WebSocket
- Corporate firewalls that whitelist WebSocket traffic
- Cloud environments with WebSocket support

### HTTP Tunnel Transport

**Purpose**: Encodes traffic as HTTP requests/responses to appear as normal web browsing

**Configuration**:
```yaml
transport:
  primaryTransport: HttpTunnel
  httpTunnel:
    proxyUrl: "https://http-proxy.example.com/tunnel"
    method: "POST"  # POST, GET, PUT
    useHttps: true
    customHeaders:
      "Authorization": "Bearer token"
      "User-Agent": "Mozilla/5.0 (compatible; slskdN)"
```

**Use Cases**:
- Networks that only allow HTTP/HTTPS traffic
- Proxy-aware environments
- CDN-based circumvention

### Obfs4 Transport

**Purpose**: Uses Tor's obfuscation protocol to make traffic appear completely random

**Configuration**:
```yaml
transport:
  primaryTransport: Obfs4
  obfs4:
    obfs4ProxyPath: "/usr/bin/obfs4proxy"
    bridgeLines:
      - "obfs4 192.0.2.1:443 1234567890ABCDEF cert=example cert iat-mode=0"
      - "obfs4 192.0.2.2:80 FEDCBA0987654321 cert=different cert iat-mode=1"
```

**Use Cases**:
- Highly censored environments
- Networks with advanced DPI
- Military-grade censorship resistance

### Meek Transport (Domain Fronting)

**Purpose**: Uses domain fronting through CDNs to hide the true destination

**Configuration**:
```yaml
transport:
  primaryTransport: Meek
  meek:
    bridgeUrl: "https://meek-bridge.example.com/connect"
    frontDomain: "www.google.com"
    userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
    customHeaders:
      "X-Meek-Version": "1.0"
```

**Use Cases**:
- CDN-based censorship (Great Firewall style)
- Domain blocking without IP blocking
- Environments where SNI inspection occurs

## Transport Selection Logic

### Automatic Selection

slskdN automatically selects transports based on:

1. **Configuration priority**: Primary transport → Fallbacks
2. **Availability**: Transport must be reachable and functional
3. **Peer policies**: Per-peer transport preferences
4. **Performance**: Fastest available transport

### Manual Override

Force a specific transport for testing:

```yaml
transport:
  primaryTransport: Obfs4  # Force Obfs4 for all connections
  fallbackTransports: []   # Disable fallbacks
```

### Fallback Behavior

If the primary transport fails, slskdN automatically tries:

```yaml
transport:
  primaryTransport: Obfs4
  fallbackTransports:
    - Meek      # Try Meek if Obfs4 fails
    - WebSocket # Try WebSocket if Meek fails
    - HttpTunnel # Try HTTP if WebSocket fails
    - Direct    # Fall back to no obfuscation
```

## Setup Instructions

### WebSocket Transport Setup

1. **Deploy WebSocket Server**:
   ```bash
   # Using Node.js
   npm install ws
   node websocket-server.js
   ```

2. **Configure slskdN**:
   ```yaml
   transport:
     primaryTransport: WebSocket
     webSocket:
       serverUrl: "wss://your-server.com:8443/slskd"
   ```

3. **Test Connection**:
   ```bash
   # Use WebSocket test tools
   wscat -c wss://your-server.com:8443/slskd
   ```

### HTTP Tunnel Setup

1. **Deploy HTTP Proxy**:
   ```bash
   # Using nginx
   server {
       listen 443 ssl;
       server_name proxy.example.com;

       location /tunnel {
           proxy_pass http://localhost:5000;  # slskdN endpoint
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection "upgrade";
       }
   }
   ```

2. **Configure slskdN**:
   ```yaml
   transport:
     primaryTransport: HttpTunnel
     httpTunnel:
       proxyUrl: "https://proxy.example.com/tunnel"
   ```

### Obfs4 Transport Setup

1. **Install obfs4proxy**:
   ```bash
   # Ubuntu/Debian
   sudo apt install obfs4proxy

   # Or download from torproject.org
   wget https://dist.torproject.org/obfs4proxy/obfs4proxy
   chmod +x obfs4proxy
   ```

2. **Find Bridge Lines**:
   - Visit https://bridges.torproject.org/
   - Request obfs4 bridges
   - Copy the bridge lines

3. **Configure slskdN**:
   ```yaml
   transport:
     primaryTransport: Obfs4
     obfs4:
       obfs4ProxyPath: "/usr/bin/obfs4proxy"
       bridgeLines:
         - "obfs4 [BRIDGE_LINE_FROM_TOR]"
   ```

### Meek Transport Setup

1. **Find Fronting Domain**:
   - Use major CDN domains that support domain fronting
   - Examples: www.google.com, www.bing.com, ajax.googleapis.com

2. **Deploy Meek Server**:
   ```bash
   # Meek server implementation
   # (Requires custom server that accepts domain-fronted requests)
   ```

3. **Configure slskdN**:
   ```yaml
   transport:
     primaryTransport: Meek
     meek:
       bridgeUrl: "https://meek-server.example.com/connect"
       frontDomain: "www.google.com"
   ```

## Performance Considerations

### Latency Impact

| Transport | Typical Latency | Notes |
|-----------|----------------|-------|
| Direct | 0-50ms | Baseline |
| WebSocket | +20-100ms | WebSocket handshake |
| HTTP Tunnel | +50-200ms | HTTP round trips |
| Obfs4 | +100-500ms | Obfuscation overhead |
| Meek | +200-1000ms | Domain fronting |

### Bandwidth Overhead

| Transport | Overhead | Notes |
|-----------|----------|-------|
| Direct | 0% | No overhead |
| WebSocket | +5-10% | WebSocket framing |
| HTTP Tunnel | +10-20% | HTTP headers |
| Obfs4 | +15-25% | Obfuscation padding |
| Meek | +20-30% | Encryption + fronting |

### CPU Usage

| Transport | CPU Impact | Notes |
|-----------|------------|-------|
| Direct | Low | Standard encryption |
| WebSocket | Low | WebSocket protocol |
| HTTP Tunnel | Medium | HTTP parsing |
| Obfs4 | High | Heavy obfuscation |
| Meek | Medium | Encryption overhead |

## Security Considerations

### Transport Security

**WebSocket Transport**:
- Uses WSS (WebSocket Secure) by default
- Certificate validation recommended
- Vulnerable to WebSocket inspection

**HTTP Tunnel Transport**:
- Uses HTTPS by default
- Supports custom certificates
- Vulnerable to HTTP inspection

**Obfs4 Transport**:
- Military-grade obfuscation
- Resists advanced DPI
- Requires trusted bridge operators

**Meek Transport**:
- Hides destination via domain fronting
- Vulnerable if CDN blocks fronting
- Requires fronting-capable domains

### Threat Model Coverage

**Against Network Filters**:
- ✅ Protocol blocking (unknown protocols)
- ✅ SNI inspection (domain fronting)
- ✅ DPI analysis (obfs4 randomization)
- ✅ Keyword filtering (encrypted payloads)

**Limitations**:
- ❌ Traffic volume analysis (use privacy layer)
- ❌ Timing attacks (use jitter + batching)
- ❌ Endpoint blocking (use bridge rotation)

## Troubleshooting

### Common Issues

#### WebSocket Connection Failures
**Symptoms**: "WebSocket transport not available"
**Solutions**:
- Verify WebSocket server is running and accessible
- Check WSS certificate validity
- Ensure firewall allows WebSocket traffic
- Test with: `wscat -c wss://your-server.com`

#### HTTP Tunnel Timeouts
**Symptoms**: Slow or failing connections
**Solutions**:
- Check HTTP proxy server logs
- Verify HTTPS certificate chain
- Test direct HTTP access to proxy
- Reduce request frequency if rate limited

#### Obfs4 Bridge Issues
**Symptoms**: "No suitable obfs4 bridge found"
**Solutions**:
- Verify bridge lines format
- Check bridge reachability: `telnet [BRIDGE_IP] [BRIDGE_PORT]`
- Try different bridge lines from torproject.org
- Ensure obfs4proxy is installed and executable

#### Meek Domain Fronting Blocks
**Symptoms**: CDN returns errors or redirects
**Solutions**:
- Try different fronting domains
- Check if CDN still supports domain fronting
- Verify meek server accepts fronted requests
- Monitor for blocking patterns

### Debug Logging

Enable transport-specific logging:

```yaml
logging:
  levels:
    slskd.Common.Security.WebSocketTransport: Debug
    slskd.Common.Security.HttpTunnelTransport: Debug
    slskd.Common.Security.Obfs4Transport: Debug
    slskd.Common.Security.MeekTransport: Debug
    slskd.Common.Security.AnonymityTransportSelector: Debug
```

### Testing Tools

**WebSocket Testing**:
```bash
# Install wscat
npm install -g wscat

# Test connection
wscat -c wss://your-websocket-server.com
```

**HTTP Testing**:
```bash
# Test proxy
curl -X POST https://your-proxy.com/tunnel \
  -H "Content-Type: application/octet-stream" \
  -d "test data"
```

**Obfs4 Testing**:
```bash
# Test bridge
/usr/bin/obfs4proxy client 127.0.0.1:0 [BRIDGE_LINE]
```

## Integration with Anonymity Layer

### Combined Usage

Obfuscated transports work best with anonymity transports:

```yaml
# Recommended configuration
anonymity:
  enabled: true
  mode: Tor

transport:
  primaryTransport: Obfs4  # Obfuscate before Tor
  fallbackTransports: [Meek, WebSocket]
```

### Transport Priority Matrix

| Scenario | Recommended Primary | Fallbacks |
|----------|-------------------|-----------|
| Corporate Firewall | WebSocket | HttpTunnel, Direct |
| National Firewall | Obfs4 | Meek, HttpTunnel |
| CDN Blocking | Meek | WebSocket, HttpTunnel |
| Protocol Blocking | HttpTunnel | WebSocket, Direct |

### Performance Optimization

For best performance with obfuscation:

```yaml
# Optimize for your network
privacy:
  # Reduce overhead
  padding:
    bucketSizes: [1024, 2048]  # Smaller buckets

  # Minimize delays
  timing:
    jitterMs: 200  # Less jitter

# Choose fastest transport
transport:
  primaryTransport: WebSocket  # Usually fastest
  fallbackTransports: [HttpTunnel, Direct]
```

## Advanced Configuration

### Custom Bridge Rotation

Automatically rotate bridges for better reliability:

```yaml
transport:
  obfs4:
    bridgeLines:
      - "obfs4 bridge1..."
      - "obfs4 bridge2..."
      - "obfs4 bridge3..."
    # slskdN rotates through these automatically
```

### Transport-Specific Timeouts

Customize timeouts per transport:

```yaml
transport:
  webSocket:
    connectTimeoutMs: 10000
  httpTunnel:
    requestTimeoutMs: 30000
  obfs4:
    bridgeTimeoutMs: 15000
  meek:
    frontingTimeoutMs: 45000
```

### Load Balancing

Distribute load across multiple endpoints:

```yaml
transport:
  webSocket:
    serverUrl: "wss://ws1.example.com,tcp://ws2.example.com,wss://ws3.example.com"
  httpTunnel:
    proxyUrl: "https://proxy1.example.com,https://proxy2.example.com"
```

## Monitoring and Maintenance

### Transport Health Monitoring

Monitor transport status via WebGUI:

1. Navigate to `Settings → Security → Adversarial → Overview`
2. Check "Transport Status" section
3. View connection statistics and error rates
4. Use "Test Connectivity" to verify functionality

### Log Analysis

Monitor transport performance:

```bash
# Search for transport events
grep "Transport.*connected\|Transport.*failed" /app/logs/slskd.log

# Monitor fallback usage
grep "failover.*successful\|failover.*failed" /app/logs/slskd.log
```

### Maintenance Tasks

**Weekly**:
- Check transport status in WebGUI
- Review error logs for patterns
- Test connectivity manually

**Monthly**:
- Update bridge lines from torproject.org
- Rotate fronting domains if needed
- Review performance metrics

**When Issues Occur**:
- Enable debug logging temporarily
- Test individual transports
- Check network firewall rules
- Verify server certificates

## Future Enhancements

**Planned Features**:
- **Bridge Discovery Service**: Automatic bridge finding
- **Transport Auto-Tuning**: Performance-based selection
- **Custom Obfuscation**: Domain-specific protocols
- **Transport Chaining**: Multiple obfuscation layers

**Research Areas**:
- **Quantum Resistance**: Post-quantum obfuscation
- **AI Detection Evasion**: Adaptive traffic shaping
- **Satellite Networks**: High-latency transport optimization

---

This guide covers the complete obfuscated transport system in slskdN. The combination of multiple transport options ensures reliable connectivity even in heavily censored environments, while maintaining performance and security.


