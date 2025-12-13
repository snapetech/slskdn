# Phase 12: Adversarial Resilience & Privacy Hardening

> **Branch**: `experimental/brainz`  
> **Date**: December 10, 2025  
> **Scope**: T-1200..T-1299  
> **Status**: 📋 Planned

---

## 1. Executive Summary

Phase 12 adds **optional, configurable security layers** to protect users in adversarial environments—dissidents, journalists, activists in repressive regimes who need to safely and anonymously share files.

**All features are:**
- ✅ **Optional** — disabled by default, enabled via WebGUI settings
- ✅ **Configurable** — granular control over each layer
- ✅ **Documented** — clear explanations in UI and docs
- ✅ **Non-breaking** — existing users see no changes unless they opt in

---

## 2. Threat Model

### 2.1 Adversary Capabilities

| **Adversary** | **Capabilities** |
|---------------|------------------|
| **ISP/Network Observer** | Deep packet inspection, traffic analysis, IP logging |
| **National Firewall (GFW-style)** | Protocol fingerprinting, active probing, IP blocking |
| **Law Enforcement** | Subpoenas, device seizure, metadata correlation |
| **Sybil Attackers** | Flood DHT with fake peers, correlation attacks |
| **Active Attackers** | MITM, timing attacks, traffic injection |

### 2.2 Protection Goals

| **Goal** | **Description** |
|----------|-----------------|
| **IP Protection** | Hide user's real IP from peers and observers |
| **Traffic Obfuscation** | Make mesh traffic indistinguishable from normal HTTPS |
| **Metadata Protection** | Prevent "who talked to whom when" correlation |
| **Censorship Resistance** | Bypass national firewalls and protocol blocking |
| **Plausible Deniability** | Cannot prove user has specific content/conversations |

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              APPLICATION LAYER                               │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐       │
│   │   PodCore   │  │  MediaCore  │  │  ChatBridge │  │   Scenes    │       │
│   └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘       │
│          │                │                │                │              │
│   ┌──────┴────────────────┴────────────────┴────────────────┴──────┐       │
│   │                    PRIVACY LAYER (NEW - Phase 12)              │       │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │       │
│   │  │   Padding    │  │    Timing    │  │   Batching   │          │       │
│   │  │  (T-1210)    │  │   (T-1211)   │  │   (T-1212)   │          │       │
│   │  └──────────────┘  └──────────────┘  └──────────────┘          │       │
│   └────────────────────────────┬───────────────────────────────────┘       │
│                                │                                           │
│   ┌────────────────────────────┴───────────────────────────────────┐       │
│   │                    ANONYMITY LAYER (NEW - Phase 12)            │       │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │       │
│   │  │    Direct    │  │  Tor Proxy   │  │ Onion Relay  │          │       │
│   │  │  (existing)  │  │  (T-1220)    │  │  (T-1240)    │          │       │
│   │  └──────────────┘  └──────────────┘  └──────────────┘          │       │
│   └────────────────────────────┬───────────────────────────────────┘       │
│                                │                                           │
│   ┌────────────────────────────┴───────────────────────────────────┐       │
│   │                    TRANSPORT LAYER (ENHANCED)                  │       │
│   │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │       │
│   │  │   QUIC   │  │WebSocket │  │  obfs4   │  │  Meek    │        │       │
│   │  │(existing)│  │ (T-1230) │  │ (T-1232) │  │ (T-1233) │        │       │
│   │  └──────────┘  └──────────┘  └──────────┘  └──────────┘        │       │
│   └────────────────────────────┬───────────────────────────────────┘       │
│                                │                                           │
│   ┌────────────────────────────┴───────────────────────────────────┐       │
│   │                    NETWORK LAYER (ENHANCED)                    │       │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │       │
│   │  │    Direct    │  │   Bridges    │  │ Domain Front │          │       │
│   │  │  (existing)  │  │   (T-1250)   │  │   (T-1251)   │          │       │
│   │  └──────────────┘  └──────────────┘  └──────────────┘          │       │
│   └────────────────────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Feature Specifications

### 4.1 Privacy Layer — Traffic Analysis Protection

#### 4.1.1 Message Padding (T-1210)

**Purpose:** Prevent message size fingerprinting.

**Design:**
```csharp
public interface IMessagePadder
{
    byte[] Pad(byte[] payload);
    byte[] Unpad(byte[] padded);
}

public class BucketPadder : IMessagePadder
{
    // Pad all messages to fixed bucket sizes
    // Buckets: 512, 1024, 2048, 4096, 8192, 16384 bytes
    // Padding bytes are random, not zeros (prevent compression attacks)
}
```

**Configuration:**
```yaml
adversarial:
  privacy:
    padding:
      enabled: false  # Default: disabled
      buckets: [512, 1024, 2048, 4096, 8192, 16384]
      random_fill: true  # Use random bytes, not zeros
```

**WebGUI:** Settings → Privacy → Message Padding
- Toggle: Enable padding
- Info tooltip: "Pads all messages to fixed sizes to prevent size-based fingerprinting"

---

#### 4.1.2 Timing Obfuscation (T-1211)

**Purpose:** Prevent timing correlation attacks.

