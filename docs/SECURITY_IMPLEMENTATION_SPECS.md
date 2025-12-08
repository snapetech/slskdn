# Security Implementation Specifications

> Detailed implementation plans for each security feature. Detailed enough for rapid implementation.

---

## Quick Reference: Existing Files to Integrate With

Before implementing any feature, review these existing files from `experimental/multi-source-swarm`:

### Security Infrastructure (DhtRendezvous/Security/)

| File | Purpose | Integrate With |
|------|---------|----------------|
| `OverlayRateLimiter.cs` | Per-IP/connection rate limits | NetworkGuard |
| `MessageValidator.cs` | Message validation, bounds checking | NetworkGuard |
| `OverlayBlocklist.cs` | IP + username blocking | PeerReputation auto-block |
| `PeerDiversityChecker.cs` | Anti-eclipse subnet checks | - |
| `PeerVerificationService.cs` | Soulseek UserInfo verification | PeerReputation |
| `SecureMessageFramer.cs` | Length-prefixed framing | - |
| `CertificateManager.cs` | TLS certificate handling | - |
| `OverlayTimeouts.cs` | Configurable timeouts | NetworkGuard |

### Database Infrastructure (HashDb/)

| Table | Existing Columns | Security Extensions |
|-------|------------------|---------------------|
| `Peers` | peer_id, caps, client_version, last_seen | Add: reputation_score, violations, blocked |
| `FlacInventory` | file_id, peer_id, path, size, hash_status | - |
| `HashDb` | flac_key, byte_hash, size, use_count | - |
| `MeshPeerState` | peer_id, last_seq_seen | - |

### Transfer Infrastructure

| File | Purpose | Integrate With |
|------|---------|----------------|
| `ContentVerificationService.cs` | SHA256 first-32KB verification | Byzantine, Commitment |
| `MultiSourceDownloadService.cs` | Multi-source downloads | Byzantine, Proof-of-Storage |
| `SourceRankingDbContext.cs` | Success/failure per user | PeerReputation |

### Core Infrastructure

| File | Purpose | Integrate With |
|------|---------|----------------|
| `Blacklist.cs` | CIDR IP blocklisting | Honeypots, ThreatIntel |
| `SecurityService.cs` | JWT, API key auth | ParanoidMode API |

---

## Table of Contents

