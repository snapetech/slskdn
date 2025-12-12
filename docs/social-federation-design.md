# Social Federation Design (ActivityPub Integration)

**Status**: DRAFT - Phase F (Future)  
**Created**: December 11, 2025  
**Priority**: LOW (after MCP, Multi-Domain, Proxy/Relay, Book/Video domains)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

## Overview

This document describes the **optional** social federation layer for VirtualSoulfind, which allows pods to:

- Expose ActivityPub actors for content domains (Music, Books, Movies, TV).
- Publish and consume WorkRefs (metadata-only references to works/items) via ActivityPub.
- Build social trust signals (likes, shares, curated lists) to improve discovery and recommendations.

**Critical Constraints:**

1. **Metadata-only**: ActivityPub is NEVER used to transport media files. All content distribution remains on Soulseek (music-only), mesh, torrent, HTTP, or local disk.
2. **Privacy-first**: Default mode is `Hermit` (no federation). Operators must explicitly opt-in.
3. **Identity separation**: ActivityPub identities are completely separate from mesh/pod identities.
4. **MCP integration**: All inbound social content and sources are untrusted by default and subject to moderation.
5. **Abuse protection**: Rate limits, queue limits, and spam filtering are mandatory.

---

## Architecture

### Components

1. **Library Actors** - One ActivityPub actor per content domain per pod (e.g., `@music@pod.example`, `@books@pod.example`)
2. **WorkRef Object Type** - Custom ActivityPub object type representing a content work (album, book, movie, etc.)
3. **Publishing Service** - Converts VirtualSoulfind lists/intents to ActivityPub Collections and Activities
4. **Ingestion Service** - Consumes inbound WorkRefs and feeds them into VirtualSoulfind as intents
5. **Social Signals Service** - Aggregates likes, shares, and other social signals for ranking/discovery

### Data Flow

```
Local VirtualSoulfind → Publishing Service → ActivityPub (outbound)
                                             ↓
ActivityPub (inbound) → Ingestion Service → VirtualSoulfind Intents
                                           ↓
                      Social Signals ← MCP/Reputation Filtering
```

---

## Privacy Modes

Social federation MUST be explicitly controlled via a privacy mode setting. The following modes are supported:

- **Hermit (default)**  
  - No ActivityPub actors are exposed.  
  - No WebFinger, no inbox, no outbox.  
  - All federation-related HTTP endpoints MUST be disabled or return an appropriate error status (e.g., 404/410/503) so that the pod appears non-federated.

- **FriendsOnly**  
  - Library actors are exposed, but:
    - Inbox accepts activities only from explicitly allowed instances and/or actors.
    - Outgoing deliveries (fan-out) are restricted to explicitly allowed instances and/or actors.
  - This mode is intended for small, semi-private networks of pods and known instances.

- **Public**  
  - Normal ActivityPub federation with:
    - Instance allowlist/denylist.
    - Actor-level allowlist/denylist.
  - New remote instances/actors are allowed by default unless blocked by configuration or moderation.

Configuration example (pseudo-JSON):

```jsonc
"SocialFederation": {
  "Mode": "Hermit", // Hermit | FriendsOnly | Public
  "AllowedInstances": ["trusted.example", "friend.example"],
  "DeniedInstances": ["spam.example"],
  "AllowedActors": ["@curator@trusted.example"],
  "DeniedActors": ["@spammer@untrusted.example"]
}
```

All federation components MUST check `SocialFederation.Mode` before enabling listeners, processing inbox events, or performing outbound deliveries.

---

## Identity Separation

ActivityPub identities MUST be treated as **separate** from mesh/pod identities.

Requirements:

* Each ActivityPub actor (e.g. `@music@pod`, `@books@pod`) MUST use its own keypair, distinct from:

  * Mesh peer identity.
  * Any local OS-level or app-level user identity.

* The system MUST NOT:

  * Automatically derive ActivityPub actor IDs or keys from mesh IDs or vice versa.
  * Log or persist implicit mappings between mesh peers and ActivityPub actors.

* Any mapping between:

  * A pod / mesh identity, and
  * An ActivityPub actor (e.g. "this pod corresponds to @xyz@example.com")

  MUST be:

  * Explicitly configured by the operator or user (via config or UI).
  * Stored in a dedicated configuration structure, not inferred from traffic.

* Logging MUST NOT include:

  * Lines that directly link a mesh peer ID or IP to an ActivityPub actor handle.
  * Identifier combinations that enable easy correlation across layers without operator intent.

---

## Abuse, Spam, and DoS Protections

The social federation layer MUST assume that inbound ActivityPub traffic can be malicious or spammy.