**Design:**
```csharp
public interface ITimingObfuscator
{
    Task DelayAsync(CancellationToken ct);
    Task<T> WithJitterAsync<T>(Func<Task<T>> action, CancellationToken ct);
}

public class RandomJitterObfuscator : ITimingObfuscator
{
    // Add random delay (0-500ms by default) to all sends
    // Configurable min/max jitter
    // Optional constant-rate cover traffic when idle
}
```

**Configuration:**
```yaml
adversarial:
  privacy:
    timing:
      enabled: false
      min_jitter_ms: 0
      max_jitter_ms: 500
      cover_traffic:
        enabled: false  # Send dummy messages when idle
        interval_ms: 30000  # Every 30 seconds
```

**WebGUI:** Settings → Privacy → Timing Protection
- Toggle: Enable timing jitter
- Sliders: Min/max delay (ms)
- Toggle: Cover traffic (advanced)

---

#### 4.1.3 Message Batching (T-1212)

**Purpose:** Aggregate multiple messages to prevent frequency analysis.

**Design:**
```csharp
public interface IMessageBatcher
{
    void Enqueue(byte[] message, string destination);
    Task FlushAsync(CancellationToken ct);
}

public class TimedBatcher : IMessageBatcher
{
    // Hold messages for configurable window (e.g., 2 seconds)
    // Send all accumulated messages in one batch
    // Configurable max batch size and flush interval
}
```

**Configuration:**
```yaml
adversarial:
  privacy:
    batching:
      enabled: false
      flush_interval_ms: 2000
      max_batch_size: 10
```

---

### 4.2 Anonymity Layer — IP Protection

#### 4.2.1 Tor SOCKS5 Proxy (T-1220)

**Purpose:** Route all mesh traffic through Tor for IP anonymization.

**Design:**
```csharp
public interface IAnonymityTransport
{
    Task<Stream> ConnectAsync(string host, int port, CancellationToken ct);
    bool IsAvailable { get; }
    string TransportName { get; }
}

public class TorSocksTransport : IAnonymityTransport
{
    // Connect via local Tor SOCKS5 proxy (default: 127.0.0.1:9050)
    // Support stream isolation via SOCKS5 auth
    // Health check: verify Tor connectivity
}
```

**Configuration:**
```yaml
adversarial:
  anonymity:
    tor:
      enabled: false
      socks_host: "127.0.0.1"
      socks_port: 9050
      stream_isolation: true  # Use different circuits per peer
      require_tor: false  # If true, fail if Tor unavailable
```

**WebGUI:** Settings → Privacy → Tor Integration
- Toggle: Route traffic through Tor
- Input: SOCKS5 address/port
- Toggle: Require Tor (block if unavailable)
- Status indicator: Tor connectivity status

**User Documentation:**
```markdown
## Using Tor with slskdn

Tor provides strong IP anonymization by routing traffic through multiple 
relays. Each relay only knows the previous and next hop, so no single 
point can see both your IP and your destination.

### Setup
1. Install Tor: `apt install tor` (Linux) or download Tor Browser
2. Ensure Tor SOCKS5 proxy is running (default: 127.0.0.1:9050)
3. Enable in slskdn: Settings → Privacy → Tor Integration

### Tradeoffs
- **Latency**: +200-500ms per connection
- **Throughput**: Lower than direct connections
- **Reliability**: Tor exit nodes may be blocked by some peers

### Recommendations
- Enable "Stream Isolation" for better anonymity
- Use bridges if Tor is blocked in your country
```

---

#### 4.2.2 I2P Integration (T-1221)

**Purpose:** Alternative anonymity network optimized for peer-to-peer.

**Design:**
```csharp
public class I2PTransport : IAnonymityTransport
{
    // Connect via I2P SAM bridge
    // Create destinations for mesh identity
    // Better suited for persistent connections than Tor
}
```

**Configuration:**
```yaml
adversarial:
  anonymity:
    i2p:
      enabled: false
      sam_host: "127.0.0.1"
      sam_port: 7656
      tunnel_length: 3  # Hops per direction
```

---

#### 4.2.3 Relay-Only Mode (T-1222)

**Purpose:** Never make direct connections; always go through relays.

**Design:**
```csharp
public class RelayOnlyTransport : IAnonymityTransport
{
    // All connections routed through trusted relay nodes
    // Relay nodes are mesh peers volunteering bandwidth
    // User never reveals IP to destination peer
}
```

**Configuration:**
```yaml
adversarial:
  anonymity:
    relay_only:
      enabled: false
      min_relays: 2  # Minimum hops
      trusted_relays: []  # Optional: prefer specific relays
```

---

### 4.3 Transport Layer — Traffic Obfuscation

#### 4.3.1 WebSocket Transport (T-1230)

**Purpose:** Make mesh traffic look like normal web traffic.

**Design:**
```csharp
public interface IObfuscatedTransport
{
    Task<Stream> ConnectAsync(string peerId, CancellationToken ct);
    string ProtocolName { get; }
}

public class WebSocketTransport : IObfuscatedTransport
{
    // Establish WSS connection to peer/relay
    // Looks like normal HTTPS to observers
    // Can traverse most firewalls
}
```

**Configuration:**
```yaml
adversarial:
  transport:
    websocket:
      enabled: false
      path: "/ws/mesh"  # WebSocket path
      headers:  # Custom headers to blend in
        User-Agent: "Mozilla/5.0 ..."
```

---

#### 4.3.2 HTTP Tunnel Transport (T-1231)

