# Tier-1 Pod-Scoped Private Service Network (VPN-like Utility) — Agent Implementation Doc

## PROJECT INTENT (FRAMING)

Implement "Tailscale-like utility" without becoming an "internet exit node":
- Pod members can securely reach specific private services hosted behind another pod member's network.
- This is pod-private service access (tunnels to explicit destinations), NOT general web browsing egress.
- Strictly opt-in, designed to avoid adding load to uninvolved peers or the broader network.

**KEY PROPERTIES**
- Only two endpoints carry traffic: Client ↔ Gateway peer (over existing authenticated overlay).
- Gateway then connects to explicit private destinations (LAN services) as configured by the gateway operator.
- No third-party relays; no multi-hop routing; no public advertisement of gateway capability.
- Strongly recommended hard cap: pods <= 3 members for MVP.

## DEFINITIONS

**Peer:**
A node in the mesh with a stable identity (PeerId), validated descriptors, pinned transport, etc.

**Pod:**
A membership + policy container. Pods define authorization and governance.

**Gateway peer (per pod):**
A specific pod member who opts in to serve private service tunnels for that pod.

**Service tunnel:**
A TCP tunnel from client to a destination reachable from the gateway (usually gateway's LAN), carried over the mesh overlay.

**Private service destination:**
A host:port (or IP:port) that the gateway operator explicitly allowlists for the pod.

## MVP SCOPE

**IN:**
- TCP tunnels to explicit allowlisted destinations (host/IP + port).
- Overlay-carried multiplexed streams (or per-tunnel stream) between client and gateway peer.
- Pod-scoped policy: membership gating, destination allowlist, quotas/timeouts.
- Safe defaults: OFF by default; private-range allowed only when gateway opts in.

**OUT (for MVP):**
- No "internet egress" / exit node behavior (do not allow arbitrary public destinations).
- No generic HTTP proxy parsing, no TLS MITM, no URL logging.
- No subnet routing / TUN/TAP IP-level VPN.
- No routing through third-party peers, no relay selection.
- No public discovery of "gateway services" beyond pod-internal configuration.

## SECURITY GOALS & THREAT MODEL

**Primary threats:**
- Unauthorized access: non-pod members trying to use tunnels.
- SSRF / lateral movement: pod member tries to reach gateway's private network beyond allowed services.
- DNS rebinding: allowlisted hostname resolves to forbidden IP later.
- DoS: excessive tunnel creation, long-lived idle connections, oversized frames.
- Identity spoofing: requests from unauthenticated or self-asserted keys.

**Security goals:**
- Only authenticated pod members can open tunnels.
- Gateway only connects to destinations explicitly allowlisted by the gateway operator for that pod.
- Default-deny for all destinations; block private/reserved ranges unless explicitly enabled for that pod.
- Enforce strict quotas/timeouts; minimize metadata logs.

## DATA MODEL CHANGES

### 1) Add Pod Capability
- `PodCapability.PrivateServiceGateway`

### 2) Add Pod Policy fields (stored with pod)
Suggested fields:

```csharp
public class PodPrivateServicePolicy
{
    // Core settings
    public bool Enabled { get; set; } = false;
    public int MaxMembers { get; set; } = 3; // Hard enforce for this capability
    public string GatewayPeerId { get; set; } = ""; // Must be member
    public List<AllowedDestination> AllowedDestinations { get; set; } = new();

    // Security settings
    public bool AllowPrivateRanges { get; set; } = false; // Can only be set by gateway operator

    // Quotas
    public int MaxConcurrentTunnelsPerPeer { get; set; } = 2;
    public int MaxConcurrentTunnelsPod { get; set; } = 5;
    public int MaxNewTunnelsPerMinutePerPeer { get; set; } = 5;
    public long MaxBytesPerDayPerPeer { get; set; } = 0; // 0 = unlimited
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(120);
    public TimeSpan MaxLifetime { get; set; } = TimeSpan.FromMinutes(60);
    public TimeSpan DialTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

public class AllowedDestination
{
    public string HostPattern { get; set; } = ""; // exact hostname, wildcard domain, or literal IP
    public int Port { get; set; }
    public string Protocol { get; set; } = "tcp"; // MVP
    public bool AllowPublic { get; set; } = false; // MVP: interpret as private-only
}
```

**IMPORTANT DEFAULTS (MVP):**
- Require `AllowedDestinations` to be non-empty to enable the capability.
- Disallow public internet destinations unless explicitly enabled in a future "advanced mode."
  For MVP, interpret `AllowedDestinations` as private-only.

### 3) Authorization roles
- Only gateway operator (`GatewayPeerId`) can modify `AllowedDestinations` and `AllowPrivateRanges`.
- Other members can request tunnels but cannot expand policy.

## PROTOCOL / SERVICE DESIGN

Implement a gateway service over your authenticated overlay/service-fabric:

**Service Name:** `"private-gateway"`

**Methods:**

**A) OpenTunnel**
```csharp
public record OpenTunnelRequest(
    string PodId,
    string DestinationHost,
    int DestinationPort,
    string ClientPeerId,
    string TunnelId // client-generated GUID for correlation
);

public record OpenTunnelResponse(
    bool Accepted,
    string Reason, // if rejected
    string SessionId // if accepted - for correlation in data frames
);
```

