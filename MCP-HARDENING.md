# MCP Layer Hardening & Security

**Task Group**: T-MCP01-04 (Moderation / Control Plane)  
**Priority**: 🔥 CRITICAL - Legal/Ethical Protection  
**Status**: MANDATORY REQUIREMENTS for MCP Implementation  
**Created**: December 11, 2025

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## Overview

This document defines **mandatory security, privacy, and hardening requirements** specifically for the **Moderation / Control Plane (MCP)** layer.

The MCP handles sensitive operations involving:
- Hash checks against blocklists (potentially prohibited content)
- Peer reputation tracking (behavior analysis)
- External moderation services (third-party API calls)
- Content filtering decisions (legal/ethical implications)

**Given this sensitivity, MCP requires EXTRA hardening beyond standard security guidelines.**

---

## 1. Privacy & Data Minimization

### 1.1 Hash Handling - NEVER LOG RAW HASHES

**CRITICAL**: Raw content hashes are potentially identifying and MUST NOT appear in logs or metrics.

#### ❌ FORBIDDEN
```csharp
// BAD: Raw hash in log
_logger.Warning("Blocked file with hash {Hash}", file.PrimaryHash);

// BAD: Hash in metric label
_metrics.Increment("mcp_blocked_files", new { hash = file.PrimaryHash });

// BAD: Hash in error message returned to client
throw new Exception($"File blocked: hash {hash}");
```

#### ✅ REQUIRED
```csharp
// GOOD: Sanitized ID only
_logger.Warning("[SECURITY] MCP blocked file | InternalId={Id} | Reason={Reason}", 
    file.Id, decision.Reason);

// GOOD: Hash prefix for debugging (first 8 chars max)
_logger.Debug("Hash check | Prefix={Prefix} | Verdict={Verdict}", 
    file.PrimaryHash.Substring(0, 8), verdict);

// GOOD: No hash in metrics, only counts
_metrics.Increment("mcp_blocked_files_total", new { reason = decision.Reason });
```

### 1.2 Path Sanitization - NEVER LOG FULL PATHS

**CRITICAL**: Full filesystem paths can reveal user identity, library structure, or prohibited content locations.

#### ❌ FORBIDDEN
```csharp
// BAD: Full path exposure
_logger.Warning("Blocked {Path}", "/home/alice/Music/artist/album/track.mp3");

// BAD: Path in exception message
throw new SecurityException($"Cannot share {fullPath}");
```

#### ✅ REQUIRED
```csharp
// GOOD: Filename only
_logger.Warning("[SECURITY] MCP blocked file | Filename={Filename}", 
    Path.GetFileName(fullPath));

// GOOD: Internal ID + sanitized indicator
_logger.Warning("[SECURITY] MCP blocked file | Id={Id} | Extension={Ext}", 
    file.Id, Path.GetExtension(file.Path));
```

### 1.3 Peer Identity Protection

**CRITICAL**: Never expose external usernames or IP addresses in MCP logs/metrics.

#### ❌ FORBIDDEN
```csharp
// BAD: Soulseek username
_logger.Warning("Peer {Username} requested blocked content", soulseekUsername);

// BAD: IP address
_logger.Warning("IP {IP} repeatedly requests blocked items", ipAddress);
```

#### ✅ REQUIRED
```csharp
// GOOD: Internal peer ID only
_logger.Warning("[SECURITY] MCP peer report | PeerId={PeerId} | Reason={Reason}", 
    internalPeerId, report.ReasonCode);

// GOOD: Opaque identifier
_logger.Warning("[SECURITY] Peer reputation event | PeerHash={Hash}", 
    HashPeerId(peerId)); // One-way hash of peer ID for correlation
```

### 1.4 Evidence Keys - NOT Raw Data

**REQUIRED**: `ModerationDecision.EvidenceKeys` MUST contain opaque identifiers, never raw hashes or paths.

```csharp
// GOOD: Evidence format
var decision = new ModerationDecision
{
    Verdict = ModerationVerdict.Blocked,
    Reason = "hash_blocklist",
    EvidenceKeys = new[] 
    { 
        $"internal:{Guid.NewGuid()}", // Internal tracking ID
        $"provider:blocklist-v1"      // Provider identifier
        // NEVER include actual hash!
    }
};
```

