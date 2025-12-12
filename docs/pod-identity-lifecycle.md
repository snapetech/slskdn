# Pod Identity & Lifecycle Management

**Status**: DRAFT - Core Architecture  
**Created**: December 11, 2025  
**Priority**: HIGH (foundational concept for multi-pod deployments)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

## Overview

This document defines what a "pod" is, how its identity is composed, how it persists, and how operators can manage the pod lifecycle (transfer, backup, retire, destroy).

**Core Principle**: A pod is **data + keys + config + services**. Persistence comes from storage; identity comes from keys.

---

## 1. What Is a Pod?

A **pod** consists of:

### Data (Persistent State)

- **Library Database** (VirtualSoulfind):
  - Works, items, quality scores, reconciliation state
  - SQLite or equivalent
- **MCP State**:
  - Reputation data (peer events, source scores)
  - Moderation decisions cache
  - Quarantined content metadata
- **Share Index**:
  - Local file catalog
  - Hash registry
- **Collections/Lists**:
  - User-created playlists, reading lists, watchlists
  - Social ingestion state (if federation enabled)

### Configuration

- **Application Config**:
  - Enabled features, backend configurations
  - Privacy modes, work budgets, rate limits
  - MCP configuration (providers, policies)
- **Network Config**:
  - Mesh/DHT settings
  - Soulseek credentials (per-backend)
  - Social federation settings

### Identity Material (Keys & Credentials)

- **Mesh/Pod Identity**:
  - Keypair for mesh/DHT overlay
  - `PodId = hash(publicKey_pod)`
- **Soulseek Identity**:
  - Username/password for Soulseek network
  - (Music domain backend only)
- **ActivityPub Identities**:
  - Per-actor keypairs (e.g., `@music@pod`, `@books@pod`)
  - Each domain actor has its own key
- **Admin Credentials**:
  - Local admin accounts (web UI, CLI, API)
  - Who can control the pod configuration

### Services (Runtime)

- VirtualSoulfind service
- Mesh/DHT service
- Content relay service
- MCP service
- Social federation service
- Scanner, planner, etc.

**Mental Model**: The **pod** is all of the above, running on some box/VM/container.

---

## 2. Identity Composition & Separation

A pod has **multiple identity surfaces**, kept intentionally separate:

### 2.1 Mesh/Pod Identity

- **Used for**: Pod-to-pod mesh communication, DHT participation, service fabric
- **Key Material**: Ed25519 or similar keypair
- **Public ID**: `PodId = hash(publicKey)`
- **Stored**: `data/identity/mesh-keypair.dat` (example path)

### 2.2 Soulseek Identity

- **Used for**: Soulseek protocol (Music domain backend only)
- **Credentials**: Username + password
- **Stored**: `config/soulseek-credentials.encrypted` (example)
- **Important**: This is **one backend**, not the pod's primary identity

### 2.3 ActivityPub Identities (Social)

- **Used for**: Social federation (optional)
- **Key Material**: Per-actor keypairs
  - `@music@pod.example` → keypair A
  - `@books@pod.example` → keypair B
  - `@movies@pod.example` → keypair C
- **Stored**: `data/identity/activitypub-actors/` (example)
- **Important**: Separate from mesh/pod identity

### 2.4 Admin Credentials

- **Used for**: Local pod administration (web UI, CLI, API)
- **Credentials**: Username + password hash, API keys
- **Stored**: `data/admin/users.db` (example)
- **Important**: Who can **control** the pod (rotate keys, change config, export identity)

---

## 3. Persistence Model

### 3.1 What Persists Without Keys?

If you have **storage but not keys**:

✅ **You Keep**:
- Library database (all works, items, quality scores)
- Local file catalog and hashes
- MCP state (reputation, quarantined content)
- Collections/lists
- Configuration (settings, preferences)

❌ **You Lose**:
- Ability to prove you're the same pod to others
- Mesh/DHT participation as previous `PodId`
- ActivityPub actors (old handles dead)
- Any encrypted data (if keys used for encryption)

**Result**: You can still use your library **locally** or with a **new identity**, but old identity is gone.