**B) TunnelData (framing)**
You need a data framing mechanism over the overlay. Two options:

**Option 1 (recommended):** Multiplexed streams
- Reuse QUIC streams or a custom stream-mux over your existing overlay connection.
- Each tunnel maps to a stream id.
- Frames carry stream id + bytes.

**Option 2:** Framed messages
- Send framed binary messages with `{TunnelId, Direction, PayloadChunk}`.
- Enforce max chunk sizes.

MVP should prefer QUIC stream multiplexing if your overlay already supports it cleanly.

**C) CloseTunnel**
```csharp
public record CloseTunnelRequest(
    string TunnelId,
    string Reason // optional
);

public record CloseTunnelResponse(
    long BytesIn,
    long BytesOut,
    TimeSpan Duration
);
```

**NOTE:** Do NOT implement an HTTP proxy; implement only TCP tunnel semantics (CONNECT-like).

## GATEWAY-SIDE VALIDATION LOGIC (MUST)

When receiving `OpenTunnel(PodId, host, port, clientPeerId)`:

### 1) Identity check (hard gate)
- Ensure the request is from an authenticated overlay session bound to `clientPeerId`
  (validated descriptor, pinned transport, control signing keys; no self-asserted keys).

### 2) Pod membership gate
- Load pod.
- Verify `clientPeerId` is member.
- Verify `GatewayPeerId == localPeerId` (this node is the gateway).
- Verify pod capability enabled and `pod member count <= MaxMembers`.

### 3) Destination allowlist gate
- Validate `DestinationHost`:
  - enforce strict charset (no whitespace/control chars)
  - max length (e.g., 255)
- Validate `DestinationPort` range (1..65535)
- Match against `AllowedDestinations`:
  - host match: exact hostname, wildcard suffix match, or IP literal match
  - port match exact

### 4) Private range policy
- If resolved IP is RFC1918/private, require `AllowPrivateRanges == true`.
- Always block by default (even if `AllowPrivateRanges == true`) unless explicitly allowed:
  - localhost (127.0.0.0/8, ::1)
  - link-local (169.254.0.0/16, fe80::/10)
  - multicast/broadcast
  - cloud metadata IP (169.254.169.254)  // always block for MVP

### 5) Quotas / rate limits / timeouts
- Enforce `MaxNewTunnelsPerMinutePerPeer`
- Enforce `MaxConcurrentTunnelsPerPeer` and per-pod global
- Enforce `DialTimeout` for outbound connect
- Enforce `IdleTimeout` and `MaxLifetime`
- Apply backoff for repeated failures per peer.

### 6) Open outbound TCP connection (gateway → destination)
- Use cancellation tokens.
- Enforce socket options and read/write timeouts.
- If connect succeeds, begin bidirectional forwarding between tunnel stream and socket.