- [1.1 PathGuard](#11-pathguard)
- [1.2 Network Guard Enhancement](#12-network-guard-enhancement)
- [1.3 Privacy Mode](#13-privacy-mode)
- [1.4 Content Safety](#14-content-safety)
- [2.1 Peer Reputation](#21-peer-reputation)
- [2.2 Cryptographic Commitment](#22-cryptographic-commitment)
- [2.3 Proof-of-Storage](#23-proof-of-storage)
- [2.4 Byzantine Consensus](#24-byzantine-consensus)
- [3.1 Probabilistic Verification](#31-probabilistic-verification)
- [3.2 Temporal Consistency](#32-temporal-consistency)
- [3.3 Canary Traps](#33-canary-traps)
- [3.4 Asymmetric Disclosure](#34-asymmetric-disclosure)
- [4.1 Fingerprint Detection](#41-fingerprint-detection)
- [4.2 Dead Reckoning](#42-dead-reckoning)
- [4.3 Honeypots](#43-honeypots)
- [4.4 Entropy Monitoring](#44-entropy-monitoring)
- [4.5 Paranoid Mode](#45-paranoid-mode)

---

## 1.1 PathGuard

### File Location
```
src/slskd/Common/Security/PathGuard.cs
```

### Dependencies
- None (standalone utility)

### Interface

```csharp
namespace slskd.Common.Security;

/// <summary>
/// Validates and sanitizes file paths to prevent directory traversal attacks.
/// ALL paths derived from peer/server input MUST go through this.
/// </summary>
public static class PathGuard
{
    /// <summary>
    /// Maximum allowed path length.
    /// </summary>
    public const int MaxPathLength = 4096;
    
    /// <summary>
    /// Maximum allowed filename length.
    /// </summary>
    public const int MaxFilenameLength = 255;
    
    /// <summary>
    /// Characters forbidden in filenames.
    /// </summary>
    public static readonly char[] ForbiddenChars = { '<', '>', ':', '"', '|', '?', '*', '\0' };
    
    /// <summary>
    /// Normalize and validate a peer-supplied path against a root directory.
    /// </summary>
    /// <param name="peerPath">The untrusted path from peer/server.</param>
    /// <param name="root">The trusted root directory all paths must be under.</param>
    /// <returns>Safe absolute path, or null if validation fails.</returns>
    public static string? NormalizeAndValidate(string peerPath, string root);
    
    /// <summary>
    /// Sanitize a filename for safe filesystem use.
    /// Removes dangerous characters, normalizes unicode, truncates length.
    /// </summary>
    public static string SanitizeFilename(string filename);
    
    /// <summary>
    /// Check if a path is safely contained within root (no escape possible).
    /// </summary>
    public static bool IsContainedIn(string path, string root);
    
    /// <summary>
    /// Validate a path doesn't contain traversal sequences.
    /// </summary>
    public static bool ContainsTraversal(string path);
    
    /// <summary>
    /// Normalize unicode to prevent homoglyph attacks.
    /// </summary>
    public static string NormalizeUnicode(string input);
}
```

### Implementation Details

```csharp
public static string? NormalizeAndValidate(string peerPath, string root)
{
    // 1. Null/empty check
    if (string.IsNullOrWhiteSpace(peerPath) || string.IsNullOrWhiteSpace(root))
        return null;
    
    // 2. Length check BEFORE any processing
    if (peerPath.Length > MaxPathLength)
        return null;
    
    // 3. Normalize unicode (NFC form)
    peerPath = NormalizeUnicode(peerPath);
    
    // 4. Check for null bytes (can truncate paths in some systems)
    if (peerPath.Contains('\0'))
        return null;
    
    // 5. Check for explicit traversal attempts
    if (ContainsTraversal(peerPath))
        return null;
    
    // 6. Reject absolute paths from peers
    if (Path.IsPathRooted(peerPath))
        return null;
    
    // 7. Combine with root and get full path
    var combined = Path.Combine(root, peerPath);
    var fullPath = Path.GetFullPath(combined);
    var fullRoot = Path.GetFullPath(root);
    
    // 8. Ensure result is still under root (handles symlinks, etc.)
    if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar) && 
        fullPath != fullRoot)
        return null;
    
    // 9. Final length check after normalization
    if (fullPath.Length > MaxPathLength)
        return null;
    
    return fullPath;
}

public static bool ContainsTraversal(string path)
{
    // Check for various traversal patterns
    var dangerous = new[] { "..", "..\\", "../", "..%2f", "..%5c", "%2e%2e" };
    var lower = path.ToLowerInvariant();
    return dangerous.Any(d => lower.Contains(d));
}

public static string SanitizeFilename(string filename)
{
    if (string.IsNullOrWhiteSpace(filename))
        return "unnamed";
    
    // Normalize unicode
    filename = NormalizeUnicode(filename);
    
    // Remove path separators (prevent hidden paths in filenames)
    filename = filename.Replace('/', '_').Replace('\\', '_');
    
    // Remove forbidden characters
    foreach (var c in ForbiddenChars)
        filename = filename.Replace(c, '_');
    
    // Remove control characters
    filename = new string(filename.Where(c => !char.IsControl(c)).ToArray());
    
    // Truncate
    if (filename.Length > MaxFilenameLength)
        filename = filename[..MaxFilenameLength];
    
    // Don't allow names that are only dots/spaces
    filename = filename.Trim('.', ' ');
    
    return string.IsNullOrWhiteSpace(filename) ? "unnamed" : filename;
}

public static string NormalizeUnicode(string input)
{
    // NFC normalization prevents homoglyph attacks
    return input.Normalize(NormalizationForm.FormC);
}
```

### Integration Points

1. **Downloads** - `TransferService.cs` when determining output path
2. **Shares** - `ShareService.cs` when validating share roots
3. **Browse** - `UserService.cs` when serving browse requests
4. **Relay** - `RelayController.cs` when handling file paths

### Integration Example

```csharp
// In TransferService.cs or DownloadService.cs
private string GetSafeDownloadPath(string peerFilename, string downloadRoot)
{
    var safePath = PathGuard.NormalizeAndValidate(peerFilename, downloadRoot);
    if (safePath == null)
    {
        Log.Warning("Rejected unsafe path from peer: {Path}", peerFilename);
        throw new InvalidPathException($"Path validation failed: {peerFilename}");
    }
    return safePath;
}
```

### Tests Required

```csharp
[Theory]
[InlineData("../etc/passwd", false)]
[InlineData("..\\windows\\system32", false)]
[InlineData("normal/path/file.flac", true)]
[InlineData("folder/../../../etc/passwd", false)]
[InlineData("/absolute/path", false)]
[InlineData("C:\\absolute\\path", false)]
[InlineData("file\0.txt", false)]
[InlineData("normal.flac", true)]
[InlineData("Artist - Album/01 - Track.flac", true)]
public void NormalizeAndValidate_HandlesTraversal(string input, bool shouldPass)
{
    var result = PathGuard.NormalizeAndValidate(input, "/safe/root");
    Assert.Equal(shouldPass, result != null);
}
```

---

## 1.2 Network Guard Enhancement

**Type:** 🟡 EXTEND existing infrastructure

### File Locations
```
src/slskd/DhtRendezvous/Security/NetworkGuard.cs (NEW - unifies existing)
src/slskd/DhtRendezvous/Security/ConnectionAging.cs (NEW)
```

### Existing Files to Integrate
```
src/slskd/DhtRendezvous/Security/OverlayRateLimiter.cs  ← WRAP, don't replace
src/slskd/DhtRendezvous/Security/MessageValidator.cs    ← USE for validation
src/slskd/DhtRendezvous/Security/OverlayBlocklist.cs    ← USE for blocking
src/slskd/DhtRendezvous/Security/OverlayTimeouts.cs     ← USE for timeouts
```

### Dependencies
- `OverlayRateLimiter.cs` (existing) - **wrap, don't duplicate**
- `MessageValidator.cs` (existing) - **use, don't duplicate**
- `OverlayBlocklist.cs` (existing) - **use for blocking actions**

### Interface

```csharp
namespace slskd.DhtRendezvous.Security;

/// <summary>
/// Centralized network guard that validates all incoming data.
/// Sits between socket layer and message processing.
/// </summary>
public class NetworkGuard
{
    private readonly OverlayRateLimiter _rateLimiter;
    private readonly ConnectionAgeTracker _ageTracker;
    private readonly ILogger<NetworkGuard> _logger;
    
    // Configurable limits
    public int MaxSearchResults { get; set; } = 10000;
    public int MaxQueuedFiles { get; set; } = 5000;
    public int MaxFolderDepth { get; set; } = 50;
    public int MaxFilesPerFolder { get; set; } = 10000;
    public int MaxMessageSizeBytes { get; set; } = 4 * 1024 * 1024; // 4MB
    
    /// <summary>
    /// Validate raw message bytes before deserialization.
    /// </summary>
    public GuardResult ValidateRawMessage(byte[] data, string peerId);
    
    /// <summary>
    /// Validate a deserialized message.
    /// </summary>
    public GuardResult ValidateMessage<T>(T message, string peerId);
    
    /// <summary>
    /// Check if a peer should be allowed to send more data.
    /// </summary>
    public GuardResult CheckPeerAllowance(string peerId, int messageSize);
    
    /// <summary>
    /// Record a violation and potentially block the peer.
    /// </summary>
    public void RecordViolation(string peerId, ViolationType type, string details);
    
    /// <summary>
    /// Get trust level based on connection age and behavior.
    /// </summary>
    public TrustLevel GetTrustLevel(string peerId);
}

public enum ViolationType
{
    OversizedMessage,
    MalformedMessage,
    RateLimitExceeded,
    InvalidProtocol,
    SuspiciousPattern,
    ReplayAttempt
}

public enum TrustLevel
{
    Untrusted,    // New connection, <1 hour
    Low,          // 1-24 hours, no violations
    Medium,       // 1-7 days, good history
    High,         // 7+ days, excellent history
    Blocked       // Too many violations
}

public readonly struct GuardResult
{
    public bool IsAllowed { get; init; }
    public string? Reason { get; init; }
    public ViolationType? ViolationType { get; init; }
    
    public static GuardResult Allow() => new() { IsAllowed = true };
    public static GuardResult Deny(string reason, ViolationType type) => 
        new() { IsAllowed = false, Reason = reason, ViolationType = type };
}
```

### Connection Age Tracker

```csharp
/// <summary>
/// Tracks connection age and builds trust over time.
/// New connections get less privilege than established ones.
/// </summary>
public class ConnectionAgeTracker
{
    private readonly ConcurrentDictionary<string, ConnectionProfile> _profiles = new();
    
    public record ConnectionProfile
    {
        public DateTimeOffset FirstSeen { get; init; }
        public DateTimeOffset LastSeen { get; set; }
        public int TotalMessages { get; set; }
        public int Violations { get; set; }
        public TimeSpan Age => DateTimeOffset.UtcNow - FirstSeen;
    }
    
    public ConnectionProfile GetOrCreate(string peerId);
    public void RecordActivity(string peerId);
    public void RecordViolation(string peerId);
    public TrustLevel CalculateTrust(string peerId);
}
```

### Implementation Notes

```csharp
public GuardResult ValidateRawMessage(byte[] data, string peerId)
{
    // 1. Size check before any parsing
    if (data.Length > MaxMessageSizeBytes)
    {
        RecordViolation(peerId, ViolationType.OversizedMessage, 
            $"Size {data.Length} > {MaxMessageSizeBytes}");
        return GuardResult.Deny("Message too large", ViolationType.OversizedMessage);
    }
    
    // 2. Check rate limit
    var rateResult = _rateLimiter.CheckMessage(peerId);
    if (!rateResult.IsAllowed)
    {
        RecordViolation(peerId, ViolationType.RateLimitExceeded, rateResult.Reason);
        return GuardResult.Deny(rateResult.Reason, ViolationType.RateLimitExceeded);
    }
    
    // 3. Check trust level allows this size
    var trust = GetTrustLevel(peerId);
    var allowedSize = trust switch
    {
        TrustLevel.Untrusted => 64 * 1024,     // 64KB
        TrustLevel.Low => 256 * 1024,          // 256KB
        TrustLevel.Medium => 1024 * 1024,      // 1MB
        _ => MaxMessageSizeBytes
    };
    
    if (data.Length > allowedSize)
    {
        return GuardResult.Deny($"Message too large for trust level {trust}", 
            ViolationType.OversizedMessage);
    }
    
    _ageTracker.RecordActivity(peerId);
    return GuardResult.Allow();
}
```

### Integration Points

1. **MeshOverlayConnection.cs** - Wrap all read operations
2. **MeshOverlayServer.cs** - Check before accepting connections
3. **DhtRendezvousService.cs** - Validate all DHT messages

---

## 1.3 Privacy Mode

### File Location
```
src/slskd/Core/Privacy/PrivacyService.cs
src/slskd/Core/Options.Privacy.cs (add to Options.cs)
```

### Configuration

```yaml
# In slskd.yml
privacy:
  enabled: true
  minimize_metadata: true
  generic_client_string: true
  avoid_public_rooms: true
  randomize_instance_id: true
  strip_local_paths: true
```

### Interface

```csharp
namespace slskd.Core.Privacy;

public class PrivacyOptions
{
    public bool Enabled { get; set; } = false;
    public bool MinimizeMetadata { get; set; } = true;
    public bool GenericClientString { get; set; } = true;
    public bool AvoidPublicRooms { get; set; } = true;
    public bool RandomizeInstanceId { get; set; } = true;
    public bool StripLocalPaths { get; set; } = true;
}

public class PrivacyService
{
    private readonly PrivacyOptions _options;
    private readonly string _sessionInstanceId;
    
    public PrivacyService(IOptions<PrivacyOptions> options)
    {
        _options = options.Value;
        _sessionInstanceId = _options.RandomizeInstanceId 
            ? Guid.NewGuid().ToString("N")[..8] 
            : "slskdn";
    }
    
    /// <summary>
    /// Get the client string to send to server/peers.
    /// </summary>
    public string GetClientString()
    {
        if (_options.GenericClientString)
            return "slskd";  // Generic, no version
        return $"slskdn {Program.Version}";
    }
    
    /// <summary>
    /// Get instance identifier for this session.
    /// </summary>
    public string GetInstanceId() => _sessionInstanceId;
    
    /// <summary>
    /// Sanitize a path before sending to peers (strip local info).
    /// </summary>
    public string SanitizePath(string localPath)
    {
        if (!_options.StripLocalPaths)
            return localPath;
        
        // Convert "/home/user/Music/Artist/Album/file.flac" 
        // to "Artist/Album/file.flac"
        // Strip everything before the share root
        return StripToShareRelative(localPath);
    }
    
    /// <summary>
    /// Check if we should auto-join public rooms.
    /// </summary>
    public bool ShouldJoinPublicRooms() => !_options.AvoidPublicRooms;
    
    /// <summary>
    /// Filter user description to remove identifying info.
    /// </summary>
    public string SanitizeDescription(string description)
    {
        if (!_options.MinimizeMetadata)
            return description;
        
        // Remove potential PII patterns (emails, IPs, hostnames)
        return PiiScrubber.Scrub(description);
    }
}
```

### Integration Points

1. **Application.cs** - Use `GetClientString()` when connecting
2. **RoomService.cs** - Check `ShouldJoinPublicRooms()` before auto-join
3. **ShareService.cs** - Use `SanitizePath()` when building share responses
4. **UserService.cs** - Use `SanitizeDescription()` for user info

---

## 1.4 Content Safety

### File Location
```
src/slskd/Common/Security/ContentSafety.cs
src/slskd/Common/Security/MagicBytes.cs
```

### Interface

```csharp
namespace slskd.Common.Security;

public static class MagicBytes
{
    public static readonly IReadOnlyDictionary<string, byte[][]> Signatures = new Dictionary<string, byte[][]>
    {
        // Audio
        ["flac"] = new[] { new byte[] { 0x66, 0x4C, 0x61, 0x43 } },  // "fLaC"
        ["mp3"] = new[] { 
            new byte[] { 0x49, 0x44, 0x33 },  // ID3
            new byte[] { 0xFF, 0xFB },        // MP3 sync
            new byte[] { 0xFF, 0xFA },
            new byte[] { 0xFF, 0xF3 },
            new byte[] { 0xFF, 0xF2 }
        },
        ["ogg"] = new[] { new byte[] { 0x4F, 0x67, 0x67, 0x53 } },   // "OggS"
        ["wav"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } },   // "RIFF"
        ["m4a"] = new[] { new byte[] { 0x00, 0x00, 0x00 } },         // ftyp (offset 4)
        ["aac"] = new[] { new byte[] { 0xFF, 0xF1 }, new byte[] { 0xFF, 0xF9 } },
        
        // Archives (suspicious for music)
        ["zip"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        ["rar"] = new[] { new byte[] { 0x52, 0x61, 0x72, 0x21 } },
        ["7z"] = new[] { new byte[] { 0x37, 0x7A, 0xBC, 0xAF } },
        
        // Executables (dangerous)
        ["exe"] = new[] { new byte[] { 0x4D, 0x5A } },               // MZ
        ["elf"] = new[] { new byte[] { 0x7F, 0x45, 0x4C, 0x46 } },   // .ELF
        ["macho"] = new[] { 
            new byte[] { 0xFE, 0xED, 0xFA, 0xCE },  // Mach-O 32
            new byte[] { 0xFE, 0xED, 0xFA, 0xCF },  // Mach-O 64
            new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }   // Universal
        },
        
        // Scripts (dangerous)
        ["script"] = new[] { 
            Encoding.ASCII.GetBytes("#!/"),
            Encoding.ASCII.GetBytes("<?php"),
            Encoding.UTF8.GetBytes("\xEF\xBB\xBF#!/")  // BOM + shebang
        }
    };
}

public enum ContentVerdict
{
    Safe,           // Extension matches content
    Mismatch,       // Extension doesn't match content type
    Suspicious,     // Known dangerous type
    Unknown         // Couldn't determine
}

public record ContentAnalysis
{
    public ContentVerdict Verdict { get; init; }
    public string ExpectedType { get; init; }      // From extension
    public string? DetectedType { get; init; }     // From magic bytes
    public string? Warning { get; init; }
}

public class ContentSafetyService
{
    private readonly ILogger<ContentSafetyService> _logger;
    private readonly string _quarantinePath;
    
    /// <summary>
    /// Analyze file content against its extension.
    /// </summary>
    public ContentAnalysis Analyze(string filename, byte[] header);
    
    /// <summary>
    /// Analyze a file on disk.
    /// </summary>
    public async Task<ContentAnalysis> AnalyzeFileAsync(string filepath);
    
    /// <summary>
    /// Detect content type from magic bytes.
    /// </summary>
    public string? DetectContentType(byte[] header);
    
    /// <summary>
    /// Check if content type is executable/dangerous.
    /// </summary>
    public bool IsDangerous(string? contentType);
    
    /// <summary>
    /// Move suspicious file to quarantine.
    /// </summary>
    public async Task QuarantineAsync(string filepath, string reason);
}
```

### Implementation

```csharp
public ContentAnalysis Analyze(string filename, byte[] header)
{
    var extension = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
    var detectedType = DetectContentType(header);
    
    // Check for dangerous content regardless of extension
    if (IsDangerous(detectedType))
    {
        return new ContentAnalysis
        {
            Verdict = ContentVerdict.Suspicious,
            ExpectedType = extension,
            DetectedType = detectedType,
            Warning = $"Dangerous content type detected: {detectedType}"
        };
    }
    
    // Check for mismatch
    if (detectedType != null && detectedType != extension)
    {
        // Some mismatches are okay (mp3 vs id3)
        if (!IsAcceptableMismatch(extension, detectedType))
        {
            return new ContentAnalysis
            {
                Verdict = ContentVerdict.Mismatch,
                ExpectedType = extension,
                DetectedType = detectedType,
                Warning = $"Extension .{extension} but content is {detectedType}"
            };
        }
    }
    
    return new ContentAnalysis
    {
        Verdict = ContentVerdict.Safe,
        ExpectedType = extension,
        DetectedType = detectedType ?? extension
    };
}

public string? DetectContentType(byte[] header)
{
    if (header.Length < 4)
        return null;
    
    foreach (var (type, signatures) in MagicBytes.Signatures)
    {
        foreach (var sig in signatures)
        {
            if (header.Length >= sig.Length && 
                header.AsSpan(0, sig.Length).SequenceEqual(sig))
            {
                return type;
            }
        }
    }
    
    return null;
}

public bool IsDangerous(string? contentType)
{
    return contentType is "exe" or "elf" or "macho" or "script";
}
```

### Integration Points

1. **TransferService.cs** - Check after download completes
2. **MultiSourceDownloadService.cs** - Verify chunks aren't executables
3. **Add post-download hook in DownloadService**

---

## 2.1 Peer Reputation

**Type:** 🟡 EXTEND existing infrastructure

### File Locations
```
src/slskd/Users/Reputation/PeerReputationService.cs (NEW)
src/slskd/Users/Reputation/PeerProfile.cs (NEW - extends existing Peers table)
```

### Existing Files to Integrate
```
src/slskd/HashDb/HashDbService.cs                      ← Peers table exists here
src/slskd/Transfers/Ranking/SourceRankingDbContext.cs  ← Success/failure counts
src/slskd/DhtRendezvous/Security/OverlayBlocklist.cs   ← Use for auto-blocking
```

### DO NOT create ReputationDbContext - extend existing tables instead!

### Database Schema

```csharp
public class PeerProfile
{
    [Key]
    public string Username { get; set; }
    
    // Counters
    public int SuccessfulTransfers { get; set; }
    public int FailedTransfers { get; set; }
    public int AbortedTransfers { get; set; }
    public int MalformedMessages { get; set; }
    public int ProtocolViolations { get; set; }
    public int TimeoutCount { get; set; }
    
    // Timing
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public DateTimeOffset? LastSuccess { get; set; }
    public DateTimeOffset? LastFailure { get; set; }
    
    // Computed
    public double Score { get; set; }
    public bool IsBlocked { get; set; }
    public DateTimeOffset? BlockedUntil { get; set; }
    public string? BlockReason { get; set; }
    
    // Metadata
    public long TotalBytesDownloaded { get; set; }
    public long TotalBytesUploaded { get; set; }
    public double AverageSpeed { get; set; }
}
```

### Interface

```csharp
public interface IPeerReputationService
{
    Task<PeerProfile> GetProfileAsync(string username);
    Task<double> GetScoreAsync(string username);
    Task<bool> IsBlockedAsync(string username);
    
    // Recording events
    Task RecordSuccessAsync(string username, long bytes, double speed);
    Task RecordFailureAsync(string username, FailureType type, string? details = null);
    Task RecordViolationAsync(string username, ViolationType type, string? details = null);
    
    // Blocking
    Task BlockAsync(string username, TimeSpan duration, string reason);
    Task UnblockAsync(string username);
    
    // Queries
    Task<IEnumerable<PeerProfile>> GetTopPeersAsync(int count);
    Task<IEnumerable<PeerProfile>> GetBlockedPeersAsync();
    Task<IEnumerable<PeerProfile>> GetSuspiciousPeersAsync(double threshold);
}

public enum FailureType
{
    Timeout,
    Disconnected,
    TransferAborted,
    FileNotFound,
    QueueFull,
    Banned,
    Unknown
}
```

### Scoring Algorithm

```csharp
public double CalculateScore(PeerProfile profile)
{
    const double BaseScore = 50.0;
    const double SuccessWeight = 2.0;
    const double FailureWeight = -5.0;
    const double AbortWeight = -3.0;
    const double ViolationWeight = -20.0;
    const double MalformedWeight = -10.0;
    const double TimeDecay = 0.95;  // Per week
    
    var ageWeeks = (DateTimeOffset.UtcNow - profile.FirstSeen).TotalDays / 7.0;
    var recencyFactor = Math.Pow(TimeDecay, Math.Max(0, ageWeeks - 1));
    
    var rawScore = BaseScore
        + (profile.SuccessfulTransfers * SuccessWeight)
        + (profile.FailedTransfers * FailureWeight)
        + (profile.AbortedTransfers * AbortWeight)
        + (profile.ProtocolViolations * ViolationWeight)
        + (profile.MalformedMessages * MalformedWeight);
    
    // Apply recency decay to negative events
    var decayedScore = rawScore * recencyFactor;
    
    // Clamp to 0-100
    return Math.Clamp(decayedScore, 0, 100);
}
```

### Integration Points

1. **TransferService** - `OnTransferCompleted`, `OnTransferFailed`
2. **MultiSourceDownloadService** - Source selection
3. **SearchService** - Result ranking
4. **MeshOverlayConnection** - Protocol violations

---

## 2.2 Cryptographic Commitment

### File Location
```
src/slskd/Transfers/Security/CommitmentProtocol.cs
src/slskd/DhtRendezvous/Messages/CommitmentMessages.cs
```

### Protocol Flow

```
1. Requester → Provider: "I want file X"
2. Provider → Requester: COMMIT { hash: SHA256(content_hash || nonce) }
3. [Transfer happens]
4. Provider → Requester: REVEAL { content_hash, nonce }
5. Requester verifies:
   a. SHA256(content_hash || nonce) == commitment
   b. SHA256(downloaded_file) == content_hash
```

### Messages

```csharp
namespace slskd.DhtRendezvous.Messages;

public class CommitmentRequestMessage : MeshMessage
{
    public override string Type => "commitment_request";
    public string Filename { get; set; }
    public long FileSize { get; set; }
}

public class CommitmentMessage : MeshMessage
{
    public override string Type => "commitment";
    public string Filename { get; set; }
    public string CommitmentHash { get; set; }  // SHA256(content_hash || nonce)
    public DateTimeOffset Timestamp { get; set; }
}

public class CommitmentRevealMessage : MeshMessage
{
    public override string Type => "commitment_reveal";
    public string Filename { get; set; }
    public string ContentHash { get; set; }     // SHA256 of file
    public string Nonce { get; set; }           // Random 32 bytes, base64
}
```

### Service

```csharp
public class CommitmentService
{
    private readonly ConcurrentDictionary<string, PendingCommitment> _pending = new();
    
    public record PendingCommitment
    {
        public string Peer { get; init; }
        public string Filename { get; init; }
        public string CommitmentHash { get; init; }
        public DateTimeOffset ReceivedAt { get; init; }
        public string? RevealedContentHash { get; set; }
        public string? RevealedNonce { get; set; }
    }
    
    /// <summary>
    /// Request a commitment from a peer before download.
    /// </summary>
    public async Task<CommitmentMessage> RequestCommitmentAsync(
        string peer, 
        string filename,
        CancellationToken ct = default);
    
    /// <summary>
    /// Store a received commitment.
    /// </summary>
    public void RecordCommitment(string peer, CommitmentMessage commitment);
    
    /// <summary>
    /// Verify a reveal against stored commitment.
    /// </summary>
    public CommitmentVerifyResult VerifyReveal(
        string peer, 
        string filename, 
        CommitmentRevealMessage reveal);
    
    /// <summary>
    /// Verify downloaded content matches revealed hash.
    /// </summary>
    public async Task<bool> VerifyContentAsync(
        string peer,
        string filename,
        Stream content);
    
    /// <summary>
    /// Generate evidence of commitment violation.
    /// </summary>
    public CommitmentViolationEvidence GenerateEvidence(
        PendingCommitment commitment,
        CommitmentRevealMessage reveal,
        byte[] actualContent);
}

public enum CommitmentVerifyResult
{
    Valid,
    InvalidCommitment,    // Reveal doesn't match commitment
    InvalidContent,       // File doesn't match revealed hash
    NoCommitmentFound,
    Expired
}
```

### Integration

```csharp
// In download flow:
public async Task<byte[]> DownloadWithCommitmentAsync(string peer, string filename)
{
    // 1. Request commitment
    var commitment = await _commitmentService.RequestCommitmentAsync(peer, filename);
    _commitmentService.RecordCommitment(peer, commitment);
    
    // 2. Do download
    var content = await _transferService.DownloadAsync(peer, filename);
    
    // 3. Verify (peer should have sent reveal at end of transfer)
    var reveal = await _meshConnection.ReceiveRevealAsync(peer, filename);
    var result = _commitmentService.VerifyReveal(peer, filename, reveal);
    
    if (result != CommitmentVerifyResult.Valid)
    {
        var evidence = _commitmentService.GenerateEvidence(...);
        await _reputationService.RecordViolationAsync(peer, ViolationType.CommitmentBreach);
        throw new CommitmentViolationException(evidence);
    }
    
    // 4. Verify actual content
    if (!await _commitmentService.VerifyContentAsync(peer, filename, content))
    {
        // Peer lied about content hash
        throw new ContentMismatchException();
    }
    
    return content;
}
```

---

## 2.3 Proof-of-Storage

### File Location
```
src/slskd/Transfers/Security/StorageProofService.cs
src/slskd/DhtRendezvous/Messages/StorageProofMessages.cs
```

### Messages

```csharp
public class StorageChallengeMessage : MeshMessage
{
    public override string Type => "storage_challenge";
    public string Filename { get; set; }
    public List<ByteRange> Ranges { get; set; }  // Random ranges to prove
}

public class ByteRange
{
    public long Start { get; set; }
    public int Length { get; set; }  // Max 1024 bytes per range
}

public class StorageProofMessage : MeshMessage
{
    public override string Type => "storage_proof";
    public string Filename { get; set; }
    public List<RangeProof> Proofs { get; set; }
}

public class RangeProof
{
    public long Start { get; set; }
    public int Length { get; set; }
    public string Hash { get; set; }  // SHA256 of the bytes
}
```

### Service

```csharp
public class StorageProofService
{
    private readonly Random _random = new();
    
    /// <summary>
    /// Challenge a peer to prove they have a file.
    /// </summary>
    public async Task<StorageProofResult> ChallengeAsync(
        string username,
        string filename,
        long fileSize,
        int numChallenges = 3,
        CancellationToken ct = default)
    {
        // Generate random ranges
        var ranges = GenerateRandomRanges(fileSize, numChallenges);
        
        var challenge = new StorageChallengeMessage
        {
            Filename = filename,
            Ranges = ranges
        };
        
        var sw = Stopwatch.StartNew();
        
        // Send challenge, await proof
        var proof = await SendChallengeAsync(username, challenge, ct);
        
        sw.Stop();
        
        return new StorageProofResult
        {
            Username = username,
            Filename = filename,
            Challenges = ranges,
            Proofs = proof.Proofs,
            ResponseTimeMs = sw.ElapsedMilliseconds,
            IsValid = ValidateProof(ranges, proof.Proofs),
            IsSuspiciouslyFast = sw.ElapsedMilliseconds < 50,  // Likely cached/fake
            IsSuspiciouslySlow = sw.ElapsedMilliseconds > 5000 // Might be proxying
        };
    }
    
    private List<ByteRange> GenerateRandomRanges(long fileSize, int count)
    {
        var ranges = new List<ByteRange>();
        const int rangeSize = 1024;
        
        for (int i = 0; i < count; i++)
        {
            var maxStart = fileSize - rangeSize;
            var start = _random.NextInt64(0, maxStart);
            ranges.Add(new ByteRange { Start = start, Length = rangeSize });
        }
        
        return ranges;
    }
    
    /// <summary>
    /// Verify stored proofs against actual downloaded file.
    /// </summary>
    public async Task<bool> VerifyAgainstFileAsync(
        StorageProofResult proof,
        Stream file)
    {
        foreach (var challenge in proof.Challenges)
        {
            file.Seek(challenge.Start, SeekOrigin.Begin);
            var buffer = new byte[challenge.Length];
            await file.ReadExactlyAsync(buffer);
            
            var expectedHash = SHA256Hash(buffer);
            var actualProof = proof.Proofs.FirstOrDefault(p => p.Start == challenge.Start);
            
            if (actualProof?.Hash != expectedHash)
            {
                return false;  // Proof was fake
            }
        }
        
        return true;
    }
}

public record StorageProofResult
{
    public string Username { get; init; }
    public string Filename { get; init; }
    public List<ByteRange> Challenges { get; init; }
    public List<RangeProof> Proofs { get; init; }
    public long ResponseTimeMs { get; init; }
    public bool IsValid { get; init; }
    public bool IsSuspiciouslyFast { get; init; }
    public bool IsSuspiciouslySlow { get; init; }
}
```

---

## 2.4 Byzantine Consensus

**Type:** 🟡 EXTEND existing infrastructure

### File Location
```
src/slskd/Transfers/MultiSource/ByzantineConsensus.cs (NEW)
```

### Existing Files to Integrate
```
src/slskd/Transfers/MultiSource/ContentVerificationService.cs  ← SHA256 verification
src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs  ← Download framework
src/slskd/Users/Reputation/PeerReputationService.cs            ← Penalize liars
```

### Interface

```csharp
public class ByzantineChunkFetcher
{
    private readonly IPeerReputationService _reputation;
    private readonly ILogger _logger;
    
    /// <summary>
    /// Fetch a chunk with Byzantine fault tolerance.
    /// Requires consensus from multiple sources.
    /// </summary>
    public async Task<ChunkResult> FetchWithConsensusAsync(
        int chunkIndex,
        long chunkOffset,
        int chunkSize,
        IReadOnlyList<SourcePeer> sources,
        int requiredConsensus = 2,
        CancellationToken ct = default)
    {
        // Need at least N+1 sources for N consensus
        var sourcesToUse = sources.Take(requiredConsensus + 1).ToList();
        
        if (sourcesToUse.Count < requiredConsensus)
        {
            throw new InsufficientSourcesException(
                $"Need {requiredConsensus} sources, have {sourcesToUse.Count}");
        }
        
        // Fetch from all sources in parallel
        var tasks = sourcesToUse.Select(s => 
            FetchChunkFromSourceAsync(s, chunkOffset, chunkSize, ct));
        
        var results = await Task.WhenAll(tasks);
        
        // Group by hash
        var groups = results
            .Where(r => r.Success)
            .GroupBy(r => r.Hash)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        if (groups.Count == 0)
        {
            throw new AllSourcesFailedException();
        }
        
        var winningGroup = groups[0];
        
        if (winningGroup.Count() < requiredConsensus)
        {
            throw new InsufficientConsensusException(
                $"Best consensus: {winningGroup.Count()}/{requiredConsensus}");
        }
        
        // Record violations for liars
        foreach (var loserGroup in groups.Skip(1))
        {
            foreach (var liar in loserGroup)
            {
                _logger.Warning(
                    "Byzantine consensus: {Peer} provided different data for chunk {Chunk}",
                    liar.Source.Username, chunkIndex);
                
                await _reputation.RecordViolationAsync(
                    liar.Source.Username, 
                    ViolationType.DataManipulation,
                    $"Chunk {chunkIndex} hash mismatch");
            }
        }
        
        return new ChunkResult
        {
            ChunkIndex = chunkIndex,
            Data = winningGroup.First().Data,
            Hash = winningGroup.Key,
            ConsensusCount = winningGroup.Count(),
            TotalSources = results.Length,
            DisagreeingSources = results.Length - winningGroup.Count()
        };
    }
}

public record ChunkResult
{
    public int ChunkIndex { get; init; }
    public byte[] Data { get; init; }
    public string Hash { get; init; }
    public int ConsensusCount { get; init; }
    public int TotalSources { get; init; }
    public int DisagreeingSources { get; init; }
}
```

### Integration with MultiSourceDownloadService

```csharp
// In MultiSourceDownloadService.cs, option to use Byzantine mode:

public async Task DownloadAsync(MultiSourceRequest request)
{
    if (request.UseByzantineConsensus)
    {
        await DownloadWithByzantineConsensusAsync(request);
    }
    else
    {
        await DownloadStandardAsync(request);
    }
}

private async Task DownloadWithByzantineConsensusAsync(MultiSourceRequest request)
{
    var fetcher = new ByzantineChunkFetcher(_reputation, _logger);
    
    for (int i = 0; i < request.TotalChunks; i++)
    {
        var chunk = await fetcher.FetchWithConsensusAsync(
            i,
            i * request.ChunkSize,
            request.ChunkSize,
            request.Sources,
            requiredConsensus: 2);
        
        await WriteChunkAsync(request.OutputPath, chunk);
    }
}
```

---

## 3.1 Probabilistic Verification

### File Location
```
src/slskd/Transfers/Security/ProbabilisticVerifier.cs
```

### Implementation

```csharp
public class ProbabilisticVerifier
{
    private readonly IPeerReputationService _reputation;
    private readonly ConcurrentDictionary<string, VerificationLevel> _peerLevels = new();
    
    public enum VerificationLevel
    {
        Maximum,    // 100% full verification
        High,       // 80% full, 20% CRC
        Normal,     // 30% full, 50% CRC, 20% spot
        Low         // 10% full, 30% CRC, 60% spot
    }
    
    public enum VerificationStrategy
    {
        FullSha256,     // Full SHA256 of chunk
        Crc32,          // Quick CRC32
        SpotCheck,      // Check specific byte positions
        None            // Trust (rare)
    }
    
    /// <summary>
    /// Get verification strategy for a specific peer and chunk.
    /// Uses deterministic but secret randomness.
    /// </summary>
    public VerificationStrategy GetStrategy(string peer, int chunkIndex)
    {
        var level = GetPeerLevel(peer);
        var random = GetDeterministicRandom(peer, chunkIndex);
        
        return level switch
        {
            VerificationLevel.Maximum => VerificationStrategy.FullSha256,
            VerificationLevel.High => random < 0.8 
                ? VerificationStrategy.FullSha256 
                : VerificationStrategy.Crc32,
            VerificationLevel.Normal => random switch
            {
                < 0.3 => VerificationStrategy.FullSha256,
                < 0.8 => VerificationStrategy.Crc32,
                _ => VerificationStrategy.SpotCheck
            },
            VerificationLevel.Low => random switch
            {
                < 0.1 => VerificationStrategy.FullSha256,
                < 0.4 => VerificationStrategy.Crc32,
                _ => VerificationStrategy.SpotCheck
            },
            _ => VerificationStrategy.FullSha256
        };
    }
    
    private VerificationLevel GetPeerLevel(string peer)
    {
        return _peerLevels.GetOrAdd(peer, p =>
        {
            var score = _reputation.GetScoreAsync(p).Result;
            return score switch
            {
                < 30 => VerificationLevel.Maximum,
                < 50 => VerificationLevel.High,
                < 75 => VerificationLevel.Normal,
                _ => VerificationLevel.Low
            };
        });
    }
    
    /// <summary>
    /// Deterministic but secret random based on peer + chunk + daily secret.
    /// Peer can't predict which chunks will be heavily verified.
    /// </summary>
    private double GetDeterministicRandom(string peer, int chunkIndex)
    {
        var dailySecret = GetDailySecret();
        var input = $"{peer}:{chunkIndex}:{dailySecret}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return (double)BitConverter.ToUInt32(hash, 0) / uint.MaxValue;
    }
    
    /// <summary>
    /// Verify a chunk using the appropriate strategy.
    /// </summary>
    public async Task<VerificationResult> VerifyChunkAsync(
        string peer,
        int chunkIndex,
        byte[] data,
        string? expectedHash = null)
    {
        var strategy = GetStrategy(peer, chunkIndex);
        
        return strategy switch
        {
            VerificationStrategy.FullSha256 => VerifyFullSha256(data, expectedHash),
            VerificationStrategy.Crc32 => VerifyCrc32(data, expectedHash),
            VerificationStrategy.SpotCheck => VerifySpotCheck(data),
            _ => VerificationResult.Passed()
        };
    }
}
```

---

## 3.2 Temporal Consistency

### File Location
```
src/slskd/Users/Monitoring/TemporalConsistencyService.cs
src/slskd/Users/Monitoring/ShareSnapshot.cs
```

### Database Schema

```csharp
public class ShareSnapshot
{
    [Key]
    public int Id { get; set; }
    public string Username { get; set; }
    public DateTimeOffset TakenAt { get; set; }
    public int FileCount { get; set; }
    public int FolderCount { get; set; }
    public long TotalSize { get; set; }
    public string StructureHash { get; set; }  // Hash of directory structure
    public string? TopLevelFolders { get; set; }  // JSON array
}

public class FileObservation
{
    [Key]
    public int Id { get; set; }
    public string Username { get; set; }
    public string Filename { get; set; }
    public long Size { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public int TimesObserved { get; set; }
    public bool WasEverMissing { get; set; }  // Appeared, disappeared, reappeared
}
```

### Service

```csharp
public class TemporalConsistencyService
{
    /// <summary>
    /// Take a snapshot of a peer's shares for future comparison.
    /// </summary>
    public async Task RecordSnapshotAsync(string username, BrowseResponse shares);
    
    /// <summary>
    /// Analyze a peer's share history for suspicious patterns.
    /// </summary>
    public async Task<ConsistencyReport> AnalyzeAsync(string username)
    {
        var snapshots = await GetSnapshotsAsync(username);
        var observations = await GetObservationsAsync(username);
        
        return new ConsistencyReport
        {
            Username = username,
            
            // Churn rate: how often files appear/disappear
            ChurnRate = CalculateChurnRate(observations),
            
            // Size consistency: files that change size
            SizeInconsistencies = FindSizeChanges(observations),
            
            // Bait detection: files that appear then disappear repeatedly
            PotentialBaitFiles = FindBaitPatterns(observations),
            
            // Structure stability: folder reorganization frequency
            StructuralStability = CalculateStructuralStability(snapshots),
            
            // Overall suspicion score
            SuspicionScore = CalculateSuspicionScore(...)
        };
    }
    
    private double CalculateChurnRate(IEnumerable<FileObservation> observations)
    {
        var total = observations.Count();
        var volatile_ = observations.Count(o => o.WasEverMissing);
        return total > 0 ? (double)volatile_ / total : 0;
    }
    
    private IEnumerable<string> FindBaitPatterns(IEnumerable<FileObservation> observations)
    {
        // Files that appeared 3+ times but were also missing in between
        return observations
            .Where(o => o.TimesObserved >= 3 && o.WasEverMissing)
            .Select(o => o.Filename);
    }
}

public record ConsistencyReport
{
    public string Username { get; init; }
    public double ChurnRate { get; init; }
    public IEnumerable<(string Filename, long OldSize, long NewSize)> SizeInconsistencies { get; init; }
    public IEnumerable<string> PotentialBaitFiles { get; init; }
    public double StructuralStability { get; init; }  // 0-1, higher = more stable
    public double SuspicionScore { get; init; }  // 0-100
}
```

---

## 3.3 Canary Traps (Watermarking)

### File Location
```
src/slskd/Shares/Watermarking/WatermarkService.cs
src/slskd/Shares/Watermarking/FlacWatermarker.cs
src/slskd/Shares/Watermarking/WatermarkDb.cs
```

### Interface

```csharp
public interface IWatermarkService
{
    /// <summary>
    /// Watermark a file stream for a specific peer.
    /// Returns a new stream with imperceptible modifications.
    /// </summary>
    Task<Stream> WatermarkAsync(
        Stream original,
        string filename,
        string requestingPeer,
        CancellationToken ct = default);
    
    /// <summary>
    /// Check if a file contains our watermark.
    /// </summary>
    Task<WatermarkDetection?> DetectAsync(
        Stream file,
        string filename,
        CancellationToken ct = default);
}

public record WatermarkDetection
{
    public string OriginalPeer { get; init; }     // Who we sent it to
    public DateTimeOffset WatermarkedAt { get; init; }
    public string OriginalFilename { get; init; }
    public double Confidence { get; init; }        // 0-1
}
```

### FLAC Watermarking (LSB in samples)

```csharp
public class FlacWatermarker
{
    /// <summary>
    /// Embed watermark in FLAC audio by modifying LSBs.
    /// Completely inaudible, survives transcoding to lossy.
    /// </summary>
    public async Task<byte[]> EmbedWatermarkAsync(
        byte[] flacData,
        byte[] watermarkPayload)  // 32 bytes = peer ID hash
    {
        // 1. Decode FLAC to samples
        // 2. Spread watermark across samples using secret pattern
        // 3. Modify LSBs of selected samples
        // 4. Re-encode to FLAC
        
        // The pattern is deterministic from a secret key,
        // so we can detect it later
    }
    
    /// <summary>
    /// Extract watermark from FLAC audio.
    /// </summary>
    public async Task<byte[]?> ExtractWatermarkAsync(byte[] flacData)
    {
        // Reverse the embedding process
        // Use error correction to handle minor corruption
    }
}
```

### Watermark Database

```csharp
public class WatermarkRecord
{
    [Key]
    public int Id { get; set; }
    public string Peer { get; set; }
    public string Filename { get; set; }
    public string FileHash { get; set; }          // Original file
    public string WatermarkHash { get; set; }     // The watermark we embedded
    public DateTimeOffset CreatedAt { get; set; }
}
```

---

## 3.4 Asymmetric Disclosure

### File Location
```
src/slskd/Shares/GatedShareService.cs
```

### Implementation

```csharp
public class GatedShareService
{
    private readonly IShareService _shares;
    private readonly IPeerReputationService _reputation;
    private readonly ConcurrentDictionary<string, QueryState> _queryStates = new();
    
    public const int MaxPageSize = 100;
    public const int BaseDelayMs = 100;
    
    private record QueryState
    {
        public int QueryCount { get; set; }
        public DateTimeOffset LastQuery { get; set; }
    }
    
    /// <summary>
    /// Get shares with trust-based visibility and rate limiting.
    /// </summary>
    public async Task<GatedShareResponse> GetSharesAsync(
        string requestingPeer,
        int offset = 0,
        CancellationToken ct = default)
    {
        // 1. Update query state and apply rate limiting
        var state = _queryStates.AddOrUpdate(
            requestingPeer,
            _ => new QueryState { QueryCount = 1, LastQuery = DateTimeOffset.UtcNow },
            (_, s) => { s.QueryCount++; s.LastQuery = DateTimeOffset.UtcNow; return s; });
        
        // Exponential backoff delay
        var delay = Math.Min(BaseDelayMs * Math.Pow(2, state.QueryCount - 1), 10000);
        await Task.Delay((int)delay, ct);
        
        // 2. Determine trust level
        var score = await _reputation.GetScoreAsync(requestingPeer);
        var trustLevel = score switch
        {
            < 30 => TrustLevel.Untrusted,
            < 60 => TrustLevel.Low,
            < 85 => TrustLevel.Medium,
            _ => TrustLevel.High
        };
        
        // 3. Get all shares, filter by visibility
        var allShares = await _shares.GetAllSharesAsync();
        var visibleShares = allShares
            .Where(s => IsVisibleAt(s.Visibility, trustLevel))
            .Skip(offset)
            .Take(MaxPageSize)
            .ToList();
        
        var totalVisible = allShares.Count(s => IsVisibleAt(s.Visibility, trustLevel));
        
        return new GatedShareResponse
        {
            Shares = visibleShares,
            Offset = offset,
            TotalAvailable = totalVisible,
            HasMore = offset + MaxPageSize < totalVisible,
            NextOffset = offset + MaxPageSize,
            TrustLevel = trustLevel
        };
    }
    
    private bool IsVisibleAt(ShareVisibility visibility, TrustLevel trust)
    {
        return visibility switch
        {
            ShareVisibility.Public => true,
            ShareVisibility.Standard => trust >= TrustLevel.Low,
            ShareVisibility.Trusted => trust >= TrustLevel.Medium,
            ShareVisibility.Private => trust >= TrustLevel.High,
            _ => false
        };
    }
}

public enum ShareVisibility
{
    Public,     // Everyone sees
    Standard,   // Low trust and above
    Trusted,    // Medium trust and above
    Private     // High trust only
}
```

---

## 4.1 Fingerprint Detection

### File Location
```
src/slskd/DhtRendezvous/Security/FingerprintDetector.cs
```

### Implementation

```csharp
public class FingerprintDetector
{
    // Valid state transitions for normal clients
    private static readonly HashSet<(string, string)> ValidTransitions = new()
    {
        ("hello", "hello_ack"),
        ("ping", "pong"),
        ("delta_request", "delta_response"),
        // ... etc
    };
    
    // Known scanner patterns
    private static readonly List<ScannerSignature> KnownScanners = new()
    {
        new ScannerSignature
        {
            Name = "rapid_probe",
            Pattern = msg => msg.Count > 10 && 
                             msg.Average(m => m.IntervalMs) < 50
        },
        new ScannerSignature
        {
            Name = "invalid_sequence",
            Pattern = msg => msg.Pairwise()
                              .Count(p => !ValidTransitions.Contains(p)) > 3
        }
    };
    
    public FingerprintingRisk Analyze(IEnumerable<MessageRecord> recentMessages)
    {
        var messages = recentMessages.ToList();
        
        // Calculate metrics
        var transitions = messages.Pairwise().ToList();
        var invalidTransitionRatio = transitions.Count > 0
            ? transitions.Count(t => !ValidTransitions.Contains((t.Item1.Type, t.Item2.Type))) 
              / (double)transitions.Count
            : 0;
        
        var timingVariance = CalculateTimingVariance(messages);
        var knownMatches = KnownScanners
            .Where(s => s.Pattern(messages))
            .Select(s => s.Name)
            .ToList();
        
        return new FingerprintingRisk
        {
            InvalidTransitionRatio = invalidTransitionRatio,
            TimingRegularity = timingVariance > 0 ? 1.0 / timingVariance : 1.0,
            KnownScannerMatches = knownMatches,
            RiskLevel = CalculateRiskLevel(invalidTransitionRatio, timingVariance, knownMatches),
            RecommendedAction = DetermineAction(...)
        };
    }
    
    public enum RecommendedAction
    {
        Allow,
        Tarpit,      // Slow down responses
        Deceive,     // Send fake info
        Block
    }
}
```

---

## 4.2 Dead Reckoning

### File Location
```
src/slskd/Core/Security/DeadReckoningService.cs
```

### Implementation

```csharp
public class DeadReckoningService
{
    private readonly ConcurrentDictionary<string, StatePrediction> _predictions = new();
    
    public record StatePrediction
    {
        public object ExpectedValue { get; init; }
        public DateTimeOffset ValidUntil { get; init; }
        public string Source { get; init; }  // What told us this
    }
    
    /// <summary>
    /// Record what we expect the server to tell us next.
    /// </summary>
    public void Predict(string key, object expected, TimeSpan validity, string source)
    {
        _predictions[key] = new StatePrediction
        {
            ExpectedValue = expected,
            ValidUntil = DateTimeOffset.UtcNow + validity,
            Source = source
        };
    }
    
    /// <summary>
    /// Check actual value against prediction.
    /// </summary>
    public PredictionResult Check(string key, object actual)
    {
        if (!_predictions.TryGetValue(key, out var prediction))
            return PredictionResult.NoPrediction;
        
        if (prediction.ValidUntil < DateTimeOffset.UtcNow)
        {
            _predictions.TryRemove(key, out _);
            return PredictionResult.Expired;
        }
        
        if (DeepEquals(prediction.ExpectedValue, actual))
            return PredictionResult.Match;
        
        return new PredictionResult
        {
            Status = PredictionStatus.Deviation,
            Expected = prediction.ExpectedValue,
            Actual = actual,
            Source = prediction.Source
        };
    }
    
    // Example predictions:
    // - Queue position should only decrease or stay same (not increase without reason)
    // - Privilege level shouldn't change without action
    // - User online status should match recent activity
}
```

---

## 4.3 Honeypots

### File Location
```
src/slskd/Core/Security/HoneypotService.cs
```

### Implementation

```csharp
public class HoneypotService : IHostedService
{
    private readonly List<TcpListener> _listeners = new();
    private readonly ILogger<HoneypotService> _logger;
    private readonly IPeerReputationService _reputation;
    
    // Ports that look like they could be overlay/API ports
    private readonly int[] HoneypotPorts = { 5031, 5032, 5033, 2244, 2255 };
    
    // Canary files in shares
    private readonly string[] CanaryFiles = 
    {
        "passwords.txt",
        "private_keys.txt", 
        "wallet.dat",
        "credentials.json",
        ".ssh/id_rsa"
    };
    
    // Decoy API endpoints
    private readonly string[] DecoyEndpoints =
    {
        "/api/v0/admin/debug",
        "/api/v0/internal/dump",
        "/api/v0/config/secrets"
    };
    
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var port in HoneypotPorts)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                _listeners.Add(listener);
                
                _ = AcceptConnectionsAsync(listener, port, ct);
                
                _logger.LogInformation("Honeypot listening on port {Port}", port);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not bind honeypot to port {Port}: {Error}", port, ex.Message);
            }
        }
    }
    
    private async Task AcceptConnectionsAsync(TcpListener listener, int port, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                var endpoint = (IPEndPoint)client.Client.RemoteEndPoint!;
                
                _logger.LogWarning(
                    "[HONEYPOT] Connection to honeypot port {Port} from {IP}",
                    port, endpoint.Address);
                
                // Record as threat
                await RecordThreatAsync(endpoint.Address, $"honeypot_port_{port}");
                
                // Optionally tarpit: hold connection open, send garbage slowly
                _ = TarpitConnectionAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogDebug("Honeypot accept error: {Error}", ex.Message);
            }
        }
    }
    
    /// <summary>
    /// Check if a file request is for a canary file.
    /// </summary>
    public bool IsCanaryRequest(string filename)
    {
        return CanaryFiles.Any(c => 
            filename.EndsWith(c, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Check if an API request is for a decoy endpoint.
    /// </summary>
    public bool IsDecoyEndpoint(string path)
    {
        return DecoyEndpoints.Any(d => 
            path.StartsWith(d, StringComparison.OrdinalIgnoreCase));
    }
}
```

---

## 4.4 Entropy Monitoring

### File Location
```
src/slskd/Common/Security/EntropyMonitor.cs
```

### Implementation

```csharp
public class EntropyMonitor
{
    private readonly BloomFilter<byte[]> _usedNonces;
    private readonly ILogger<EntropyMonitor> _logger;
    
    public EntropyMonitor()
    {
        _usedNonces = new BloomFilter<byte[]>(
            capacity: 1_000_000,
            errorRate: 0.001);
    }
    
    /// <summary>
    /// Generate a cryptographically secure nonce with collision detection.
    /// </summary>
    public byte[] GenerateNonce(int length = 32)
    {
        var nonce = RandomNumberGenerator.GetBytes(length);
        var hash = SHA256.HashData(nonce);
        
        // This should NEVER happen with good randomness
        if (_usedNonces.Contains(hash))
        {
            _logger.LogCritical(
                "[ENTROPY] Nonce collision detected! Possible PRNG compromise!");
            
            // Try again with explicit re-seeding
            Thread.Sleep(100);  // Allow entropy pool to refill
            return GenerateNonce(length);
        }
        
        _usedNonces.Add(hash);
        return nonce;
    }
    
    /// <summary>
    /// Test quality of random bytes.
    /// </summary>
    public EntropyQuality AssessQuality(byte[] sample)
    {
        if (sample.Length < 32)
            return EntropyQuality.InsufficientSample;
        
        // Chi-squared test for uniform distribution
        var chiSquared = CalculateChiSquared(sample);
        
        // Runs test for randomness
        var runsTest = CalculateRunsTest(sample);
        
        // Compression test - random data shouldn't compress well
        var compressionRatio = TestCompression(sample);
        
        return new EntropyQuality
        {
            ChiSquaredPValue = chiSquared,
            RunsTestPValue = runsTest,
            CompressionRatio = compressionRatio,
            IsAcceptable = chiSquared > 0.01 && runsTest > 0.01 && compressionRatio > 0.95
        };
    }
}

public record EntropyQuality
{
    public double ChiSquaredPValue { get; init; }
    public double RunsTestPValue { get; init; }
    public double CompressionRatio { get; init; }
    public bool IsAcceptable { get; init; }
}
```

---

## 4.5 Paranoid Mode

### File Location
```
src/slskd/Core/Security/ParanoidModeService.cs
src/web/src/components/ParanoidMode/ParanoidDashboard.jsx
```

### Backend Service

```csharp
public class ParanoidModeService
{
    public bool Enabled { get; set; }
    public ParanoidLevel Level { get; set; } = ParanoidLevel.Log;
    
    private readonly ConcurrentQueue<ServerEvent> _events = new();
    
    public enum ParanoidLevel
    {
        Off,
        Log,      // Just log suspicious activity
        Warn,     // Log + UI warnings
        Enforce   // Log + warn + block suspicious
    }
    
    /// <summary>
    /// Record a server event for analysis.
    /// </summary>
    public void RecordServerEvent(ServerEvent evt)
    {
        if (!Enabled) return;
        
        _events.Enqueue(evt);
        
        // Keep last 1000 events
        while (_events.Count > 1000)
            _events.TryDequeue(out _);
        
        // Check for suspicious patterns
        var suspicion = AnalyzeEvent(evt);
        if (suspicion.IsSuspicious)
        {
            OnSuspiciousActivity(evt, suspicion);
        }
    }
    
    /// <summary>
    /// Get recent events for UI display.
    /// </summary>
    public IEnumerable<ServerEvent> GetRecentEvents(int count = 100)
    {
        return _events.TakeLast(count);
    }
    
    /// <summary>
    /// Get current suspicion analysis.
    /// </summary>
    public ParanoidReport GetReport()
    {
        return new ParanoidReport
        {
            TotalEvents = _events.Count,
            SuspiciousEvents = _events.Count(e => AnalyzeEvent(e).IsSuspicious),
            EventsByType = _events.GroupBy(e => e.Type)
                                  .ToDictionary(g => g.Key, g => g.Count()),
            RecentSuspicious = _events
                .Where(e => AnalyzeEvent(e).IsSuspicious)
                .TakeLast(10)
                .ToList()
        };
    }
}

public record ServerEvent
{
    public DateTimeOffset Timestamp { get; init; }
    public string Type { get; init; }
    public string Details { get; init; }
    public Dictionary<string, object> Data { get; init; }
}
```

### Frontend Component

```jsx
// ParanoidDashboard.jsx
import React, { useState, useEffect } from 'react';
import { Card, Table, Badge, Switch } from 'semantic-ui-react';

export const ParanoidDashboard = () => {
    const [report, setReport] = useState(null);
    const [enabled, setEnabled] = useState(false);
    
    useEffect(() => {
        const interval = setInterval(fetchReport, 5000);
        return () => clearInterval(interval);
    }, []);
    
    const fetchReport = async () => {
        const res = await fetch('/api/v0/security/paranoid/report');
        setReport(await res.json());
    };
    
    return (
        <Card fluid>
            <Card.Content>
                <Card.Header>
                    Paranoid Mode
                    <Switch 
                        checked={enabled} 
                        onChange={() => setEnabled(!enabled)} 
                    />
                </Card.Header>
            </Card.Content>
            <Card.Content>
                {report && (
                    <>
                        <p>Total events: {report.totalEvents}</p>
                        <p>Suspicious: {report.suspiciousEvents}</p>
                        
                        <Table compact>
                            <Table.Header>
                                <Table.Row>
                                    <Table.HeaderCell>Time</Table.HeaderCell>
                                    <Table.HeaderCell>Type</Table.HeaderCell>
                                    <Table.HeaderCell>Details</Table.HeaderCell>
                                    <Table.HeaderCell>Status</Table.HeaderCell>
                                </Table.Row>
                            </Table.Header>
                            <Table.Body>
                                {report.recentSuspicious.map((evt, i) => (
                                    <Table.Row key={i} negative>
                                        <Table.Cell>{evt.timestamp}</Table.Cell>
                                        <Table.Cell>{evt.type}</Table.Cell>
                                        <Table.Cell>{evt.details}</Table.Cell>
                                        <Table.Cell>
                                            <Badge color="red">Suspicious</Badge>
                                        </Table.Cell>
                                    </Table.Row>
                                ))}
                            </Table.Body>
                        </Table>
                    </>
                )}
            </Card.Content>
        </Card>
    );
};
```

---

## Quick Reference: File Locations

| Feature | Primary File |
|---------|--------------|
| PathGuard | `src/slskd/Common/Security/PathGuard.cs` |
| NetworkGuard | `src/slskd/DhtRendezvous/Security/NetworkGuard.cs` |
| PrivacyMode | `src/slskd/Core/Privacy/PrivacyService.cs` |
| ContentSafety | `src/slskd/Common/Security/ContentSafety.cs` |
| PeerReputation | `src/slskd/Users/Reputation/PeerReputationService.cs` |
| Commitment | `src/slskd/Transfers/Security/CommitmentProtocol.cs` |
| StorageProof | `src/slskd/Transfers/Security/StorageProofService.cs` |
| Byzantine | `src/slskd/Transfers/MultiSource/ByzantineConsensus.cs` |
| Probabilistic | `src/slskd/Transfers/Security/ProbabilisticVerifier.cs` |
| Temporal | `src/slskd/Users/Monitoring/TemporalConsistencyService.cs` |
| Watermarking | `src/slskd/Shares/Watermarking/WatermarkService.cs` |
| GatedShares | `src/slskd/Shares/GatedShareService.cs` |
| Fingerprint | `src/slskd/DhtRendezvous/Security/FingerprintDetector.cs` |
| DeadReckoning | `src/slskd/Core/Security/DeadReckoningService.cs` |
| Honeypots | `src/slskd/Core/Security/HoneypotService.cs` |
| Entropy | `src/slskd/Common/Security/EntropyMonitor.cs` |
| ParanoidMode | `src/slskd/Core/Security/ParanoidModeService.cs` |

---

*Last updated: 2025-01-08*


---

## Database Alignment with experimental/multi-source-swarm

The multi-source-swarm branch already has significant database infrastructure that security features should leverage or extend.

### Existing Databases in multi-source-swarm

| Database | Tables | Purpose |
|----------|--------|---------|
| `hashdb.db` | Peers, FlacInventory, HashDb, MeshPeerState, HashDbState | Peer tracking, file hashes, mesh sync |
| `ranking.db` | DownloadHistory | Success/failure counts per user |
| `transfers.db` | Transfers | Transfer state and history |
| `search.db` | Searches | Search history |
| `messaging.db` | Conversations, Messages | Chat history |
| `events.db` | Events | Event log |

### Schema Comparison: Existing vs Security Needs

#### Peers Table (hashdb.db) vs PeerProfile (Security)

**Existing Peers table:**
```sql
CREATE TABLE Peers (
    peer_id TEXT PRIMARY KEY,
    caps INTEGER DEFAULT 0,
    client_version TEXT,
    last_seen INTEGER NOT NULL,
    last_cap_check INTEGER,
    backfills_today INTEGER DEFAULT 0,
    backfill_reset_date INTEGER
);
```

**Security PeerProfile needs:**
```sql
-- Fields to ADD to Peers table:
ALTER TABLE Peers ADD COLUMN malformed_messages INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN protocol_violations INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN aborted_transfers INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN timeout_count INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN first_seen INTEGER;
ALTER TABLE Peers ADD COLUMN reputation_score REAL DEFAULT 50.0;
ALTER TABLE Peers ADD COLUMN is_blocked INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN blocked_until INTEGER;
ALTER TABLE Peers ADD COLUMN block_reason TEXT;
ALTER TABLE Peers ADD COLUMN total_bytes_downloaded INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN total_bytes_uploaded INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN average_speed REAL;
```

**Recommendation**: Extend the existing `Peers` table rather than creating a separate ReputationDb. This keeps all peer data in one place.

#### DownloadHistory (ranking.db) - Already Compatible

**Existing:**
```csharp
public class DownloadHistoryEntry
{
    public string Username { get; set; }  // PK
    public int Successes { get; set; }
    public int Failures { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

**Action**: This can be used as-is for basic reputation. The extended Peers table will have the detailed metrics.

### New Tables Needed

#### 1. Security Events Table (security.db or events.db)

```sql
CREATE TABLE SecurityEvents (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp INTEGER NOT NULL,
    event_type TEXT NOT NULL,        -- 'violation', 'suspicious', 'blocked', 'honeypot'
    peer_id TEXT,
    ip_address TEXT,
    details TEXT,                    -- JSON blob
    severity TEXT DEFAULT 'info'     -- 'info', 'warning', 'critical'
);

CREATE INDEX idx_security_events_peer ON SecurityEvents(peer_id);
CREATE INDEX idx_security_events_type ON SecurityEvents(event_type);
CREATE INDEX idx_security_events_time ON SecurityEvents(timestamp);
```

#### 2. Temporal Consistency Tables (hashdb.db)

```sql
-- Share snapshots for temporal analysis
CREATE TABLE ShareSnapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    peer_id TEXT NOT NULL,
    taken_at INTEGER NOT NULL,
    file_count INTEGER,
    folder_count INTEGER,
    total_size INTEGER,
    structure_hash TEXT,             -- Hash of directory tree
    top_level_folders TEXT           -- JSON array
);

CREATE INDEX idx_snapshots_peer ON ShareSnapshots(peer_id);
CREATE INDEX idx_snapshots_time ON ShareSnapshots(taken_at);

-- File observation history
CREATE TABLE FileObservations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    peer_id TEXT NOT NULL,
    filename TEXT NOT NULL,
    size INTEGER NOT NULL,
    first_seen INTEGER NOT NULL,
    last_seen INTEGER NOT NULL,
    times_observed INTEGER DEFAULT 1,
    was_ever_missing INTEGER DEFAULT 0,
    UNIQUE(peer_id, filename, size)
);

CREATE INDEX idx_observations_peer ON FileObservations(peer_id);
```

#### 3. Commitment Protocol Tables (hashdb.db or transfers.db)

```sql
CREATE TABLE TransferCommitments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    peer_id TEXT NOT NULL,
    filename TEXT NOT NULL,
    commitment_hash TEXT NOT NULL,   -- SHA256(content_hash || nonce)
    received_at INTEGER NOT NULL,
    revealed_content_hash TEXT,
    revealed_nonce TEXT,
    verified INTEGER DEFAULT 0,      -- 0=pending, 1=valid, -1=violation
    verification_time INTEGER
);

CREATE INDEX idx_commitments_peer ON TransferCommitments(peer_id);
CREATE INDEX idx_commitments_filename ON TransferCommitments(filename);
```

#### 4. Watermark Tracking Table (hashdb.db)

```sql
CREATE TABLE Watermarks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    peer_id TEXT NOT NULL,
    filename TEXT NOT NULL,
    original_file_hash TEXT NOT NULL,
    watermark_hash TEXT NOT NULL,    -- The watermark we embedded
    created_at INTEGER NOT NULL
);

CREATE INDEX idx_watermarks_peer ON Watermarks(peer_id);
CREATE INDEX idx_watermarks_hash ON Watermarks(watermark_hash);
```

#### 5. Threat Intelligence Table (security.db)

```sql
CREATE TABLE ThreatIntel (
    ip_address TEXT PRIMARY KEY,
    first_seen INTEGER NOT NULL,
    last_seen INTEGER NOT NULL,
    threat_type TEXT,                -- 'scanner', 'honeypot_hit', 'rate_limit', 'protocol_violation'
    hit_count INTEGER DEFAULT 1,
    is_blocked INTEGER DEFAULT 0,
    blocked_until INTEGER,
    details TEXT                     -- JSON blob
);

CREATE INDEX idx_threats_type ON ThreatIntel(threat_type);
CREATE INDEX idx_threats_blocked ON ThreatIntel(is_blocked);
```

### Migration Strategy

Create a migration file that extends existing tables:

```
Location: src/slskd/Core/Data/Migrations/Z_Security_PeerReputationMigration.cs
```

```csharp
public class SecurityPeerReputationMigration : IMigration
{
    public string Name => "Z_Security_PeerReputationMigration";
    
    public void Up(SqliteConnection connection)
    {
        // Extend Peers table with security columns
        var alterCommands = new[]
        {
            "ALTER TABLE Peers ADD COLUMN malformed_messages INTEGER DEFAULT 0",
            "ALTER TABLE Peers ADD COLUMN protocol_violations INTEGER DEFAULT 0",
            "ALTER TABLE Peers ADD COLUMN aborted_transfers INTEGER DEFAULT 0",
            "ALTER TABLE Peers ADD COLUMN timeout_count INTEGER DEFAULT 0",
            "ALTER TABLE Peers ADD COLUMN first_seen INTEGER",
            "ALTER TABLE Peers ADD COLUMN reputation_score REAL DEFAULT 50.0",
            "ALTER TABLE Peers ADD COLUMN is_blocked INTEGER DEFAULT 0",
            "ALTER TABLE Peers ADD COLUMN blocked_until INTEGER",
            "ALTER TABLE Peers ADD COLUMN block_reason TEXT",
            "ALTER TABLE Peers ADD COLUMN total_bytes_downloaded INTEGER DEFAULT 0",
            "ALTER TABLE Peers ADD COLUMN total_bytes_uploaded INTEGER DEFAULT 0",
            "ALTER TABLE Peers ADD COLUMN average_speed REAL",
        };
        
        foreach (var cmd in alterCommands)
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = cmd;
                command.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1) 
            {
                // Column already exists, ignore
            }
        }
        
        // Create new security tables
        using var createCmd = connection.CreateCommand();
        createCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SecurityEvents (...);
            CREATE TABLE IF NOT EXISTS ShareSnapshots (...);
            CREATE TABLE IF NOT EXISTS FileObservations (...);
            CREATE TABLE IF NOT EXISTS TransferCommitments (...);
            CREATE TABLE IF NOT EXISTS Watermarks (...);
            CREATE TABLE IF NOT EXISTS ThreatIntel (...);
        ";
        createCmd.ExecuteNonQuery();
    }
}
```

### Integration with Existing Services

#### HashDbService Extension

The `HashDbService` already manages `hashdb.db`. Extend it or create a companion `SecurityDbService`:

**Option A: Extend HashDbService** (simpler, keeps peer data together)
```csharp
// Add to IHashDbService interface:
Task RecordSecurityEventAsync(SecurityEvent evt, CancellationToken ct = default);
Task<PeerSecurityProfile> GetPeerSecurityProfileAsync(string username, CancellationToken ct = default);
Task UpdatePeerReputationAsync(string username, ReputationDelta delta, CancellationToken ct = default);
Task RecordShareSnapshotAsync(string username, ShareSnapshot snapshot, CancellationToken ct = default);
```

**Option B: Create SecurityDbService** (cleaner separation)
```csharp
public interface ISecurityDbService
{
    // Events
    Task RecordEventAsync(SecurityEvent evt, CancellationToken ct = default);
    Task<IEnumerable<SecurityEvent>> GetRecentEventsAsync(int count, CancellationToken ct = default);
    
    // Reputation
    Task<PeerSecurityProfile> GetProfileAsync(string username, CancellationToken ct = default);
    Task UpdateReputationAsync(string username, ReputationDelta delta, CancellationToken ct = default);
    Task BlockPeerAsync(string username, TimeSpan duration, string reason, CancellationToken ct = default);
    
    // Temporal
    Task RecordSnapshotAsync(ShareSnapshot snapshot, CancellationToken ct = default);
    Task<IEnumerable<FileObservation>> GetFileHistoryAsync(string username, string filename, CancellationToken ct = default);
    
    // Commitments
    Task StoreCommitmentAsync(TransferCommitment commitment, CancellationToken ct = default);
    Task<TransferCommitment> GetCommitmentAsync(string peer, string filename, CancellationToken ct = default);
    
    // Threats
    Task RecordThreatAsync(IPAddress ip, ThreatType type, string details = null, CancellationToken ct = default);
    Task<bool> IsIpBlockedAsync(IPAddress ip, CancellationToken ct = default);
}
```

**Recommendation**: Option B - Create `SecurityDbService` that:
1. Uses the same `hashdb.db` file for peer-related security tables
2. Creates `security.db` for security events and threat intel
3. Integrates with existing `HashDbService` for peer lookups

### Dependency Graph

```
HashDbService (existing)
    └── Peers table
         └── Extended with security columns
         
SecurityDbService (new)
    ├── Uses hashdb.db for:
    │   ├── Peer security profile queries
    │   ├── ShareSnapshots
    │   ├── FileObservations
    │   ├── TransferCommitments
    │   └── Watermarks
    │
    └── Uses security.db for:
        ├── SecurityEvents
        └── ThreatIntel

PeerReputationService (new)
    ├── Depends on: HashDbService
    ├── Depends on: SecurityDbService
    └── Depends on: SourceRankingService (existing)
```

### Files to Create

| File | Database | Purpose |
|------|----------|---------|
| `src/slskd/Core/Security/SecurityDbService.cs` | security.db, hashdb.db | Security data access |
| `src/slskd/Core/Security/ISecurityDbService.cs` | - | Interface |
| `src/slskd/Core/Data/Migrations/Z_Security_*.cs` | hashdb.db | Schema migrations |
| `src/slskd/Users/Reputation/PeerReputationService.cs` | - | Uses both services |


---

## 5.1 Hardened Deployment Patterns

### File Locations
```
packaging/systemd/slskdn.service
packaging/systemd/slskdn-hardened.service
packaging/docker/Dockerfile.hardened
packaging/docker/docker-compose.hardened.yml
docs/deployment/SECURITY_DEPLOYMENT.md
```

### Systemd Service (Hardened)

```ini
# /etc/systemd/system/slskdn.service
[Unit]
Description=slskdn - Hardened Soulseek Client
After=network.target
Wants=network-online.target

[Service]
Type=simple
User=slskdn
Group=slskdn
WorkingDirectory=/var/lib/slskdn

# Binary
ExecStart=/usr/bin/slskdn --config /etc/slskdn/slskdn.yml

# Restart policy
Restart=always
RestartSec=10
TimeoutStopSec=30

# Security hardening
NoNewPrivileges=yes
PrivateTmp=yes
PrivateDevices=yes
ProtectSystem=strict
ProtectHome=yes
ProtectKernelTunables=yes
ProtectKernelModules=yes
ProtectControlGroups=yes
RestrictNamespaces=yes
RestrictRealtime=yes
RestrictSUIDSGID=yes
LockPersonality=yes
MemoryDenyWriteExecute=yes
RemoveIPC=yes

# Filesystem access
ReadWritePaths=/var/lib/slskdn
ReadWritePaths=/srv/media/downloads
ReadOnlyPaths=/srv/media/music
ReadOnlyPaths=/etc/slskdn

# Network
RestrictAddressFamilies=AF_INET AF_INET6 AF_UNIX
IPAddressAllow=any
IPAddressDeny=localhost

# Capabilities
CapabilityBoundingSet=
AmbientCapabilities=

# System calls
SystemCallFilter=@system-service
SystemCallFilter=~@privileged @resources
SystemCallArchitectures=native

# Resource limits
LimitNOFILE=65536
LimitNPROC=512
MemoryMax=4G
TasksMax=256

[Install]
WantedBy=multi-user.target
```

### Docker Compose (Hardened)

```yaml
# docker-compose.hardened.yml
version: "3.9"

services:
  slskdn:
    image: ghcr.io/snapetech/slskdn:latest
    container_name: slskdn
    
    # Run as non-root
    user: "1000:1000"
    
    # Read-only root filesystem
    read_only: true
    
    # Security options
    security_opt:
      - no-new-privileges:true
      - seccomp:unconfined  # Or use custom seccomp profile
    
    # Drop all capabilities
    cap_drop:
      - ALL
    
    # Tmpfs for writable directories
    tmpfs:
      - /tmp:size=100M,mode=1777
    
    # Volumes
    volumes:
      # Config (read-only)
      - ./config:/config:ro
      
      # Data directory (read-write)
      - ./data:/app/data:rw
      
      # Downloads (read-write)
      - /srv/downloads:/downloads:rw
      
      # Media library (read-only)
      - /srv/music:/music:ro
    
    # Environment
    environment:
      - SLSKD_CONFIG=/config/slskdn.yml
      - SLSKD_APP_DIR=/app/data
      - SLSKD_DOWNLOADS_DIR=/downloads
      - SLSKD_SHARES_DIR=/music
      - TZ=UTC
    
    # Network
    ports:
      - "5030:5030"   # Web UI
      - "5031:5031"   # Soulseek
      - "50300:50300" # Soulseek file transfer
    
    # Health check
    healthcheck:
      test: ["CMD", "wget", "-q", "--spider", "http://localhost:5030/api/v0/application"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s
    
    # Resource limits
    deploy:
      resources:
        limits:
          cpus: "2"
          memory: 4G
        reservations:
          memory: 512M
    
    # Logging
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
    
    # Restart policy
    restart: unless-stopped

# Optional: Reverse proxy with auth
  caddy:
    image: caddy:alpine
    container_name: slskdn-proxy
    ports:
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
    depends_on:
      - slskdn
    read_only: true
    security_opt:
      - no-new-privileges:true
    cap_drop:
      - ALL
    cap_add:
      - NET_BIND_SERVICE

volumes:
  caddy_data:
```

### Dockerfile (Hardened)

```dockerfile
# Dockerfile.hardened
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base

# Create non-root user
RUN addgroup -g 1000 slskdn && \
    adduser -u 1000 -G slskdn -D -H slskdn

# Install minimal dependencies
RUN apk add --no-cache \
    ca-certificates \
    tzdata \
    && rm -rf /var/cache/apk/*

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/slskd/slskd.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false \
    /p:PublishTrimmed=false

# Final stage
FROM base AS final
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Create directories
RUN mkdir -p /app/data /downloads && \
    chown -R slskdn:slskdn /app /downloads

# Switch to non-root user
USER slskdn

# Expose ports
EXPOSE 5030 5031 50300

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD wget -q --spider http://localhost:5030/api/v0/application || exit 1

# Entry point
ENTRYPOINT ["dotnet", "slskd.dll"]
```

### Firewall Rules (iptables/nftables)

```bash
#!/bin/bash
# firewall-slskdn.sh - Restrictive firewall for slskdn

# Variables
SLSKDN_USER="slskdn"
WEB_PORT=5030
SLSK_PORT=5031
FILE_PORT=50300

# Allow outbound to Soulseek server
iptables -A OUTPUT -m owner --uid-owner $SLSKDN_USER -p tcp --dport 2271 -j ACCEPT
iptables -A OUTPUT -m owner --uid-owner $SLSKDN_USER -p tcp --dport 2242 -j ACCEPT

# Allow outbound to peers (dynamic ports)
iptables -A OUTPUT -m owner --uid-owner $SLSKDN_USER -p tcp --dport 1024:65535 -j ACCEPT

# Allow inbound on configured ports
iptables -A INPUT -p tcp --dport $WEB_PORT -j ACCEPT
iptables -A INPUT -p tcp --dport $SLSK_PORT -j ACCEPT
iptables -A INPUT -p tcp --dport $FILE_PORT -j ACCEPT

# Allow established connections
iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT

# Block everything else from slskdn user
iptables -A OUTPUT -m owner --uid-owner $SLSKDN_USER -j DROP
```

### VPN/SOCKS Configuration

```yaml
# slskdn.yml - VPN/Proxy configuration
soulseek:
  # Route Soulseek traffic through SOCKS5 proxy (e.g., VPN)
  proxy:
    enabled: true
    type: socks5
    address: 127.0.0.1
    port: 1080
    # Optional authentication
    username: ""
    password: ""

# DNS leak prevention
network:
  dns:
    servers:
      - 1.1.1.1
      - 9.9.9.9
    # Use DoH if available
    use_doh: true
```

### Security Checklist

```markdown
# Deployment Security Checklist

## Pre-Deployment
- [ ] Create dedicated user account (non-root)
- [ ] Set up restrictive file permissions
- [ ] Configure firewall rules
- [ ] Set up VPN/proxy if desired
- [ ] Review and customize slskdn.yml

## File Permissions
- [ ] /etc/slskdn/ - root:slskdn 750
- [ ] /etc/slskdn/slskdn.yml - root:slskdn 640
- [ ] /var/lib/slskdn/ - slskdn:slskdn 700
- [ ] /srv/downloads/ - slskdn:slskdn 755
- [ ] /srv/music/ - slskdn:slskdn 555 (read-only)

## Network
- [ ] Bind web UI to localhost or VPN interface only
- [ ] Enable HTTPS with valid certificate
- [ ] Configure reverse proxy with authentication
- [ ] Set up fail2ban for API endpoint

## Monitoring
- [ ] Enable Prometheus metrics export
- [ ] Configure log rotation
- [ ] Set up alerts for security events
- [ ] Monitor disk space and resource usage

## Backups
- [ ] Back up /var/lib/slskdn/*.db regularly
- [ ] Back up /etc/slskdn/slskdn.yml
- [ ] Test restore procedure

## Updates
- [ ] Subscribe to security announcements
- [ ] Plan regular update schedule
- [ ] Test updates in staging environment
```


---

## Appendix: Comparison with experimental/multi-source-swarm

This section compares what's **already implemented** in `experimental/multi-source-swarm` vs what the security specs propose as **new**.

### Already Implemented in multi-source-swarm

| Component | File | Status | Description |
|-----------|------|--------|-------------|
| **TLS 1.3 Overlay** | `MeshOverlayConnection.cs` | ✅ Complete | TLS 1.3 with certificate pinning |
| **Message Validation** | `MessageValidator.cs` | ✅ Complete | Username, features, ports, nonce, hash validation |
| **Rate Limiting** | `OverlayRateLimiter.cs` | ✅ Complete | Per-IP, per-connection, global limits with backoff |
| **Peer Diversity** | `PeerDiversityChecker.cs` | ✅ Complete | Anti-eclipse attack subnet diversity |
| **Secure Framing** | `SecureMessageFramer.cs` | ✅ Complete | Length-prefixed with max size |
| **Overlay Blocklist** | `OverlayBlocklist.cs` | ✅ Complete | IP + username blocking with expiry |
| **Peer Verification** | `PeerVerificationService.cs` | ✅ Complete | Soulseek UserInfo challenge verification |
| **Certificate Manager** | `CertificateManager.cs` | ✅ Complete | TOFU certificate pinning |
| **Overlay Timeouts** | `OverlayTimeouts.cs` | ✅ Complete | Configurable timeouts |
| **IP Blacklist** | `Blacklist.cs` | ✅ Complete | CIDR/P2P/DAT format IP blocklists |
| **Content Verification** | `ContentVerificationService.cs` | ✅ Complete | SHA256 first-32KB verification for multi-source |
| **Source Ranking DB** | `SourceRankingDbContext.cs` | ✅ Complete | Success/failure counts per user |
| **Hash Database** | `HashDbService.cs` | ✅ Complete | Peers, FlacInventory, HashDb, MeshPeerState tables |

### Gap Analysis: What's New in Security Specs

| Feature | Proposal | Existing | Gap |
|---------|----------|----------|-----|
| **1.1 PathGuard** | Full path sanitization | None | 🔴 **NEW** - No path traversal protection exists |
| **1.2 Network Guard** | Enhanced message validation | MessageValidator.cs | 🟡 **EXTEND** - Add connection aging, trust levels |
| **1.3 Privacy Mode** | Metadata minimization | None | 🔴 **NEW** |
| **1.4 Content Safety** | Magic byte verification | None | 🔴 **NEW** - Only hash verification exists |
| **2.1 Peer Reputation** | Full behavioral scoring | SourceRankingDbContext (basic) | 🟡 **EXTEND** - Add violations, protocol errors, blocking |
| **2.2 Commitment Protocol** | Cryptographic commitments | None | 🔴 **NEW** |
| **2.3 Proof-of-Storage** | Random byte challenges | None | 🔴 **NEW** |
| **2.4 Byzantine Consensus** | Multi-source consensus | ContentVerificationService (hash only) | 🟡 **EXTEND** - Add consensus voting |
| **3.1 Probabilistic Verify** | Random verification intensity | None | 🔴 **NEW** |
| **3.2 Temporal Consistency** | Share history tracking | None | 🔴 **NEW** |
| **3.3 Canary Traps** | File watermarking | None | 🔴 **NEW** |
| **3.4 Asymmetric Disclosure** | Gated share visibility | None | 🔴 **NEW** |
| **4.1 Fingerprint Detection** | Protocol fingerprinting | None | 🔴 **NEW** |
| **4.2 Dead Reckoning** | State prediction | None | 🔴 **NEW** |
| **4.3 Honeypots** | Decoy ports/files | None | 🔴 **NEW** |
| **4.4 Entropy Monitoring** | RNG quality checks | None | 🔴 **NEW** |
| **4.5 Paranoid Mode** | Server action monitoring | None | 🔴 **NEW** |
| **5.1 Hardened Deploy** | systemd/Docker hardening | Basic Dockerfile | 🟡 **EXTEND** |

### Integration Points with Existing Code

#### OverlayRateLimiter.cs → NetworkGuard
The existing `OverlayRateLimiter` handles connection and message rate limiting. The proposed `NetworkGuard` should **wrap** it and add:
- Connection aging/trust levels
- Trust-based message size limits
- Violation tracking that feeds into peer reputation

```csharp
// NetworkGuard uses OverlayRateLimiter internally
public class NetworkGuard
{
    private readonly OverlayRateLimiter _rateLimiter;
    private readonly ConnectionAgeTracker _ageTracker;
    
    public GuardResult CheckMessage(byte[] data, string peerId)
    {
        // 1. Check rate limit (existing)
        var rateResult = _rateLimiter.CheckMessage(peerId);
        if (!rateResult.IsAllowed)
            return GuardResult.Deny(rateResult.Reason);
        
        // 2. Check trust-based size limit (NEW)
        var trustLevel = _ageTracker.GetTrustLevel(peerId);
        var maxSize = GetMaxSizeForTrust(trustLevel);
        if (data.Length > maxSize)
            return GuardResult.Deny("Message too large for trust level");
        
        return GuardResult.Allow();
    }
}
```

#### SourceRankingDbContext → PeerReputationService
The existing `DownloadHistoryEntry` has `Successes` and `Failures`. The proposed `PeerReputationService` should **extend** this:

```csharp
// Extend existing DownloadHistoryEntry
public class DownloadHistoryEntry  // Existing
{
    public string Username { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public DateTime LastUpdated { get; set; }
}

// PeerReputationService combines multiple sources
public class PeerReputationService
{
    private readonly SourceRankingDbContext _rankingDb;  // Existing
    private readonly IHashDbService _hashDb;             // Existing (Peers table)
    private readonly ISecurityDbService _securityDb;     // NEW
    
    public async Task<double> GetScoreAsync(string username)
    {
        // Combine data from all sources
        var ranking = await _rankingDb.GetHistoryAsync(username);
        var peer = await _hashDb.GetOrCreatePeerAsync(username);
        var security = await _securityDb.GetProfileAsync(username);
        
        return CalculateScore(ranking, peer, security);
    }
}
```

#### ContentVerificationService → Byzantine + Commitment
The existing `ContentVerificationService` does SHA256 verification. The proposed features should **wrap** it:

```csharp
// Existing: ContentVerificationService.VerifySourcesAsync()
// Proposed: Add commitment and Byzantine consensus

public class SecureMultiSourceDownloader
{
    private readonly IContentVerificationService _verification;  // Existing
    private readonly CommitmentService _commitment;              // NEW
    private readonly ByzantineChunkFetcher _byzantine;          // NEW
    
    public async Task<byte[]> DownloadAsync(MultiSourceRequest request)
    {
        // 1. Request commitments from all sources (NEW)
        var commitments = await _commitment.RequestCommitmentsAsync(request.Sources);
        
        // 2. Verify sources match (EXISTING)
        var verified = await _verification.VerifySourcesAsync(request);
        
        // 3. Download with Byzantine consensus (NEW)
        var result = await _byzantine.DownloadWithConsensusAsync(verified);
        
        // 4. Verify commitments match downloaded content (NEW)
        await _commitment.VerifyAllAsync(commitments, result);
        
        return result;
    }
}
```

#### OverlayBlocklist → Peer Reputation Auto-Block
The existing `OverlayBlocklist` handles manual blocking. It should integrate with automatic reputation-based blocking:

```csharp
// PeerReputationService triggers blocks via OverlayBlocklist
public class PeerReputationService
{
    private readonly OverlayBlocklist _blocklist;  // Existing
    
    public async Task RecordViolationAsync(string username, ViolationType type)
    {
        var profile = await GetProfileAsync(username);
        profile.Violations++;
        
        var score = CalculateScore(profile);
        if (score < BlockThreshold)
        {
            // Auto-block via existing blocklist
            _blocklist.BlockUsername(
                username,
                $"Reputation score below threshold: {score}",
                duration: TimeSpan.FromDays(7));
        }
    }
}
```

### Database Migration Path

Since `hashdb.db` already has the `Peers` table, the migration extends it:

```sql
-- Migration: Extend existing Peers table with security columns
-- File: Z_Security_PeerReputationMigration.cs

-- These columns are NEW (not in multi-source-swarm):
ALTER TABLE Peers ADD COLUMN malformed_messages INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN protocol_violations INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN aborted_transfers INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN timeout_count INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN first_seen INTEGER;
ALTER TABLE Peers ADD COLUMN reputation_score REAL DEFAULT 50.0;
ALTER TABLE Peers ADD COLUMN is_blocked INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN blocked_until INTEGER;
ALTER TABLE Peers ADD COLUMN block_reason TEXT;
ALTER TABLE Peers ADD COLUMN total_bytes_downloaded INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN total_bytes_uploaded INTEGER DEFAULT 0;
ALTER TABLE Peers ADD COLUMN average_speed REAL;

-- Create indexes for new columns
CREATE INDEX IF NOT EXISTS idx_peers_reputation ON Peers(reputation_score);
CREATE INDEX IF NOT EXISTS idx_peers_blocked ON Peers(is_blocked);
```

### Summary: Implementation Priority Adjusted

Given what's already implemented, the priority shifts:

| Priority | Feature | Reason |
|----------|---------|--------|
| 🔴 **1st** | PathGuard | Zero protection currently, critical vulnerability |
| 🔴 **2nd** | Content Safety (magic bytes) | Only hash verification exists, no file type check |
| 🟡 **3rd** | Peer Reputation extension | Build on existing SourceRankingDbContext |
| 🟡 **4th** | Network Guard extension | Build on existing OverlayRateLimiter |
| 🔴 **5th** | Commitment Protocol | New, enables provable violations |
| 🔴 **6th** | Privacy Mode | New, easy to implement |

Features that can **reuse existing code**:
- Byzantine Consensus → extends ContentVerificationService
- Enhanced blocking → uses OverlayBlocklist
- Peer verification → uses PeerVerificationService patterns

Features that are **completely new**:
- PathGuard, Content Safety, Temporal Consistency, Canary Traps
- Dead Reckoning, Honeypots, Entropy Monitoring, Paranoid Mode