**Purpose:** Tunnel mesh protocol over HTTP POST/GET requests.

**Design:**
```csharp
public class HttpTunnelTransport : IObfuscatedTransport
{
    // Encode mesh messages as HTTP request/response bodies
    // Long-polling or chunked transfer for bidirectional
    // Looks like API traffic
}
```

---

#### 4.3.3 obfs4 Pluggable Transport (T-1232)

**Purpose:** Tor's obfuscation protocol for anti-DPI.

**Design:**
```csharp
public class Obfs4Transport : IObfuscatedTransport
{
    // Uses obfs4proxy binary (Tor Project)
    // Traffic looks like random noise
    // Resists active probing
}
```

**Configuration:**
```yaml
adversarial:
  transport:
    obfs4:
      enabled: false
      binary_path: "/usr/bin/obfs4proxy"
      bridges: []  # obfs4 bridge lines
```

**WebGUI:** Settings → Privacy → Obfuscated Transports
- Dropdown: Transport type (Direct, WebSocket, obfs4, Meek)
- Bridge configuration for obfs4

---

#### 4.3.4 Meek (CDN-based) Transport (T-1233)

**Purpose:** Route through major CDNs to avoid blocking.

**Design:**
```csharp
public class MeekTransport : IObfuscatedTransport
{
    // HTTPS to CDN (Azure, Cloudflare, etc.)
    // CDN forwards to actual relay
    // Blocking requires blocking entire CDN
}
```

**Configuration:**
```yaml
adversarial:
  transport:
    meek:
      enabled: false
      front_domain: "ajax.aspnetcdn.com"  # CDN domain
      relay_url: "https://meek-relay.example.com/"
```

---

### 4.4 Native Onion Routing (Advanced)

#### 4.4.1 Circuit Builder (T-1240)

**Purpose:** Build onion-routed circuits within the mesh network.

**Design:**
```csharp
public interface ICircuitBuilder
{
    Task<ICircuit> BuildCircuitAsync(
        string targetPeerId, 
        int hopCount = 3, 
        CancellationToken ct = default);
}

public interface ICircuit : IAsyncDisposable
{
    string CircuitId { get; }
    IReadOnlyList<string> Hops { get; }
    Task<byte[]> SendAsync(byte[] data, CancellationToken ct);
    IAsyncEnumerable<byte[]> ReceiveAsync(CancellationToken ct);
}

public class MeshCircuitBuilder : ICircuitBuilder
{
    // Select relay nodes from mesh peers with RelayCapability
    // Build circuit: encrypt message in layers (like onion)
    // Each relay unwraps one layer, forwards to next
    // Only exit relay sees plaintext destination
}
```

**Circuit Construction:**
```
User → Relay1 → Relay2 → Relay3 → Target

Encryption layers (innermost to outermost):
  Layer 3 (for Relay3): {target, message}
  Layer 2 (for Relay2): {relay3, encrypted_layer3}
  Layer 1 (for Relay1): {relay2, encrypted_layer2}

Each relay:
  1. Decrypts with its private key
  2. Reads next hop
  3. Forwards encrypted payload
```

**Configuration:**
```yaml
adversarial:
  onion:
    enabled: false
    default_hops: 3
    circuit_lifetime_minutes: 10
    relay_selection:
      prefer_diverse_asn: true  # Different ISPs
      avoid_same_country: true
```

---

#### 4.4.2 Relay Node Service (T-1241)

**Purpose:** Allow mesh peers to volunteer as relay nodes.

**Design:**
```csharp
public interface IRelayService
{
    bool IsRelayEnabled { get; }
    Task HandleRelayRequestAsync(RelayRequest request, CancellationToken ct);
}

public class MeshRelayService : IRelayService
{
    // Process forwarding requests
    // Rate limiting per source
    // Bandwidth accounting
    // No logging of forwarded content
}
```

**Configuration:**
```yaml
adversarial:
  relay:
    enabled: false  # Volunteer as relay
    max_bandwidth_mbps: 10
    max_circuits: 100
    allow_exit: false  # Be exit relay (more risk)
```

**WebGUI:** Settings → Privacy → Relay Node
- Toggle: Enable relay node
- Slider: Max bandwidth
- Warning: Legal implications of relay operation

---

#### 4.4.3 Circuit Selection & Path Diversity (T-1242)

**Purpose:** Intelligent relay selection for security.

**Design:**
```csharp
public interface IRelaySelector
{
    Task<IReadOnlyList<string>> SelectRelaysAsync(
        int count,
        RelaySelectionCriteria criteria,
        CancellationToken ct);
}

public class DiverseRelaySelector : IRelaySelector
{
    // Prefer relays in different:
    //   - Autonomous Systems (ASNs)
    //   - Countries/jurisdictions
    //   - Network segments
    // Avoid relays controlled by same entity
    // Use reputation scores
}
```

---

### 4.5 Censorship Resistance

#### 4.5.1 Bridge Nodes (T-1250)

**Purpose:** Unlisted entry points for users behind firewalls.

**Design:**
```csharp
public interface IBridgeDiscovery
{
    Task<IReadOnlyList<BridgeInfo>> GetBridgesAsync(CancellationToken ct);
}

public class BridgeInfo
{
    public string Address { get; init; }
    public int Port { get; init; }
    public string Transport { get; init; }  // "direct", "obfs4", "meek"
    public string Fingerprint { get; init; }  // For verification
    public Dictionary<string, string> TransportParams { get; init; }
}
```

