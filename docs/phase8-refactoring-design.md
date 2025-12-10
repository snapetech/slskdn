# Phase 8: Code Quality & Refactoring - Detailed Design

> **Phase**: 8 (Post-implementation cleanup)  
> **Dependencies**: Phases 2-6 implementation complete  
> **Branch**: `experimental/brainz`  
> **Estimated Duration**: 8-12 weeks  
> **Tasks**: T-1000 through T-1050

---

## Overview

After rapid implementation of Phases 2-6, this phase focuses on **structural cleanup, modularity, testability, and long-term maintainability**. The goal is to transform experimental code into production-grade architecture.

### Philosophy

This branch has accumulated:
- Multi-source/swarm downloads
- DHT-based discovery + mesh overlay
- Security hardening stack (NetworkGuard, PeerReputation, ByzantineConsensus)
- MusicBrainz integration, AudioVariant analysis
- AI-assisted rapid iteration patterns

Without hard boundaries, this will become unmaintainable. Phase 8 establishes **explicit module boundaries** and **clean interfaces**.

### Guiding Principle: No Feature Loss, Focus on Speed + Security

**Overarching constraints for all refactors:**

1. **Do not remove features we've already committed to conceptually**:
   - DHT as a first-class data path (not just lookup sidecar)
   - NAT experimentation and multi-path strategies
   - Multi-swarm overlay
   - MBID/Brainz-based jobs, discography, repair, canonical selection

2. When forced to choose between:
   - (A) cleaner/"nicer" abstractions and
   - (B) retaining high flexibility for DHT/NAT/mesh experiments,
   
   Default to **(B)**, as long as we can still test and reason about it.

3. Refactors must be justified by:
   - **Performance wins** (throughput, latency, better resource usage), or
   - **Security/robustness wins** (less attack surface, clearer policy, better disaster behaviour),
   
   Not *only* by aesthetics.

---

## 1. Hard Boundaries: Split into Explicit Modules

### 1.1. Bounded Contexts