---

## 2. External Service Hardening

### 2.1 External Moderation Client - SSRF Protection

**CRITICAL**: External moderation services are third-party APIs that could be exploited for SSRF.

#### Configuration Validation

```csharp
public class ExternalModerationOptions
{
    /// <summary>
    /// Allowed domains for external moderation API calls.
    /// </summary>
    [Required]
    public string[] AllowedDomains { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Maximum metadata size to send to external services (bytes).
    /// </summary>
    [Range(0, 10240)] // Max 10KB
    public int MaxMetadataSize { get; init; } = 2048;
    
    /// <summary>
    /// Timeout for external API calls (milliseconds).
    /// </summary>
    [Range(100, 30000)] // 100ms to 30s
    public int TimeoutMs { get; init; } = 5000;
    
    /// <summary>
    /// Maximum file size to submit for analysis (bytes).
    /// </summary>
    [Range(0, 104857600)] // Max 100MB
    public long MaxFileSizeForAnalysis { get; init; } = 10485760; // 10MB default
}
```

#### Implementation Requirements

```csharp
public class ExternalModerationClient : IExternalModerationClient
{
    public async Task<ModerationDecision> AnalyzeFileAsync(
        LocalFileMetadata file, 
        CancellationToken ct)
    {
        // 1. VALIDATE: Size bounds
        if (file.SizeBytes > _options.MaxFileSizeForAnalysis)
        {
            _logger.Debug("File too large for external analysis | Size={Size}", file.SizeBytes);
            return ModerationDecision.Unknown();
        }
        
        // 2. VALIDATE: Domain allowlist (SSRF protection)
        if (!Uri.TryCreate(_options.ApiEndpoint, UriKind.Absolute, out var uri))
        {
            _logger.Error("[SECURITY] Invalid external moderation endpoint configured");
            return ModerationDecision.Unknown();
        }
        
        if (!_options.AllowedDomains.Contains(uri.Host))
        {
            _logger.Error("[SECURITY] External moderation domain not in allowlist | Host={Host}", 
                uri.Host);
            return ModerationDecision.Unknown();
        }
        
        // 3. RATE LIMIT: Work budget check
        if (!_workBudget.TryConsume(WorkCosts.ExternalModerationCheck))
        {
            _logger.Warning("[SECURITY] External moderation quota exceeded");
            return ModerationDecision.Unknown();
        }
        
        // 4. SANITIZE: Build minimal request (NO raw hashes, NO full paths)
        var request = new
        {
            id = file.Id, // Internal ID only
            size = file.SizeBytes,
            extension = Path.GetExtension(file.Id),
            mediaType = DetermineMediaType(file.MediaInfo) // Generic category only
            // NEVER send: PrimaryHash, full path, detailed metadata
        };
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_options.TimeoutMs);
            
            var response = await _httpClient.PostAsJsonAsync(uri, request, cts.Token);
            
            // 5. VALIDATE: Response
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("[SECURITY] External moderation API error | Status={Status}", 
                    response.StatusCode);
                return ModerationDecision.Unknown();
            }
            
            var result = await response.Content.ReadFromJsonAsync<ExternalModerationResponse>(ct);
            
            // 6. VALIDATE: Response schema
            if (result == null || !Enum.IsDefined(typeof(ModerationVerdict), result.Verdict))
            {
                _logger.Warning("[SECURITY] Invalid external moderation response");
                return ModerationDecision.Unknown();
            }
            
            return new ModerationDecision
            {
                Verdict = result.Verdict,
                Reason = "external_moderation",
                EvidenceKeys = new[] { $"external:{uri.Host}" }
            };
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("[SECURITY] External moderation timeout");
            return ModerationDecision.Unknown();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[SECURITY] External moderation error");
            return ModerationDecision.Unknown();
        }
    }
}
```

### 2.2 Hash Blocklist - Side Channel Protection

**CRITICAL**: Hash blocklist checks could leak information via timing or network patterns.

#### Timing Attack Mitigation