### 3.2 What Persists With Keys?

If you have **storage + keys**:

✅ **Full Persistence**:
- Library and state (as above)
- Mesh/pod identity (same `PodId`)
- ActivityPub actors (same handles)
- Ability to rejoin networks as the same entity

**Result**: The pod is **fully recoverable** and maintains continuity.

### 3.3 Recommended Storage Layout

```
/pod-data/
  identity/
    mesh-keypair.dat              # Mesh/pod identity
    activitypub-actors/           # Per-actor keypairs
      music.key
      books.key
      movies.key
    encryption-keys.dat           # For encrypted persistence (reputation, etc.)
  
  config/
    application.json              # Main application config
    soulseek-credentials.encrypted # Soulseek backend creds
    moderation-config.json        # MCP configuration
    social-federation.json        # Federation config
  
  library/
    virtualsoulfind.db            # VirtualSoulfind catalogue
    shares.db                     # Share index
    collections.db                # User lists/collections
  
  mcp/
    reputation.db.encrypted       # Peer reputation data
    quarantine/                   # Quarantined content metadata
  
  admin/
    users.db                      # Admin accounts
```

**Backup Strategy**: Back up entire `/pod-data/` directory for full recovery.

---

## 4. Identity Bundle (Export/Import)

### 4.1 Identity Bundle Contents

An **identity bundle** is an encrypted archive containing:

- Mesh/pod keypair
- ActivityPub actor keypairs (all domains)
- Encryption keys (for encrypted persistence)
- Optional: Soulseek credentials (if operator wants to transfer backend access)

**Does NOT include**:
- Library database (too large, exported separately)
- MCP state (operator-specific)
- Configuration (may contain host-specific paths)
- Admin accounts (operator-specific)

### 4.2 Export Flow

```csharp
public interface IPodIdentityService
{
    Task<byte[]> ExportIdentityBundleAsync(
        string passphrase,
        bool includeSoulseekCreds,
        CancellationToken ct);
}
```

**Steps**:
1. Admin authenticates
2. Provides strong passphrase
3. System creates encrypted archive with:
   - Mesh keypair
   - ActivityPub actor keys
   - Encryption keys
   - Optional: Soulseek creds
4. Returns encrypted bundle (or writes to file)

**Security**:
- Requires admin authentication
- Passphrase-protected (not just stored key)
- Logged (sanitized: "Identity bundle exported at {timestamp} by {adminId}")

### 4.3 Import Flow

```csharp
public interface IPodIdentityService
{
    Task ImportIdentityBundleAsync(
        byte[] bundle,
        string passphrase,
        CancellationToken ct);
}
```

**Steps**:
1. Admin authenticates
2. Provides encrypted bundle + passphrase
3. System decrypts and validates bundle
4. Imports keys to `identity/` directory
5. Restarts services (or notifies to restart)

**Security**:
- Requires admin authentication
- Validates bundle integrity (signatures, checksums)
- Overwrites existing identity (with confirmation)
- Logged (sanitized)

### 4.4 Use Cases

**Backup/Recovery**:
- Export identity bundle → store securely offsite
- If keys lost → import bundle on new instance

**Transfer Ownership**:
1. Current operator exports bundle (with strong passphrase)
2. Exports library database separately (large files)
3. Gives both to new operator (secure channel)
4. New operator imports bundle + restores library
5. Current operator retires old instance (see § 5)

**Migration**:
- Move pod from one host/VM to another
- Export bundle + copy library → import on new host
- Old host shuts down, new host takes over identity

---

## 5. Pod Lifecycle: Retire, Identity Suicide, Full Wipe

Three levels of "end of life" for a pod:

### 5.1 Soft Retire (Stop Participating, Keep Local Library)

**What Happens**:
- Stop all external services:
  - No mesh/DHT/torrent/Soulseek
  - No ActivityPub (or keep read-only)
- Keep data + keys (can revive later)

**Use Case**:
- Temporary shutdown
- Going into "read-only" mode
- Preparing for maintenance

