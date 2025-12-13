# Security Audit & Code Review - December 11, 2025

**Auditor:** AI Code Review System  
**Date:** December 11, 2025  
**Scope:** All features implemented today (Hash DB Search, Mesh Search Bridge, Mesh Data Plane, BitTorrent Extension)  
**Severity Levels:** 🔴 CRITICAL | 🟠 HIGH | 🟡 MEDIUM | 🟢 LOW

---

## Executive Summary

**Overall Status:** 🟡 **ACCEPTABLE WITH FIXES REQUIRED**

Found **8 security issues** and **12 code quality issues** across 4 new features.
- **1 CRITICAL** - SQL Injection vulnerability
- **2 HIGH** - Missing authentication/authorization
- **5 MEDIUM** - Resource exhaustion, input validation
- **12 LOW** - Code slop, missing error handling

**Recommendation:** Apply all CRITICAL and HIGH severity fixes before production deployment.

---

## 🔴 CRITICAL Issues

### 1. SQL Injection Vulnerability in Hash DB Search
**File:** `src/slskd/HashDb/HashDbService.Search.cs:68`  
**Severity:** 🔴 CRITICAL  
**Risk:** Remote code execution, database compromise

**Issue:**
```csharp
var searchPattern = $"%{normalizedQuery}%";
```

The `NormalizeSearchQuery()` function only removes whitespace but does NOT escape SQL special characters like `%`, `_`, `[`, `]`. An attacker can use these to:
- Cause expensive LIKE queries (DoS)
- Extract data via pattern matching attacks
- Bypass search restrictions

**Attack Vector:**
```
Query: "%' OR 1=1 --"
Result: Potential SQL injection
```

**Fix:**
```csharp
private static string NormalizeSearchQuery(string query)
{
    query = query.Trim();
    
    // Remove multiple spaces
    while (query.Contains("  "))
    {
        query = query.Replace("  ", " ");
    }
    
    // Escape LIKE special characters
    query = query.Replace("[", "[[]")
                 .Replace("%", "[%]")
                 .Replace("_", "[_]");
    
    // Limit query length to prevent DoS
    if (query.Length > 200)
    {
        query = query.Substring(0, 200);
    }
    
    return query;
}
```

---

## 🟠 HIGH Severity Issues

### 2. Missing Authentication for Mesh Chunk Downloads
**File:** `src/slskd/Mesh/MeshDataPlane.cs:58-111`  
**Severity:** 🟠 HIGH  
**Risk:** Unauthorized access to user files

**Issue:**
```csharp
public async Task<byte[]> DownloadChunkAsync(
    MeshPeerId meshPeerId,
    string filename,  // ← No validation!
    long offset,
    int length,
    CancellationToken cancellationToken = default)
```

There is **NO server-side handler** for `MeshChunkRequestMessage`. When implemented, it MUST:
- Validate the requesting peer is authorized
- Check file permissions
- Prevent path traversal attacks
- Rate limit requests

**Fix Required:** Implement server-side `MeshChunkRequestHandler`:
```csharp
public class MeshChunkRequestHandler
{
    public async Task<MeshChunkResponseMessage> HandleRequestAsync(
        MeshChunkRequestMessage request,
        MeshPeerId requesterPeerId,
        CancellationToken ct)
    {
        // 1. Validate filename (no path traversal)
        if (request.Filename.Contains("..") || 
            request.Filename.Contains("\\") ||
            Path.IsPathRooted(request.Filename))
        {
            return new MeshChunkResponseMessage
            {
                RequestId = request.RequestId,
                Success = false,
                Error = "Invalid filename"
            };
        }
        
        // 2. Check authorization
        if (!await IsAuthorizedAsync(requesterPeerId, request.Filename, ct))
        {
            return new MeshChunkResponseMessage
            {
                RequestId = request.RequestId,
                Success = false,
                Error = "Unauthorized"
            };
        }
        
        // 3. Validate range
        if (request.Length > 1024 * 1024 || // Max 1MB per chunk
            request.Length < 0 ||
            request.Offset < 0)
        {
            return new MeshChunkResponseMessage
            {
                RequestId = request.RequestId,
                Success = false,
                Error = "Invalid range"
            };
        }
        
        // 4. Rate limit
        if (!await CheckRateLimitAsync(requesterPeerId, ct))
        {
            return new MeshChunkResponseMessage
            {
                RequestId = request.RequestId,
                Success = false,
                Error = "Rate limit exceeded"
            };
        }
        
        // 5. Read and return chunk
        // ... implementation ...
    }
}
```