```csharp
public class HashBlocklistChecker : IHashBlocklistChecker
{
    private readonly IOptionsMonitor<ModerationOptions> _options;
    private readonly ILogger<HashBlocklistChecker> _logger;
    private readonly ConcurrentDictionary<string, byte> _bloomFilter; // For quick rejection
    private readonly HashSet<string> _blocklist; // Actual blocklist
    
    public async Task<bool> IsBlockedHashAsync(string hash, CancellationToken ct)
    {
        // 1. VALIDATE: Hash format
        if (string.IsNullOrWhiteSpace(hash) || hash.Length < 32 || hash.Length > 128)
        {
            _logger.Warning("[SECURITY] Invalid hash format for blocklist check");
            return false; // Conservative: don't block on invalid input
        }
        
        // 2. NORMALIZE: Consistent format (lowercase hex)
        hash = hash.ToLowerInvariant();
        
        // 3. TIMING PROTECTION: Bloom filter for quick rejection without full lookup
        // This prevents timing side-channels for most queries
        if (!_bloomFilter.ContainsKey(hash.Substring(0, 8)))
        {
            // Not in bloom filter = definitely not blocked
            // BUT: Add constant-time delay to prevent timing leak
            await Task.Delay(TimeSpan.FromMilliseconds(1), ct);
            return false;
        }
        
        // 4. FULL CHECK: Constant-time comparison
        var isBlocked = _blocklist.Contains(hash);
        
        // 5. LOGGING: Never log the hash itself
        if (isBlocked)
        {
            _logger.Warning("[SECURITY] MCP hash blocklist match | HashPrefix={Prefix}", 
                hash.Substring(0, 8));
            
            // Increment metric (no hash in label)
            _metrics.Increment("mcp_blocklist_hits_total");
        }
        
        return isBlocked;
    }
    
    // REQUIRED: Secure blocklist loading
    public async Task LoadBlocklistAsync(string source, CancellationToken ct)
    {
        // 1. VALIDATE: Source is local file or trusted URL
        if (!IsAllowedSource(source))
        {
            throw new SecurityException($"Blocklist source not allowed: {source}");
        }
        
        // 2. VALIDATE: Size bounds (prevent DoS)
        const long MaxBlocklistSize = 100 * 1024 * 1024; // 100MB
        var size = await GetSourceSizeAsync(source, ct);
        if (size > MaxBlocklistSize)
        {
            throw new SecurityException($"Blocklist too large: {size} bytes");
        }
        
        // 3. LOAD: Parse and validate each hash
        var hashes = new HashSet<string>();
        await foreach (var line in ReadLinesAsync(source, ct))
        {
            var hash = line.Trim().ToLowerInvariant();
            
            // Validate hash format
            if (IsValidHashFormat(hash))
            {
                hashes.Add(hash);
                
                // Build bloom filter (first 8 chars)
                _bloomFilter.TryAdd(hash.Substring(0, 8), 0);
            }
        }
        
        _logger.Information("[SECURITY] Loaded hash blocklist | Count={Count} | Source={Source}", 
            hashes.Count, SanitizeSource(source));
        
        _blocklist.Clear();
        foreach (var hash in hashes)
        {
            _blocklist.Add(hash);
        }
    }
}
```

---

## 3. Peer Reputation Hardening

### 3.1 Reputation Store - Sybil Resistance

**CRITICAL**: Prevent reputation gaming via peer ID spoofing or Sybil attacks.