## CLIENT-SIDE BEHAVIOR

- User selects a pod and chooses "Connect to service" from a list (derived from pod `AllowedDestinations`, not free-form).
- Client chooses the gateway peer designated by the pod policy.
- Client opens overlay session to gateway peer (existing mechanism).
- Client calls `OpenTunnel`; on success, maps local port forwarding OR returns a stream handle to the UI.

**MVP client UX options:**

**A) Simple local port forward (recommended):**
- Client starts a local listener on `127.0.0.1:<ephemeral>`.
- When local app connects, client opens tunnel and bridges bytes.
- This gives "VPN-like utility" without OS-level routing.

**B) In-app service access:**
- For HTTP services, embed a webview that uses the tunnel.

**MVP:** start with local port forward.

## NETWORK IMPACT GUARANTEE ("No neighbor load")

To guarantee no load on uninvolved peers:
- Do not implement relay routing for this feature.
- Do not publish "gateway availability" in global DHT.
- Only connect client ↔ gateway directly using existing peer connectivity.
- Do not route tunnel traffic through any other mesh nodes.

If a transport fallback uses Tor/I2P, it must still be direct client↔gateway (no third-party mesh relays).

## LOGGING & PRIVACY

**Default logging (minimal):**
- timestamp
- podId
- clientPeerId (or hashed)
- destination host:port (optional; consider hashing host with a salt if you want)
- bytes in/out
- allow/deny + reason codes

**Do NOT log:**
- payload bytes
- HTTP headers/URLs
- credentials

Add an explicit "debug mode" to log more, with warnings.

## FAIL-SAFE DEFAULTS

- Capability disabled by default.
- `AllowedDestinations` empty => capability cannot be enabled.
- `AllowPrivateRanges` default false.
- Block all reserved/local/metadata IPs always.
- If identity validation is not yet complete in your code, disable the gateway feature behind a build flag until signed descriptors + pinning + peer-bound control verification land.

## IMPLEMENTATION TASK LIST (FILE-LEVEL)

### 1) Pod policy model + persistence
- Add capability flags and policy fields to pod models.
- Update pod create/update API to include policy (gateway-only edit permissions).
- Enforce `MaxMembers <= 3` when enabling capability.

### 2) Gateway service implementation
- Add service "private-gateway" in ServiceFabric or equivalent.
- Implement OpenTunnel, data forwarding, CloseTunnel.
- Add strict validation logic per above.

### 3) Client local port forward
- Implement a local listener that maps `localhost:<port>` to tunnel stream.
- Provide a CLI or UI entry to select destination from allowlist.

### 4) Quotas/timeouts/rate limits
- Implement per-peer and per-pod tunnel counters.
- Implement idle timeout and max lifetime.
- Implement dial timeout and backoff.

### 5) Security hardening utilities
- IP range classifier (private/reserved/local/metadata)
- DNS resolution + rebinding defense checks
- Constant-time compares where relevant
- Strict input validation functions

### 6) Tests
- Pod policy enforcement: members > 3 => cannot enable / cannot open tunnel.
- Membership gate: non-member denied.
- Destination allowlist: deny-by-default; exact allow matches succeed.
- Private range: denied unless `AllowPrivateRanges == true`.
- Metadata IP always denied.
- Rate limits: excessive OpenTunnel attempts denied.
- Timeouts: dial timeout triggers; idle timeout closes tunnel.

## ACCEPTANCE CRITERIA

- A pod member can securely access an allowlisted private service through a gateway peer with only client↔gateway traffic in the mesh.
- No third-party peers see or carry tunnel traffic.
- Gateway cannot be used as a general internet proxy in MVP.
- Unauthorized peers cannot open tunnels.
- SSRF protections prevent reaching arbitrary LAN/metadata endpoints.
- Quotas prevent resource exhaustion.
- Minimal logs by default; no content logging.

---

## IMPLEMENTATION ARCHITECTURE

### Core Components

