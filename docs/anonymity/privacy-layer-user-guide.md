# Privacy Layer User Guide

This guide explains how to configure and use slskdN's privacy layer for traffic analysis protection and anonymity.

## Overview

The privacy layer provides multiple layers of protection against traffic analysis:

- **Message Padding**: Prevents size-based traffic analysis
- **Timing Obfuscation**: Randomizes message timing to obscure patterns
- **Message Batching**: Groups messages to reduce timing metadata
- **Cover Traffic**: Generates dummy traffic during idle periods
- **Transport Selection**: Automatically chooses appropriate anonymity transports

## Quick Start

### Enable Basic Privacy Layer

```yaml
# In slskd.yml
privacy:
  enabled: true
  padding:
    enabled: true
    bucketSizes: [512, 1024, 2048, 4096]
  timing:
    enabled: true
    jitterMs: 500
  batching:
    enabled: false  # Start with false, enable after testing
  coverTraffic:
    enabled: false  # Start with false, enable after testing
```

### Enable Anonymity Layer

```yaml
# In slskd.yml
anonymity:
  enabled: true
  mode: Tor  # Options: Direct, Tor, I2P, RelayOnly

  # Tor configuration
  tor:
    socksAddress: "127.0.0.1:9050"
    isolateStreams: true

  # Transport preferences
  transport:
    primaryTransport: Tor
    fallbackTransports: [Direct]
```

## Privacy Layer Configuration

### Message Padding

Prevents attackers from inferring message content from packet sizes.

```yaml
privacy:
  padding:
    enabled: true
    bucketSizes: [512, 1024, 2048, 4096, 8192]
    useRandomFill: true
    maxOverheadPercent: 200  # Max 200% size increase
```

**Options:**
- `bucketSizes`: Target message sizes (messages are rounded up to next bucket)
- `useRandomFill`: Fill padding with random bytes (true) or zeros (false)
- `maxOverheadPercent`: Maximum allowed size increase before skipping padding

**Example:**
- Original message: 300 bytes
- Rounds up to: 512 bytes (212 bytes padding)
- Overhead: 71% (under 200% limit)

### Timing Obfuscation

Adds random delays to prevent timing analysis.

```yaml
privacy:
  timing:
    enabled: true
    jitterMs: 500  # Max delay: 0-500ms
```

**Options:**
- `jitterMs`: Maximum random delay in milliseconds

**Behavior:**
- Each outbound message gets a random delay between 0 and `jitterMs`
- Inbound messages are not delayed (to avoid UI lag)
- Delays are applied before message batching

### Message Batching

Groups multiple messages together to reduce timing metadata.

```yaml
privacy:
  batching:
    enabled: true
    maxBatchSize: 10      # Max messages per batch
    batchWindowMs: 1000   # Max wait time for batch
```

**Options:**
- `maxBatchSize`: Maximum messages in a batch
- `batchWindowMs`: Maximum time to wait for more messages

**Behavior:**
- Messages queue until `maxBatchSize` reached OR `batchWindowMs` expires
- Batched messages sent together
- Reduces the number of timing events an observer can see

### Cover Traffic

Generates dummy messages during idle periods.

```yaml
privacy:
  coverTraffic:
    enabled: true
    intervalSeconds: 30     # Send dummy message every 30s when idle
    onlyWhenIdle: true      # Only send when no real traffic
    maxSizeBytes: 1024      # Max size of dummy messages
```

**Options:**
- `intervalSeconds`: How often to check for idle periods
- `onlyWhenIdle`: Only send when no real messages sent recently
- `maxSizeBytes`: Maximum size of dummy messages

**Behavior:**
- Monitors outbound activity
- When idle, sends dummy messages at configured intervals
- Makes it harder to distinguish real from fake traffic

## Anonymity Layer Configuration

### Transport Modes

#### Direct Mode (No Anonymity)
```yaml
anonymity:
  enabled: true
  mode: Direct
```
- Uses standard network connections
- No additional latency or overhead
- Still benefits from privacy layer protections

#### Tor Mode
```yaml
anonymity:
  enabled: true
  mode: Tor

  tor:
    socksAddress: "127.0.0.1:9050"  # Tor SOCKS proxy
    isolateStreams: true             # Different circuits per peer
```
- Routes traffic through Tor network
- High latency (~1-2 seconds)
- Strong anonymity but slower performance

#### I2P Mode
```yaml
anonymity:
  enabled: true
  mode: I2P

  i2p:
    samAddress: "127.0.0.1:7656"  # I2P SAM bridge
```
- Uses I2P anonymous network
- Lower latency than Tor for persistent connections
- Fully decentralized, no exit nodes

#### RelayOnly Mode
```yaml
anonymity:
  enabled: true
  mode: RelayOnly

  relayOnly:
    trustedRelayPeers: ["peer1", "peer2", "peer3"]
    maxChainLength: 3
```
- Routes through trusted mesh relay nodes
- Zero IP address exposure
- Requires pre-configured trusted relays

### Obfuscated Transports

Additional transport layers for censorship resistance:

#### WebSocket Transport
```yaml
transport:
  primaryTransport: WebSocket
  webSocket:
    serverUrl: "wss://websocket-server.example.com/tunnel"
    subProtocol: "slskd-tunnel"
    customHeaders:
      "X-Custom-Header": "value"
```

#### HTTP Tunnel Transport
```yaml
transport:
  primaryTransport: HttpTunnel
  httpTunnel:
    proxyUrl: "https://http-proxy.example.com/tunnel"
    method: "POST"
    useHttps: true
    customHeaders:
      "Authorization": "Bearer token"
```

#### Obfs4 Transport
```yaml
transport:
  primaryTransport: Obfs4
  obfs4:
    obfs4ProxyPath: "/usr/bin/obfs4proxy"
    bridgeLines:
      - "obfs4 192.0.2.1:443 1234567890ABCDEF cert=... iat-mode=0"
```

#### Meek Transport (Domain Fronting)
```yaml
transport:
  primaryTransport: Meek
  meek:
    bridgeUrl: "https://meek-bridge.example.com/connect"
    frontDomain: "www.google.com"
    userAgent: "Mozilla/5.0 (compatible; slskdN)"
```

## WebGUI Configuration

### Privacy Settings Panel

Navigate to `Settings → Security → Adversarial → Privacy` tab:

1. **Enable Privacy Layer**: Master toggle for all privacy features
2. **Message Padding**: Configure bucket sizes and fill options
3. **Timing Obfuscation**: Set maximum jitter delay
4. **Message Batching**: Configure batch size and window
5. **Cover Traffic**: Set idle detection and dummy message parameters

### Anonymity Settings Panel

Navigate to `Settings → Security → Adversarial → Anonymity` tab:

1. **Anonymity Mode**: Choose Direct/Tor/I2P/RelayOnly
2. **Tor Configuration**: SOCKS address and stream isolation
3. **I2P Configuration**: SAM bridge settings
4. **Relay Configuration**: Trusted peer list and chain limits

### Transport Settings Panel

Navigate to `Settings → Security → Adversarial → Transport` tab:

1. **Primary Transport**: Choose obfuscated transport type
2. **Transport-Specific Settings**: Configure WebSocket, HTTP, Obfs4, or Meek parameters
3. **Fallback Options**: Configure automatic failover behavior

### Status Monitoring

Navigate to `Settings → Security → Adversarial → Overview` tab:

- **Privacy Layer Status**: Shows active privacy transformations
- **Transport Status**: Shows available anonymity transports
- **Connection Statistics**: Active connections and success rates
- **Test Connectivity**: Button to test all transport configurations

## Performance Considerations

### Latency Impact

| Feature | Typical Latency | Notes |
|---------|----------------|-------|
| Message Padding | +0-5ms | CPU-bound, depends on message size |
| Timing Obfuscation | +0-500ms | Configurable random delay |
| Message Batching | +0-1000ms | Wait time for batch completion |
| Cover Traffic | +0ms | Background process |
| Tor Transport | +1000-2000ms | Network routing through Tor |
| I2P Transport | +500-1000ms | Garlic routing in I2P |
| WebSocket Transport | +50-200ms | WebSocket handshake overhead |

### Bandwidth Overhead

| Feature | Overhead | Notes |
|---------|----------|-------|
| Message Padding | +0-300% | Depends on bucket sizes |
| Timing Obfuscation | +0% | No bandwidth impact |
| Message Batching | -20% to +10% | Can reduce or increase depending on batching efficiency |
| Cover Traffic | +10-50% | Dummy messages during idle periods |
| Tor Transport | +10-20% | Tor protocol overhead |
| I2P Transport | +5-10% | I2P protocol overhead |

### CPU/Memory Usage

| Feature | CPU Impact | Memory Impact |
|---------|------------|----------------|
| Message Padding | Moderate | Low |
| Timing Obfuscation | Low | Low |
| Message Batching | Low | Moderate (queue) |
| Cover Traffic | Low | Low |
| Tor Transport | Low | Low |
| I2P Transport | Moderate | Moderate |

## Troubleshooting

### Privacy Layer Issues

#### Messages Not Being Padded
**Symptoms:** Message sizes remain unchanged
**Solutions:**
- Check `privacy.padding.enabled: true`
- Verify `bucketSizes` array is not empty
- Check logs for "Padding overhead X% exceeds maximum Y%" warnings

#### Excessive Delays
**Symptoms:** UI feels sluggish, messages take too long
**Solutions:**
- Reduce `privacy.timing.jitterMs`
- Disable `privacy.batching.enabled` temporarily
- Check `privacy.coverTraffic.intervalSeconds` is not too low

#### High CPU Usage
**Symptoms:** System becomes slow during high traffic
**Solutions:**
- Reduce `privacy.padding.bucketSizes` (fewer large buckets)
- Disable `privacy.padding.useRandomFill` (use zeros instead)
- Check for excessive cover traffic generation

### Anonymity Layer Issues

#### Tor Connection Failures
**Symptoms:** "Tor SOCKS proxy not available"
**Solutions:**
- Verify Tor is running: `systemctl status tor`
- Check SOCKS address: `telnet 127.0.0.1 9050`
- Verify torrc allows SOCKS connections
- Check Tor bootstrap status: `tail -f /var/log/tor/log`