```csharp
public class PeerReputationStore : IPeerReputationStore
{
    private readonly ConcurrentDictionary<string, PeerReputation> _reputations = new();
    private readonly IOptionsMonitor<ModerationOptions> _options;
    
    public async Task RecordPeerEventAsync(
        string peerId, 
        PeerReport report, 
        CancellationToken ct)
    {
        // 1. VALIDATE: Peer ID format
        if (!IsValidPeerId(peerId))
        {
            _logger.Warning("[SECURITY] Invalid peer ID for reputation event");
            return;
        }
        
        // 2. RATE LIMIT: Prevent reputation flooding
        var reputation = _reputations.GetOrAdd(peerId, _ => new PeerReputation(peerId));
        
        if (!reputation.TryRecordEvent(report, _options.CurrentValue.Reputation))
        {
            _logger.Warning("[SECURITY] Peer reputation event rate limit | PeerId={PeerId}", 
                HashPeerId(peerId));
            return;
        }
        
        // 3. CALCULATE: Weighted score
        var score = CalculateReputationScore(reputation);
        
        // 4. AUTO-BAN: Threshold check
        if (score <= _options.CurrentValue.Reputation.AutoBanThreshold)
        {
            reputation.IsBanned = true;
            
            _logger.Warning("[SECURITY] Peer auto-banned by reputation | PeerId={PeerId} | Score={Score} | Events={Events}", 
                HashPeerId(peerId), score, reputation.EventCount);
            
            _metrics.Increment("mcp_peer_bans_total", 
                new { reason = "reputation_threshold" });
        }
        
        // 5. PERSIST: Save to database (async, fire-and-forget with error handling)
        _ = Task.Run(async () =>
        {
            try
            {
                await PersistReputationAsync(reputation, ct);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[SECURITY] Failed to persist peer reputation");
            }
        }, ct);
    }
    
    public async Task<bool> IsPeerBannedAsync(string peerId, CancellationToken ct)
    {
        // 1. VALIDATE: Peer ID
        if (!IsValidPeerId(peerId))
        {
            _logger.Warning("[SECURITY] Invalid peer ID for ban check");
            return false; // Conservative: don't ban on invalid input
        }
        
        // 2. CHECK: In-memory cache first (fast path)
        if (_reputations.TryGetValue(peerId, out var reputation))
        {
            return reputation.IsBanned;
        }
        
        // 3. FALLBACK: Database lookup
        try
        {
            return await LoadPeerBanStatusAsync(peerId, ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[SECURITY] Failed to load peer ban status");
            return false; // Fail open (conservative)
        }
    }
    
    // REQUIRED: Peer ID hashing for logging (one-way, consistent)
    private string HashPeerId(string peerId)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(peerId + _secretSalt);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).Substring(0, 16); // 16 hex chars
    }
    
    // REQUIRED: Reputation decay (prevent permanent bans)
    private double CalculateReputationScore(PeerReputation reputation)
    {
        var decayFactor = Math.Exp(-0.1 * reputation.DaysSinceLastEvent); // Exponential decay
        var weightedScore = reputation.Events
            .Sum(e => GetEventWeight(e.ReasonCode) * decayFactor);
        
        return weightedScore;
    }
}

// REQUIRED: Reputation data model
internal class PeerReputation
{
    public string PeerId { get; init; }
    public bool IsBanned { get; set; }
    public int EventCount => Events.Count;
    public double DaysSinceLastEvent => 
        (DateTime.UtcNow - Events.Max(e => e.Timestamp)).TotalDays;
    
    public List<ReputationEvent> Events { get; } = new();
    
    private DateTime _lastEventTime = DateTime.MinValue;
    private const int MaxEventsPerMinute = 10; // Rate limit
    
    public bool TryRecordEvent(PeerReport report, ReputationOptions options)
    {
        // Rate limiting: Prevent event flooding
        var now = DateTime.UtcNow;
        if ((now - _lastEventTime).TotalSeconds < 6) // Max 10/min = 1 per 6s
        {
            return false;
        }
        
        Events.Add(new ReputationEvent
        {
            ReasonCode = report.ReasonCode,
            Timestamp = now
        });
        
        _lastEventTime = now;
        
        // Sliding window: Keep only recent events (last 30 days)
        var cutoff = now.AddDays(-30);
        Events.RemoveAll(e => e.Timestamp < cutoff);
        
        return true;
    }
}
```

### 3.2 Reputation Persistence - Secure Storage

**REQUIRED**: Reputation data must be encrypted at rest and access-controlled.