```csharp
// Service interfaces
public interface IPrivateServiceGateway
{
    Task<OpenTunnelResponse> OpenTunnelAsync(OpenTunnelRequest request);
    Task CloseTunnelAsync(CloseTunnelRequest request);
    Task ForwardTunnelDataAsync(string tunnelId, byte[] data, bool fromClient);
}

public interface IPrivateServiceClient
{
    Task<int> CreateLocalTunnelAsync(string podId, string destinationHost, int destinationPort);
    Task CloseLocalTunnelAsync(int localPort);
}

// Data models
public record PodPrivateServicePolicy { /* as above */ }
public record AllowedDestination { /* as above */ }
public record TunnelSession { /* runtime state */ }

// Security utilities
public static class PrivateServiceSecurity
{
    public static bool IsAllowedDestination(string host, int port, PodPrivateServicePolicy policy);
    public static bool IsSafeIpAddress(IPAddress ip, PodPrivateServicePolicy policy);
    public static bool ValidateHostname(string hostname);
}

// Quota management
public interface ITunnelQuotaManager
{
    Task<bool> CanOpenTunnelAsync(string podId, string peerId);
    Task RecordTunnelOpenedAsync(string tunnelId, string podId, string peerId);
    Task RecordTunnelClosedAsync(string tunnelId);
}
```

### File Structure

```
src/slskd/
├── PrivateService/
│   ├── Models/
│   │   ├── PodPrivateServicePolicy.cs
│   │   ├── AllowedDestination.cs
│   │   ├── TunnelSession.cs
│   │   └── OpenTunnelRequest.cs
│   ├── Security/
│   │   ├── PrivateServiceSecurity.cs
│   │   └── IpRangeClassifier.cs
│   ├── Gateway/
│   │   ├── IPrivateServiceGateway.cs
│   │   ├── PrivateServiceGateway.cs
│   │   └── TunnelForwarder.cs
│   ├── Client/
│   │   ├── IPrivateServiceClient.cs
│   │   ├── PrivateServiceClient.cs
│   │   └── LocalPortForwarder.cs
│   ├── Quotas/
│   │   ├── ITunnelQuotaManager.cs
│   │   └── TunnelQuotaManager.cs
│   └── API/
│       └── PrivateServiceController.cs
```

### Integration Points

1. **PodCore**: Extend pod models and validation
2. **Mesh/ServiceFabric**: Add private-gateway service
3. **WebGUI**: Add pod settings and client tunnel management
4. **Overlay**: Reuse existing authenticated transport

### Security Integration

- Leverages existing peer authentication and transport pinning
- Adds pod-scoped authorization on top of mesh-level auth
- Integrates with existing quota and rate limiting systems
- Uses existing logging infrastructure with privacy controls

---

## TESTING STRATEGY

### Unit Tests
- Security validation functions
- IP range classification
- Quota enforcement logic
- Input validation

### Integration Tests
- End-to-end tunnel creation and data flow
- Authentication and authorization
- Quota limit enforcement
- Timeout behavior

### Security Tests
- SSRF attempt vectors
- Unauthorized access attempts
- DNS rebinding scenarios
- DoS attack simulations

### Performance Tests
- Concurrent tunnel handling
- Large data transfers
- Connection lifecycle stress testing

---

## DEPLOYMENT CONSIDERATIONS

### Feature Flags
- `PrivateServiceGateway.Enabled` - Master feature toggle
- `PrivateServiceGateway.AllowPrivateRanges` - Advanced private network access
- `PrivateServiceGateway.DebugLogging` - Enhanced logging for troubleshooting

### Monitoring
- Tunnel creation/closure events
- Bandwidth usage per tunnel
- Error rates and failure patterns
- Security violation attempts

### Upgrades
- Backward compatibility for pods without private service policies
- Graceful handling of existing tunnels during updates
- Migration path for policy schema changes

---

## CONCLUSION

This implementation provides a secure, pod-scoped private service access mechanism that gives users "VPN-like utility" without the risks and complexity of full internet egress. By maintaining strict controls and leveraging existing mesh infrastructure, it delivers meaningful functionality while preserving the system's security and performance characteristics.

