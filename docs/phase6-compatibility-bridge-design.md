# Phase 6X: Legacy Client Compatibility Bridge - Detailed Design

> **Purpose**: Let legacy Soulseek clients (Nicotine+, SoulseekQt, etc.) access Virtual Soulfind mesh benefits  
> **Tasks**: T-850 to T-860 (11 tasks)  
> **Branch**: `experimental/virtual-soulfind`  
> **Dependencies**: Phase 6 (Virtual Soulfind Mesh)

---

## Overview: The Power of Backward Compatibility

**The Idea**: Run a **local Soulfind instance** that acts as a Soulseek server for legacy clients, but **proxies all operations** into the Virtual Soulfind mesh (DHT + overlay + shadow index).

**Why This Is Brilliant**:
- Your friends can use **any Soulseek client** (Nicotine+, SoulseekQt, Seeker mobile)
- They **automatically benefit** from your mesh intelligence (MBID-aware search, canonical variants, rescue mode, disaster resilience)
- You **extend the mesh's reach** to the entire Soulseek ecosystem
- No forced migration - users choose what works for them

**Network Effect**:
```
Traditional:
  Legacy Client → Official Server → Legacy Network

With Bridge:
  Legacy Client → Local Soulfind → slskdn → Virtual Soulfind Mesh
                                     ↓
                                  DHT + Overlay (MBID-aware, canonical, disaster-ready)
```

---

## The Ethos Match

This fits **perfectly** with slskdn's philosophy:

1. **"Soulseek is origin, not center"** ✓
   - Legacy clients still work through familiar interface
   - But backend is mesh-powered, not server-dependent

2. **"No forced migration"** ✓
   - Users can keep using Nicotine+, SoulseekQt, etc.
   - They don't need to learn slskdn's UI

3. **"Augment, don't replace"** ✓
   - Legacy protocol preserved at the surface
   - Enhanced capabilities underneath

4. **"Community-first"** ✓
   - Helps your friends/LAN/community access the mesh
   - Gradually demonstrates mesh benefits to skeptics

---

## Use Cases

### Use Case 1: LAN Party / Friends

**Scenario**: You run slskdn with the bridge. Your friends visit with laptops.

**Flow**:
```
Friend's Nicotine+ → Your slskdn bridge (192.168.1.10:2242)
                     ↓
                  Your mesh connection (DHT + overlay)
                     ↓
                  Global Virtual Soulfind mesh
```

**Benefits**:
- Friends get **instant access** to your mesh (no slskdn install needed)
- They benefit from **canonical variants**, **rescue mode**, **shadow index**
- Searches are **MBID-enhanced** transparently
- Downloads use **multi-swarm** behind the scenes

---

### Use Case 2: Private Communities / Pods

**Scenario**: A music collective runs slskdn nodes + a shared bridge.

**Setup**:
- 5 core members run full slskdn (mesh participants)
- 20 casual members use legacy clients → shared bridge
- Bridge runs on a trusted member's machine or VPS

**Benefits**:
- Casual members don't need technical setup
- Everyone benefits from **scene-scoped searches** (label crates, pod preferences)
- **Canonical scoring** ensures high-quality shares
- **Disaster mode** means the pod survives if official server dies

---

### Use Case 3: Mobile Users

**Scenario**: Users want to use Seeker (Android) or other mobile clients.

**Flow**:
```
Seeker app → Home slskdn bridge (via VPN or tailscale)
             ↓
          Virtual Soulfind mesh
```

**Benefits**:
- Mobile client works without modification
- Gets mesh intelligence (fast searches, better quality)
- Transparent rescue mode for slow mobile connections

---

## Architecture

### Deployment Model

```
┌─────────────────────────────────────────────────────────┐
│  Your Machine / LAN                                      │
│                                                          │
│  ┌─────────────┐         ┌──────────────────────────┐   │
│  │   slskdn    │◄────────┤  Local Soulfind Instance │   │
│  │             │         │  (Bridge Mode)           │   │
│  │  - DHT Node │         │  - Listens on :2242      │   │
│  │  - Overlay  │         │  - Acts as server        │   │
│  │  - Mesh     │         │  - Proxies to slskdn     │   │
│  └─────────────┘         └──────────────────────────┘   │
│        ▲                            ▲                    │
│        │                            │                    │
│        │                      ┌─────┴──────┐            │
│        │                      │  Legacy    │            │
│        │                      │  Clients   │            │
│        │                      │  (Nicotine+│            │
│        │                      │   SoulseekQt│           │
│        │                      │   Seeker)  │            │
│        │                      └────────────┘            │
│        │                                                 │
│        └────► Virtual Soulfind Mesh (Global DHT)        │
└─────────────────────────────────────────────────────────┘
```

