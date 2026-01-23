# Security Comparison: slskdN (whatAmIThinking) vs. Upstream slskd

**Date**: January 21, 2026  
**Analysis**: Comparing security features between slskdN master branch and upstream slskd

---

## Executive Summary

**Verdict**: Our security features are primarily **protecting our NEW additions** (mesh networking, DHT, multi-source downloads, pods), but we **ARE fixing at least one path traversal vulnerability** that exists in the upstream project.

---

## 1. Path Traversal Protection

### 🔴 **UPSTREAM HAS VULNERABILITY** (Severity: MEDIUM-HIGH)

**Upstream Issue (FileService.cs:263)**:
```csharp
// important! we must fully expand the path with GetFullPath() to resolve a given relative directory, like '..'
if (!AllowedDirectories.Any(allowed => directory.StartsWith(allowed)))
{
    throw new UnauthorizedException($"Only application-controlled directories can be deleted");
}
```

**Problem**: Uses `string.StartsWith()` for path containment checking, which is vulnerable to **prefix matching attacks**.

**Example Exploit**:
- Allowed directory: `/var/lib/slskd`
- Attacker creates: `/var/lib/slskd-evil/malicious`
- `"/var/lib/slskd-evil/malicious".StartsWith("/var/lib/slskd")` → **TRUE** ✅ (incorrectly passes!)
- Attacker can now access files outside the intended directory

**Our Fix (PathGuard.cs:152-162)**:
```csharp
// 13. Ensure root ends with separator for proper prefix matching
if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
{
    fullRoot += Path.DirectorySeparatorChar;
}

// 14. Ensure result is still under root (handles symlinks, etc.)
if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) &&
    !fullPath.Equals(fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
{
    return null;
}
```