**Bridge Distribution:**
- Out-of-band sharing (email, QR codes, word of mouth)
- BridgeDB-style web distribution with CAPTCHA
- Steganographic embedding in images (advanced)

**Configuration:**
```yaml
adversarial:
  bridges:
    enabled: false
    sources:
      - type: "static"
        bridges:
          - "obfs4 192.0.2.1:443 fingerprint=abc123 cert=xyz..."
      - type: "email"
        address: "bridges@slskdn.org"
      - type: "web"
        url: "https://bridges.slskdn.org/get"
```

**WebGUI:** Settings → Privacy → Bridges
- Textarea: Paste bridge lines
- Button: Request bridges via email
- Info: How to get bridges

---

#### 4.5.2 Domain Fronting (T-1251)

**Purpose:** Disguise mesh traffic as requests to major services.

**Design:**
```csharp
public class DomainFrontedTransport : IObfuscatedTransport
{
    // TLS SNI: ajax.aspnetcdn.com (or other CDN)
    // HTTP Host header: mesh-relay.example.com
    // CDN routes based on Host header
    // Observer sees traffic to CDN, not actual destination
}
```

**Configuration:**
```yaml
adversarial:
  transport:
    domain_fronting:
      enabled: false
      front_domain: "ajax.aspnetcdn.com"
      host_header: "mesh.slskdn.org"
```

**Note:** Domain fronting effectiveness varies as CDN providers may block it.

---

#### 4.5.3 Steganographic Bridge Distribution (T-1252)

**Purpose:** Hide bridge information in innocuous content.

**Design:**
```csharp
public interface ISteganographyCodec
{
    byte[] Encode(byte[] data, byte[] carrier);
    byte[] Decode(byte[] carrier);
}

public class ImageSteganography : ISteganographyCodec
{
    // Hide bridge info in image LSBs
    // Looks like normal image
    // Can be shared on social media
}
```

---

### 4.6 Plausible Deniability

#### 4.6.1 Deniable Storage (T-1260)

**Purpose:** Hidden volumes that cannot be proven to exist.

**Design:**
```csharp
public interface IDeniableStorage
{
    Task<bool> VolumeExistsAsync(string passphrase);
    Task<IKeyValueStore> OpenVolumeAsync(string passphrase, CancellationToken ct);
    Task CreateVolumeAsync(string passphrase, long sizeBytes, CancellationToken ct);
}

public class DeniableVolumeStorage : IDeniableStorage
{
    // Multiple passphrases reveal different volumes
    // Outer volume: innocent content
    // Hidden volume: sensitive pods/chats
    // Cryptographically impossible to prove hidden volume exists
}
```

**Configuration:**
```yaml
adversarial:
  deniability:
    storage:
      enabled: false
      # Configured per-volume via secure setup wizard
```

**WebGUI:** Settings → Privacy → Deniable Storage
- Wizard: Create outer/hidden volumes
- Warning: Backup passphrases securely

---

#### 4.6.2 Decoy Pods (T-1261)

**Purpose:** Maintain innocent-looking pods alongside sensitive ones.

**Design:**
```csharp
public class DecoyPodService
{
    // Auto-generate/join harmless music pods
    // Maintain realistic activity patterns
    // Sensitive pods only visible with correct passphrase
}
```

---

### 4.7 WebGUI Configuration Interface

#### 4.7.1 Privacy Settings Panel (T-1270)

**Design:**
```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Settings → Privacy & Security                                               │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ Security Level: ○ Standard  ○ Enhanced  ● Maximum                       ││
│  │                                                                         ││
│  │ Standard: Direct connections, no padding (fastest)                      ││
│  │ Enhanced: Tor routing, message padding (recommended for privacy)        ││
│  │ Maximum: Full onion routing, timing protection, bridges (slowest)       ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│  ▼ Traffic Analysis Protection                                               │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ [✓] Message Padding           [ Padding to fixed sizes prevents       ] ││
│  │     Bucket sizes: 512, 1024, 2048, 4096, 8192 bytes                    ││
│  │                               [ observers from inferring content      ] ││
│  │ [✓] Timing Jitter             [ from message sizes.                   ] ││
│  │     Delay: 0-500ms                                                      ││
│  │                                                                         ││
│  │ [ ] Cover Traffic (Advanced)                                            ││
│  │     Send dummy messages when idle                                       ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│  ▼ IP Anonymization                                                          │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ Transport: [Tor SOCKS5        ▼]                                        ││
│  │                                                                         ││
│  │ Tor Settings:                                                           ││
│  │   SOCKS Address: [127.0.0.1   ] Port: [9050    ]                       ││
│  │   [✓] Stream Isolation (different circuit per peer)                     ││
│  │   [ ] Require Tor (block if unavailable)                                ││
│  │                                                                         ││
│  │   Status: ● Connected (circuit established)                             ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│  ▼ Obfuscated Transports                                                     │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ Primary Transport: [WebSocket (looks like web traffic)  ▼]              ││
│  │                                                                         ││
│  │ [ ] obfs4 (Tor-style obfuscation)                                       ││
│  │     Bridges:                                                            ││
│  │     ┌──────────────────────────────────────────────────────────────┐   ││
│  │     │ obfs4 192.0.2.1:443 cert=... iat-mode=0                     │   ││
│  │     │                                                              │   ││
│  │     └──────────────────────────────────────────────────────────────┘   ││
│  │     [Request Bridges via Email]  [Scan QR Code]                         ││
│  │                                                                         ││
│  │ [ ] Domain Fronting (Advanced)                                          ││
│  │     Front domain: [ajax.aspnetcdn.com]                                  ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│  ▼ Relay Node (Help Others)                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ [ ] Enable Relay Node                                                   ││
│  │     Volunteer bandwidth to help users in censored regions               ││
│  │                                                                         ││
│  │     Max bandwidth: [10     ] Mbps                                       ││
│  │     Max circuits:  [100    ]                                            ││
│  │                                                                         ││
│  │     ⚠ Running a relay may have legal implications in your jurisdiction ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│                                                    [Save]  [Reset to Default]│
└──────────────────────────────────────────────────────────────────────────────┘
```