**Implementation**:
```csharp
public Task SoftRetireAsync(CancellationToken ct)
{
    await _meshService.StopAsync(ct);
    await _dhtService.StopAsync(ct);
    await _socialFederation.SetModeAsync(FederationMode.Hermit, ct);
    // Keep VirtualSoulfind, library, MCP running locally
}
```

**Reversible**: Yes (restart services)

---

### 5.2 Identity Suicide (Destroy Keys, Keep Content)

**What Happens**:
- Wipe:
  - Mesh/pod keypair
  - ActivityPub actor keys
  - Optional: Soulseek credentials
  - Encryption keys (if used)
- Keep:
  - Library database
  - Share index
  - MCP state
  - Collections/lists

**Use Case**:
- Want to use library offline/internally
- Cannot rejoin networks as same identity
- Privacy-focused reset

**Implementation**:
```csharp
public Task IdentitySuicideAsync(bool wipeSoulseekCreds, CancellationToken ct)
{
    // 1. Stop all services
    await SoftRetireAsync(ct);
    
    // 2. Wipe identity material
    File.Delete("data/identity/mesh-keypair.dat");
    Directory.Delete("data/identity/activitypub-actors/", recursive: true);
    File.Delete("data/identity/encryption-keys.dat");
    
    if (wipeSoulseekCreds)
        File.Delete("config/soulseek-credentials.encrypted");
    
    // 3. Log event (sanitized)
    _logger.LogWarning("Pod identity destroyed. Library retained. New identity required for network participation.");
}
```

**Outcome**:
- To go back online: Generate new identity (new `PodId`, new AP actors)
- Old identity appears as "node that went offline and never came back"

**Reversible**: No (keys are gone)

---

### 5.3 Full Wipe (Destroy Keys + Content)

**What Happens**:
- Wipe **everything**:
  - Identity material (as above)
  - Library database
  - Share index
  - MCP state
  - Collections/lists
  - Configuration (optional)

**Use Case**:
- Complete shutdown
- Privacy-focused destruction
- Decommissioning hardware

**Implementation**:
```csharp
public Task FullWipeAsync(bool wipeConfig, CancellationToken ct)
{
    // 1. Stop all services
    await SoftRetireAsync(ct);
    
    // 2. Wipe identity
    await IdentitySuicideAsync(wipeSoulseekCreds: true, ct);
    
    // 3. Wipe data
    File.Delete("data/library/virtualsoulfind.db");
    File.Delete("data/library/shares.db");
    File.Delete("data/library/collections.db");
    Directory.Delete("data/mcp/", recursive: true);
    
    if (wipeConfig)
        Directory.Delete("config/", recursive: true);
    
    // 4. Log event (sanitized)
    _logger.LogCritical("Pod fully wiped. All identity and data destroyed.");
}
```

**Outcome**: Pod as others knew it is gone forever

**Reversible**: No (no data, no keys)

---

### 5.4 Protection & Confirmation

All retire/wipe operations MUST:

✅ **Require admin authentication**  
✅ **Require confirmation** (type pod ID or hostname)  
✅ **Be clearly labeled** (in UI/CLI/docs) as irreversible  
✅ **Log events** (sanitized, no sensitive data)  
✅ **Provide warnings** about what will be lost  

**Example Confirmation Flow**:
```
WARNING: This will destroy your pod identity and cannot be undone.
Other pods will see you as offline permanently.
Your library will be retained but you cannot rejoin networks as this identity.

To confirm, type your pod ID: a1b2c3d4-5e6f-7a8b-9c0d-1e2f3a4b5c6d

[Type pod ID]
[Confirm] [Cancel]
```

---

## 6. Multi-Admin Model (Preventing Single-Controller SPOF)

### 6.1 Admin Account Model

Pod supports **multiple admin accounts** (not just "whoever has SSH"):

```csharp
public class AdminAccount
{
    public string Id { get; init; }
    public string Username { get; init; }
    public string PasswordHash { get; init; }
    public string[] Roles { get; init; } // e.g., ["admin", "moderator"]
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
```

### 6.2 Admin Capabilities

