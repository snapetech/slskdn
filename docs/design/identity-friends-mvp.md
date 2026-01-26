# Identity & Friends MVP — Design & Implementation Guide

**Goal:** Enable "befriend → group → share → recipient backfill download → recipient stream" workflow via WebUI without requiring users to paste 200-char peerIds.

**Principle:** Clean separation between:
- **Canonical identity** (cryptographic, stable, verifiable): `PeerId` / public key
- **Display name / nickname** (human-friendly, mutable): "Keith-Laptop", "Mike", etc.
- **Discovery** (how you find them): LAN discovery or invite link

---

## 1. Core Data Model

### 1.1 PeerProfile (signed, human-friendly layer)

**Purpose:** Public profile that any peer can publish. Signed to prevent spoofing.

```csharp
namespace slskd.Identity;

public sealed class PeerProfile
{
    /// <summary>Canonical peer ID (derived from public key).</summary>
    public string PeerId { get; set; } = string.Empty;
    
    /// <summary>Public key (raw bytes, base64-encoded).</summary>
    public string PublicKey { get; set; } = string.Empty;
    
    /// <summary>Human-friendly display name (e.g., "Keith – Office").</summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>Optional avatar URL or data URI.</summary>
    public string? Avatar { get; set; }
    
    /// <summary>Capabilities bitmask: stream, download, mesh search, etc.</summary>
    public int Capabilities { get; set; }
    
    /// <summary>Endpoints to reach this peer: direct HTTP/QUIC, relay hints, etc.</summary>
    public List<PeerEndpoint> Endpoints { get; set; } = new();
    
    /// <summary>When this profile was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    /// <summary>When this profile expires (short TTL for rotating presence).</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    
    /// <summary>Signature over canonical JSON bytes, signed with peer's private key.</summary>
    public string Signature { get; set; } = string.Empty;
}

public sealed class PeerEndpoint
{
    /// <summary>Endpoint type: Direct, Relay, QUIC, etc.</summary>
    public string Type { get; set; } = string.Empty; // "Direct", "Relay", "QUIC"
    
    /// <summary>Endpoint URL or address.</summary>
    public string Address { get; set; } = string.Empty; // "https://host:port", "relay://relayId/peerId", "quic://..."
    
    /// <summary>Priority (lower = preferred).</summary>
    public int Priority { get; set; }
}
```

**Signing:** Serialize to canonical JSON (sorted keys, no whitespace), sign with Ed25519 private key, base64-encode signature.

**Validation:** Deserialize JSON, verify signature with `PublicKey`, check `ExpiresAt > Now`.

---

### 1.2 Contact (local nickname store)

**Purpose:** Local-only contact list with petnames (like Signal). You talk to a verified key; the name is your label.

```csharp
namespace slskd.Identity;

[Table("Contacts")]
public class Contact
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>Canonical peer ID (foreign key to PeerProfile).</summary>
    [Required]
    [MaxLength(128)]
    public string PeerId { get; set; } = string.Empty;
    
    /// <summary>Local-only nickname (e.g., "Marisa", "PirateBuddy").</summary>
    [MaxLength(128)]
    public string Nickname { get; set; } = string.Empty;
    
    /// <summary>Whether signature was verified and key is pinned.</summary>
    public bool Verified { get; set; }
    
    /// <summary>Last time we saw this peer online.</summary>
    public DateTimeOffset? LastSeen { get; set; }
    
    /// <summary>Cached endpoints from last profile fetch.</summary>
    public string? CachedEndpointsJson { get; set; }
    
    /// <summary>When this contact was added.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

**Note:** `PeerId` is the canonical identifier. `Nickname` is local-only and mutable.

---

### 1.3 Friend Code (short, shareable representation)

**Purpose:** UI-friendly short code derived from `PeerId` for copy/paste.

**Format:** Base32 or bech32 encoding, 10–16 bytes, formatted with dashes.

**Example:** `SLSKDN1-K9F3-8Q2P-5M7C`

**Implementation:**
- Derive from first 10 bytes of `PeerId` (or hash of `PeerId`)
- Encode with Base32 (RFC 4648)
- Format: `XXXXX-XXXX-XXXX-XXXX`
- Decode back to `PeerId` prefix for lookup (may need fuzzy match if collision)

**Storage:** Not stored separately; computed on-demand from `PeerId`.

---

### 1.4 FriendInvite (invite link payload)

**Purpose:** Self-contained invite for WAN or non-LAN discovery.

```csharp
namespace slskd.Identity;

public sealed class FriendInvite
{
    /// <summary>Invite format version.</summary>
    public int InviteVersion { get; set; } = 1;
    