---

#### 4.7.2 Privacy Dashboard (T-1271)

**Design:**
```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Dashboard → Privacy Status                                                  │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Current Protection Level: [████████████████░░░░] 80% (Enhanced)             │
│                                                                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │  IP Hidden  │  │  Traffic    │  │  Timing     │  │  Censorship │         │
│  │     ✓       │  │  Padded ✓   │  │  Jittered ✓ │  │  Resistant  │         │
│  │  via Tor    │  │  512-8K     │  │  0-500ms    │  │     ✗       │         │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘         │
│                                                                              │
│  Recent Activity:                                                            │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ 14:23:01  Circuit established via 3 relays (de→nl→us)                  ││
│  │ 14:22:58  Tor connection established                                    ││
│  │ 14:22:55  Privacy layer initialized                                     ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│                                                                              │
│  Recommendations:                                                            │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ ⚠ Consider enabling obfs4 bridges if Tor is unreliable in your region  ││
│  │ ⚠ Cover traffic is disabled - timing attacks may be possible           ││
│  └─────────────────────────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Configuration Schema

### 5.1 Full Configuration Reference

```yaml
# slskdn adversarial resilience configuration
# All features are OPTIONAL and DISABLED by default

adversarial:
  # Master enable switch
  enabled: false
  
  # Quick presets (overrides individual settings)
  # Options: "standard", "enhanced", "maximum", "custom"
  preset: "standard"
  
  # Privacy layer (traffic analysis protection)
  privacy:
    padding:
      enabled: false
      buckets: [512, 1024, 2048, 4096, 8192, 16384]
      random_fill: true
    timing:
      enabled: false
      min_jitter_ms: 0
      max_jitter_ms: 500
      cover_traffic:
        enabled: false
        interval_ms: 30000
    batching:
      enabled: false
      flush_interval_ms: 2000
      max_batch_size: 10
  
  # Anonymity layer (IP protection)
  anonymity:
    mode: "direct"  # "direct", "tor", "i2p", "relay_only"
    tor:
      enabled: false
      socks_host: "127.0.0.1"
      socks_port: 9050
      stream_isolation: true
      require_tor: false
    i2p:
      enabled: false
      sam_host: "127.0.0.1"
      sam_port: 7656
      tunnel_length: 3
    relay_only:
      enabled: false
      min_relays: 2
      trusted_relays: []
  
  # Obfuscated transports
  transport:
    primary: "quic"  # "quic", "websocket", "http_tunnel", "obfs4", "meek"
    websocket:
      enabled: false
      path: "/ws/mesh"
    obfs4:
      enabled: false
      binary_path: "/usr/bin/obfs4proxy"
      bridges: []
    meek:
      enabled: false
      front_domain: ""
      relay_url: ""
    domain_fronting:
      enabled: false
      front_domain: ""
      host_header: ""
  
  # Native onion routing
  onion:
    enabled: false
    default_hops: 3
    circuit_lifetime_minutes: 10
    relay_selection:
      prefer_diverse_asn: true
      avoid_same_country: true
  
  # Relay node (volunteer)
  relay:
    enabled: false
    max_bandwidth_mbps: 10
    max_circuits: 100
    allow_exit: false
  
  # Bridges (censorship circumvention)
  bridges:
    enabled: false
    sources: []
  
  # Deniable storage
  deniability:
    storage:
      enabled: false
```

### 5.2 Options Class

```csharp
public class AdversarialOptions
{
    public bool Enabled { get; set; } = false;
    public string Preset { get; set; } = "standard";
    public PrivacyOptions Privacy { get; set; } = new();
    public AnonymityOptions Anonymity { get; set; } = new();
    public TransportOptions Transport { get; set; } = new();
    public OnionOptions Onion { get; set; } = new();
    public RelayOptions Relay { get; set; } = new();
    public BridgeOptions Bridges { get; set; } = new();
    public DeniabilityOptions Deniability { get; set; } = new();
}

public class PrivacyOptions
{
    public PaddingOptions Padding { get; set; } = new();
    public TimingOptions Timing { get; set; } = new();
    public BatchingOptions Batching { get; set; } = new();
}