Admins can:
- ✅ Change configuration
- ✅ Rotate keys (mesh, ActivityPub, Soulseek)
- ✅ Export/import identity bundles
- ✅ Add/remove admin accounts
- ✅ Trigger retire/wipe flows
- ✅ View MCP state (quarantined content, reputation)
- ✅ Override reputation (ban/unban sources)

### 6.3 Preventing SPOF

**Best Practice**: Add a second admin account before you lose the first.

**Recovery Scenario**:
- Admin A loses access (forgotten password, lost 2FA)
- Admin B can:
  - Reset Admin A's password
  - Create new admin account
  - Export identity bundle for offsite backup

**Implementation**:
```csharp
public interface IAdminService
{
    Task<AdminAccount> CreateAdminAsync(string username, string password, CancellationToken ct);
    Task ResetPasswordAsync(string adminId, string newPassword, string requestingAdminId, CancellationToken ct);
    Task<IEnumerable<AdminAccount>> ListAdminsAsync(CancellationToken ct);
}
```

---

## 7. Transfer Scenarios

### 7.1 Physical Transfer (Same Identity, New Operator)

**Steps**:
1. Current operator gives new operator the box/VM
2. New operator changes admin credentials
3. Network sees same pod identity, just new human behind it

**No Export/Import Needed**: Keys stay in place on disk.

---

### 7.2 Logical Transfer (Recommended)

**Steps**:

**Current Operator**:
1. Export identity bundle (encrypted with strong passphrase)
2. Export library database (separate, large files)
3. Securely transmit both to new operator
4. Retire old instance (soft retire or identity suicide)

**New Operator**:
1. Import identity bundle on their machine
2. Restore library database
3. Update configuration (host-specific paths, domains)
4. Start services

**Network View**: Same pod identity, different host.

**Benefits**:
- Current operator can decommission old hardware
- New operator has full continuity
- Clear handoff process

---

### 7.3 Fork (Clone Library, New Identity)

**Steps**:

**Current Operator**:
1. Export library database (no identity bundle)
2. Give to new operator

**New Operator**:
1. Import library database
2. Generate **new** identity (new mesh key, new AP actors)
3. Start services

**Network View**: Entirely new pod with similar library.

**Use Case**:
- Spin up test/dev instance with production data
- Share curated library with friend (they get their own identity)
- Create backup pod with independent identity

---

## 8. Recovery Scenarios

### 8.1 Lose OS/Admin Account, Still Have Disk

**Scenario**: Forgotten admin password, but can mount filesystem.

**Recovery**:
1. Mount disk on recovery system
2. Reset admin credentials directly in `data/admin/users.db`
3. Boot pod with recovered credentials
4. Add new admin account for redundancy

**Result**: ✅ Full recovery (identity intact)

---

### 8.2 Lose Keys, Still Have Storage

**Scenario**: Keys destroyed/corrupted, but library database intact.

**Recovery Options**:

**Option A: Continue as New Identity**
1. Generate new mesh/pod key → new `PodId`
2. Generate new ActivityPub actor keys
3. Point services at existing library database
4. Old identity dead, but library usable

**Option B: Import Backup Identity Bundle** (if exists)
1. Restore identity bundle from offsite backup
2. Import into `identity/` directory
3. Restart services

**Result**: 
- Option A: ✅ Library recovered, ❌ Identity new
- Option B: ✅ Full recovery (if backup exists)

**Prevention**: Regular identity bundle backups (encrypted, offsite)

---

### 8.3 Lose Soulseek Credentials

**Scenario**: Soulseek username/password lost or account banned.

**Recovery**:
1. Pod still works (mesh, torrent, HTTP, local, social all intact)
2. Music domain can't use Soulseek backend
3. Update configuration with new Soulseek account (if desired)

**Result**: ✅ Pod identity intact, ❌ One backend unavailable

**Note**: Soulseek is **one backend**, not critical to pod identity.

---

## 9. Security: Can Someone "Steal" a Pod?

### 9.1 Attack Scenarios

**Q: Can someone attach a malicious MCP from outside?**

**A: No.**