```csharp
public class SecureReputationStore
{
    // REQUIRED: Encryption for sensitive reputation data
    private readonly IDataProtectionProvider _dataProtection;
    private readonly IDataProtector _protector;
    
    public SecureReputationStore(IDataProtectionProvider dataProtection)
    {
        _dataProtection = dataProtection;
        _protector = dataProtection.CreateProtector("MCP.PeerReputation.v1");
    }
    
    public async Task PersistReputationAsync(PeerReputation reputation, CancellationToken ct)
    {
        // 1. SERIALIZE: To JSON
        var json = JsonSerializer.Serialize(reputation);
        
        // 2. ENCRYPT: Protect data at rest
        var encrypted = _protector.Protect(json);
        
        // 3. STORE: In dedicated reputation database
        using var context = _contextFactory.CreateDbContext();
        
        var record = await context.PeerReputations
            .FirstOrDefaultAsync(r => r.PeerId == reputation.PeerId, ct);
        
        if (record == null)
        {
            record = new PeerReputationRecord
            {
                PeerId = reputation.PeerId,
                EncryptedData = encrypted,
                LastUpdated = DateTime.UtcNow
            };
            context.PeerReputations.Add(record);
        }
        else
        {
            record.EncryptedData = encrypted;
            record.LastUpdated = DateTime.UtcNow;
        }
        
        await context.SaveChangesAsync(ct);
    }
}
```

---

## 4. MCP Configuration Hardening

### 4.1 Secure Defaults

**REQUIRED**: MCP configuration must be secure by default.

```csharp
public class ModerationOptions
{
    /// <summary>
    /// Master kill switch for MCP.
    /// </summary>
    [Argument(default, "mcp-enabled")]
    [EnvironmentVariable("MCP_ENABLED")]
    [Description("Enable Moderation / Control Plane")]
    public bool Enabled { get; init; } = true; // ON by default for safety
    
    /// <summary>
    /// Hash blocklist configuration.
    /// </summary>
    [Validate]
    public HashBlocklistOptions HashBlocklist { get; init; } = new();
    
    /// <summary>
    /// External moderation service configuration.
    /// </summary>
    [Validate]
    public ExternalModerationOptions ExternalModeration { get; init; } = new();
    
    /// <summary>
    /// Peer reputation configuration.
    /// </summary>
    [Validate]
    public ReputationOptions Reputation { get; init; } = new();
    
    /// <summary>
    /// Fail-safe mode: What to do when MCP providers fail.
    /// </summary>
    [Argument(default, "mcp-failsafe-mode")]
    [EnvironmentVariable("MCP_FAILSAFE_MODE")]
    [Description("Failsafe mode: 'block' (conservative) or 'allow' (permissive)")]
    public string FailsafeMode { get; init; } = "block"; // Conservative default
}

public class HashBlocklistOptions
{
    /// <summary>
    /// Enable hash blocklist checking.
    /// </summary>
    public bool Enabled { get; init; } = false; // OFF by default (requires operator config)
    
    /// <summary>
    /// Blocklist source (file path or HTTPS URL).
    /// </summary>
    [Required(ErrorMessage = "HashBlocklist.Source required when Enabled=true")]
    public string? Source { get; init; } = null;
    
    /// <summary>
    /// Allowed domains for remote blocklist fetching.
    /// </summary>
    public string[] AllowedDomains { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Refresh interval (hours).
    /// </summary>
    [Range(1, 168)] // 1 hour to 7 days
    public int RefreshIntervalHours { get; init; } = 24;
}

public class ReputationOptions
{
    /// <summary>
    /// Enable peer reputation tracking.
    /// </summary>
    public bool Enabled { get; init; } = true; // ON by default
    
    /// <summary>
    /// Auto-ban threshold (weighted score).
    /// </summary>
    [Range(-100, 0)] // Negative score = ban
    public int AutoBanThreshold { get; init; } = -10;
    
    /// <summary>
    /// Event weights for reputation calculation.
    /// </summary>
    public Dictionary<string, int> EventWeights { get; init; } = new()
    {
        ["associated_with_blocked_content"] = -5,
        ["requested_blocked_content"] = -2,
        ["repeated_violations"] = -10
    };
    
    /// <summary>
    /// Reputation decay rate (days).
    /// </summary>
    [Range(1, 365)]
    public int DecayPeriodDays { get; init; } = 30;
}
```

### 4.2 Configuration Validation

**REQUIRED**: Validate MCP configuration at startup.