    /// <summary>The signed PeerProfile of the inviter.</summary>
    public PeerProfile Profile { get; set; } = null!;
    
    /// <summary>Nonce for replay protection.</summary>
    public string Nonce { get; set; } = string.Empty;
    
    /// <summary>When this invite expires.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    
    /// <summary>Optional signature over the whole invite (if signed by inviter).</summary>
    public string? InviteSignature { get; set; }
}
```

**Encoding:**
- Serialize to JSON
- Base64URL-encode
- Format: `slskdn://invite/<base64url>`
- Also render as QR code in WebUI

**Decoding:**
- Extract base64url from `slskdn://invite/...`
- Decode to JSON
- Deserialize `FriendInvite`
- Validate `ExpiresAt > Now`
- Verify `Profile.Signature`
- Optionally verify `InviteSignature` if present

---

## 2. Discovery Mechanisms

### 2.1 LAN Discovery via mDNS

**Service Type:** `_slskdn._tcp` or `_slskdn._udp`

**TXT Records:**
- `peerCode` (short friend code)
- `displayName` (from profile)
- `apiPort` / endpoint
- `capabilities` (bitmask as string)
- `peerId` (full canonical ID, optional)

**Implementation:**
- Use `System.Net.NetworkInformation` or a library like `Zeroconf` (NuGet: `Zeroconf`)
- Advertise on startup (when feature enabled)
- Browse on "Nearby" page load
- When user clicks "Add", fetch full profile from endpoint, verify signature, store as Contact

**UI Flow:**
1. Contacts → "Nearby" tab
2. Shows list: **displayName + peerCode** + "Add" button
3. User clicks Add → fetch profile from `https://<discovered-ip>:<port>/api/v0/profile/{peerId}`
4. Verify signature → store Contact with `Verified = true`

---

### 2.2 Invite Links / QR

**For WAN or non-LAN scenarios.**

**UI Flow:**
1. "Create Invite" button → generate `FriendInvite` → encode as `slskdn://invite/...`
2. Display as:
   - Copyable link
   - QR code (use a QR library in WebUI)
3. Recipient "Add Friend" → paste link or scan QR
4. Decode → preview profile → accept → saved Contact

---

## 3. API Endpoints

### 3.1 Profile Endpoints

```csharp
[ApiController]
[Route("api/v0/profile")]
public class ProfileController : ControllerBase
{
    /// <summary>Get this peer's own profile (signed).</summary>
    [HttpGet("me")]
    [Authorize(Policy = AuthPolicy.Any)]
    public IActionResult GetMyProfile() { }
    
    /// <summary>Update this peer's profile (re-signs).</summary>
    [HttpPut("me")]
    [Authorize(Policy = AuthPolicy.Any)]
    public IActionResult UpdateMyProfile([FromBody] UpdateProfileRequest req) { }
    
    /// <summary>Get a peer's profile by PeerId (public, no auth required).</summary>
    [HttpGet("{peerId}")]
    [AllowAnonymous]
    public IActionResult GetProfile([FromRoute] string peerId) { }
    
    /// <summary>Generate an invite link/QR.</summary>
    [HttpPost("invite")]
    [Authorize(Policy = AuthPolicy.Any)]
    public IActionResult CreateInvite([FromBody] CreateInviteRequest req) { }
}
```

---

### 3.2 Contacts Endpoints

```csharp
[ApiController]
[Route("api/v0/contacts")]
[Authorize(Policy = AuthPolicy.Any)]
public class ContactsController : ControllerBase
{
    /// <summary>List all contacts.</summary>
    [HttpGet]
    public IActionResult GetAll() { }
    
    /// <summary>Get a contact by ID.</summary>
    [HttpGet("{id}")]
    public IActionResult Get([FromRoute] Guid id) { }
    
    /// <summary>Add a contact from an invite link.</summary>
    [HttpPost("from-invite")]
    public IActionResult AddFromInvite([FromBody] AddFromInviteRequest req) { }
    
    /// <summary>Add a contact from LAN discovery (by PeerId, after fetching profile).</summary>
    [HttpPost("from-discovery")]
    public IActionResult AddFromDiscovery([FromBody] AddFromDiscoveryRequest req) { }
    
    /// <summary>Update contact nickname.</summary>
    [HttpPut("{id}")]
    public IActionResult Update([FromRoute] Guid id, [FromBody] UpdateContactRequest req) { }
    
    /// <summary>Remove a contact.</summary>
    [HttpDelete("{id}")]
    public IActionResult Delete([FromRoute] Guid id) { }
    
    /// <summary>Browse nearby peers (mDNS).</summary>
    [HttpGet("nearby")]
    public IActionResult GetNearby() { }
}
```