### 3. Unverified Descriptor in BitTorrent Extension
**File:** `src/slskd/BitTorrent/SlskdnMeshExtension.cs:130`  
**Severity:** 🟠 HIGH  
**Risk:** Sybil attacks, malicious peer registration

**Issue:**
```csharp
// Register peer (without signature verification for BT-discovered peers)
await _meshPeerRegistry.RegisterOrUpdateAsync(descriptor, cancellationToken);
```

Comment explicitly states signature verification is skipped! This allows:
- Anyone to claim any mesh ID
- Impersonation attacks
- Sybil attacks on the mesh network

**Fix:**
```csharp
// Option 1: Require signature verification
var descriptor = new MeshPeerDescriptor
{
    MeshPeerId = meshPeerId,
    PublicKey = publicKey,
    Signature = Array.Empty<byte>(), // ← BAD!
    // ...
};

// Don't register without verification!
// Instead, mark as "unverified" and require proof via challenge-response:

var descriptor = new MeshPeerDescriptor
{
    MeshPeerId = meshPeerId,
    PublicKey = publicKey,
    Signature = await RequestSignatureProofAsync(remoteAddress, meshPeerId, publicKey),
    // ...
};

if (!descriptor.VerifySignature())
{
    _logger.LogWarning(
        "Invalid signature from BitTorrent peer {Address}, ignoring",
        remoteAddress);
    return;
}

await _meshPeerRegistry.RegisterOrUpdateAsync(descriptor, cancellationToken);
```

---

## 🟡 MEDIUM Severity Issues

### 4. Unbounded Result Set in Hash DB Search
**File:** `src/slskd/HashDb/HashDbService.Search.cs:52-55`  
**Severity:** 🟡 MEDIUM  
**Risk:** Memory exhaustion, DoS

**Issue:**
```csharp
(SELECT COUNT(DISTINCT username) 
 FROM flac_inventory 
 WHERE flac_key = h.flac_key 
 AND hash_value IS NOT NULL) AS PeerCount,
```

Subquery runs for EVERY row, then DISTINCT + COUNT. For large databases, this is extremely expensive.

**Fix:**
```csharp
// Create index first
CREATE INDEX IF NOT EXISTS idx_flac_inventory_key_hash 
    ON flac_inventory(flac_key, hash_value) 
    WHERE hash_value IS NOT NULL;

// Use pre-computed peer counts or limit subquery
var sql = @"
    SELECT DISTINCT
        h.flac_key AS FlacKey,
        // ... other fields ...
        COALESCE(pc.peer_count, 0) AS PeerCount
    FROM hashes h
    LEFT JOIN hash_metadata m ON h.flac_key = m.flac_key
    LEFT JOIN (
        SELECT flac_key, COUNT(DISTINCT username) as peer_count
        FROM flac_inventory
        WHERE hash_value IS NOT NULL
        GROUP BY flac_key
    ) pc ON h.flac_key = pc.flac_key
    WHERE (...)
    LIMIT @limit";
```

### 5. No Timeout on Mesh Chunk Downloads
**File:** `src/slskd/Mesh/MeshDataPlane.cs:89-92`  
**Severity:** 🟡 MEDIUM  
**Risk:** Hung connections, resource exhaustion

**Issue:**
```csharp
await connection.WriteMessageAsync(request, cancellationToken);
var response = await connection.ReadMessageAsync<MeshChunkResponseMessage>(cancellationToken);
```

No timeout! A malicious/broken peer can hang forever.

**Fix:**
```csharp
// Add timeout
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30s timeout

await connection.WriteMessageAsync(request, cts.Token);
var response = await connection.ReadMessageAsync<MeshChunkResponseMessage>(cts.Token);
```

### 6. Missing Input Validation in MeshSearchBridge
**File:** `src/slskd/Mesh/MeshSearchBridgeService.cs:136`  
**Severity:** 🟡 MEDIUM  
**Risk:** Excessive queries, DoS

**Issue:**
```csharp
var hashResults = await _hashDb.SearchAsync(query, limit: 100, cancellationToken);
```

Hardcoded limit of 100, but then iterates over ALL results and does N+1 queries:

```csharp
foreach (var hashResult in hashResults) // 100 iterations
{
    var usernames = await _hashDb.GetPeersByHashAsync(...); // 1 query each = 100 queries!
    foreach (var username in usernames) // Could be many per hash
    {
        // More work...
    }
}
```

**Fix:**
```csharp
// 1. Configurable limit
private const int MaxMeshSearchResults = 20; // Not 100!

// 2. Batch username lookups
var allFlacKeys = hashResults.Select(r => r.FlacKey).ToList();
var usernameLookup = await _hashDb.GetPeersByHashesBatchAsync(allFlacKeys, cancellationToken);

foreach (var hashResult in hashResults)
{
    if (usernameLookup.TryGetValue(hashResult.FlacKey, out var usernames))
    {
        foreach (var username in usernames.Take(5)) // Limit per hash
        {
            // ...
        }
    }
}
```