**Why it's better**:
- Adds directory separator to root before comparison
- Prevents partial prefix matching (e.g., `/var/lib/slskd/` vs `/var/lib/slskd-evil/`)
- More comprehensive validation (15 checks vs upstream's basic path combination)

**Additional Protections We Have**:
1. Unicode normalization (prevents homoglyph attacks)
2. URL-encoded traversal detection (catches `%2e%2e` and double-encoding)
3. Control character detection
4. Null byte detection
5. Maximum path depth enforcement
6. Windows drive letter rejection
7. Explicit traversal pattern detection

**Recommendation**: We should report this to upstream slskd.

---

## 2. CSRF Protection

### 🟡 **NEITHER PROJECT HAS CSRF TOKENS** (Severity: LOW-MEDIUM)

**Upstream**: No CSRF protection found (no AntiForgery tokens, no CSRF middleware)
**slskdN**: No CSRF protection found in core API

**Mitigating Factors** (for both projects):
- Authentication required for most endpoints (JWT/Bearer tokens)
- Same-origin policy in browsers
- Not typically accessed from untrusted web pages

**However**: Our Service Fabric Gateway **does** have API key authentication:
- Located in: `src/slskd/Mesh/ServiceFabric/MeshServiceRouter.cs`
- Uses API keys for service authentication
- This is only for mesh services, not the main web UI

**Verdict**: Both projects equally vulnerable to CSRF on authenticated endpoints. Not a high priority due to authentication requirements, but should be addressed eventually.

---

## 3. Rate Limiting / DoS Protection

### 🔴 **UPSTREAM HAS MINIMAL PROTECTION** (Severity: MEDIUM)

**Upstream**:
- Has `RateLimiter.cs` - but this is a **timer-based debouncing utility**, NOT a security rate limiter
- Used for internal event throttling (e.g., "don't update UI more than once per second")
- **No network-level rate limiting**
- **No connection caps**
- **No request throttling per IP**

**slskdN**:
- **NetworkGuard.cs**: Comprehensive network-level protection
  - Max connections per IP (default: 100)
  - Max global connections (default: 100)
  - Max messages per minute (default: 60)
  - Max message size (64KB)
  - Max pending requests per IP (10)
  - Automatic cleanup of expired trackers
  
- **ViolationTracker.cs**: Auto-escalating ban system
  - Tracks violations per peer/IP
  - Automatic temporary bans
  - Escalating ban durations
  - Permanent bans for repeated abuse

**Example Protection**:
```csharp
// Our NetworkGuard prevents:
// 1. Connection floods (100 connections max per IP)
// 2. Message spam (60 messages/minute max)
// 3. Global resource exhaustion (100 total connections)
```

**Verdict**: Upstream is **vulnerable to connection floods and DoS attacks**. Our NetworkGuard fixes this.

**However**: NetworkGuard is **primarily used for our mesh overlay**, not the main Soulseek client connections (which use Soulseek.NET library's built-in limits).

---

## 4. Content Safety / File Upload Validation

### 🟢 **slskdN HAS ADDITIONAL PROTECTIONS** (New Feature)

**Upstream**:
- Basic file type filtering in shares
- No magic byte verification
- No executable detection beyond extension checking

**slskdN**:
- **ContentSafety.cs** (from our security framework):
  - Magic byte verification (detects `.exe` disguised as `.mp3`)
  - Dangerous extension blocking (`.exe`, `.bat`, `.dll`, `.sh`, etc.)
  - Safe audio extension allowlist
  - File quarantine for suspicious content

**Verdict**: This is protecting **users downloading from our mesh**, not fixing an upstream bug.

---

## 5. Input Validation

### 🟢 **slskdN HAS MORE COMPREHENSIVE VALIDATION**

**Both have**:
- ASP.NET Core model validation
- Required field validation
- Basic data type validation

**slskdN adds**:
- **PathGuard** (15-step path validation)
- **LoggingSanitizer** (prevents log injection, PII leakage)
- **ViolationTracker** (behavioral anomaly detection)
- **EntropyMonitor** (RNG health checking)

**Verdict**: Our additional validation is **primarily for new features** (mesh, DHT, pods), but PathGuard **does fix upstream's path validation issue**.

---

## 6. Summary Table

| Security Issue | Upstream slskd | slskdN whatAmIThinking | Fixing Upstream Bug? |
|---------------|----------------|------------------------|---------------------|
| **Path Traversal (Prefix Matching)** | ❌ Vulnerable | ✅ Fixed | **YES** ✅ |
| **Path Traversal (URL Encoding)** | ⚠️ Basic | ✅ Comprehensive | **YES** ✅ |
| **Path Traversal (Unicode)** | ❌ None | ✅ Normalized | **YES** ✅ |
| **CSRF Protection** | ❌ None | ❌ None | NO |
| **Rate Limiting (Network)** | ❌ None | ✅ NetworkGuard | **PARTIAL** 🟡 |
| **Connection Caps** | ❌ None | ✅ Per-IP limits | **PARTIAL** 🟡 |
| **Magic Byte Verification** | ❌ None | ✅ ContentSafety | NO (new feature) |
| **Behavioral Anomaly Detection** | ❌ None | ✅ ViolationTracker | NO (new feature) |
| **Log Sanitization** | ⚠️ Basic | ✅ Comprehensive | NO (enhancement) |
| **Byzantine Consensus** | ❌ None | ✅ Multi-source only | NO (new feature) |
| **Cryptographic Commitment** | ❌ None | ✅ Mesh transfers | NO (new feature) |
| **Peer Reputation** | ❌ None | ✅ Behavioral scoring | NO (new feature) |

---

## 7. Detailed Recommendations

### For Upstream slskd:

1. **Fix Path Traversal Vulnerability** (HIGH PRIORITY)
   - Change `directory.StartsWith(allowed)` to proper path containment check
   - Add directory separator to root before comparison
   - See our `PathGuard.IsContainedIn()` implementation

2. **Add Network-Level Rate Limiting** (MEDIUM PRIORITY)
   - Connection caps per IP
   - Request throttling
   - Global resource limits

3. **Consider CSRF Protection** (LOW PRIORITY)
   - Add AntiForgery tokens to state-changing endpoints
   - Or use SameSite cookies with proper configuration

### For slskdN:

1. **Report Path Traversal Issue to Upstream** (IMMEDIATE)
   - Document the `StartsWith` vulnerability
   - Provide proof-of-concept exploit
   - Offer our PathGuard as a solution

2. **Add CSRF Protection** (MEDIUM PRIORITY)
   - Service Fabric already has API keys
   - Main web UI should have CSRF tokens

3. **Document Security Features** (ONGOING)
   - Make it clear which features protect new additions
   - Which features fix upstream issues
   - Don't oversell security claims

---

## 8. Conclusion

**Are we fixing upstream bugs?**
- **YES**: Path traversal vulnerability (prefix matching)
- **YES**: Lack of URL-encoding/Unicode normalization in path validation
- **PARTIAL**: Network DoS protection (though primarily for our mesh overlay)
- **NO**: CSRF (neither project has it)

**Are our security features primarily for new additions?**
- **YES**: ContentSafety, Byzantine Consensus, Peer Reputation, Cryptographic Commitment are all for mesh/multi-source features
- **BUT**: PathGuard is universal and fixes a real upstream vulnerability

**Severity Assessment**:
- Path traversal: **MEDIUM-HIGH** (exploitable if attacker has filesystem access to create directories)
- DoS: **MEDIUM** (can exhaust server resources)
- CSRF: **LOW-MEDIUM** (mitigated by authentication requirements)

**Recommended Action**:
1. Report path traversal issue to upstream (responsible disclosure)
2. Continue using PathGuard for all file operations
3. Consider backporting NetworkGuard to protect core Soulseek client endpoints
4. Add CSRF protection to both projects

---

**Analysis by**: AI Security Review  
**Verified by**: Manual code inspection of both repositories  
**Scope**: Core file handling, network security, authentication/authorization