#### I2P Connection Issues
**Symptoms:** "I2P SAM bridge not responding"
**Solutions:**
- Verify I2P is running: `systemctl status i2p`
- Check SAM bridge: `telnet 127.0.0.1 7656`
- Wait for I2P network integration (30+ minutes first time)
- Check I2P web console: http://127.0.0.1:7657

#### Transport Fallback Not Working
**Symptoms:** Connections fail even with fallbacks configured
**Solutions:**
- Check transport priority order in logs
- Verify fallback transports are available
- Enable debug logging to see selection process
- Test individual transports with "Test Connectivity" button

### Common Configuration Mistakes

#### Oversized Bucket Sizes
```yaml
# PROBLEMATIC - too much overhead
privacy:
  padding:
    bucketSizes: [1024, 10000, 100000]  # 100KB buckets!

# BETTER
privacy:
  padding:
    bucketSizes: [512, 1024, 2048, 4096]  # Reasonable sizes
```

#### Excessive Jitter
```yaml
# PROBLEMATIC - 5 second delays!
privacy:
  timing:
    jitterMs: 5000

# BETTER
privacy:
  timing:
    jitterMs: 500  # 0.5 second max delay
```

#### Too Frequent Cover Traffic
```yaml
# PROBLEMATIC - constant traffic
privacy:
  coverTraffic:
    intervalSeconds: 1  # Every second!

# BETTER
privacy:
  coverTraffic:
    intervalSeconds: 60  # Every minute when idle
```

## Security Best Practices

### Defense in Depth

1. **Enable Everything**: Start with all privacy features enabled
2. **Monitor Performance**: Gradually tune for acceptable latency
3. **Regular Updates**: Keep Tor/I2P and slskdN updated
4. **Secure Configuration**: Use strong passwords, limit access
5. **Network Monitoring**: Watch for unusual connection patterns

### Threat Model Awareness

**Privacy Layer Protects Against:**
- Size-based traffic analysis
- Timing pattern analysis
- Message frequency analysis
- Passive network observers

**Anonymity Layer Protects Against:**
- IP address correlation
- Network-level surveillance
- ISP traffic inspection
- Exit node eavesdropping

**Combined Protection:**
- Multi-layer defense against sophisticated adversaries
- Both passive and active attack mitigation
- Censorship resistance through transport diversity

## Advanced Configuration

### Custom Transport Policies

For per-peer transport preferences:

```yaml
# In mesh configuration
transportPolicies:
  - peerId: "peer-alice"
    podId: "work-pod"
    transportPreferences: ["Tor", "I2P", "Direct"]
    disableClearnet: true
    preferPrivateTransports: true
```

### Performance Tuning

For high-throughput scenarios:

```yaml
privacy:
  # Minimize padding overhead
  padding:
    bucketSizes: [512, 1024, 2048]
    maxOverheadPercent: 50

  # Reduce timing jitter
  timing:
    jitterMs: 100

  # Disable batching for low-latency needs
  batching:
    enabled: false

  # Conservative cover traffic
  coverTraffic:
    intervalSeconds: 300  # 5 minutes
```

### Debug Logging

Enable detailed logging for troubleshooting:

```yaml
logging:
  levels:
    slskd.Common.Security: Debug
    slskd.Mesh.Transport: Debug
    slskd.Mesh.Privacy: Debug
```

## Migration Guide

### Upgrading from No Privacy

1. **Start Simple**: Enable basic padding and timing first
2. **Test Performance**: Ensure acceptable latency
3. **Add Anonymity**: Gradually enable Tor/I2P
4. **Monitor Usage**: Watch for resource consumption
5. **Tune Configuration**: Optimize for your use case

### Switching Transport Modes

1. **Test New Mode**: Use "Test Connectivity" in WebGUI
2. **Gradual Rollout**: Change one peer at a time
3. **Monitor Connections**: Watch for failures
4. **Fallback Configuration**: Ensure good fallback options
5. **User Communication**: Inform users of potential latency changes

## Support and Resources

### Getting Help

1. **Check Logs**: `tail -f /app/logs/slskd.log`
2. **WebGUI Status**: Settings → Security → Adversarial → Overview
3. **Transport Tests**: Use "Test Connectivity" buttons
4. **Debug Mode**: Enable debug logging temporarily

### Community Resources

- **Tor Documentation**: https://community.torproject.org/
- **I2P Documentation**: https://geti2p.net/en/docs
- **Traffic Analysis Papers**: Search for "website fingerprinting" research
- **Privacy Tools**: https://www.privacytools.io/

### Professional Services

For enterprise deployments requiring custom privacy configurations or security audits, consider:

- Security consulting for threat model validation
- Performance optimization for high-throughput scenarios
- Custom transport implementations for specific censorship environments
- Integration with enterprise security monitoring tools

---

This guide covers the complete privacy layer functionality in slskdN. The combination of message-level protections, timing obfuscation, and anonymity transports provides comprehensive defense against traffic analysis attacks while maintaining usable performance.