---

## Implementation Design

### Component 1: slskdn ↔ Soulfind Bridge Service

```csharp
namespace slskd.VirtualSoulfind.Bridge
{
    /// <summary>
    /// Manages local Soulfind instance and bridges legacy clients to mesh.
    /// </summary>
    public interface ISoulfindBridgeService
    {
        Task StartAsync(CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);
        bool IsRunning { get; }
        SoulfindBridgeStatus Status { get; }
    }
    
    public class SoulfindBridgeService : ISoulfindBridgeService
    {
        private readonly ILogger<SoulfindBridgeService> log;
        private readonly IOptionsMonitor<Options> options;
        private Process soulfindProcess;
        
        public async Task StartAsync(CancellationToken ct)
        {
            var config = options.CurrentValue.Bridge;
            
            if (!config.Enabled)
            {
                log.Information("[BRIDGE] Soulfind bridge disabled in config");
                return;
            }
            
            log.Information("[BRIDGE] Starting local Soulfind instance on port {Port}",
                config.Port);
            
            // Start Soulfind as child process or Docker container
            if (config.UseDocker)
            {
                await StartSoulfindContainerAsync(ct);
            }
            else
            {
                await StartSoulfindProcessAsync(ct);
            }
            
            // Wait for Soulfind to be ready
            await WaitForSoulfindReadyAsync(ct);
            
            // Start message interceptor
            await StartMessageInterceptorAsync(ct);
            
            log.Information("[BRIDGE] Soulfind bridge active and accepting legacy clients");
        }
        
        private async Task StartSoulfindContainerAsync(CancellationToken ct)
        {
            var config = options.CurrentValue.Bridge;
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run -d --name slskdn-bridge " +
                           $"-p {config.Port}:2242 " +
                           $"-e SOULFIND_PROXY_MODE=true " +
                           $"-e SOULFIND_PROXY_TARGET=http://localhost:5030 " +
                           $"soulfind/soulfind:latest",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using var process = Process.Start(startInfo);
            await process.WaitForExitAsync(ct);
            
            if (process.ExitCode != 0)
            {
                throw new Exception("Failed to start Soulfind container");
            }
            
            soulfindProcess = process;
        }
    }
    
    public class SoulfindBridgeStatus
    {
        public bool IsRunning { get; set; }
        public int ConnectedClients { get; set; }
        public long RequestsProxied { get; set; }
        public DateTimeOffset StartedAt { get; set; }
    }
}
```

---

### Component 2: Soulfind Extensions (Custom Proxy Mode)

**Concept**: We need to **extend** Soulfind with a "proxy mode" that forwards certain operations to slskdn.

#### Soulfind Modifications Needed

Create a fork/patch of Soulfind that adds:

1. **Environment variable**: `SOULFIND_PROXY_MODE=true`
2. **Configuration**: `SOULFIND_PROXY_TARGET=http://localhost:5030` (slskdn API)

When in proxy mode, Soulfind:

**Handles locally** (server protocol compliance):
- Connection handshake
- User authentication (validate with slskdn)
- Room join/leave (map to scene subscriptions)
- Chat messages (forward to overlay pubsub)

**Proxies to slskdn** (mesh intelligence):
- Search requests → `POST /api/bridge/search` (slskdn translates to mesh)
- File requests → resolved via shadow index + overlay
- User info → merged from Soulseek + overlay stats

---

### Component 3: slskdn Bridge API (Soulfind ← slskdn)