// ... (full class definitions in implementation)
```

---

## 6. Implementation Phases

### Phase 12A: Privacy Layer (T-1200..T-1219)
**Timeline:** 2 weeks  
**Dependencies:** None

| Task | Description | Priority | Effort |
|------|-------------|----------|--------|
| T-1200 | Define AdversarialOptions configuration model | P1 | S |
| T-1201 | Implement IPrivacyLayer interface | P1 | S |
| T-1202 | Add adversarial section to WebGUI settings | P1 | M |
| T-1210 | Implement BucketPadder (message padding) | P1 | M |
| T-1211 | Implement RandomJitterObfuscator (timing) | P1 | M |
| T-1212 | Implement TimedBatcher (message batching) | P2 | M |
| T-1213 | Implement CoverTrafficGenerator | P3 | M |
| T-1214 | Integrate privacy layer with overlay messaging | P1 | M |
| T-1215 | Add privacy layer unit tests | P1 | M |
| T-1216 | Add privacy layer integration tests | P2 | M |
| T-1217 | Write privacy layer user documentation | P2 | S |

### Phase 12B: Anonymity Layer (T-1220..T-1229)
**Timeline:** 3 weeks  
**Dependencies:** Phase 12A

| Task | Description | Priority | Effort |
|------|-------------|----------|--------|
| T-1220 | Implement TorSocksTransport | P1 | M |
| T-1221 | Implement I2PTransport | P3 | L |
| T-1222 | Implement RelayOnlyTransport | P2 | M |
| T-1223 | Add Tor connectivity status to WebGUI | P1 | S |
| T-1224 | Implement stream isolation | P2 | M |
| T-1225 | Add anonymity transport selection logic | P1 | M |
| T-1226 | Integrate with MeshTransportService | P1 | M |
| T-1227 | Add Tor integration tests | P1 | M |
| T-1228 | Write Tor setup documentation | P1 | M |
| T-1229 | Add I2P setup documentation | P3 | S |

### Phase 12C: Obfuscated Transports (T-1230..T-1239)
**Timeline:** 3 weeks  
**Dependencies:** Phase 12B

| Task | Description | Priority | Effort |
|------|-------------|----------|--------|
| T-1230 | Implement WebSocketTransport | P1 | M |
| T-1231 | Implement HttpTunnelTransport | P2 | M |
| T-1232 | Implement Obfs4Transport | P2 | L |
| T-1233 | Implement MeekTransport | P3 | L |
| T-1234 | Add transport selection WebGUI | P1 | M |
| T-1235 | Implement transport fallback logic | P1 | M |
| T-1236 | Add obfuscated transport tests | P1 | M |
| T-1237 | Write obfuscation user documentation | P2 | M |
| T-1238 | Add transport performance benchmarks | P3 | M |

### Phase 12D: Native Onion Routing (T-1240..T-1249)
**Timeline:** 4 weeks  
**Dependencies:** Phase 12C

| Task | Description | Priority | Effort |
|------|-------------|----------|--------|
| T-1240 | Implement MeshCircuitBuilder | P2 | XL |
| T-1241 | Implement MeshRelayService | P2 | L |
| T-1242 | Implement DiverseRelaySelector | P2 | M |
| T-1243 | Add relay node WebGUI controls | P2 | M |
| T-1244 | Implement circuit keepalive and rotation | P2 | M |
| T-1245 | Add relay bandwidth accounting | P2 | M |
| T-1246 | Add onion routing unit tests | P2 | L |
| T-1247 | Add onion routing integration tests | P2 | L |
| T-1248 | Write relay operator documentation | P2 | M |
| T-1249 | Add circuit visualization to WebGUI | P3 | M |

### Phase 12E: Censorship Resistance (T-1250..T-1259)
**Timeline:** 2 weeks  
**Dependencies:** Phase 12C

| Task | Description | Priority | Effort |
|------|-------------|----------|--------|
| T-1250 | Implement BridgeDiscovery service | P1 | M |
| T-1251 | Implement DomainFrontedTransport | P2 | M |
| T-1252 | Implement ImageSteganography (bridge distribution) | P3 | L |
| T-1253 | Add bridge configuration WebGUI | P1 | M |
| T-1254 | Implement bridge health checking | P2 | S |
| T-1255 | Add bridge fallback logic | P2 | M |
| T-1256 | Write bridge setup documentation | P1 | M |
| T-1257 | Add censorship resistance tests | P2 | M |

### Phase 12F: Plausible Deniability (T-1260..T-1269)
**Timeline:** 3 weeks  
**Dependencies:** Phase 12E

| Task | Description | Priority | Effort |
|------|-------------|----------|--------|
| T-1260 | Implement DeniableVolumeStorage | P3 | XL |
| T-1261 | Implement DecoyPodService | P3 | M |
| T-1262 | Add deniable storage setup wizard | P3 | L |
| T-1263 | Implement volume passphrase handling | P3 | M |
| T-1264 | Add deniability unit tests | P3 | M |
| T-1265 | Write deniability user documentation | P3 | M |

### Phase 12G: WebGUI & Integration (T-1270..T-1289)
**Timeline:** 2 weeks  
**Dependencies:** All previous

| Task | Description | Priority | Effort |
|------|-------------|----------|--------|
| T-1270 | Implement Privacy Settings panel | P1 | L |
| T-1271 | Implement Privacy Dashboard | P2 | M |
| T-1272 | Add security preset selector | P1 | M |
| T-1273 | Implement real-time status indicators | P2 | M |
| T-1274 | Add privacy recommendations engine | P3 | M |
| T-1275 | Integrate all layers with existing systems | P1 | L |
| T-1276 | Add end-to-end privacy tests | P1 | L |
| T-1277 | Write comprehensive user guide | P1 | L |
| T-1278 | Create threat model documentation | P2 | M |
| T-1279 | Add privacy audit logging (opt-in) | P3 | M |

### Phase 12H: Testing & Documentation (T-1290..T-1299)
**Timeline:** 2 weeks  
**Dependencies:** All previous

| Task | Description | Priority | Effort |
|------|-------------|----------|--------|
| T-1290 | Create adversarial test scenarios | P1 | L |
| T-1291 | Implement traffic analysis resistance tests | P2 | M |
| T-1292 | Add censorship simulation tests | P2 | M |
| T-1293 | Performance benchmarking suite | P2 | M |
| T-1294 | Security review and audit | P1 | L |
| T-1295 | Write operator guide (relay/bridge) | P2 | M |
| T-1296 | Create video tutorials | P3 | L |
| T-1297 | Add localization for privacy UI | P3 | M |
| T-1298 | Final integration testing | P1 | L |
| T-1299 | Phase 12 release notes | P1 | S |

---

## 7. Risks & Mitigations

| **Risk** | **Impact** | **Mitigation** |
|----------|------------|----------------|
| Tor blocked in target region | Users can't connect | Bridges, obfs4, meek fallback |
| Performance degradation | User experience | Presets with clear tradeoffs, benchmarks |
| Legal issues for relay operators | Operator liability | Clear documentation, exit policy controls |
| Deniable storage complexity | Data loss, UX issues | Wizard-based setup, strong warnings |
| Traffic analysis still possible | Privacy leak | Multiple layers, cover traffic |
| Domain fronting blocked by CDN | Censorship circumvention fails | Multiple front domains, fallback |

---

## 8. Success Criteria

1. **Usability:** Users can enable privacy features with 3 clicks (preset selection)
2. **Security:** Traffic from "Maximum" preset indistinguishable from HTTPS noise
3. **Performance:** "Enhanced" preset adds <500ms latency to connections
4. **Reliability:** All transports have >95% connection success rate
5. **Documentation:** Complete user guides for each protection level

---

## 8. Signal System for Multi-Channel Communication

### 8.1 Overview

The signal system enables **reliable, multi-channel control signaling** between slskdn peers, supporting both Mesh and BitTorrent extension channels with automatic deduplication and fallback.

**Key Features:**
- Multi-channel delivery (Mesh primary, BT extension secondary)
- Automatic deduplication via `SignalId`
- Channel fallback when primary channel fails
- Optional acknowledgment pattern for critical signals
- Extensible type system for domain-specific signals

**Use Cases:**
- Swarm control signals (e.g., `Swarm.RequestBtFallback`)
- Pod membership updates (`Pod.MembershipUpdate`)
- Variant opinion updates (`Pod.VariantOpinionUpdate`)
- Job cancellation (`Swarm.JobCancel`)

### 8.2 Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │SwarmCore │  │ PodCore  │  │MediaCore │  │Security  │    │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘    │
│       │             │             │             │            │
│       └─────────────┴─────────────┴─────────────┘            │
│                          │                                   │
│                    ┌─────▼─────┐                            │
│                    │ SignalBus │                            │
│                    │  (T-1280)  │                            │
│                    └─────┬─────┘                            │
│                          │                                   │
│       ┌───────────────────┴───────────────────┐             │
│       │                                         │             │
│  ┌────▼──────────┐                    ┌───────▼──────┐      │
│  │ Mesh Channel  │                    │ BT Extension │      │
│  │ Handler       │                    │ Handler      │      │
│  │ (T-1281)      │                    │ (T-1282)     │      │
│  └───────────────┘                    └──────────────┘      │
│       │                                         │             │
│       └───────────────────┬───────────────────┘             │
│                           │                                   │
│                    ┌──────▼──────┐                          │
│                    │   MeshCore  │                          │
│                    │ BitTorrent  │                          │
│                    └─────────────┘                          │
└─────────────────────────────────────────────────────────────┘
```

