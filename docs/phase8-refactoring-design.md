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
  ├── DHT/             # DHT bootstrap / nodes
  ├── Overlay/         # Mesh overlay connections
  ├── Gossip/          # Hash / availability gossip
  ├── Transport/       # NAT, UPnP, TLS
  └── Directory/       # IMeshDirectory abstraction

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

// Mesh
public interface IMeshDirectory
{
    Task<IEnumerable<MeshPeerInfo>> FindPeersByKeyAsync(string key, int maxResults, CancellationToken ct);
    Task PublishAvailabilityAsync(string key, MeshPeerInfo self, CancellationToken ct);
    IAsyncEnumerable<MeshEvent> SubscribeAsync(CancellationToken ct);
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

### 3.1. Narrow IMeshDirectory

**Current Problem**: Mesh does too much - DHT, TLS, sync, greetings, NAT.

**Solution**: Treat mesh as directory + message bus:

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

### 3.2. Isolate Transport Ugliness

**Solution**: `MeshTransportService` handles UPnP, NAT-PMP, TLS, reconnects:

```csharp
internal class MeshTransportService
{
    private readonly INatDiscoveryService _nat;
    private readonly ITlsProvisioningService _tls;
    private readonly IDhtBootstrapService _dht;
    
    internal async Task<IMeshConnection> EstablishConnectionAsync(
        IPEndPoint remote, 
        CancellationToken ct)
    {
        // Handle NAT traversal, TLS handshake, etc.
    }
}
```

**Exposed only through**: `IMeshDirectory` and `IMeshTransportMetrics` (for UI).

---

## 4. Brainz: Turn into Pipeline

### 4.1. Job Pipeline

**Current Problem**: Fire-and-forget async calls for MB/Brainz scattered everywhere.

**Solution**: Formal job abstraction:

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
    
    public async Task RunAsync(CancellationToken ct)
    {
        var release = await _catalog.GetReleaseAsync(MusicBrainzReleaseId, ct);
        
        foreach (var track in release.Tracks)
        {
            // Discover sources, start swarm download
        }
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

## 5. Security: Policy Engine

### 5.1. Unified Security Abstraction

**Current Problem**: Security checks (NetworkGuard, ViolationTracker, PathGuard, ContentSafety, etc.) interleaved in controllers.

**Solution**: Policy engine:

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
```

---

## 6. Configuration: Strongly Typed Options

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

## 7. Testability & Simulation

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
    public async Task Should_Discover_Peers_Via_DHT()
    {
        var sim = new MeshSimulator();
        var alice = sim.CreateNode("alice", inventory: aliceLibrary);
        var bob = sim.CreateNode("bob", inventory: bobLibrary);
        var carol = sim.CreateNode("carol", inventory: new());
        
        sim.ConnectNodes(alice, bob, carol);
        
        var peers = await carol.Mesh.FindPeersByKeyAsync("mbid:abc-123", maxResults: 10, CancellationToken.None);
        
        Assert.Contains(peers, p => p.Id == alice.Id);
    }
}
```

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

## 9. Implementation Order (Concrete Staging)

### Stage 1: Module Boundaries (Weeks 1-2)
- T-1000: Create namespace structure (Swarm, Mesh, Security, Brainz, Integrations)
- T-1001: Define core interfaces (ISwarmDownloadService, IMeshDirectory, etc.)
- T-1002: Move types into namespaces

### Stage 2: Swarm Refactor (Weeks 3-4)
- T-1010: Implement SwarmDownloadOrchestrator as BackgroundService
- T-1011: Create SwarmJob/SwarmFile/SwarmChunk model
- T-1012: Implement IVerificationEngine
- T-1013: Replace ad-hoc Task.Run with orchestrator
- T-1014: Centralize chunk verification

### Stage 3: Mesh Refactor (Weeks 5-6)
- T-1020: Implement IMeshDirectory abstraction
- T-1021: Create MeshTransportService (NAT, TLS, reconnect)
- T-1022: Hide DHT/overlay behind IMeshDirectory
- T-1023: Event-based mesh notifications

### Stage 4: Brainz Pipeline (Weeks 7-8)
- T-1030: Implement IMetadataJob abstraction
- T-1031: Create MetadataJobRunner as BackgroundService
- T-1032: Implement codec analyzers (FLAC, MP3, Opus, AAC)
- T-1033: Create unified BrainzClient with caching/rate-limiting
- T-1034: Convert fire-and-forget calls to job enqueueing

### Stage 5: Security Engine (Week 9)
- T-1040: Implement ISecurityPolicyEngine
- T-1041: Create CompositeSecurityPolicy
- T-1042: Implement individual policies (NetworkGuard, Reputation, Consensus, ContentSafety)
- T-1043: Replace inline security checks with policy engine

### Stage 6: Configuration (Week 10)
- T-1050: Create strongly-typed options classes
- T-1051: Wire options via IOptions<T>
- T-1052: Remove direct IConfiguration access

### Stage 7: Testability (Week 11)
- T-1060: Eliminate static singletons
- T-1061: Add interfaces for all major subsystems
- T-1062: Constructor injection cleanup

### Stage 8: Test Infrastructure (Week 12)
- T-1070: Implement Soulfind test harness
- T-1071: Implement MeshSimulator
- T-1072: Write integration-soulseek test suite
- T-1073: Write integration-mesh test suite

### Stage 9: Cleanup (Ongoing)
- T-1080: Remove dead code and unused concepts
- T-1081: Normalize naming across codebase
- T-1082: Move narrative comments to docs
- T-1083: Collapse forwarding classes

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