```csharp
namespace slskd.VirtualSoulfind.Bridge.API
{
    [ApiController]
    [Route("api/bridge")]
    [Produces("application/json")]
    public class BridgeController : ControllerBase
    {
        private readonly IShadowIndexService shadowIndex;
        private readonly ISceneService scenes;
        private readonly IMusicBrainzClient musicBrainz;
        
        /// <summary>
        /// Bridge search: Soulfind → slskdn mesh.
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> BridgeSearch(
            [FromBody] BridgeSearchRequest request,
            CancellationToken ct)
        {
            log.Information("[BRIDGE] Search from legacy client: {Query}", request.Query);
            
            var results = new List<BridgeSearchResult>();
            
            // Strategy 1: Try to resolve query to MBIDs (if looks like artist/album)
            var mbidResults = await TryResolveTomBIDsAsync(request.Query, ct);
            
            if (mbidResults.Any())
            {
                // Query shadow index for peers with these MBIDs
                foreach (var mbid in mbidResults)
                {
                    var peers = await shadowIndex.QueryPeersForReleaseAsync(mbid, ct);
                    
                    // Get variant details from overlay
                    foreach (var peer in peers)
                    {
                        var variants = await overlay.GetVariantsFromPeerAsync(peer, mbid, ct);
                        
                        results.AddRange(variants.Select(v => new BridgeSearchResult
                        {
                            Username = AnonymizePeerForBridge(peer),
                            Filename = SynthesizeFilename(v),  // Create friendly filename
                            Size = v.FileSizeBytes,
                            BitRate = v.BitrateKbps,
                            Length = v.DurationMs / 1000,
                            Quality = v.QualityScore,  // Extra metadata
                            IsCanonical = v.IsCanonical
                        }));
                    }
                }
            }
            
            // Strategy 2: Forward to real Soulseek (if available)
            if (soulseekHealth.CurrentHealth == SoulseekHealth.Healthy)
            {
                var soulseekResults = await soulseek.SearchAsync(request.Query, ct);
                results.AddRange(MapSoulseekResults(soulseekResults));
            }
            
            // Merge and rank results (prefer canonical, faster peers)
            var ranked = results
                .OrderByDescending(r => r.IsCanonical)
                .ThenByDescending(r => r.Quality)
                .Take(request.Limit ?? 200)
                .ToList();
            
            return Ok(new { results = ranked });
        }
        
        /// <summary>
        /// Bridge download: Soulfind → slskdn multi-swarm.
        /// </summary>
        [HttpPost("download")]
        public async Task<IActionResult> BridgeDownload(
            [FromBody] BridgeDownloadRequest request,
            CancellationToken ct)
        {
            // Extract MBID from request metadata (if available)
            string recordingId = ExtractMBIDFromRequest(request);
            
            if (recordingId != null)
            {
                // Create multi-swarm job (transparent to legacy client)
                var job = await multiSource.CreateJobAsync(new MultiSourceJobRequest
                {
                    RecordingId = recordingId,
                    TargetPath = request.TargetPath,
                    Sources = await shadowIndex.QueryPeersForRecordingAsync(recordingId, ct)
                }, ct);
                
                return Ok(new { job_id = job.JobId });
            }
            else
            {
                // Fall back to traditional download
                var transfer = await transfers.EnqueueDownloadAsync(new DownloadOptions
                {
                    Username = request.Username,
                    Filename = request.RemotePath,
                    DestinationDirectory = Path.GetDirectoryName(request.TargetPath)
                }, ct);
                
                return Ok(new { transfer_id = transfer.Id });
            }
        }
        
        private string SynthesizeFilename(AudioVariant variant)
        {
            // Create a friendly filename for legacy clients from variant metadata
            // e.g., "Artist - Title (FLAC 16-44.1).flac"
            
            var codec = variant.Codec.ToUpperInvariant();
            var quality = variant.BitDepth.HasValue 
                ? $"{variant.BitDepth}bit-{variant.SampleRateHz / 1000.0:F1}kHz"
                : $"{variant.BitrateKbps}kbps";
            
            return $"{variant.Artist} - {variant.Title} ({codec} {quality}).{variant.Container.ToLowerInvariant()}";
        }
        
        private string AnonymizePeerForBridge(string overlayPeerId)
        {
            // Create a consistent but anonymized "username" for overlay peers
            // e.g., "mesh-peer-a1b2c3"
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(overlayPeerId));
            return $"mesh-peer-{Convert.ToHexString(hash)[..6].ToLowerInvariant()}";
        }
    }
}
```

---

## The Magic: Transparent MBID Enhancement

### What Legacy Clients See

**Search in Nicotine+**:
```
Query: "Radiohead OK Computer"

Results:
  user123: Radiohead - Paranoid Android (FLAC 16-44.1).flac [42 MB] ✓ CANONICAL
  user456: Radiohead - Paranoid Android (MP3 320).mp3 [9 MB]
  mesh-peer-4f3a2c: Radiohead - Paranoid Android (FLAC 24-96).flac [120 MB] ✓ HI-RES
```