```csharp
public class ModerationOptionsValidator : IValidateOptions<ModerationOptions>
{
    public ValidateOptionsResult Validate(string name, ModerationOptions options)
    {
        var errors = new List<string>();
        
        // 1. Hash blocklist validation
        if (options.HashBlocklist.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.HashBlocklist.Source))
            {
                errors.Add("HashBlocklist.Source required when Enabled=true");
            }
            else if (options.HashBlocklist.Source.StartsWith("http://"))
            {
                errors.Add("HashBlocklist.Source must use HTTPS, not HTTP");
            }
            else if (options.HashBlocklist.Source.StartsWith("https://"))
            {
                // Validate domain allowlist
                if (!Uri.TryCreate(options.HashBlocklist.Source, UriKind.Absolute, out var uri))
                {
                    errors.Add("HashBlocklist.Source is not a valid URL");
                }
                else if (!options.HashBlocklist.AllowedDomains.Contains(uri.Host))
                {
                    errors.Add($"HashBlocklist domain '{uri.Host}' not in AllowedDomains");
                }
            }
        }
        
        // 2. External moderation validation
        if (options.ExternalModeration.Enabled)
        {
            if (options.ExternalModeration.AllowedDomains.Length == 0)
            {
                errors.Add("ExternalModeration.AllowedDomains required when Enabled=true");
            }
            
            if (options.ExternalModeration.TimeoutMs < 100)
            {
                errors.Add("ExternalModeration.TimeoutMs too low (minimum 100ms)");
            }
        }
        
        // 3. Failsafe mode validation
        if (options.FailsafeMode != "block" && options.FailsafeMode != "allow")
        {
            errors.Add("FailsafeMode must be 'block' or 'allow'");
        }
        
        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }
        
        return ValidateOptionsResult.Success;
    }
}
```

---

## 5. MCP Error Handling & Fail-Safe

### 5.1 Graceful Degradation

**REQUIRED**: MCP must handle failures gracefully without crashing or leaking data.

```csharp
public class CompositeModerationProvider : IModerationProvider
{
    public async Task<ModerationDecision> CheckLocalFileAsync(
        LocalFileMetadata file, 
        CancellationToken ct)
    {
        var decisions = new List<ModerationDecision>();
        
        // 1. HASH BLOCKLIST (if enabled)
        if (_hashBlocklist != null)
        {
            try
            {
                var isBlocked = await _hashBlocklist.IsBlockedHashAsync(file.PrimaryHash, ct);
                if (isBlocked)
                {
                    return new ModerationDecision
                    {
                        Verdict = ModerationVerdict.Blocked,
                        Reason = "hash_blocklist",
                        EvidenceKeys = new[] { "provider:blocklist" }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[SECURITY] Hash blocklist check failed | Id={Id}", file.Id);
                
                // FAILSAFE: Apply configured mode
                if (_options.FailsafeMode == "block")
                {
                    _logger.Warning("[SECURITY] Failsafe mode 'block' activated");
                    return new ModerationDecision
                    {
                        Verdict = ModerationVerdict.Blocked,
                        Reason = "failsafe_block_on_error"
                    };
                }
                // Otherwise: Continue to next provider
            }
        }
        
        // 2. EXTERNAL MODERATION (if enabled)
        if (_externalClient != null)
        {
            try
            {
                var decision = await _externalClient.AnalyzeFileAsync(file, ct);
                if (decision.Verdict == ModerationVerdict.Blocked || 
                    decision.Verdict == ModerationVerdict.Quarantined)
                {
                    return decision;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[SECURITY] External moderation check failed | Id={Id}", file.Id);
                
                // FAILSAFE: Apply configured mode
                if (_options.FailsafeMode == "block")
                {
                    return new ModerationDecision
                    {
                        Verdict = ModerationVerdict.Blocked,
                        Reason = "failsafe_block_on_error"
                    };
                }
            }
        }
        
        // 3. DEFAULT: No blockers found
        return new ModerationDecision
        {
            Verdict = ModerationVerdict.Unknown,
            Reason = "no_blockers_triggered"
        };
    }
}
```

---

## 6. MCP Testing Requirements

### 6.1 Security Test Cases

**REQUIRED**: All MCP implementations must include these security tests.