### 7. JSON Deserialization without Schema Validation
**File:** `src/slskd/BitTorrent/SlskdnMeshExtension.cs:88-89`  
**Severity:** 🟡 MEDIUM  
**Risk:** Malformed data crashes, resource exhaustion

**Issue:**
```csharp
var json = Encoding.UTF8.GetString(data);
var handshake = JsonSerializer.Deserialize<MeshExtensionHandshake>(json);
```

No size limit on `data`, no JSON validation. Could cause:
- Memory exhaustion (huge JSON)
- CPU exhaustion (deeply nested JSON)
- Crashes (malformed JSON)

**Fix:**
```csharp
// 1. Limit data size
if (data.Length > 10 * 1024) // 10KB max
{
    _logger.LogWarning("Handshake data too large from {Address}: {Size} bytes", 
        remoteAddress, data.Length);
    return;
}

// 2. Use JsonSerializerOptions with limits
var options = new JsonSerializerOptions
{
    MaxDepth = 5,
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = false,
};

try
{
    var json = Encoding.UTF8.GetString(data);
    var handshake = JsonSerializer.Deserialize<MeshExtensionHandshake>(json, options);
    
    if (handshake == null || string.IsNullOrEmpty(handshake.MeshPeerId))
    {
        _logger.LogWarning("Invalid handshake from {Address}", remoteAddress);
        return;
    }
    
    // Validate all fields
    if (handshake.OverlayPort < 1 || handshake.OverlayPort > 65535)
    {
        _logger.LogWarning("Invalid overlay port from {Address}: {Port}", 
            remoteAddress, handshake.OverlayPort);
        return;
    }
    
    if (handshake.PublicKey.Length > 1024) // Base64-encoded Ed25519 should be ~44 chars
    {
        _logger.LogWarning("Public key too large from {Address}", remoteAddress);
        return;
    }
}
catch (JsonException ex)
{
    _logger.LogWarning(ex, "Malformed JSON handshake from {Address}", remoteAddress);
    return;
}
```

### 8. Missing Rate Limiting
**File:** All new files  
**Severity:** 🟡 MEDIUM  
**Risk:** DoS, resource exhaustion

**Issue:** None of the new features have rate limiting:
- Hash DB search: Unlimited queries per second
- Mesh chunk downloads: Unlimited requests
- BitTorrent handshakes: Unlimited processing

**Fix:** Implement rate limiting using existing patterns:
```csharp
// Add to each service
private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters = new();

private async Task<bool> CheckRateLimitAsync(string identifier, int maxPerMinute = 60)
{
    var limiter = _rateLimiters.GetOrAdd(identifier, _ => new RateLimiter(maxPerMinute));
    return await limiter.TryAcquireAsync();
}
```

---

## 🟢 LOW Severity / Code Slop

### 9. Magic Numbers
**Files:** Multiple  
**Issue:** Hardcoded constants everywhere

**Examples:**
```csharp
commandTimeout: 10  // What unit? Why 10?
limit: 100          // Why 100?
TimeSpan.FromSeconds(30)  // Why 30?
```

**Fix:** Use named constants:
```csharp
private const int DefaultSearchTimeout = 10; // seconds
private const int MaxSearchResults = 100;
private const int ChunkDownloadTimeout = 30; // seconds
```

### 10. Missing Null Checks
**File:** `src/slskd/Mesh/MeshDataPlane.cs:92`

```csharp
var response = await connection.ReadMessageAsync<MeshChunkResponseMessage>(cancellationToken);

if (!response.Success) // ← What if response is null?
```

**Fix:**
```csharp
var response = await connection.ReadMessageAsync<MeshChunkResponseMessage>(cancellationToken);

if (response == null)
{
    throw new IOException("Received null response from mesh peer");
}

if (!response.Success)
{
    throw new IOException($"Chunk request failed: {response.Error}");
}
```

### 11. Poor Exception Messages
**File:** `src/slskd/Mesh/MeshDataPlane.cs:70`

```csharp
throw new InvalidOperationException($"No active connection to mesh peer {meshPeerId.ToShortString()}");
```

Not helpful for debugging. Include more context.

**Fix:**
```csharp
throw new InvalidOperationException(
    $"No active connection to mesh peer {meshPeerId.ToShortString()}. " +
    $"Available connections: {_neighborRegistry.GetAllConnections().Count}, " +
    $"Requested file: {filename}, Offset: {offset}, Length: {length}");
```