**Why**:
- MCP is **not remotable** (no network API)
- Providers are **compiled-in or configured locally**
- To change MCP config, attacker needs:
  - OS-level compromise (change config files), OR
  - Admin account compromise (change via UI/API)

**At that point**: MCP is least of your worries (they can do anything).

**Q: Can a malicious LLM "take over" a pod?**

**A: No.**

**Why** (from `§ 14` of this doc):
- LLM is **one provider** in composite (cannot override hash/blocklist)
- Max severity aggregation (any `Blocked` → final `Blocked`)
- Cannot mutate config, blocklists, or reputation directly
- Can only: Classify content (return `ModerationDecision`)

**Worst-Case**:
- Misconfigured LLM can ruin UX for that operator
- Cannot: Install in other pods, override hard rules, cause network-wide meltdown

**Q: Can someone steal my pod identity?**

**A: Only if they steal your keys.**

**Protections**:
- Keys stored in protected directory (filesystem permissions)
- Optional: Encrypt keys with passphrase
- Optional: Store keys on hardware security module (HSM)
- Require admin authentication for key export

**If Keys Stolen**:
- Attacker can impersonate your pod on mesh/DHT/social
- Mitigation: Identity suicide + generate new identity
- Optional: Publish "movedTo" pointer on ActivityPub (if possible before attacker takes over)

---

## 10. Implementation Tasks

Add to `TASK_STATUS_DASHBOARD.md`:

### T-POD01: Pod Identity Management Service

- Implement `IPodIdentityService`:
  - `ExportIdentityBundleAsync` (encrypted with passphrase)
  - `ImportIdentityBundleAsync` (decrypt and restore)
  - `RotateMeshKeyAsync` (generate new mesh keypair)
  - `RotateActorKeyAsync` (generate new AP actor key)

- Identity bundle format:
  - JSON or binary with versioning
  - Encrypted with AES-256 (key derived from passphrase)
  - Includes: mesh keypair, AP keys, encryption keys, optional Soulseek

- Storage layout:
  - Well-defined paths for all identity material
  - Filesystem permissions (read-only for pod process, admin-only write)

- Add tests:
  - Export/import round-trip preserves keys
  - Encryption is secure (strong passphrase required)
  - Invalid bundles rejected

### T-POD02: Admin Account Management

- Implement multi-admin support:
  - `IAdminService` with CRUD operations
  - Password hashing (bcrypt or Argon2)
  - Role-based access control (admin, moderator, read-only)

- Protect against lockout:
  - Require at least one admin account
  - Warn when removing last admin
  - Provide recovery mechanism (offline password reset)

- Add tests:
  - Multi-admin scenarios
  - Password reset flows
  - Permission enforcement

### T-POD03: Lifecycle Management (Retire/Wipe Flows)

- Implement lifecycle operations:
  - `SoftRetireAsync` (stop external services, keep data+keys)
  - `IdentitySuicideAsync` (wipe keys, keep data)
  - `FullWipeAsync` (wipe keys + data)

- Protection mechanisms:
  - Admin authentication required
  - Confirmation required (type pod ID)
  - Clear warnings about irreversibility
  - Audit logging

- Graceful shutdown:
  - Notify peers (if possible) before going offline
  - Drain work queues
  - Flush databases

- Add tests:
  - Soft retire is reversible
  - Identity suicide destroys keys, keeps data
  - Full wipe destroys everything
  - Confirmation required, cannot be bypassed

### H-POD01: Identity Security & Key Protection

- Implement key protection:
  - Filesystem permissions (restrict access to identity/)
  - Optional: Encrypt keys at rest with passphrase
  - Optional: HSM integration (future)

- Key rotation:
  - Rotate mesh key → new PodId (breaking change, document carefully)
  - Rotate AP actor keys → publish keyRotation activity (if AP spec supports)
  - Rotate encryption keys → re-encrypt reputation data

- Audit logging:
  - Log all key operations (export, import, rotate, wipe)
  - Sanitized (no actual key material in logs)
  - Include: timestamp, admin ID, operation type