### 8.3 Signal Model

**Core Signal Type:**

```csharp
sealed class Signal
{
    public string SignalId { get; }          // ULID/UUID for deduplication
    public string FromPeerId { get; }        // slskdn Mesh PeerId
    public string ToPeerId { get; }          // Target PeerId
    public DateTimeOffset SentAt { get; }
    public string Type { get; }              // e.g. "Swarm.RequestBtFallback"
    public IReadOnlyDictionary<string, object> Body { get; }
    public TimeSpan Ttl { get; }
    public IReadOnlyList<SignalChannel> PreferredChannels { get; }
}

enum SignalChannel
{
    Mesh,           // Primary control plane
    BtExtension,    // Secondary via BT extension protocol
    Direct          // Direct peer-to-peer (future)
}
```

**Example Signal Types:**

| Type | Purpose | Ack Required | Channels |
|------|---------|--------------|----------|
| `Swarm.RequestBtFallback` | Request BT fallback for failed transfer | Yes | Mesh, BtExtension |
| `Swarm.RequestBtFallbackAck` | Acknowledge BT fallback request | No | Mesh, BtExtension |
| `Swarm.JobCancel` | Cancel a swarm job | Yes | Mesh |
| `Pod.MembershipUpdate` | Update pod membership | No | Mesh |
| `Pod.VariantOpinionUpdate` | Update variant preference | No | Mesh |