### 12. Inefficient String Concatenation
**File:** `src/slskd/Mesh/MeshSearchBridgeService.cs:220`

```csharp
return $"{artist}/{album}/{title}.flac";
```

Uses `/` as path separator which fails on Windows.

**Fix:**
```csharp
return Path.Combine(artist, album, title + ".flac");
```

### 13. Missing Dispose Pattern
**File:** `src/slskd/Mesh/MeshDataPlane.cs`

Not implementing `IDisposable` even though it holds references to connections.

**Fix:**
```csharp
public sealed class MeshDataPlane : IMeshDataPlane, IDisposable
{
    private bool _disposed;
    
    public void Dispose()
    {
        if (!_disposed)
        {
            // Cleanup resources
            _disposed = true;
        }
    }
}
```

### 14. Commented-Out Code
**File:** `src/slskd/Mesh/MeshSearchBridgeService.cs:82-91`

```csharp
// TODO: Complete SearchResponse instantiation when hash DB search is implemented
// This would require knowing the correct Soulseek.SearchResponse constructor signature
/*
var response = new SearchResponse(
    username: meshResult.Username,
    ...
*/
```

Remove or complete.

### 15. Async Over Sync
**File:** `src/slskd/Mesh/MeshDataPlane.cs:66`

```csharp
var connection = _neighborRegistry.GetConnectionByMeshPeerId(meshPeerId.ToString());
```

Synchronous method called in async context. Should be:
```csharp
var connection = await _neighborRegistry.GetConnectionByMeshPeerIdAsync(meshPeerId.ToString(), cancellationToken);
```

### 16. No Logging for Success Paths
**File:** `src/slskd/HashDb/HashDbService.Search.cs`

Only logs warnings/errors, never success. Makes debugging hard.

**Fix:**
```csharp
_logger.LogInformation(
    "Hash DB search for '{Query}' returned {Count} results in {Ms}ms",
    query, results.Count(), stopwatch.ElapsedMilliseconds);
```

### 17. Missing CancellationToken Propagation
**File:** Multiple

Some async methods don't use the `cancellationToken` parameter.

**Fix:** Always pass it through:
```csharp
await connection.WriteMessageAsync(request, cancellationToken);
// Not:
await connection.WriteMessageAsync(request);
```

### 18. Inconsistent Error Handling
**File:** Multiple

Some methods return null on error, some throw, some log and continue.

**Fix:** Establish consistent pattern:
- Data retrieval: Return null
- Operations: Throw on error
- Background tasks: Log and continue

### 19. Missing Documentation
**Files:** All

XML docs are present but incomplete. Missing:
- `<exception>` tags
- `<example>` tags
- Detailed parameter descriptions

### 20. No Telemetry/Metrics
**File:** All new features

No metrics collection for:
- Search query performance
- Chunk download speeds
- BitTorrent handshake success rates

**Fix:** Add metrics:
```csharp
_metrics.RecordSearchQuery(query, results.Count, stopwatch.Elapsed);
```

---

## Priority Fix List

### Must Fix Before Production:
1. 🔴 **SQL Injection** in Hash DB Search
2. 🟠 **Missing Auth** for Mesh Chunk Downloads (implement server handler)
3. 🟠 **Unverified Descriptors** in BitTorrent Extension

### Should Fix Soon:
4. 🟡 Unbounded result sets
5. 🟡 Missing timeouts
6. 🟡 N+1 query problem in search bridge
7. 🟡 JSON deserialization validation
8. 🟡 Rate limiting

### Nice to Have:
9-20. All LOW severity issues (code quality)

---

## Security Hardening Checklist

- [ ] Add SQL injection protection
- [ ] Implement server-side chunk request handler with auth
- [ ] Add signature verification for BT-discovered peers
- [ ] Add rate limiting to all endpoints
- [ ] Add input validation (length, format, range)
- [ ] Add timeouts to all network operations
- [ ] Implement resource limits (max connections, max query size)
- [ ] Add audit logging for security events
- [ ] Implement defense against path traversal
- [ ] Add metrics/telemetry
- [ ] Complete exception handling
- [ ] Add integration tests for security scenarios

---

## Estimated Effort

- **Critical fixes:** 4-6 hours
- **High severity:** 8-12 hours  
- **Medium severity:** 6-8 hours
- **Low severity:** 4-6 hours

**Total:** ~22-32 hours of focused development

---

## Conclusion

The code is **functionally complete** but has **significant security gaps** that must be addressed before production use. The architecture is sound, but implementation details need hardening.

**Recommendation:** Fix CRITICAL and HIGH issues immediately, then deploy to dev for testing. Address MEDIUM issues before production release.