**What really happened**:
1. Nicotine+ sent search to bridge
2. Bridge resolved "Radiohead OK Computer" → MB Release ID
3. Bridge queried shadow index via DHT
4. Bridge synthesized "users" from overlay peer IDs
5. Bridge returned mesh results + real Soulseek results (if available)

**Download in Nicotine+**:
```
User clicks "Download" on mesh-peer-4f3a2c
```

**What really happens**:
1. Nicotine+ requests file from "mesh-peer-4f3a2c"
2. Bridge maps that to overlay peer + variant
3. Bridge creates multi-swarm job
4. File downloads via overlay chunks (fast!)
5. Nicotine+ shows progress normally

**User experience**: Completely normal. They don't know it's mesh-powered.

---

## Architecture: The Bridge Stack

```
┌─────────────────────────────────────────────────────────────┐
│  Legacy Client (Nicotine+, SoulseekQt, Seeker)              │
│  Uses standard Soulseek protocol                            │
└────────────────┬────────────────────────────────────────────┘
                 │ TCP :2242 (Soulseek protocol)
                 ▼
┌─────────────────────────────────────────────────────────────┐
│  Local Soulfind Instance (Bridge Mode)                      │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Protocol Layer (Soulseek wire format)              │    │
│  │  - Handshake, login, search, download, rooms        │    │
│  └──────────────┬──────────────────────────────────────┘    │
│                 │                                            │
│  ┌──────────────▼──────────────────────────────────────┐    │
│  │  Proxy Layer (Bridge-specific)                      │    │
│  │  - Intercepts search/download/room operations       │    │
│  │  - Forwards to slskdn bridge API                    │    │
│  │  - Translates mesh responses to Soulseek format     │    │
│  └──────────────┬──────────────────────────────────────┘    │
└─────────────────┼──────────────────────────────────────────┘
                  │ HTTP :5030 (slskdn bridge API)
                  ▼
┌─────────────────────────────────────────────────────────────┐
│  slskdn                                                      │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Bridge API Controller                               │    │
│  │  - /api/bridge/search                               │    │
│  │  - /api/bridge/download                             │    │
│  │  - /api/bridge/rooms → scenes                       │    │
│  └──────────────┬──────────────────────────────────────┘    │
│                 │                                            │
│  ┌──────────────▼──────────────────────────────────────┐    │
│  │  Virtual Soulfind Mesh Services                     │    │
│  │  - Shadow index (DHT queries)                       │    │
│  │  - Scene management                                 │    │
│  │  - MBID resolution                                  │    │
│  │  - Canonical scoring                                │    │
│  └──────────────┬──────────────────────────────────────┘    │
│                 │                                            │
│                 ▼                                            │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Overlay + DHT + Multi-Swarm                        │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

---

## Task Breakdown

### Phase 6X: Compatibility Bridge (T-850 to T-860)

**T-850**: Bridge service lifecycle (start/stop Soulfind)  
**T-851**: Soulfind proxy mode implementation (fork/patch Soulfind)  
**T-852**: Bridge API endpoints (search, download, rooms)  
**T-853**: MBID resolution from legacy queries  
**T-854**: Filename synthesis from variants  
**T-855**: Peer ID anonymization  
**T-856**: Room → Scene mapping  
**T-857**: Transfer progress proxying (show mesh speed in legacy client)  
**T-858**: Bridge configuration UI  
**T-859**: Bridge status dashboard  
**T-860**: Integration tests with Nicotine+ test automation

---

## Configuration

```yaml
bridge:
  enabled: false  # Opt-in (requires Soulfind install)
  
  # Soulfind execution
  use_docker: true
  docker_image: "slskdn/soulfind-bridge:latest"
  port: 2242
  
  # Or use local binary
  soulfind_binary_path: "/usr/local/bin/soulfind"
  
  # Authentication
  require_password: true
  password: "changeme"  # Or sync with slskdn users
  
  # Behavior
  expose_mesh_peers: true  # Show "mesh-peer-*" in results
  expose_canonical_hints: true  # Mark canonical variants
  auto_upgrade_to_multiswarm: true  # Use overlay for downloads
  
  # Privacy
  anonymize_overlay_peers: true
  max_clients: 10  # Concurrent legacy clients
  
  # Rooms → Scenes
  map_rooms_to_scenes: true
  default_scenes:
    - "scene:label:warp-records"
    - "scene:genre:dub-techno"