### Rate Limits and Quotas

* Implement per-instance and per-actor rate limits:

  * Configure maximum activities per time window (e.g. per minute/per hour).
  * Integrate these limits with the global work-budget/quota system.

* In case of rate-limit violations:

  * Drop or defer processing of further activities from that instance/actor.
  * Optionally emit a structured log/metric event indicating rate-limit enforcement (without PII).

### Inbox Queue Limits

* The ActivityPub inbox MUST have a bounded queue:

  * Maximum number of pending activities.
  * Maximum storage size for unprocessed items.

* On overflow:

  * New inbound activities MUST either be rejected with an error or dropped.
  * The system MUST fail-safe (protect core services), not attempt best-effort beyond configured bounds.

### Basic Validation and Filtering

* Inbound activities MUST be validated before further processing:

  * Reject activities above a maximum size (body size limit).
  * Reject obviously malformed JSON-LD or missing required fields.
  * Reject activities with unsupported or unexpected object types, unless explicitly enabled.

* Optionally, implement basic spam heuristics (e.g. repeated identical Notes from the same actor) and treat such actors as suspicious, feeding signals into the reputation system.

---

## Logging and Metrics for Federation

Logging and metrics MUST NOT leak sensitive or identifying information.

### Logging

* Logs MUST NOT contain:

  * Full ActivityPub JSON payloads.
  * Full actor handles (e.g. `@user@example.com`), except where strictly necessary for debugging and explicitly scoped.
  * HTTP request headers or query parameters from remote instances.

* When referencing remote instances or actors in logs:

  * Prefer:

    * A hashed or truncated form of instance domain (if needed).
    * Generic placeholders for actors (e.g. `actor_id_1234`).
  * Avoid combinations that allow easy reconstruction of the full handle.

* Any debug log mode that temporarily relaxes these constraints MUST be:

  * Explicitly opt-in.
  * Clearly documented as unsafe for production.
  * Disabled by default.

### Metrics

* Metrics MUST use **low-cardinality labels only**, e.g.:

  * `instanceDomain` (optionally truncated/anonymized).
  * `objectType` (`Note`, `Collection`, `Follow`, etc.).
  * `result` (`accepted`, `rejected_rate_limit`, `rejected_validation`, etc.).
  * `privacyMode` (`Hermit`, `FriendsOnly`, `Public`).

* Metrics MUST NOT include:

  * Full actor handles.
  * Activity IDs.
  * URL paths or query strings.

---

## MCP and Reputation Integration for Federation Sources

Federation sources (remote instances and actors) MUST be integrated with the existing moderation and reputation logic.

* Treat each remote instance and actor as a `SocialSource`:

  * Keyed by:

    * Instance domain.
    * Actor ID (if needed).

* Map `SocialSource` signals into MCP / reputation as:

  * Positive: well-behaved sources that send valid, useful WorkRefs/lists.
  * Negative: sources that:

    * Send spammy or malformed activities.
    * Repeatedly reference blocked/abusive content.
    * Violate rate limits.

* Integrate with `IPeerReputationStore` (or a shared reputation abstraction):

  * Store `SocialSource` events along with other peer-related events.
  * Allow the same enforcement mechanisms (ban/penalize) to be applied consistently.

* Enforcement:

  * Sources with poor reputation may:

    * Have their activities silently dropped.
    * Be required to pass stricter validation.
    * Be fully blocked via configuration and/or MCP.

* MCP MUST remain the ultimate gatekeeper:

  * Social signals MUST NOT override:

    * Local blocklists.
    * Content moderation decisions.
    * Security policies on any domain.

---

## WorkRef Object Type

A **WorkRef** is a custom ActivityPub object type representing a content work (album, book, movie, episode, etc.).

### Structure

```jsonc
{
  "@context": [
    "https://www.w3.org/ns/activitystreams",
    "https://example.org/ns/virtualsoulfind"  // Extension context
  ],
  "type": "WorkRef",
  "id": "https://pod.example/workref/a1b2c3d4",
  "domain": "Music",  // Music | Book | Movie | Tv
  "externalIds": {
    "musicbrainz:release": "12345678-1234-1234-1234-123456789012",
    "discogs:release": "1234567"
  },
  "title": "Album Title",
  "creator": "Artist Name",
  "year": 2023,
  "metadata": {
    // Domain-specific metadata (flexible)
  },
  "published": "2025-12-11T12:00:00Z"
}
```

### Security Requirements

WorkRef serialization:

- ✅ MUST include: domain, external IDs (MBID, ISBN, TMDB, etc.), human-readable metadata
- ❌ MUST NOT include: local paths, file hashes, mesh peer IDs, IP addresses, internal database IDs