Create explicit mini-domains (separate C# projects or top-level namespaces):

#### Module Structure

```
Slskdn.Core/           # Existing upstream-ish core (don't touch)
Slskdn.Swarm/          # Multi-source download engine
  ├── Models/          # SwarmFile, SwarmChunk, SwarmSource
  ├── Orchestration/   # SwarmDownloadOrchestrator
  ├── Scheduling/      # Chunk scheduler
  ├── Verification/    # Chunk verification
  └── Jobs/            # Swarm job orchestration

Slskdn.Mesh/           # DHT + overlay
  ├── DHT/             # DHT bootstrap / nodes (first-class data path)
  ├── Overlay/         # Mesh overlay connections (second rail)
  ├── Gossip/          # Hash / availability gossip
  ├── Transport/       # NAT, UPnP, TLS, multi-path strategies
  ├── Directory/       # IMeshDirectory (high-level API)
  └── Advanced/        # IMeshAdvanced (power features, experiments)

Slskdn.Security/       # Security hardening
  ├── Policies/        # Policy engine + individual policies
  ├── Reputation/      # NetworkGuard, ViolationTracker
  ├── Consensus/       # ByzantineConsensus
  └── Content/         # ContentSafety, PathGuard

Slskdn.Brainz/         # MusicBrainz integration
  ├── Models/          # AudioVariant, CanonicalStats
  ├── Analyzers/       # FLAC, MP3, Opus, AAC analyzers
  ├── Jobs/            # Discography, repair, backfill jobs
  ├── Catalog/         # MB/AcoustID client
  └── Pipeline/        # Job runner

Slskdn.Integrations/   # External integrations
  ├── Soulbeet/        # Soulbeet API
  ├── Soulfind/        # Test harness hooks
  └── Notifications/   # Ntfy, Pushover, etc.
```

#### Key Interfaces

```csharp
// Swarm
public interface ISwarmDownloadService
{
    Task<SwarmJob> StartDownloadAsync(SwarmRequest request, CancellationToken ct);
    Task<SwarmStatus> GetStatusAsync(string jobId, CancellationToken ct);
}

public interface IVerificationEngine
{
    Task<VerificationResult> VerifyChunkAsync(SwarmChunk chunk, PeerId peer, CancellationToken ct);
}

// Mesh (Dual API: High-Level + Advanced)
public interface IMeshDirectory
{
    Task<IEnumerable<MeshPeerInfo>> FindPeersByKeyAsync(string key, int maxResults, CancellationToken ct);
    Task PublishAvailabilityAsync(string key, MeshPeerInfo self, CancellationToken ct);
    IAsyncEnumerable<MeshEvent> SubscribeAsync(CancellationToken ct);
}

public interface IMeshAdvanced
{
    // Raw DHT access for power features
    Task DhtPutAsync(string key, ReadOnlyMemory<byte> value, CancellationToken ct);
    Task<IReadOnlyList<ReadOnlyMemory<byte>>> DhtGetAsync(string key, CancellationToken ct);
    
    // NAT / transport controls
    Task<MeshTransportStats> GetTransportStatsAsync(CancellationToken ct);
    Task ForceRebindAsync(NatStrategy strategy, CancellationToken ct);
    
    // Low-level events for debugging, simulation, lab features
    IAsyncEnumerable<LowLevelMeshEvent> SubscribeLowLevelAsync(CancellationToken ct);
}

// Security
public interface ISecurityPolicyEngine
{
    Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct);
}

// Brainz
public interface IBrainzCatalogService
{
    Task<AudioVariant> AnalyzeFileAsync(string filePath, CancellationToken ct);
    Task<Release> GetReleaseAsync(string mbid, CancellationToken ct);
}

public interface IMetadataJobRunner
{
    Task<string> EnqueueJobAsync(IMetadataJob job, CancellationToken ct);
    Task<JobStatus> GetStatusAsync(string jobId, CancellationToken ct);
}
```

### API Usage Guidelines

- **High-level code** (controllers, simple jobs): Use `IMeshDirectory`
- **Experimental features** (DHT-heavy flows, NAT experiments, mesh simulations): Use `IMeshAdvanced`
- **Swarm orchestrator**: Uses `IMeshDirectory`, but can leverage `IMeshAdvanced` for advanced strategies

### Direction Enforcement

- Web API / UI → Application layer → Domain services → Infrastructure
- **No domain layer calling back into controllers/SignalR**
- Services depend on abstractions, not implementations

---

## 2. Swarm Engine: Tighten Scheduling

### 2.1. Proper Scheduler (Replace Task.Run Chaos)

**Current Problem**: Ad-hoc `Task.Run(() => ...)` and `System.Timers.Timer` scattered everywhere.

**Solution**: Single `SwarmDownloadOrchestrator` as `IHostedService`:

```csharp
public class SwarmDownloadOrchestrator : BackgroundService
{
    private readonly Channel<SwarmJob> _jobQueue;
    private readonly Dictionary<string, SwarmFileState> _activeFiles;
    private readonly IVerificationEngine _verification;
    private readonly IMeshDirectory _mesh;
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var job in _jobQueue.Reader.ReadAllAsync(ct))
        {
            // Coordinate chunks, peers, verification
        }
    }
    
    private async Task ProcessFileAsync(SwarmJob job, CancellationToken ct)
    {
        var state = new SwarmFileState(job);
        
        while (!state.IsComplete)
        {
            var chunk = state.GetNextChunk();
            var peers = await SelectPeersAsync(chunk, ct);
            
            foreach (var peer in peers)
            {
                await DownloadChunkAsync(chunk, peer, ct);
                var result = await _verification.VerifyChunkAsync(chunk, peer, ct);
                
                if (result.IsValid)
                {
                    state.MarkChunkComplete(chunk);
                    break;
                }
            }
        }
    }
}
```

#### SwarmJob Model

```csharp
public class SwarmJob
{
    public string JobId { get; set; }
    public SwarmFile File { get; set; }
    public List<SwarmSource> Sources { get; set; }
    public SwarmOptions Options { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class SwarmFile
{
    public string Id { get; set; }  // file_hash_sha256
    public long Size { get; set; }
    public int ChunkSize { get; set; }
    public List<SwarmChunk> Chunks { get; set; }
}

public class SwarmChunk
{
    public int Index { get; set; }
    public long Offset { get; set; }
    public int Length { get; set; }
    public string ExpectedHash { get; set; }
}

public class SwarmSource
{
    public PeerId Peer { get; set; }
    public SwarmCapabilities Capabilities { get; set; }
    public SwarmSourceStats Stats { get; set; }
}
```

### 2.2. Centralize Verification

**Current Problem**: Verification logic (CryptographicCommitment, ProofOfStorage, ByzantineConsensus) scattered across swarm code.

**Solution**: Single `IVerificationEngine`:

```csharp
public class VerificationEngine : IVerificationEngine
{
    private readonly IHashDbService _hashDb;
    private readonly IByzantineConsensusService _consensus;
    private readonly IPeerReputationService _reputation;
    private readonly ISecurityPolicyEngine _security;
    private readonly IMemoryCache _cache;
    
    public async Task<VerificationResult> VerifyChunkAsync(
        SwarmChunk chunk, 
        PeerId peer, 
        CancellationToken ct)
    {
        var cacheKey = $"{chunk.Index}:{chunk.ExpectedHash}";
        if (_cache.TryGetValue(cacheKey, out VerificationResult cached))
            return cached;
        
        // Hash check
        if (!await VerifyHashAsync(chunk, ct))
            return VerificationResult.Reject("Hash mismatch");
        
        // Consensus check
        if (!await _consensus.ValidateAsync(chunk, peer, ct))
            return VerificationResult.SoftFail("Consensus failed");
        
        // Reputation check
        if (!await _reputation.IsTrustedAsync(peer, ct))
            return VerificationResult.Throttle("Low reputation");
        
        var result = VerificationResult.Ok();
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }
}
```

---

## 3. Mesh & DHT: Clean Directory Service

### Design Philosophy: DHT is First-Class, Not a Sidecar

**Key Principle**: DHT is first-class, TCP/overlay is a second rail.

- **DHT is encouraged** for:
  - Discovery
  - Small/medium control-plane payloads (hashes, availability shards, swarm descriptors, repair mission descriptors)

- **Overlay/TCP** is:
  - A fallback or second rail for:
    - Streaming
    - Larger payloads
    - Cases where DHT isn't appropriate or fails

**We prefer to send as much as sensibly possible over DHT**, as long as it's not abusive (e.g., not huge blobs).

### 3.1. Dual API: IMeshDirectory + IMeshAdvanced

**Current Problem**: Single abstraction would hide DHT power and limit experimentation.

**Solution**: Two complementary interfaces.

#### 3.1.1. High-Level API: IMeshDirectory

For normal application code (swarm, brainz, jobs):

```csharp
public interface IMeshDirectory
{
    // Discovery
    Task<IEnumerable<MeshPeerInfo>> FindPeersByKeyAsync(
        string key, 
        int maxResults, 
        CancellationToken ct);
    
    // Publishing
    Task PublishAvailabilityAsync(
        string key, 
        MeshPeerInfo self, 
        CancellationToken ct);
    
    // Events
    IAsyncEnumerable<MeshEvent> SubscribeAsync(CancellationToken ct);
}

public class MeshPeerInfo
{
    public PeerId Id { get; set; }
    public IPEndPoint Endpoint { get; set; }
    public MeshCapabilities Capabilities { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public abstract class MeshEvent
{
    public DateTimeOffset Timestamp { get; set; }
}

public class PeerJoinedEvent : MeshEvent
{
    public MeshPeerInfo Peer { get; set; }
}

public class PeerLeftEvent : MeshEvent
{
    public PeerId PeerId { get; set; }
}

public class AvailabilityAnnouncedEvent : MeshEvent
{
    public string Key { get; set; }
    public MeshPeerInfo Peer { get; set; }
}
```

**Used by**:
- Swarm orchestrator (find peers for MBIDs/hashes)
- Brainz/metadata jobs (discography, repair missions)
- Scenes/micro-networks logic

**Internally**: Can use DHT and overlay in whatever combination we choose.

#### 3.1.2. Advanced API: IMeshAdvanced

For power features and experiments:

```csharp
public interface IMeshAdvanced
{
    // ===== Raw DHT Access =====
    // For DHT-heavy flows that need direct control
    Task DhtPutAsync(string key, ReadOnlyMemory<byte> value, CancellationToken ct);
    Task<IReadOnlyList<ReadOnlyMemory<byte>>> DhtGetAsync(string key, CancellationToken ct);
    
    // Bulk operations for efficiency
    Task DhtPutBatchAsync(IEnumerable<(string key, ReadOnlyMemory<byte> value)> items, CancellationToken ct);
    
    // ===== NAT / Transport Controls =====
    Task<MeshTransportStats> GetTransportStatsAsync(CancellationToken ct);
    Task ForceRebindAsync(NatStrategy strategy, CancellationToken ct);
    Task<NatTraversalResult> TestNatTraversalAsync(IPEndPoint target, CancellationToken ct);
    
    // ===== Low-Level Mesh Events =====
    // For debugging, simulation, lab features
    IAsyncEnumerable<LowLevelMeshEvent> SubscribeLowLevelAsync(CancellationToken ct);
}

public class MeshTransportStats
{
    public int ActiveDhtNodes { get; set; }
    public int ActiveOverlayConnections { get; set; }
    public NatType DetectedNatType { get; set; }
    public TimeSpan AvgDhtLatency { get; set; }
    public TimeSpan AvgOverlayLatency { get; set; }
    public long BytesSentDht { get; set; }
    public long BytesSentOverlay { get; set; }
}

public abstract class LowLevelMeshEvent : MeshEvent
{
}

public class DhtNodeAddedEvent : LowLevelMeshEvent
{
    public IPEndPoint Node { get; set; }
}

public class NatRebindEvent : LowLevelMeshEvent
{
    public NatStrategy Strategy { get; set; }
    public bool Success { get; set; }
}
```

**Used by**:
- "Mesh lab" / experimental modules
- Network simulation jobs
- DHT-heavy or NAT-specific features that need low-level access

**Important**: Refactor MUST preserve ability to:
- Experiment with different NAT strategies
- Push more data over DHT where it makes sense
- Mirror certain control flows over both DHT and overlay

### 3.2. Configurable Transport Preference

Add mesh options for transport strategy:

```csharp
public class MeshOptions
{
    public List<string> BootstrapNodes { get; set; } = new();
    public int DhtPort { get; set; } = 6881;
    public int OverlayPort { get; set; } = 6882;
    public bool EnableNatTraversal { get; set; } = true;
    
    // Transport preference: how to use DHT vs overlay
    public MeshTransportPreference TransportPreference { get; set; } = MeshTransportPreference.DhtFirst;
}

public enum MeshTransportPreference
{
    DhtFirst,      // Try DHT, use overlay as fallback (default for experimental/brainz)
    Mirrored,      // Send key control-plane messages via both DHT and overlay
    OverlayFirst,  // Conservative mode: prefer overlay, use DHT for discovery only
}
```

**Configuration**:

```yaml
mesh:
  transportPreference: dht-first  # or mirrored, overlay-first
  dhtPort: 6881
  overlayPort: 6882
  enableNatTraversal: true
  bootstrapNodes:
    - dht.slskdn.org:6881
```

**Default for experimental/brainz**: `dht-first` or `mirrored` (encourage DHT usage, don't hide it).

### 3.3. Internal Transport Service

Transport ugliness (UPnP, NAT-PMP, TLS, reconnects) lives in internal service:

```csharp
internal class MeshTransportService
{
    private readonly INatDiscoveryService _nat;
    private readonly ITlsProvisioningService _tls;
    private readonly IDhtBootstrapService _dht;
    private readonly MeshOptions _options;
    
    internal async Task<IMeshConnection> EstablishConnectionAsync(
        IPEndPoint remote, 
        MeshTransportPreference preference,
        CancellationToken ct)
    {
        switch (preference)
        {
            case MeshTransportPreference.DhtFirst:
                // Try DHT, fallback to overlay
                break;
            case MeshTransportPreference.Mirrored:
                // Establish both paths
                break;
            case MeshTransportPreference.OverlayFirst:
                // Try overlay, use DHT for discovery only
                break;
        }
    }
}
```

**Exposed only through**: `IMeshDirectory` and `IMeshAdvanced`.

---

## 4. Brainz: Turn into Pipeline (Power Feature)

### Design Philosophy: Jobs as First-Class Citizens

Jobs are not just cleanup—they **enable new functionality**:
- Discography jobs
- Repair missions
- Network stress-test jobs
- Long-running MB/AcoustID lookups

We accept asynchronous, eventually-consistent behaviour for heavy operations.

### 4.1. Job Pipeline

**Current Problem**: Fire-and-forget async calls for MB/Brainz scattered everywhere.

**Solution**: Formal job abstraction + centralized runner.

```csharp
public interface IMetadataJob
{
    string JobId { get; }
    string Type { get; }
    Task RunAsync(CancellationToken ct);
}

public class AlbumBackfillJob : IMetadataJob
{
    public string JobId { get; set; }
    public string Type => "album_backfill";
    public string MusicBrainzReleaseId { get; set; }
    
    private readonly IBrainzCatalogService _catalog;
    private readonly ISwarmDownloadService _swarm;
    private readonly IMeshDirectory _mesh;
    
    public async Task RunAsync(CancellationToken ct)
    {
        var release = await _catalog.GetReleaseAsync(MusicBrainzReleaseId, ct);
        
        foreach (var track in release.Tracks)
        {
            // Discover sources via mesh
            var peers = await _mesh.FindPeersByKeyAsync($"mbid:{track.RecordingId}", 10, ct);
            
            // Start swarm download
            await _swarm.StartDownloadAsync(new SwarmRequest
            {
                Sources = peers,
                TargetQuality = QualityPreference.Canonical
            }, ct);
        }
    }
}

public class NetworkStressTestJob : IMetadataJob
{
    public string JobId { get; set; }
    public string Type => "network_stress_test";
    
    private readonly IMeshAdvanced _meshAdvanced;
    
    public async Task RunAsync(CancellationToken ct)
    {
        // Use IMeshAdvanced for low-level mesh simulation
        var stats = await _meshAdvanced.GetTransportStatsAsync(ct);
        
        // Force NAT rebind to test resilience
        await _meshAdvanced.ForceRebindAsync(NatStrategy.PortPreservation, ct);
        
        // Stress DHT with batch operations
        var testData = GenerateTestPayloads();
        await _meshAdvanced.DhtPutBatchAsync(testData, ct);
    }
}

public class MetadataJobRunner : BackgroundService
{
    private readonly Channel<IMetadataJob> _jobQueue;
    private readonly IJobRepository _repo;
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var job in _jobQueue.Reader.ReadAllAsync(ct))
        {
            await _repo.UpdateStatusAsync(job.JobId, JobStatus.Running, ct);
            
            try
            {
                await job.RunAsync(ct);
                await _repo.UpdateStatusAsync(job.JobId, JobStatus.Completed, ct);
            }
            catch (Exception ex)
            {
                await _repo.UpdateStatusAsync(job.JobId, JobStatus.Failed, ct);
            }
        }
    }
}
```

### 4.2. AudioVariant Analyzers

Implement codec-specific analyzers from Phase 2-Extended design:

```csharp
public interface IAudioAnalyzer
{
    string SupportedCodec { get; }
    Task<AudioVariantFeatures> AnalyzeAsync(string filePath, CancellationToken ct);
}

public class FlacAnalyzer : IAudioAnalyzer
{
    public string SupportedCodec => "FLAC";
    
    public async Task<AudioVariantFeatures> AnalyzeAsync(string filePath, CancellationToken ct)
    {
        // Extract streaminfo hash, PCM MD5, detect transcodes via spectral analysis
    }
}

// MP3Analyzer, OpusAnalyzer, AacAnalyzer...
```

### 4.3. Unified External API Client

**Current Problem**: Direct `HttpClient` calls to MB/AcoustID/Soulbeet, no rate limiting or caching.

**Solution**: Single `BrainzClient` for all external metadata:

```csharp
public class BrainzClient
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly IBackoffStrategy _backoff;
    
    public async Task<Release> GetReleaseAsync(string mbid, CancellationToken ct)
    {
        if (_cache.TryGetValue($"mb:release:{mbid}", out Release cached))
            return cached;
        
        await _rateLimiter.WaitAsync(ct);
        
        try
        {
            var response = await _http.GetAsync($"https://musicbrainz.org/ws/2/release/{mbid}", ct);
            
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await _backoff.DelayAsync(ct);
                return await GetReleaseAsync(mbid, ct);  // Retry
            }
            
            var release = await response.Content.ReadFromJsonAsync<Release>(ct);
            _cache.Set($"mb:release:{mbid}", release, TimeSpan.FromHours(24));
            return release;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}
```

---

## 5. Security: Policy Engine (With Network Cleverness Preserved)

### Design Philosophy: Real Security Gains, Not Just Tidiness

Policy engine must improve:
- **Auditability**: Clear record of security decisions
- **Extensibility**: Easy to add new defenses
- **Simulatability**: Run security scenarios in tests

### 5.1. Unified Security Abstraction

**Current Problem**: Security checks (NetworkGuard, ViolationTracker, PathGuard, ContentSafety, etc.) interleaved in controllers.

**Solution**: Policy engine that allows network-level cleverness:

```csharp
public interface ISecurityPolicyEngine
{
    Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct);
}

public class SecurityContext
{
    public PeerId Peer { get; set; }
    public OperationType Operation { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public enum OperationType
{
    Download,
    Upload,
    SwarmChunk,
    MeshGossip,
    Command,
}

public class SecurityDecision
{
    public SecurityAction Action { get; set; }
    public List<string> Reasons { get; set; }
    
    public static SecurityDecision Allow() => new() { Action = SecurityAction.Allow };
    public static SecurityDecision Deny(string reason) => new() { Action = SecurityAction.Deny, Reasons = new() { reason } };
    public static SecurityDecision Throttle(string reason) => new() { Action = SecurityAction.Throttle, Reasons = new() { reason } };
}

public enum SecurityAction
{
    Allow,
    Deny,
    Throttle,
}
```

#### Policy Composition

```csharp
public class CompositeSecurityPolicy : ISecurityPolicyEngine
{
    private readonly IEnumerable<ISecurityPolicy> _policies;
    
    public async Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken ct)
    {
        foreach (var policy in _policies)
        {
            var decision = await policy.EvaluateAsync(context, ct);
            
            if (decision.Action != SecurityAction.Allow)
                return decision;  // Short-circuit on deny/throttle
        }
        
        return SecurityDecision.Allow();
    }
}

// Individual policies
public class NetworkGuardPolicy : ISecurityPolicy { }
public class ReputationPolicy : ISecurityPolicy { }
public class ConsensusPolicy : ISecurityPolicy { }
public class ContentSafetyPolicy : ISecurityPolicy { }
public class HoneypotPolicy : ISecurityPolicy 
{
    // Can use IMeshAdvanced to deploy trap data, monitor DHT access patterns
}
public class NatAbuseDetectionPolicy : ISecurityPolicy
{
    // Can examine transport stats to detect NAT manipulation attacks
}
```

**Key Requirement**: Policy engine must allow:
- Network-level cleverness (honeypot peers, trap data)
- Policy decisions that take DHT/NAT behaviour into account
- Integration with mesh transport stats

---

## 6. Configuration: Strongly Typed Options (Not Feature-Limiting)

### Design Philosophy: Tune Behaviour, Don't Disable Capabilities

Typed config should:
- **Enable** tuning and profiling
- **Not disable** capabilities we've committed to (DHT-heavy flows, NAT experiments, etc.)
- **Allow** selection of profiles (experimental, conservative, etc.)

### 6.1. Module-Specific Options

Replace magic strings with typed options:

```csharp
public class SwarmOptions
{
    public int DefaultChunkSize { get; set; } = 524288;  // 512 KB
    public int MaxConcurrentChunks { get; set; } = 16;
    public int MaxPeersPerFile { get; set; } = 10;
    public TimeSpan SourceTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public class MeshOptions
{
    public List<string> BootstrapNodes { get; set; } = new();
    public int DhtPort { get; set; } = 6881;
    public int OverlayPort { get; set; } = 6882;
    public bool EnableNatTraversal { get; set; } = true;
    
    // Transport preference (encourage DHT usage)
    public MeshTransportPreference TransportPreference { get; set; } = MeshTransportPreference.DhtFirst;
    
    // Advanced options (for experiments)
    public int DhtBatchSize { get; set; } = 100;
    public TimeSpan DhtTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool EnableNatExperiments { get; set; } = true;
}

public enum MeshTransportPreference
{
    DhtFirst,      // Default for experimental/brainz
    Mirrored,
    OverlayFirst,
}

public class SecurityOptions
{
    public SecurityProfile Profile { get; set; } = SecurityProfile.Balanced;
    public bool EnableByzantineConsensus { get; set; } = true;
    public bool EnablePeerReputation { get; set; } = true;
    public bool EnableContentSafety { get; set; } = true;
}

public class BrainzOptions
{
    public string MusicBrainzApiKey { get; set; }
    public string AcoustIdApiKey { get; set; }
    public int RateLimitPerMinute { get; set; } = 50;
    public bool EnableAutoTagging { get; set; } = true;
}
```

### 6.2. DI Registration

```csharp
services.Configure<SwarmOptions>(configuration.GetSection("Swarm"));
services.Configure<MeshOptions>(configuration.GetSection("Mesh"));
services.Configure<SecurityOptions>(configuration.GetSection("Security"));
services.Configure<BrainzOptions>(configuration.GetSection("Brainz"));
```

---

## 7. Testability & Simulation: DHT-Heavy + Disaster Scenarios

### Design Philosophy: Test Real Capabilities

Tests must cover:
- **DHT-first discovery**: Not just overlay fallback
- **DHT-carried control-plane**: Availability shards, missions
- **Disaster mode**: Operations via DHT + overlay only (no Soulseek)
- **NAT edge cases**: Using `IMeshAdvanced` to force scenarios

### 7.1. Make Services Testable

**Requirements**:
- No static singletons
- No direct `new HttpClient()`
- All dependencies injected

**Constructor Injection Pattern**:

```csharp
public class SwarmDownloadOrchestrator
{
    private readonly IMeshDirectory _mesh;
    private readonly IVerificationEngine _verification;
    private readonly ISwarmRepository _repo;
    private readonly IOptions<SwarmOptions> _options;
    private readonly ILogger<SwarmDownloadOrchestrator> _log;
    
    public SwarmDownloadOrchestrator(
        IMeshDirectory mesh,
        IVerificationEngine verification,
        ISwarmRepository repo,
        IOptions<SwarmOptions> options,
        ILogger<SwarmDownloadOrchestrator> log)
    {
        _mesh = mesh;
        _verification = verification;
        _repo = repo;
        _options = options;
        _log = log;
    }
}
```

### 7.2. Integration Test Suites

Implement Phase 7 designs:

```csharp
// Soulfind-based integration tests
[Collection("integration-soulseek")]
public class SwarmDownloadIntegrationTests
{
    [Fact]
    public async Task Should_Download_File_From_Multiple_Peers()
    {
        using var soulfind = SoulfindRunner.Start();
        using var alice = SlskdnTestClient.Create("alice", shareDir: "/fixtures/alice");
        using var bob = SlskdnTestClient.Create("bob", shareDir: "/fixtures/bob");
        using var carol = SlskdnTestClient.Create("carol");
        
        await alice.ConnectToSoulseekAsync(soulfind.Host, soulfind.Port);
        await bob.ConnectToSoulseekAsync(soulfind.Host, soulfind.Port);
        await carol.ConnectToSoulseekAsync(soulfind.Host, soulfind.Port);
        
        var results = await carol.SearchAsync("test-track.flac");
        var download = await carol.DownloadAsync(results.First());
        
        await download.WaitForCompletionAsync();
        
        Assert.Equal(TransferState.Completed, download.State);
        Assert.True(download.BytesTransferred > 0);
    }
}

// Mesh-only simulation tests
[Collection("integration-mesh")]
public class MeshDiscoveryTests
{
    [Fact]
    public async Task Should_Discover_Peers_Via_DHT_First()
    {
        var sim = new MeshSimulator(new MeshSimulatorOptions
        {
            TransportPreference = MeshTransportPreference.DhtFirst
        });
        
        var alice = sim.CreateNode("alice", inventory: aliceLibrary);
        var bob = sim.CreateNode("bob", inventory: bobLibrary);
        var carol = sim.CreateNode("carol", inventory: new());
        
        sim.ConnectNodes(alice, bob, carol);
        
        // Test DHT-first discovery
        var peers = await carol.Mesh.FindPeersByKeyAsync("mbid:abc-123", maxResults: 10, CancellationToken.None);
        
        Assert.Contains(peers, p => p.Id == alice.Id);
        
        // Verify DHT was used, not overlay
        var stats = await carol.MeshAdvanced.GetTransportStatsAsync(CancellationToken.None);
        Assert.True(stats.BytesSentDht > 0);
    }
    
    [Fact]
    public async Task Should_Continue_Operations_In_Disaster_Mode()
    {
        var sim = new MeshSimulator();
        
        var alice = sim.CreateNode("alice", inventory: aliceLibrary);
        var bob = sim.CreateNode("bob", inventory: bobLibrary);
        
        // Simulate official server down
        sim.DisableExternalConnections();
        
        // Should still discover via DHT + overlay
        var peers = await bob.Mesh.FindPeersByKeyAsync("mbid:xyz-789", maxResults: 10, CancellationToken.None);
        
        Assert.Contains(peers, p => p.Id == alice.Id);
    }
    
    [Fact]
    public async Task Should_Handle_NAT_Edge_Cases()
    {
        var sim = new MeshSimulator();
        
        var alice = sim.CreateNode("alice", natType: NatType.Symmetric);
        var bob = sim.CreateNode("bob", natType: NatType.PortRestrictedCone);
        
        // Force NAT rebind using IMeshAdvanced
        await alice.MeshAdvanced.ForceRebindAsync(NatStrategy.PortPreservation, CancellationToken.None);
        
        // Verify connection still works
        var peers = await alice.Mesh.FindPeersByKeyAsync("test-key", maxResults: 10, CancellationToken.None);
        Assert.Contains(peers, p => p.Id == bob.Id);
    }
}
```

**Key Requirements**:
- Mesh-only tests must cover DHT-first discovery
- Tests must verify DHT-carried control-plane payloads
- Disaster-mode tests ensure operations continue without Soulseek
- Simulations can use `IMeshAdvanced` to inspect/force low-level behaviour

**We accept**: Extra CI complexity for robustness and confidence that DHT/NAT cleverness doesn't silently regress.

---

## 8. AI Detritus Cleanup

### 8.1. Dead Code & Unused Concepts

**Tasks**:
- Remove unused enums, flags, half-implemented concepts
- Collapse "proto-classes" that just forward calls
- Kill experimental branches that never panned out

**Example**:
```csharp
// BEFORE: AI-generated forwarding class
public class SwarmCoordinator
{
    private readonly SwarmService _service;
    public Task DownloadAsync() => _service.DownloadAsync();
}

// AFTER: Just use SwarmService directly
services.AddSingleton<ISwarmService, SwarmService>();
```

### 8.2. Naming Normalization

**Vocabulary standardization**:
- Swarm: `SwarmJob`, `SwarmChunk`, `SwarmSource`
- Mesh: `MeshPeer`, `MeshEvent`, `MeshDirectory`
- Brainz: `AudioVariant`, `MetadataJob`, `Release`
- Security: `SecurityPolicy`, `SecurityDecision`, `SecurityContext`

**Avoid**:
- Mixing "Download" vs "Transfer" vs "Job"
- Mixing "Peer" vs "Node" vs "Source"
- Mixing "Metadata" vs "Info" vs "Data"

### 8.3. Comment Cleanup

**Move narrative comments to docs**:

```csharp
// BEFORE: Long narrative in code
/// <summary>
/// The swarm download orchestrator coordinates multi-source downloads
/// by maintaining a queue of chunk assignments and tracking per-peer
/// performance metrics to optimize throughput. It integrates with the
/// mesh directory for peer discovery and the verification engine for
/// chunk validation. The orchestrator implements a sophisticated
/// scheduling algorithm that...
/// </summary>

// AFTER: Concise implementation comment
/// <summary>
/// Coordinates multi-source downloads with chunk-level scheduling.
/// See docs/swarm-architecture.md for design details.
/// </summary>
```

---

## 9. Implementation Order (Updated for Power-First Approach)

### Execution Strategy

Refactors prioritized by **feature enablement** and **performance/security wins**, not just cleanup.

### Stage 1: Define Mesh APIs with Power Preserved (Weeks 1-2)
- T-1000: Create namespace structure (Swarm, Mesh, Security, Brainz, Integrations)
- T-1001: Define `IMeshDirectory` + `IMeshAdvanced` interfaces
- T-1002: Add `MeshOptions.TransportPreference` (DHT-first/mirrored/overlay-first)
- T-1003: Implement `MeshTransportService` with configurable preference
- **Deliverable**: Dual API preserving DHT-first capabilities

### Stage 2: Introduce Job Pipeline (Weeks 3-4)
- T-1030: Implement `IMetadataJob` abstraction
- T-1031: Create `MetadataJobRunner` as BackgroundService
- T-1034: Convert existing metadata tasks to jobs
- T-1035: Add network simulation job support
- **Deliverable**: Working job pipeline enabling discography, repair, sim jobs
- **Why first**: Starts delivering feature value immediately

### Stage 3: Implement Swarm Orchestrator (Weeks 5-6)
- T-1010: Implement `SwarmDownloadOrchestrator` as BackgroundService
- T-1011: Create `SwarmJob`/`SwarmFile`/`SwarmChunk` model
- T-1012: Implement `IVerificationEngine`
- T-1013: Replace ad-hoc Task.Run with orchestrator
- T-1014: Integrate with `IMeshDirectory` and `IMeshAdvanced`
- **Deliverable**: Centralized multi-swarm scheduling with mesh integration
- **Expected win**: Better throughput, more robust multi-swarm behaviour

### Stage 4: Security Policy Engine (Week 7)
- T-1040: Implement `ISecurityPolicyEngine`
- T-1041: Create `CompositeSecurityPolicy`
- T-1042: Implement individual policies (NetworkGuard, Reputation, Consensus, ContentSafety, Honeypot, NatAbuseDetection)
- T-1043: Replace inline security checks with policy engine
- **Deliverable**: Unified security with auditability and simulatability
- **Expected win**: Clearer policy, easier to add new defenses

### Stage 5: Typed Configuration (Week 8)
- T-1050: Create strongly-typed options (SwarmOptions, MeshOptions, SecurityOptions, BrainzOptions)
- T-1051: Wire options via IOptions<T>
- T-1052: Remove direct IConfiguration access
- **Deliverable**: Type-safe config tuning

### Stage 6: Codec Analyzers (Week 9)
- T-1032: Implement codec analyzers (FlacAnalyzer, Mp3Analyzer, OpusAnalyzer, AacAnalyzer)
- T-1033: Create unified BrainzClient with caching/rate-limiting
- **Deliverable**: AudioVariant analysis from Phase 2-Extended

### Stage 7: Testability Cleanup (Week 10)
- T-1060: Eliminate static singletons
- T-1061: Add interfaces for all major subsystems
- T-1062: Constructor injection cleanup
- **Deliverable**: Fully mockable services

### Stage 8: Test Infrastructure (Week 11)
- T-1070: Implement Soulfind test harness (from Phase 7)
- T-1071: Implement MeshSimulator with DHT-first + disaster mode support
- T-1072: Write integration-soulseek tests
- T-1073: Write integration-mesh tests (DHT-heavy + NAT edge cases)
- **Deliverable**: Comprehensive test coverage including experimental features

### Stage 9: Final Cleanup (Week 12)
- T-1080: Remove dead code
- T-1081: Normalize naming
- T-1082: Move narrative comments to docs
- T-1083: Collapse forwarding classes
- **Deliverable**: Production-ready architecture

---

## 10. Success Criteria

### Code Quality Metrics
- **Coupling**: No circular dependencies between modules
- **Cohesion**: Each module has single responsibility
- **Testability**: All services mockable via interfaces
- **Configuration**: Zero magic strings, all typed options

### Performance Metrics
- **Swarm**: Chunk scheduling latency < 10ms
- **Mesh**: Peer discovery < 2s
- **Brainz**: MB API calls cached, rate-limited
- **Security**: Policy evaluation < 5ms

### Maintainability
- **New feature**: Can add without touching 5+ files
- **Bug fix**: Can isolate to single module
- **Testing**: Integration tests pass in < 5 minutes
- **Onboarding**: New dev understands architecture in < 1 day

---

*Phase 8 specification complete. Ready for systematic refactoring.*