- Add tests:
  - Unauthorized access to keys prevented
  - Key rotation updates all dependent systems
  - Audit logs capture all operations

---

## 11. Documentation for Operators

Provide clear operator documentation:

### 11.1 Backup Guide

**Regular Backups Should Include**:
1. Identity bundle (small, encrypted, critical)
   - Export monthly or after major changes
   - Store offsite (encrypted)
2. Library database (large, can be regenerated)
   - Export weekly or after major library changes
3. Configuration (small, host-specific)
   - Export after configuration changes

**Recovery Priority**:
1. Identity bundle → Critical (cannot be regenerated)
2. Library database → Important (time-consuming to regenerate)
3. Configuration → Nice to have (can be reconfigured)

### 11.2 Transfer Guide

**How to Transfer Your Pod to Someone Else**:
1. Export identity bundle (strong passphrase)
2. Export library database (separate file)
3. Securely transmit to new operator (encrypted channel)
4. New operator imports on their machine
5. You retire old instance (soft retire or identity suicide)

### 11.3 Retire/Wipe Guide

**Three Options**:
- **Soft Retire**: Temporary shutdown, keep everything, can revive
- **Identity Suicide**: Keep library, destroy identity, cannot rejoin as same pod
- **Full Wipe**: Destroy everything, cannot recover

**When to Use Each**:
- Soft retire: Maintenance, temporary downtime
- Identity suicide: Privacy reset, offline-only use
- Full wipe: Decommissioning, complete shutdown

---

## 12. Future Enhancements (Optional)

### 12.1 Multi-Pod Sync (Same Operator)

**Use Case**: Operator runs multiple pods (home, server, mobile).

**Design** (future):
- Share identity bundle across pods (same keys)
- Sync library state (via mesh or dedicated sync protocol)
- Each pod can act as "same entity" to network

**Security**: Requires careful key distribution and conflict resolution.

### 12.2 Federated Identity Migration (ActivityPub)

**Use Case**: Moving to new pod identity, but keeping social connections.

**Design** (future):
- Publish `Move` activity (if ActivityPub supports):
  ```json
  {
    "type": "Move",
    "actor": "https://old-pod.example/actors/music",
    "object": "https://old-pod.example/actors/music",
    "target": "https://new-pod.example/actors/music"
  }
  ```
- Followers/followings transfer to new identity
- Old identity points to new identity

**Security**: Requires signing with old key (proof of control).

### 12.3 Hardware Security Module (HSM) Integration

**Use Case**: Store keys on HSM for better protection.

**Design** (future):
- Keys never leave HSM
- Signing operations performed on HSM
- Export/import flows use HSM APIs

---

## 13. Summary

### What Makes a Pod "Itself"?

**Identity**: Keys (mesh, ActivityPub, credentials)  
**Persistence**: Data (library, MCP state, config)  
**Control**: Admin accounts (who can manage the pod)

### Persistence Model

- **With keys**: ✅ Full persistence (identity + data)
- **Without keys**: ✅ Data persists, ❌ Identity lost (appears as new pod)
- **Backup**: Identity bundle (critical) + library database (important)

### Security Guarantees

✅ MCP is local (no remote control)  
✅ Providers are read-only classifiers (cannot mutate state)  
✅ Max severity aggregation (cannot downgrade verdicts)  
✅ LLM is opt-in, off by default  
✅ Malicious LLM can only harm its own operator (not network-wide)  
✅ Keys required to steal/impersonate pod  

### Lifecycle Operations

1. **Soft Retire**: Stop services, keep data+keys (reversible)
2. **Identity Suicide**: Wipe keys, keep data (irreversible)
3. **Full Wipe**: Wipe keys+data (irreversible)

All operations require admin auth + confirmation.

---

## 14. Related Documents

- `docs/moderation-v1-design.md` § 14 - MCP Security Model
- `docs/security-hardening-guidelines.md` § 15 - Identity Separation
- `docs/social-federation-design.md` - ActivityPub identity management
- `TASK_STATUS_DASHBOARD.md` - T-POD01-03, H-POD01 tasks