---

## Library Actors

Each pod exposes one ActivityPub actor per content domain:

- `@music@pod.example` - Music domain actor
- `@books@pod.example` - Books domain actor
- `@movies@pod.example` - Movies domain actor
- `@tv@pod.example` - TV domain actor

### Actor Document Structure

```jsonc
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Service",  // Or "Application"
  "id": "https://pod.example/actors/music",
  "preferredUsername": "music",
  "name": "Music Library @ pod.example",
  "inbox": "https://pod.example/actors/music/inbox",
  "outbox": "https://pod.example/actors/music/outbox",
  "followers": "https://pod.example/actors/music/followers",
  "following": "https://pod.example/actors/music/following",
  "publicKey": {
    "id": "https://pod.example/actors/music#main-key",
    "owner": "https://pod.example/actors/music",
    "publicKeyPem": "-----BEGIN PUBLIC KEY-----\n..."
  }
}
```

### Keypair Management

- Each actor MUST have its own Ed25519 or RSA keypair
- Keys are stored separately from mesh/pod identity keys
- Keys are generated on first federation enable
- Private keys are protected via `IDataProtectionProvider`

---

## Publishing Service

Converts local VirtualSoulfind state to ActivityPub activities.

### What Gets Published

1. **List Creation/Update** - When a user creates/updates a playlist/reading list/watchlist
2. **WorkRef Addition** - When a work is added to a list
3. **WorkRef Annotation** - When a user adds notes/ratings to a work (optional)

### Activity Examples

**Create a Collection (List)**:
```jsonc
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "actor": "https://pod.example/actors/music",
  "object": {
    "type": "Collection",
    "id": "https://pod.example/lists/my-playlist",
    "name": "My Playlist",
    "items": [
      // WorkRef objects
    ]
  }
}
```

**Add WorkRef to Collection**:
```jsonc
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Add",
  "actor": "https://pod.example/actors/music",
  "object": {
    "type": "WorkRef",
    // ...
  },
  "target": "https://pod.example/lists/my-playlist"
}
```

### Delivery (Fan-out)

- Respect privacy mode and per-list visibility settings
- Use HTTP signatures for authentication
- Integrate with work-budget system for delivery rate limiting
- Queue deliveries asynchronously; fail gracefully on remote errors

---

## Ingestion Service

Processes inbound ActivityPub activities and converts them to VirtualSoulfind intents.

### What Gets Ingested

1. **Collections from followed actors** - Lists of WorkRefs
2. **WorkRef shares** - Individual works shared by other actors
3. **Annotations/Notes** - Comments/ratings attached to WorkRefs (optional)

### Ingestion Pipeline

```
Inbox → Validation → MCP Check → WorkRef Mapping → Intent Creation
```

1. **Validation**: Check activity structure, size, required fields
2. **MCP Check**: Verify source instance/actor reputation
3. **WorkRef Mapping**: Map external IDs to local `ContentWorkId`
4. **Intent Creation**: Create VirtualSoulfind acquisition intent if work is not yet owned

### Security

- Apply rate limits per instance/actor
- Reject oversized or malformed activities
- Respect `SocialFederation.Mode` (no ingestion in Hermit mode)
- Queue inbound activities with bounded queue size

---

## Social Signals Service

Aggregates social signals (likes, shares, list appearances) to inform discovery and ranking.

### Signal Types

1. **List Appearances** - How many trusted sources include this work in their lists
2. **Likes/Favorites** - Direct appreciation signals
3. **Shares/Announces** - Amplification signals
4. **Annotations** - Quality/sentiment from notes/ratings (optional)

### Ranking Integration

Social signals are **soft hints only**:

- Used to break ties or adjust priority in VirtualSoulfind planner
- NEVER override:
  - User explicit intents
  - Content domain rules (e.g., no Soulseek for non-music)
  - MCP decisions (blocked content stays blocked)
  - Local quality scoring (hash match, bitrate, etc.)

### Reputation Filtering

- Signals from low-reputation sources are excluded
- Signals from banned sources are discarded
- Reputation changes retroactively affect signal aggregation

---

## Optional Social Features (Extensible Friendship Layer)

The following features are optional and SHOULD be implemented only if they can respect all security, privacy, and moderation rules above.

### Circles and Per-List Visibility

* Introduce **circles**, which are named groups of instances/actors, e.g.:

  * `close_friends`
  * `trusted_instances`
  * `public_fediverse`

* Each playlist/reading list/watchlist MUST be able to specify visibility:

  * `private` (local only).
  * `circle:<name>` (deliver only to that circle).
  * `public` (deliver to all followers / normal AP public).