---

## 4. Services & Repositories

### 4.1 IProfileService

```csharp
namespace slskd.Identity;

public interface IProfileService
{
    /// <summary>Get this peer's own profile (generates if missing).</summary>
    Task<PeerProfile> GetMyProfileAsync(CancellationToken ct = default);
    
    /// <summary>Update this peer's profile and re-sign.</summary>
    Task<PeerProfile> UpdateMyProfileAsync(string displayName, string? avatar, int capabilities, List<PeerEndpoint> endpoints, CancellationToken ct = default);
    
    /// <summary>Get a peer's profile by PeerId (from cache or fetch from endpoint).</summary>
    Task<PeerProfile?> GetProfileAsync(string peerId, CancellationToken ct = default);
    
    /// <summary>Verify a profile's signature.</summary>
    bool VerifyProfile(PeerProfile profile);
    
    /// <summary>Sign a profile with this peer's private key.</summary>
    PeerProfile SignProfile(PeerProfile profile);
    
    /// <summary>Generate a friend code from PeerId.</summary>
    string GetFriendCode(string peerId);
    
    /// <summary>Decode a friend code back to PeerId (fuzzy match if needed).</summary>
    string? DecodeFriendCode(string code);
}
```

---

### 4.2 IContactService

```csharp
namespace slskd.Identity;

public interface IContactService
{
    Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default);
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Contact?> GetByPeerIdAsync(string peerId, CancellationToken ct = default);
    Task<Contact> AddAsync(string peerId, string nickname, bool verified, CancellationToken ct = default);
    Task UpdateAsync(Contact contact, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
```

---

### 4.3 ILanDiscoveryService

```csharp
namespace slskd.Identity;

public interface ILanDiscoveryService
{
    /// <summary>Start advertising this peer via mDNS.</summary>
    Task StartAdvertisingAsync(CancellationToken ct = default);
    
    /// <summary>Stop advertising.</summary>
    Task StopAdvertisingAsync();
    
    /// <summary>Browse for nearby peers.</summary>
    Task<IReadOnlyList<DiscoveredPeer>> BrowseAsync(CancellationToken ct = default);
    
    /// <summary>Event fired when a peer is discovered.</summary>
    event EventHandler<DiscoveredPeer>? PeerDiscovered;
}

public sealed class DiscoveredPeer
{
    public string PeerCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty; // "https://ip:port"
    public int Capabilities { get; set; }
}
```

---

### 4.4 Repositories

```csharp
namespace slskd.Identity;

public interface IContactRepository
{
    Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default);
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Contact?> GetByPeerIdAsync(string peerId, CancellationToken ct = default);
    Task<Contact> AddAsync(Contact contact, CancellationToken ct = default);
    Task UpdateAsync(Contact contact, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
```

**Storage:** SQLite via `DbContext` (similar to `CollectionsDbContext`).

---

## 5. Integration with Existing Systems

### 5.1 ShareGroups → Use Contacts

**Change:** `ShareGroupMember.UserId` should reference `Contact.PeerId` (or add `ContactId` foreign key).

**UI:** When creating/editing a ShareGroup, show Contacts list (with nicknames) instead of raw user IDs.

---

### 5.2 Share Consumption → Resolve Endpoints

