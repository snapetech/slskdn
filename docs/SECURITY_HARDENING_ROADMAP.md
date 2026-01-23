# Security Hardening Roadmap

> **Philosophy**: Assume the protocol is hostile. Assume the server is compromised. Assume peers are adversaries. Build defenses that work even when everything else fails.

This document outlines a comprehensive security hardening strategy for slskdn, treating the Soulseek protocol, server, and peer network as **zero-trust** environments.

---

## Table of Contents

1. [Design Principles](#design-principles)
2. [Existing Security Infrastructure](#existing-security-infrastructure)
3. [Phase 1: Foundation Hardening](#phase-1-foundation-hardening)
4. [Phase 2: Trust & Verification](#phase-2-trust--verification)
5. [Phase 3: Advanced Defenses](#phase-3-advanced-defenses)
6. [Phase 4: Intelligence & Detection](#phase-4-intelligence--detection)
7. [Implementation Priority Matrix](#implementation-priority-matrix)
8. [Public Framing](#public-framing)

---

## Design Principles

### Core Assumptions (Zero Trust)

1. **The Soulseek server may be compromised** - Log everything, verify independently
2. **Peers may be malicious** - Validate all input, trust no claims
3. **The protocol has unknown vulnerabilities** - Defense in depth, fail secure
4. **Adversaries are intelligent and patient** - Assume long-con attacks
5. **Adversaries observe before attacking** - Make behavior unpredictable

### Defense Strategy

| Principle | Implementation |
|-----------|----------------|
| **Defense in Depth** | Multiple independent security layers |
| **Fail Secure** | On error, deny rather than permit |
| **Information Asymmetry** | Know more about adversaries than they know about you |
| **Unpredictability** | Never give adversaries consistent behavior to optimize against |
| **Evidence Generation** | Create cryptographic proof of malicious behavior |

---

## Existing Security Infrastructure

slskdn (experimental/multi-source-swarm branch) already implements significant security infrastructure. This section documents what exists so new features can integrate properly.

### ✅ Mesh Overlay Security (DhtRendezvous/Security/)

| Component | File | Description |
|-----------|------|-------------|
| **TLS 1.3 Transport** | `MeshOverlayConnection.cs` | TLS 1.3 only, ephemeral keys, certificate pinning (TOFU) |
| **Message Validation** | `MessageValidator.cs` | Username regex, feature validation, port bounds, nonce format, hash validation |
| **Rate Limiting** | `OverlayRateLimiter.cs` | Per-IP (max 3), per-minute (30), global (100), violation backoff |
| **Peer Diversity** | `PeerDiversityChecker.cs` | Anti-eclipse: max 3 peers per /16, max 2 per /24 subnet |
| **Secure Framing** | `SecureMessageFramer.cs` | Length-prefixed, max 4KB messages, validates before alloc |
| **Overlay Blocklist** | `OverlayBlocklist.cs` | IP + username blocking with auto-expiry (24h default, permanent option) |
| **Peer Verification** | `PeerVerificationService.cs` | Soulseek UserInfo challenge to verify username ownership |
| **Certificate Manager** | `CertificateManager.cs` | Self-signed cert generation, TOFU pinning |
| **Timeouts** | `OverlayTimeouts.cs` | Configurable timeouts for connect, TLS, handshake, read, idle |

### ✅ Core Security (Core/)

| Component | File | Description |
|-----------|------|-------------|
| **IP Blacklist** | `Blacklist.cs` | CIDR/P2P/DAT format IP blocklists, efficient lookup |
| **JWT Auth** | `Security/SecurityService.cs` | JWT generation, API key auth with CIDR restrictions |
| **Validation Attributes** | `Common/Validation/*.cs` | Path, IP, enum, certificate validation attributes |
| **Cryptography** | `Common/Cryptography/*.cs` | AES, PBKDF2, secure random, X509 utilities |

### ✅ Transfer Security (Transfers/)

| Component | File | Description |
|-----------|------|-------------|
| **Content Verification** | `MultiSource/ContentVerificationService.cs` | SHA256 of first 32KB, FLAC header parsing |
| **Source Ranking** | `Ranking/SourceRankingDbContext.cs` | Success/failure tracking per user |

### ✅ Database Infrastructure (HashDb/)

| Table | Purpose | Security Relevance |
|-------|---------|-------------------|
| **Peers** | Tracks all seen peers | Has `caps`, `last_seen` - can extend for reputation |
| **FlacInventory** | File discovery tracking | `hash_status`, `hash_value` for integrity |
| **HashDb** | Content-addressed hashes | `use_count`, `seq_id` for mesh sync |
| **MeshPeerState** | Per-peer sync state | `last_seq_seen` for delta sync |

### Existing Validation Limits (MessageValidator.cs)

```csharp
public const int MaxUsernameLength = 64;
public const int MaxFeatures = 20;
public const int MaxFeatureLength = 32;
public const int MaxNonceLength = 64;
public const int MaxReasonLength = 256;
public const int MinPort = 1;
public const int MaxPort = 65535;
public const int FlacKeyLength = 16;      // 64-bit truncated hash
public const int Sha256HexLength = 64;
```

### Existing Rate Limits (OverlayRateLimiter.cs)

```csharp
public const int MaxConnectionsPerIp = 3;
public const int MaxConnectionsPerMinute = 30;
public const int MaxTotalConnections = 100;
public const int MaxMessagesPerSecond = 10;
public const int MaxDeltaRequestsPerHour = 60;
public const int ViolationBackoffSeconds = 300;  // 5 minutes
public const int MaxViolationsBeforeBan = 3;
```

### Existing Timeouts (OverlayTimeouts.cs)

| Timeout | Duration | Purpose |
|---------|----------|---------|
| Connect | 10s | TCP connection |
| TLS Handshake | 15s | TLS negotiation |
| Protocol Handshake | 30s | HELLO/ACK exchange |
| Message Read | 60s | Individual message |
| Pong | 10s | Ping response |
| Idle | 5m | No activity |
| Keepalive | 2m | Send ping interval |

---

### 🔧 Gaps: What Needs Enhancement

| Area | Current State | Gap |
|------|---------------|-----|
| **Path Sanitization** | None | 🔴 **Critical** - No traversal protection |
| **Peer Reputation** | Basic success/fail counts | Need behavioral scoring, auto-blocking |
| **Magic Byte Verification** | None | Only hash verification, no content-type check |
| **Server Monitoring** | Basic logging | No anomaly detection or state tracking |
| **Network Guard** | Components exist separately | Need unified guard integrating all checks |
| **Privacy Controls** | None | No metadata minimization options |
| **Connection Aging** | None | New connections get same trust as old |

---

### Integration Strategy for New Features

New security features should **integrate with** existing infrastructure:

```
┌─────────────────────────────────────────────────────────────┐
│                    NEW: NetworkGuard                        │
│  (Unified entry point for all security checks)              │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ EXISTING:   │  │ EXISTING:   │  │ NEW:                │  │
│  │ RateLimiter │  │ Validator   │  │ ConnectionAging     │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────┐    │
│  │ EXISTING: OverlayBlocklist + NEW: Auto-blocking     │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                NEW: PeerReputationService                   │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐  │
│  │ EXISTING:       │  │ EXISTING:       │  │ NEW:        │  │
│  │ SourceRanking   │  │ HashDb.Peers    │  │ SecurityDb  │  │
│  │ (success/fail)  │  │ (caps,last_seen)│  │ (violations)│  │
│  └─────────────────┘  └─────────────────┘  └─────────────┘  │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│            NEW: Secure Multi-Source Download                │
├─────────────────────────────────────────────────────────────┤
│  ┌────────────────────┐  ┌────────────────┐  ┌───────────┐  │
│  │ EXISTING:          │  │ NEW:           │  │ NEW:      │  │
│  │ ContentVerification│→ │ Commitment     │→ │ Byzantine │  │
│  │ (SHA256 first 32KB)│  │ Protocol       │  │ Consensus │  │
│  └────────────────────┘  └────────────────┘  └───────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## Phase 1: Foundation Hardening

*"Network Guard: hardening against malformed traffic, abuse, and buggy peers."*

### 1.1 Zero-Trust Network Guard

**Priority**: 🔴 Critical  
**Effort**: Medium  
**Status**: 🟡 EXTEND - Components exist, need unification

**Existing Infrastructure:**
- `OverlayRateLimiter.cs` - Per-IP, per-connection, global rate limits ✅
- `MessageValidator.cs` - Username, features, ports, nonce validation ✅
- `OverlayBlocklist.cs` - IP + username blocking with expiry ✅
- `OverlayTimeouts.cs` - Configurable timeouts ✅

**What's Missing:**
- Unified NetworkGuard entry point
- Connection aging / trust levels
- Trust-based message size limits

Treat every byte from server/peers as hostile.

#### Requirements

- [x] **Strict Message Decoding**
  - Verify lengths before allocation
  - Enforce upper bounds on all collections (max search results, max path length, max folder depth)
  - Drop or quarantine messages that violate spec instead of trying to be tolerant

- [x] **Enhanced Rate Limiting**
  - Per-peer message rate caps
  - Backoff when flooded with search results, queue updates, or chat spam
  - Sliding windows for bytes/sec and msgs/sec per peer

- [x] **Connection Hard Caps**
  - Max concurrent incoming connections
  - Max queued upload/download requests per peer and globally
  - Connection aging (new connections get less trust)

#### Implementation

```
Location: src/slskd/DhtRendezvous/Security/NetworkGuard.cs (new)
Dependencies: OverlayRateLimiter.cs, MessageValidator.cs

public class NetworkGuard
{
    // All decoded messages go through sanity checks
    public ValidationResult ValidateMessage(byte[] raw, string peerId);
    
    // Maintain sliding windows per peer
    public RateLimitResult CheckPeerRate(string peerId, int messageSize);
    
    // Log offenders for diagnostics, default behavior: drop, don't die
    public void RecordViolation(string peerId, ViolationType type);
}
```

---

### 1.2 Sandboxed Sharing & Download Paths

**Priority**: 🔴 Critical  
**Effort**: Low  
**Status**: 🔴 Not Implemented

*"Safe Sharing: protect your system from misconfigured shares and malicious paths."*

#### Requirements

- [x] **PathGuard Utility**
  - Hard enforce all shared files live under configured root
  - Reject paths containing `..` components
  - Reject absolute paths from peer input
  - Reject paths exceeding configured length
  - Normalize Unicode to prevent homoglyph attacks

- [x] **Download Sandboxing**
  - Force all downloads into dedicated download root
  - No peer-supplied relative paths can escape that root
  - Sanitize filenames (remove special chars, limit length)

- [x] **Systemd Hardening Documentation**
  - `ProtectHome`, `ProtectSystem=strict`
  - `ReadWritePaths` only for media roots
  - `NoNewPrivileges=yes`, `PrivateTmp=yes`

#### Implementation

```
Location: src/slskd/Common/Security/PathGuard.cs (new)

public static class PathGuard
{
    /// <summary>
    /// Normalize and validate a peer-supplied path.
    /// Returns safe absolute path or null if invalid.
    /// </summary>
    public static string? NormalizeAndValidate(string peerPath, string root);
    
    /// <summary>
    /// Sanitize a filename for safe filesystem use.
    /// </summary>
    public static string SanitizeFilename(string filename);
    
    /// <summary>
    /// Check if a path is safely contained within root.
    /// </summary>
    public static bool IsContainedIn(string path, string root);
}
```

---

### 1.3 Privacy / OPSEC Modes

**Priority**: 🟡 High  
**Effort**: Low  
**Status**: 🔴 Not Implemented

*"Privacy Mode: minimize the data you expose to the network."*

#### Requirements

- [x] **Metadata Minimizer**
  - Strip or randomize optional metadata
  - Don't send rich OS/host/client identifiers beyond what's necessary
  - Avoid embedding local folder names that leak real names or machine layout

- [x] **Minimal Profile Mode**
  - Very short, generic user description
  - No auto-joining public rooms on startup
  - Per-session random client instance ID

- [x] **Configuration Options**
  ```yaml
  privacy:
    minimize_metadata: true
    avoid_public_rooms: true
    generic_client_string: true
    randomize_instance_id: true
  ```

---

### 1.4 Content Safety: File Type & Executable Guard

**Priority**: 🟡 High  
**Effort**: Medium  
**Status**: 🔴 Not Implemented

*"Content Safety: prevent accidental execution or exposure to unsafe content."*

#### Requirements

- [x] **Magic Byte Verification**
  - Sniff first 4-8 bytes of completed downloads
  - Match against known signatures (FLAC, MP3, OGG, ZIP, EXE, MKV)
  - Flag files where extension ≠ magic bytes

- [x] **Quarantine System**
  - Auto-quarantine suspicious files
  - Move to quarantine folder or warn user
  - Mark as `Suspicious` in database

- [x] **Extension/Content Mismatch Detection**
  - If you request `.flac` and first bytes look like EXE/PE/ELF/ZIP, flag it
  - Default refusal to serve or auto-open suspicious files

#### Implementation

```
Location: src/slskd/Common/Security/ContentSafety.cs (new)

public static class MagicBytes
{
    public static readonly byte[] FLAC = { 0x66, 0x4C, 0x61, 0x43 }; // "fLaC"
    public static readonly byte[] MP3_ID3 = { 0x49, 0x44, 0x33 };    // "ID3"
    public static readonly byte[] MP3_SYNC = { 0xFF, 0xFB };
    public static readonly byte[] OGG = { 0x4F, 0x67, 0x67, 0x53 };  // "OggS"
    public static readonly byte[] ZIP = { 0x50, 0x4B, 0x03, 0x04 };  // "PK"
    public static readonly byte[] EXE_MZ = { 0x4D, 0x5A };           // "MZ"
    public static readonly byte[] ELF = { 0x7F, 0x45, 0x4C, 0x46 };  // ".ELF"
}

public class ContentSafetyService
{
    public ContentType DetectContentType(byte[] header);
    public bool IsExtensionContentMismatch(string filename, byte[] header);
    public Task QuarantineFileAsync(string path, string reason);
}
```

---

## Phase 2: Trust & Verification

### 2.1 Peer Reputation & Behavioral Scoring

**Priority**: 🟡 High  
**Effort**: Medium (reduced from High)  
**Status**: 🟡 EXTEND - Basic tracking exists

**Existing Infrastructure:**
- `SourceRankingDbContext.cs` - `DownloadHistory` table with `Successes`, `Failures` ✅
- `HashDbService.cs` - `Peers` table with `caps`, `last_seen`, `client_version` ✅
- `OverlayBlocklist.cs` - Blocking mechanism exists ✅

**What's Missing:**
- Extended Peers table columns (violations, protocol errors, reputation score)
- Scoring algorithm
- Auto-blocking integration

*"Smart Peer Reputation: automatically deprioritize abusive or flaky peers."*

#### Requirements

- [x] **PeerProfile Record**
  - `MalformedMessageCount`
  - `AbortedTransfers`
  - `SuccessfulTransfers`
  - `FirstSeen`, `LastSeen`
  - `ProtocolViolations`

- [x] **Scoring Heuristic**
  ```
  score = base + α*successes - β*failures - γ*malformed - δ*violations
  ```

- [x] **Score-Based Actions**
  - Avoid low-score peers for multi-source unless no alternative
  - Auto-block worst offenders
  - Soft-ban threshold with automatic expiry

- [x] **Event Integration**
  - Update on `OnTransferStarted`, `OnTransferAborted`, `OnMessageParseError`
  - Decay scores over time (recent behavior weighted more)

#### Implementation

```
Location: src/slskd/Users/Reputation/PeerReputationService.cs (new)

public class PeerProfile
{
    public string Username { get; init; }
    public int MalformedMessageCount { get; set; }
    public int AbortedTransfers { get; set; }
    public int SuccessfulTransfers { get; set; }
    public int ProtocolViolations { get; set; }
    public DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; set; }
    public double Score => CalculateScore();
}

public class PeerReputationService
{
    public Task<double> GetScoreAsync(string username);
    public Task RecordSuccessAsync(string username);
    public Task RecordFailureAsync(string username, FailureType type);
    public Task<IEnumerable<string>> GetBlockedPeersAsync();
}
```

---

### 2.2 Cryptographic Commitment Protocol

**Priority**: 🔴 Critical  
**Effort**: Medium  
**Status**: 🔴 Not Implemented

*"Transfer Integrity: cryptographic guarantees that files haven't been swapped mid-transfer."*

#### Requirements

- [x] **Pre-Transfer Commitment**
  - Peer commits to `Hash(content_hash || nonce)` before transfer starts
  - Reveals `content_hash` and `nonce` after commitment
  - If actual file doesn't match, they provably lied

- [x] **Evidence Generation**
  - Commitment + reveal provides cryptographic proof of malicious intent
  - Can be shared with mesh peers as irrefutable evidence

- [ ] **Prevents**
  - Bait-and-switch attacks
  - Selective poisoning (serving different content to different requesters)
  - Mid-transfer file swapping

#### Implementation

```
Location: src/slskd/Transfers/Security/CommitmentProtocol.cs (new)

public class TransferCommitment
{
    public string CommitmentHash { get; init; }  // Hash(content_hash || nonce)
    public DateTimeOffset CommittedAt { get; init; }
    public string? RevealedContentHash { get; set; }
    public string? RevealedNonce { get; set; }
    
    public bool IsValid => 
        CommitmentHash == SHA256($"{RevealedContentHash}{RevealedNonce}");
    
    public static string ComputeCommitment(string contentHash, string nonce)
        => SHA256(contentHash + nonce).ToHex();
}

public class CommitmentService
{
    public Task<TransferCommitment> RequestCommitmentAsync(string peer, string filename);
    public Task<bool> VerifyCommitmentAsync(TransferCommitment commitment, byte[] actualContent);
    public Task RecordViolationAsync(string peer, TransferCommitment commitment, byte[] actualContent);
}
```

---

### 2.3 Proof-of-Actual-Storage Challenges

**Priority**: 🟡 High  
**Effort**: Medium  
**Status**: 🔴 Not Implemented

*"Storage Verification: confirm peers actually have files before downloading."*

#### Requirements

- [x] **Random Byte Range Challenge**
  - Request arbitrary byte ranges (e.g., bytes 847293-847392)
  - Legitimate peer serves instantly
  - Faker must store whole file or be exposed

- [x] **Challenge-Response Protocol**
  ```
  You: "CHALLENGE: SHA256 of bytes [random_start, random_start+1024]"
  Peer: "RESPONSE: <hash>"
  You: (verify after download, or against known good)
  ```

- [x] **Pre-Download Filtering**
  - Challenge cheaply before committing to full download
  - Especially useful for large files from unknown peers

#### Implementation

```
Location: src/slskd/Transfers/Security/StorageProofService.cs (new)

public class StorageProofService
{
    public async Task<StorageProofResult> ChallengeAsync(
        string username,
        string filename,
        long fileSize,
        int numChallenges = 3)
    {
        var challenges = GenerateRandomRanges(fileSize, numChallenges, rangeSize: 1024);
        // Fetch each range, hash, record timing
        // Fast response = likely has file
        // Slow response = might be proxying
        return AnalyzeResponses(responses);
    }
}
```

---

### 2.4 Byzantine Fault Tolerance for Multi-Source

**Priority**: 🟡 High  
**Effort**: Medium (reduced from High)  
**Status**: 🟡 EXTEND - Verification exists, need consensus

**Existing Infrastructure:**
- `ContentVerificationService.cs` - SHA256 first-32KB verification ✅
- `MultiSourceDownloadService.cs` - Multi-source download framework ✅
- Per-source verification already happens ✅

**What's Missing:**
- N-of-M consensus voting
- Automatic liar identification
- Merkle tree support

*"Resilient Downloads: tolerate malicious sources without corruption."*

#### Requirements

- [x] **N-of-M Verification**
  - Require majority agreement on chunk content
  - If 3 sources give chunk A and 1 gives chunk B, accept A
  - Automatically identify the lying source

- [x] **Redundant Fetching for Critical Chunks**
  - First/last chunks, file headers: always get from multiple sources
  - Compare before accepting

- [x] **Merkle Tree Cross-Verification**
  - Build Merkle tree of file
  - Each peer provides tree root at start
  - Any single chunk verifiable against tree

#### Implementation

```
Location: src/slskd/Transfers/MultiSource/ByzantineConsensus.cs (new)

public class ByzantineDownloader
{
    public async Task<byte[]> FetchChunkWithConsensusAsync(
        int chunkIndex,
        IReadOnlyList<SourcePeer> sources,
        int requiredConsensus = 2)
    {
        var results = await Task.WhenAll(
            sources.Take(requiredConsensus + 1)
                   .Select(s => FetchChunkAsync(s, chunkIndex)));
        
        var groups = results
            .Where(r => r.Success)
            .GroupBy(r => SHA256(r.Data))
            .OrderByDescending(g => g.Count());
        
        if (groups.First().Count() < requiredConsensus)
            throw new InsufficientConsensusException();
        
        // Penalize liars
        foreach (var liar in groups.Skip(1).SelectMany(g => g))
            await PenalizePeerAsync(liar.Source, PenaltyType.DataManipulation);
        
        return groups.First().First().Data;
    }
}
```

---

## Phase 3: Advanced Defenses

### 3.1 Probabilistic Verification with Hidden Intensity

**Priority**: 🟡 High  
**Effort**: Medium  
**Status**: 🔴 Not Implemented

*"Smart Verification: intelligent, resource-efficient integrity checking."*

#### Requirements

- [x] **Random Verification Intensity**
  - Some chunks: full SHA256 verification
  - Some chunks: quick CRC checks
  - Some chunks: spot-checks of specific byte ranges
  - **Peers never know which chunks will be scrutinized**

- [x] **Hidden Challenge Points**
  - Randomly request same chunk from multiple peers
  - Compare responses - divergence indicates malice
  - Vary which peers get challenged

- [x] **Reputation-Adaptive**
  - New peers: 100% verification
  - Established peers: probabilistic (unknown probability)
  - Suspicious peers: hidden increased scrutiny

#### Key Insight

Adversaries can't optimize attacks against unknown verification. This is **game-theoretically optimal**: forces adversary to assume worst case.

---

### 3.2 Temporal Consistency Monitoring

**Priority**: 🟢 Medium  
**Effort**: Medium  
**Status**: 🔴 Not Implemented

*"Share Stability Tracking: detect suspicious changes in peer offerings."*

#### Requirements

- [x] **File Fingerprint History**
  - Record `(filename, size, directory_structure, first_seen, last_seen)` per peer
  - A FLAC file that changes size is suspicious
  - High churn = possible bot or compromised account

- [x] **Structural Stability Score**
  - Legitimate shares tend to be stable
  - Folder structures that constantly reorganize suggest manipulation
  - Files appearing/disappearing repeatedly are suspicious

- [x] **Metadata Drift Detection**
  - Same filename, different sizes over time = red flag
  - Inconsistent bitrates for "same" album = possible fakes

---

### 3.3 Canary Traps / Fingerprinted Shares

**Priority**: 🟢 Medium  
**Effort**: High  
**Status**: 🔴 Not Implemented

*"Share Watermarking: track file provenance and detect unauthorized redistribution."*

#### Requirements

- [x] **Per-Request Watermarking**
  - For media files, introduce imperceptible modifications
  - Each peer gets slightly different version, cryptographically tied to identity
  - LSB changes in audio, invisible metadata in images

- [x] **Provenance Tracking**
  - If watermarked file shows up elsewhere, identify which peer leaked
  - Store watermark→peer mapping in encrypted local database

- [x] **Trust Violation Detection**
  - Identify peers who redistribute "private" shares
  - Build evidence for blocklisting without reputation hearsay

---

### 3.4 Asymmetric Information Disclosure

**Priority**: 🟢 Medium  
**Effort**: Low  
**Status**: 🔴 Not Implemented

*"Gradual Trust: reveal shares incrementally based on peer trust."*

#### Requirements

- [x] **Paginated Share Responses**
  - Don't send entire share list in one message
  - Paginate with delays between pages
  - Makes enumeration attacks slow and detectable

- [x] **Trust-Gated Visibility**
  - New peers see only subset of shares
  - Full visibility unlocks with established trust
  - "Premium" shares visible only to high-trust peers

- [x] **Query Rate Limiting**
  - Cap how fast any peer can browse shares
  - Exponential backoff for repeated queries
  - Prevents automated scraping

---

## Phase 4: Intelligence & Detection

### 4.1 Protocol Fingerprinting Detection

**Priority**: 🟢 Medium  
**Effort**: Medium  
**Status**: 🔴 Not Implemented

*"Protocol Guard: detect reconnaissance and fingerprinting attempts."*

#### Requirements

- [x] **Unusual Message Sequence Detection**
  - Normal clients follow predictable patterns
  - Scanners send unusual sequences to probe
  - Track message ordering, flag deviations

- [x] **Timing Analysis**
  - Automated tools probe at regular intervals
  - Legitimate peers have natural timing variance
  - Flag suspiciously regular patterns

- [ ] **Countermeasures**
  - **Tarpit**: slow down responses to waste scanner time
  - **Disinformation**: fake version strings, misleading capabilities
  - Don't just block - make scanning expensive

---

### 4.2 Dead Reckoning for Network State

**Priority**: 🟢 Medium  
**Effort**: Medium  
**Status**: 🔴 Not Implemented

*"State Verification: detect server manipulation and replay attacks."*

#### Requirements

- [ ] **Predictive Modeling**
  - Track what server told you before
  - Predict what it should tell you next
  - Large deviations = possible manipulation

- [x] **State Checksums**
  - Periodically request state checksums
  - Store expected checksum based on messages received
  - Mismatch indicates bug, MITM, or manipulation

- [x] **Mesh Cross-Reference**
  - Cross-reference server state with mesh peers
  - If server tells you X and mesh tells you Y, investigate

---

### 4.3 Honeypot Ports and Decoy Services

**Priority**: 🟢 Medium  
**Effort**: Low  
**Status**: 🔴 Not Implemented

*"Intrusion Detection: early warning system for network probing."*

#### Requirements

- [x] **Honeypot Overlay Ports**
  - Open additional ports that look like overlay ports
  - Any traffic to them is definitionally suspicious
  - Log connection details for threat intelligence

- [x] **Decoy API Endpoints**
  - `/api/v0/admin/debug/dump` - anyone accessing is probing
  - Return plausible-looking fake data to waste attacker time

- [x] **Canary Files in Shares**
  - Place files with attractive names: `passwords.txt`, `private_keys.txt`
  - Track who requests them
  - Automatic reputation penalty

---

### 4.4 Entropy and Randomness Monitoring

**Priority**: 🟢 Medium  
**Effort**: Low  
**Status**: 🔴 Not Implemented

*"Cryptographic Health: ensuring strong randomness for all security operations."*

#### Requirements

- [x] **Entropy Pool Monitoring**
  - Track available system entropy
  - Alert if entropy drops dangerously low
  - Never perform crypto operations with weak randomness

- [x] **Nonce Uniqueness Verification**
  - Track all nonces generated (bloom filter)
  - Detect accidental reuse (catastrophic for many protocols)
  - Alert on suspicious patterns

---

### 4.5 Paranoid Mode for Server Interaction

**Priority**: 🟢 Medium  
**Effort**: Medium  
**Status**: 🔴 Not Implemented

*"Paranoid Mode: extra sanity checks and 'explain what the server is doing' UI."*

#### Requirements

- [x] **Server Action Logging UI**
  - Shows raw categories of server actions
  - Search result counts, unexpected disconnects
  - Suspicious server messages (invalid IPs, absurd ports)

- [x] **Client-Side Constraints**
  - Limit max search results accepted per query
  - Ignore IPs in unexpected private ranges
  - Ignore ports outside safe range

- [x] **Server Failsafe**
  - If server sends burst of nonsense, back off rather than crash
  - Rate limit server message acceptance

---

## Implementation Priority Matrix

### Legend

| Symbol | Meaning |
|--------|---------|
| 🔴 NEW | Completely new feature |
| 🟡 EXTEND | Extends existing infrastructure |
| ✅ EXISTS | Already implemented |

---

### Tier 1: Critical - Implement First

| ID | Feature | Type | Impact | Effort | Why |
|----|---------|------|--------|--------|-----|
| 1.2 | PathGuard | 🔴 NEW | Critical | Low | **Zero protection exists** - path traversal vulnerability |
| 1.4 | Content Safety | 🔴 NEW | Critical | Medium | Only hash verification exists, no content-type check |
| 2.2 | Cryptographic Commitment | 🔴 NEW | Critical | Medium | Prevents bait-and-switch, generates evidence |

### Tier 2: High Priority - Extend Existing

| ID | Feature | Type | Impact | Effort | Why |
|----|---------|------|--------|--------|-----|
| 1.1 | Network Guard | 🟡 EXTEND | High | Medium | Unify existing RateLimiter + Validator + add aging |
| 2.1 | Peer Reputation | 🟡 EXTEND | High | Medium | Extend SourceRanking + HashDb.Peers |
| 2.4 | Byzantine Consensus | 🟡 EXTEND | High | Medium | Extend ContentVerificationService |
| 1.3 | Privacy Mode | 🔴 NEW | High | Low | Easy to implement, high user value |

### Tier 3: Medium Priority - New Features

| ID | Feature | Type | Impact | Effort | Why |
|----|---------|------|--------|--------|-----|
| 2.3 | Proof-of-Storage | 🔴 NEW | Medium | Medium | Filters fakers before wasting bandwidth |
| 3.1 | Probabilistic Verification | 🔴 NEW | Medium | Medium | Game-theoretically optimal |
| 3.4 | Asymmetric Disclosure | 🔴 NEW | Medium | Low | Information asymmetry advantage |
| 4.3 | Honeypots | 🔴 NEW | Medium | Low | Early warning system |

### Tier 4: Advanced/Paranoid

| ID | Feature | Type | Impact | Effort | Why |
|----|---------|------|--------|--------|-----|
| 3.2 | Temporal Consistency | 🔴 NEW | Medium | Medium | Catches long-con attacks |
| 4.1 | Fingerprint Detection | 🔴 NEW | Medium | Medium | Defends against reconnaissance |
| 3.3 | Canary Traps | 🔴 NEW | Medium | High | Unique capability |
| 4.2 | Dead Reckoning | 🔴 NEW | Low | Medium | Catches sophisticated MITM |
| 4.4 | Entropy Monitoring | 🔴 NEW | Low | Low | Paranoid but important for crypto |
| 4.5 | Paranoid Mode UI | 🔴 NEW | Low | Medium | Power user feature |

---

### Effort Reduction from Existing Infrastructure

| Feature | Without Existing | With Existing | Savings |
|---------|------------------|---------------|---------|
| Network Guard | High | Medium | RateLimiter, Validator exist |
| Peer Reputation | High | Medium | SourceRanking, Peers table exist |
| Byzantine Consensus | High | Medium | ContentVerificationService exists |
| Auto-blocking | Medium | Low | OverlayBlocklist exists |

---

## Public Framing

How to describe these features publicly without saying "we distrust Soulseek":

### README/Marketing Language

> **slskdn** is a **privacy-aware, robust Soulseek client** with extra hardening for resource abuse and malformed traffic.
>
> We add **defense-in-depth** features: sandboxed sharing, network guardrails, encrypted overlays for advanced features, and peer reputation for healthier sharing.
>
> These features are **opt-in**, configurable, and designed not to break compatibility with the existing network.

### Feature Categories for UI/Docs

| Internal Name | Public Name | Description |
|---------------|-------------|-------------|
| PathGuard | Safe Sharing | Protect your system from misconfigured shares |
| NetworkGuard | Network Hardening | Protection against malformed traffic and abuse |
| PeerReputation | Smart Peer Scoring | Automatically deprioritize unreliable peers |
| ContentSafety | Download Safety | Verify downloads match expected file types |
| CommitmentProtocol | Transfer Integrity | Cryptographic verification of file authenticity |
| ByzantineConsensus | Resilient Multi-Source | Tolerate unreliable sources in swarm downloads |
| PrivacyMode | Privacy Controls | Minimize data exposed to the network |
| ParanoidMode | Advanced Diagnostics | Detailed network activity monitoring |

---

## Deployment Patterns

### Recommended systemd Unit

```ini
[Service]
User=slskd
Group=slskd
NoNewPrivileges=yes
PrivateTmp=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/srv/media /var/lib/slskd
```

### Recommended Docker Configuration

```yaml
services:
  slskdn:
    image: slskdn:latest
    user: "1000:1000"
    read_only: true
    security_opt:
      - no-new-privileges:true
    volumes:
      - ./config:/config:ro
      - ./downloads:/downloads
      - ./media:/media:ro
    cap_drop:
      - ALL
```

---

## Contributing

When implementing security features:

1. **Fail secure** - When in doubt, deny
2. **Log violations** - Create audit trail without crashing
3. **Test adversarially** - Write tests that try to break the feature
4. **Document assumptions** - Make threat model explicit
5. **Don't leak information** - Error messages should not help attackers

---

*Last updated: 2025-01-08*
*Branch: experimental/security*