* Federation logic MUST:

  * Use circle membership to decide where to deliver list-related Activities.
  * Respect per-list visibility when publishing.

### Ephemeral Rooms (Listening/Reading/Watching Rooms)

* Support an optional concept of ephemeral "rooms":

  * A room is a temporary grouping of WorkRefs and lightweight presence signals (e.g., "now listening/reading/watching").
  * Each room has:

    * A logical ID.
    * Optional association with a circle or visibility rule.

* Pods MAY:

  * Publish WorkRefs with timestamps to a room context.
  * Show, in the local UI, which actors are currently active in the room (by handle), if the user opts in.

* Rooms remain metadata-only:

  * No streaming or playback over ActivityPub.
  * No requirement to expose local IPs/peers.

### Shadow Following of Lists (Anonymous Mirroring)

* Support a mode where a pod can "mirror" a public Collection (list) **without**:

  * Exposing a `Follow` relationship from its own actors.
  * Being observable as a follower by the remote instance.

* Implementation:

  * Configuration-driven polling of a Collection URL on a remote instance.
  * `SocialIngestionService` ingests the Collection content, maps WorkRefs to local `ContentWorkId`s, and creates/updates a local mirror list.

* This allows users to:

  * Benefit from remote lists.
  * Avoid explicit social graph linkage, aiding anonymity and privacy.

### Federated Tags and Meta-Lists

* Use WorkRef as the anchor for federated tagging:

  * Notes and annotations associated with a WorkRef MAY include tags/keywords.
  * Tags MUST be:

    * Sanitized and moderated (MCP).
    * Stored independently of any single remote instance.

* Pods MAY:

  * Aggregate tags from multiple sources.
  * Expose a merged tag view per `ContentWorkId` in their local UI.

* Meta-lists:

  * Lists of lists (e.g., "Best playlists for this month", "Curated cross-media trios") MAY be represented as Collections of Collection references.
  * All meta-lists MUST remain metadata-only, referencing WorkRefs and other Collections by URL/ID, never direct media.

All optional features MUST inherit and respect the same privacy modes, rate limits, logging policies, and MCP integration defined above.

---

## Configuration

```jsonc
"SocialFederation": {
  "Enabled": false,  // Default: false (Hermit mode)
  "Mode": "Hermit",  // Hermit | FriendsOnly | Public
  
  // Identity
  "InstanceDomain": "pod.example",  // Public domain for actor IDs
  
  // Privacy & Access Control
  "AllowedInstances": [],  // For FriendsOnly mode
  "DeniedInstances": [],   // For Public mode
  "AllowedActors": [],     // Fine-grained actor allowlist
  "DeniedActors": [],      // Fine-grained actor denylist
  
  // Abuse Protection
  "RateLimits": {
    "ActivitiesPerInstancePerMinute": 60,
    "ActivitiesPerActorPerMinute": 10,
    "MaxInboxQueueSize": 1000,
    "MaxInboxQueueBytes": 10485760  // 10MB
  },
  
  // Publishing
  "PublishingEnabled": false,
  "PublishDomains": ["Music"],  // Which domains to publish
  
  // Ingestion
  "IngestionEnabled": false,
  "IngestDomains": ["Music", "Book"],  // Which domains to ingest
  
  // Social Signals
  "SocialSignalsEnabled": false,
  "MinSourceReputationForSignals": 0.5  // 0.0 to 1.0
}
```

---

## Implementation Notes

### Phase F Tasks (T-FED Series)

Social federation is **Phase F** in the master plan, implemented AFTER:

- ✅ Phase A: Service Fabric
- ✅ Phase B: MCP (Moderation)
- ✅ Phase C: Multi-Domain VirtualSoulfind
- 📋 Phase D: Proxy/Relay Services
- 📋 Phase E: Book & Video Domains

### Dependencies

- `T-MCP01-04` - MCP integration for source reputation
- `T-VC01-04` - Multi-domain foundation for WorkRef mapping
- `T-PR02` - Catalogue fetch service (for shadow following)
- `H-02` - Work budget system (for rate limiting)

### Security Reviews Required

Before enabling social federation in production:

1. External security review of ActivityPub implementation
2. Penetration testing for abuse/spam scenarios
3. Privacy audit of logging and metrics
4. Identity separation verification

---

## References

- [ActivityPub Specification](https://www.w3.org/TR/activitypub/)
- [ActivityStreams 2.0](https://www.w3.org/TR/activitystreams-core/)
- [HTTP Signatures](https://datatracker.ietf.org/doc/html/draft-cavage-http-signatures)
- [Mastodon's ActivityPub Implementation](https://docs.joinmastodon.org/spec/activitypub/)