```csharp
public class McpSecurityTests
{
    [Fact]
    public async Task HashBlocklist_Never_Logs_Raw_Hash()
    {
        // Arrange
        var logCapture = new TestLoggerSink();
        var checker = CreateHashBlocklistChecker(logCapture);
        
        // Act
        await checker.IsBlockedHashAsync("deadbeef1234567890abcdef", CancellationToken.None);
        
        // Assert
        var logs = logCapture.GetLogs();
        Assert.DoesNotContain("deadbeef1234567890abcdef", logs); // Full hash not logged
        Assert.Contains("deadbeef", logs); // Prefix OK
    }
    
    [Fact]
    public async Task PeerReputation_Never_Logs_External_Username()
    {
        // Arrange
        var logCapture = new TestLoggerSink();
        var store = CreateReputationStore(logCapture);
        
        // Act
        await store.RecordPeerEventAsync("soulseek:alice123", new PeerReport
        {
            ReasonCode = "requested_blocked_content"
        }, CancellationToken.None);
        
        // Assert
        var logs = logCapture.GetLogs();
        Assert.DoesNotContain("alice123", logs); // Username not logged
        Assert.DoesNotContain("soulseek:", logs); // Protocol prefix not logged
    }
    
    [Fact]
    public async Task ExternalModeration_Blocks_SSRF_Attempts()
    {
        // Arrange
        var options = new ExternalModerationOptions
        {
            Enabled = true,
            AllowedDomains = new[] { "trusted-moderator.com" }
        };
        var client = CreateExternalModerationClient(options);
        
        // Act & Assert
        await Assert.ThrowsAsync<SecurityException>(async () =>
        {
            await client.AnalyzeFileAsync(new LocalFileMetadata
            {
                // Attempt SSRF to internal network
                ApiEndpoint = "http://169.254.169.254/latest/meta-data/"
            }, CancellationToken.None);
        });
    }
    
    [Fact]
    public async Task MCP_Respects_Failsafe_Block_Mode()
    {
        // Arrange
        var provider = CreateCompositeModerationProvider(
            failsafeMode: "block",
            hashBlocklist: CreateFaultyHashBlocklist() // Will throw
        );
        
        // Act
        var decision = await provider.CheckLocalFileAsync(
            new LocalFileMetadata { Id = "test" }, 
            CancellationToken.None);
        
        // Assert
        Assert.Equal(ModerationVerdict.Blocked, decision.Verdict);
        Assert.Equal("failsafe_block_on_error", decision.Reason);
    }
    
    [Fact]
    public async Task PeerReputation_Rate_Limits_Event_Flooding()
    {
        // Arrange
        var store = CreateReputationStore();
        var peerId = "malicious-peer";
        
        // Act: Try to flood with events
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => store.RecordPeerEventAsync(peerId, new PeerReport
            {
                ReasonCode = "spam"
            }, CancellationToken.None));
        
        await Task.WhenAll(tasks);
        
        // Assert: Not all events recorded (rate limited)
        var reputation = await store.GetReputationAsync(peerId);
        Assert.True(reputation.EventCount < 20); // Max ~10/min
    }
}
```

---

## 7. MCP Observability & Metrics

### 7.1 Required Metrics

**REQUIRED**: Expose these metrics for MCP monitoring (NO sensitive data in labels).

```csharp
// File checks
mcp_file_checks_total{verdict=allowed|blocked|quarantined|unknown, provider=hash|external}
mcp_file_check_duration_seconds{provider=hash|external}

// Content checks
mcp_content_checks_total{verdict=allowed|blocked|quarantined|unknown}

// Peer reputation
mcp_peer_events_total{reason_code=...}
mcp_peer_bans_total{reason=reputation_threshold|manual}
mcp_peer_ban_checks_total

// Errors
mcp_errors_total{component=hash_blocklist|external_api|reputation}
mcp_failsafe_activations_total{mode=block|allow}

// Blocklist
mcp_blocklist_size_total
mcp_blocklist_last_refresh_timestamp_seconds
```

### 7.2 Health Checks

**REQUIRED**: MCP health endpoint for monitoring.

```csharp
public class McpHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken ct)
    {
        var data = new Dictionary<string, object>();
        
        try
        {
            // Check hash blocklist status
            if (_hashBlocklist != null)
            {
                var blocklistAge = DateTime.UtcNow - _hashBlocklist.LastRefreshTime;
                data["blocklist_age_hours"] = blocklistAge.TotalHours;
                
                if (blocklistAge.TotalHours > 48)
                {
                    return HealthCheckResult.Degraded("Hash blocklist stale", data: data);
                }
            }
            
            // Check peer reputation store
            if (_reputationStore != null)
            {
                var bannedCount = await _reputationStore.GetBannedCountAsync(ct);
                data["banned_peers"] = bannedCount;
            }
            
            return HealthCheckResult.Healthy("MCP operational", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MCP error", ex, data);
        }
    }
}
```