```

---

## Security Considerations

### 1. Trust Boundary

**Local Bridge** (trusted):
- Runs on your machine/LAN
- You control who connects
- Can use firewall rules to limit access

**Remote Bridge** (semi-trusted):
- If you expose bridge to internet (VPN, tailscale)
- Requires strong authentication
- Rate limiting per legacy client

### 2. Username Privacy

**Problem**: Legacy clients send Soulseek usernames in the clear.

**Solution**:
- Bridge authenticates legacy clients
- Maps their usernames to overlay identities
- Never publishes raw Soulseek usernames to DHT
- Overlay peers only see anonymized IDs

### 3. Abuse Prevention

- **Rate limit** legacy clients (search, download requests)
- **Fairness tracking** per client (count toward your fairness quotas)
- **Disconnect** clients that violate policies

---

## User Experience

### Setup (One-Time)

**In slskdn config**:
```yaml
bridge:
  enabled: true
  password: "your-secure-password"
```

**Start slskdn**:
```bash
./slskdn
# Logs: "[BRIDGE] Soulfind bridge active on :2242"
```

**Configure legacy client** (e.g., Nicotine+):
```
Server: localhost (or your LAN IP)
Port: 2242
Username: your-slskdn-username
Password: your-secure-password
```

**That's it!** The legacy client now uses the mesh.

---

### User Experience (Advanced)

**Scene as "Room"**:
```
In Nicotine+, join room "warp-records"
→ Bridge maps to scene:label:warp-records
→ Chat goes to overlay pubsub
→ Searches are scene-scoped (Warp releases only)
```

**Disaster Mode**:
```
Official Soulseek server goes down
→ Nicotine+ still works (connects to your bridge)
→ Bridge uses pure mesh (DHT + overlay)
→ User doesn't notice (maybe slightly slower searches)
```

---

## Why This Is Game-Changing

### 1. **Network Effect Multiplier**

Every slskdn node with bridge enabled becomes an **entry point** for legacy clients:
- Your friends don't need slskdn
- They still benefit from the mesh
- More users → more mesh participants → better for everyone

### 2. **Gradual Migration Path**

Users can:
1. Start with legacy client → your bridge (low friction)
2. See the benefits (faster, better quality, disaster resilience)
3. Eventually switch to full slskdn (if they want more features)

No forced migration = more adoption.

### 3. **Community Tool**

Perfect for:
- Music collectives / labels
- Private communities
- Friend groups
- LAN parties

One person runs slskdn + bridge → everyone benefits.

### 4. **Disaster Resilience Extension**

When Soulseek dies:
- slskdn users continue (mesh-only)
- **Bridge users also continue** (mesh via bridge)
- Legacy-only users (no bridge) are offline

This extends the mesh's resilience to the broader community.

---

## Implementation Priority

**Recommendation**: Implement AFTER Phase 6 core (shadow index, scenes, disaster mode).

**Reasoning**:
- Bridge needs the mesh to be functional first
- Core mesh benefits slskdn users immediately
- Bridge is additive enhancement

**Timeline**:
- Phase 6A-D first (16 weeks)
- Then Phase 6X (4-6 weeks)

---

## Open Questions

1. **Soulfind fork maintenance**: Do we maintain our own fork or contribute upstream?
   - **Answer**: Start with fork, contribute proxy mode upstream if maintainer interested

2. **Authentication**: How do legacy clients auth to bridge?
   - **Answer**: Use slskdn's existing user system or simple shared password

3. **Performance**: Can one slskdn handle 10+ legacy clients?
   - **Answer**: Likely yes (bridge is mostly proxying), but needs load testing

4. **Mobile clients**: Do they have any special requirements?
   - **Answer**: Test with Seeker specifically (Android), may need tweaks

---

## Summary

The **Compatibility Bridge** is the killer feature that:

✅ Extends mesh benefits to **all Soulseek clients**  
✅ Creates **zero-friction** onboarding for your community  
✅ Provides **disaster resilience** to legacy users  
✅ Maintains **backward compatibility** while enabling future features  
✅ Aligns perfectly with slskdn's **"augment, don't replace"** ethos

**Think of it as**: Your slskdn becomes a "personal Soulseek server" for your friends, powered by the global mesh.

This is **not** redundant with DHT - it's a **UX multiplier** that brings mesh intelligence to users who aren't ready for slskdn yet.

---

**Implementation Status**: Fully designed, ready for task breakdown after Phase 6 core completes.
