**When recipient clicks "Stream" or "Backfill":**
1. Look up Contact by `ShareGrant.AudienceId` (if it's a PeerId)
2. Get `Contact.CachedEndpointsJson` or fetch fresh profile
3. Pick best endpoint (Direct > Relay > QUIC)
4. Request stream/download from that endpoint

**Example:**
```csharp
var contact = await _contactService.GetByPeerIdAsync(audienceId, ct);
var endpoints = JsonSerializer.Deserialize<List<PeerEndpoint>>(contact.CachedEndpointsJson ?? "[]");
var bestEndpoint = endpoints.OrderBy(e => e.Priority).FirstOrDefault();
// Use bestEndpoint.Address to make HTTP request
```

---

## 6. WebUI Components (React)

### 6.1 Contacts Page (`/contacts`)

**Tabs:**
- **All:** List of contacts (nickname + verified badge + last seen)
- **Nearby:** mDNS discovered peers (displayName + peerCode + "Add" button)

**Actions:**
- "Add Friend" → modal: "Paste invite link" or "Scan QR"
- "Create Invite" → shows link + QR code
- Edit nickname
- Remove / block

---

### 6.2 ShareGroups Page

**Change:** When adding members, show Contacts dropdown (nicknames) instead of text input for user ID.

---

### 6.3 Shared with Me Page

**Change:** Show contact nickname instead of raw PeerId.

**Actions:**
- "Backfill downloads" → resolves endpoint from Contact → downloads from peer
- "Stream now" → resolves endpoint → streams from peer

---

## 7. Implementation Checklist

### Phase 1: Core Identity ✅ COMPLETE
- [x] `PeerProfile` model + signing/verification
- [x] `Contact` model + `IContactRepository` + `ContactRepository`
- [x] `IContactService` + `ContactService`
- [x] `IProfileService` + `ProfileService` (sign/verify, friend code encoding)
- [x] `ProfileController` (GET/PUT `/api/v0/profile/me`, GET `/api/v0/profile/{peerId}`, POST `/api/v0/profile/invite`)
- [x] `ContactsController` (CRUD + from-invite + from-discovery + nearby)
- [x] `IdentityDbContext` (SQLite, Contact table)
- [x] DI registration in `Program.cs`
- [x] **Tests**: 73 unit tests covering all components

### Phase 2: Discovery ✅ COMPLETE
- [x] `ILanDiscoveryService` + `LanDiscoveryService` (mDNS advertise + browse)
- [x] `MdnsAdvertiser` - Raw UDP socket implementation for mDNS advertising
- [x] `FriendInvite` model + encoding/decoding
- [x] Integration: `ProfileController.CreateInvite` → generate invite
- [x] Integration: `ContactsController.AddFromInvite` → decode + verify + add
- [x] Integration: `ContactsController.GetNearby` → call `ILanDiscoveryService.BrowseAsync`
- [x] Startup integration: Auto-start advertising on `ApplicationStarted` (if feature enabled)
- [x] **Tests**: 17 additional unit tests for mDNS components

### Phase 3: Integration ✅ COMPLETE
- [x] Update `ShareGroupMember` to reference `Contact.PeerId` (or add `ContactId`) - Added optional `PeerId` field
- [x] Update ShareGroups API to support PeerId - API accepts PeerId or UserId
- [x] Update manifest to show contact nicknames - Manifest includes owner contact info
- [x] Update Share consumption to resolve endpoints from Contact - **Deferred** (stream URL already points to host, no additional resolution needed)

### Phase 4: WebUI ✅ COMPLETE
- [x] Contacts page (`/contacts`) with All/Nearby tabs
- [x] "Add Friend" modal (invite link input)
- [x] "Create Invite" modal (link display)
- [x] ShareGroups page (`/sharegroups`) with Contacts dropdown integration
- [x] "Shared with me" page (`/shared`) showing contact nicknames
- [ ] QR code scanner/display - **Placeholder** (requires QR library - can be added later)

---

## 8. Feature Flags

Add to `Options.Feature`:
```csharp
/// <summary>Identity and friends (profiles, contacts, LAN discovery, invites).</summary>
public bool IdentityFriends { get; init; } = false;
```

Gate all endpoints and services behind this flag.

---

## 9. Security Notes

- **Profile signing:** Use Ed25519 (or existing mesh keypair if available)
- **Invite expiration:** Default 24 hours
- **Profile expiration:** Default 7 days (for rotating presence)
- **Contact verification:** Only mark `Verified = true` if signature verified
- **Endpoint validation:** Sanitize/validate endpoint URLs before storing

---

## 10. MVP Acceptance Test

With two peers (A and B) on the same LAN:

1. On A: set display name "Pirate A" → profile created
2. On B: set display name "Pirate B" → profile created
3. On B: Contacts → Nearby → see "Pirate A" → Add
4. On A: (optional) accept reciprocal request
5. On A: create Group "Crew", add "Pirate B" (from Contacts)
6. On A: create ShareList "Booty" and add 3 fixture items
7. On A: share "Booty" to "Crew" with stream+download allowed
8. On B: Shared with me → see "Booty" (from "Pirate A")
9. On B: click Stream on an item → playback works with seek
10. On B: click Backfill downloads → file downloads from A
11. (Optional) B now re-shares to a third peer if policy permits

If you can do that, your MVP is real.

---

## 11. Dependencies

**NuGet Packages:**
- `Zeroconf` 3.0.30 (for mDNS browsing) ✅ Added
- QR code library for WebUI (e.g., `qrcode.react` for React) - **Pending Phase 4**

**Existing:**
- Mesh keypair (for signing profiles) or generate new Ed25519 keypair
- `CollectionsDbContext` pattern (for `IdentityDbContext`)

---

**Next Steps:** Start with Phase 1 (Core Identity) and work through the checklist.