### 8.4 Implementation Tasks

#### 8.4.1 SignalBus Core (T-1280)

**Purpose:** Central signal routing and deduplication service.

**Tasks:**
- Define `ISignalBus` interface with `SendAsync` and `SubscribeAsync`
- Implement `SignalBus` with LRU cache for `SignalId` deduplication
- Add signal type registry for validation
- Implement timeout handling for pending signals
- Add metrics/logging for signal delivery success/failure

**Dependencies:** None (foundational)

**Estimated Effort:** M (Medium)

#### 8.4.2 Mesh Signal Channel Handler (T-1281)

**Purpose:** Deliver signals over Mesh overlay network.

**Tasks:**
- Implement `MeshSignalChannelHandler : ISignalChannelHandler`
- Wrap `Signal` into `slskdnSignal` Mesh message envelope
- Route via `MeshCore` to target `PeerId`
- Handle inbound `slskdnSignal` messages and forward to `SignalBus`
- Implement `CanSendTo(PeerId)` check for Mesh availability

**Dependencies:** T-1280 (SignalBus), MeshCore (Phase 8)

**Estimated Effort:** M (Medium)

#### 8.4.3 BT Extension Signal Channel Handler (T-1282)

**Purpose:** Deliver signals over BitTorrent extension protocol.

**Tasks:**
- Implement `BtExtensionSignalChannelHandler : ISignalChannelHandler`
- Serialize `Signal` to CBOR/JSON
- Wrap into `slskdnExtensionMessage` with `Kind = SignalEnvelope`
- Send via BT extension message ID `"slskdn"`
- Subscribe to inbound BT extension messages and deserialize to `Signal`
- Implement `CanSendTo(PeerId)` check for active BT session

**Dependencies:** T-1280 (SignalBus), BitTorrentBackend (Phase 8)

**Estimated Effort:** M (Medium)

#### 8.4.4 Swarm.RequestBtFallback Signal (T-1283)

**Purpose:** Implement canonical BT fallback request signal.

**Tasks:**
- Define `Swarm.RequestBtFallback` signal type and body schema
- Implement sender in `SwarmCore` (trigger on repeated transfer failures)
- Implement receiver handler in `SwarmCore` (validate, check security, prepare torrent)
- Define and implement `Swarm.RequestBtFallbackAck` acknowledgment
- Add sender-side ack handling and timeout logic
- Add WebGUI toggle for BT fallback feature

**Dependencies:** T-1280, T-1281, T-1282, SwarmCore, SecurityCore

**Estimated Effort:** L (Large)

**Reference:** See `docs/design/signal-request-bt-fallback.md` for complete specification.

#### 8.4.5 Additional Signal Types (T-1284)

**Purpose:** Implement additional signal types for Pod and Swarm control.

**Tasks:**
- `Swarm.JobCancel` - Cancel swarm job with ack
- `Pod.MembershipUpdate` - Broadcast pod membership changes
- `Pod.VariantOpinionUpdate` - Share variant preferences
- `Swarm.TransferProgress` - Optional progress updates (low priority)

**Dependencies:** T-1280, T-1281, PodCore (Phase 10)

**Estimated Effort:** L (Large)

#### 8.4.6 Signal System Testing (T-1285)

**Purpose:** Comprehensive test coverage for signal system.

**Tasks:**
- Unit tests for `SignalBus` deduplication and routing
- Unit tests for channel handlers (Mesh and BT)
- Integration tests for `Swarm.RequestBtFallback` end-to-end
- Test channel fallback behavior (Mesh fails, BT succeeds)
- Test ack timeout and retry logic
- Performance tests for high signal volume

**Dependencies:** T-1280, T-1281, T-1282, T-1283

**Estimated Effort:** M (Medium)

### 8.5 Configuration

**WebGUI Settings:**

```yaml
SignalSystem:
  Enabled: true                    # Enable signal system
  DeduplicationCacheSize: 10000   # LRU cache size for SignalIds
  DefaultTtl: "00:05:00"          # Default signal TTL
  MeshChannel:
    Enabled: true
    Priority: 1                    # Primary channel
  BtExtensionChannel:
    Enabled: true
    Priority: 2                    # Secondary channel
    RequireActiveSession: true     # Only use if BT session exists
```

**Security Considerations:**

- Signals are **not encrypted** by default (rely on Mesh/BT encryption)
- For adversarial environments, enable Mesh encryption (Phase 12)
- Signal `Body` should not contain sensitive data (use references/IDs)
- `SignalId` should be cryptographically random (ULID recommended)

---

## 9. References

- [Tor Project](https://www.torproject.org/)
- [I2P](https://geti2p.net/)
- [obfs4](https://gitlab.torproject.org/tpo/anti-censorship/pluggable-transports/obfs4)
- [Meek](https://trac.torproject.org/projects/tor/wiki/doc/meek)
- [Domain Fronting](https://www.bamsoftware.com/papers/fronting/)
- [Traffic Analysis](https://www.freehaven.net/anonbib/topic.html#Traffic_20analysis)

---

*Document Version: 1.0*  
*Last Updated: December 10, 2025*  
*Author: slskdn development team*