---

## 8. MCP Audit Log

### 8.1 Required Audit Events

**CRITICAL**: All security-relevant MCP decisions must be auditable.

```csharp
public class McpAuditLogger
{
    public void LogFileBlocked(string fileId, ModerationDecision decision)
    {
        _securityLogger.LogWarning(
            "[AUDIT] [MCP] File blocked | " +
            "Id={Id} | Verdict={Verdict} | Reason={Reason} | " +
            "Evidence={Evidence} | Timestamp={Timestamp}",
            fileId,
            decision.Verdict,
            decision.Reason,
            string.Join(",", decision.EvidenceKeys),
            DateTime.UtcNow);
    }
    
    public void LogPeerBanned(string peerId, string reason, double score)
    {
        _securityLogger.LogWarning(
            "[AUDIT] [MCP] Peer banned | " +
            "PeerHash={PeerHash} | Reason={Reason} | Score={Score} | " +
            "Timestamp={Timestamp}",
            HashPeerId(peerId),
            reason,
            score,
            DateTime.UtcNow);
    }
    
    public void LogExternalModerationCall(string fileId, string endpoint, ModerationDecision decision)
    {
        _securityLogger.LogInformation(
            "[AUDIT] [MCP] External moderation | " +
            "Id={Id} | Endpoint={Endpoint} | Verdict={Verdict} | " +
            "Duration={Duration}ms | Timestamp={Timestamp}",
            fileId,
            endpoint,
            decision.Verdict,
            duration,
            DateTime.UtcNow);
    }
}
```

---

## 9. MCP Anti-Slop Checklist

Before shipping any MCP code, verify:

### Privacy
- [ ] No raw hashes in logs or metrics
- [ ] No full filesystem paths in logs
- [ ] No external usernames/IPs in logs
- [ ] Evidence keys are opaque identifiers
- [ ] Peer IDs are hashed for logging

### External Services
- [ ] SSRF protection via domain allowlist
- [ ] Request size limits enforced
- [ ] Timeouts configured
- [ ] Work budget integration
- [ ] No sensitive data in external requests

### Hash Blocklist
- [ ] Timing attack mitigation (bloom filter)
- [ ] Constant-time comparisons where possible
- [ ] Secure loading from trusted sources only
- [ ] Size bounds validation

### Peer Reputation
- [ ] Sybil resistance (rate limiting)
- [ ] Reputation decay implemented
- [ ] Auto-ban thresholds configurable
- [ ] Encrypted persistence
- [ ] No external identifiers stored

### Configuration
- [ ] Secure defaults (MCP enabled, blocklist disabled)
- [ ] Validation at startup
- [ ] Failsafe mode configured
- [ ] HTTPS-only for remote sources

### Error Handling
- [ ] Graceful degradation on provider failure
- [ ] Failsafe mode respected
- [ ] No data leaks in error messages
- [ ] All exceptions caught and logged safely

### Testing
- [ ] Security tests for logging (no leaks)
- [ ] SSRF protection tests
- [ ] Rate limiting tests
- [ ] Failsafe mode tests
- [ ] Timing attack resistance tests

### Observability
- [ ] Metrics exposed (no sensitive labels)
- [ ] Health checks implemented
- [ ] Audit log for security events
- [ ] Dashboard for MCP status

---

## 10. Summary

**The MCP layer is CRITICAL for legal/ethical protection. Security is non-negotiable.**

Key principles:
1. **Privacy First**: Never log hashes, paths, or external identifiers
2. **Fail Safe**: Block on error, don't fail open
3. **Rate Limit Everything**: Work budget, reputation events, external calls
4. **SSRF Protection**: Domain allowlists for all external services
5. **Audit Everything**: Security events must be traceable

**If in doubt, be MORE paranoid, not less.**

---

**Status**: MANDATORY for T-MCP01-04  
**Review Required**: Security team + legal team  
**Next**: Begin T-MCP01 implementation with these requirements

