# Mesh Identity Refactoring Plan

## Overview
Decouple mesh/DHT identity from Soulseek presence to enable mesh-only peers.

## Current State Analysis

### Existing Components

#### 1. Mesh Identity (`MeshPeerDescriptor`)
- **Location**: `src/slskd/Mesh/Dht/MeshPeerDescriptor.cs`
- **Current Structure**:
  - `PeerId` (string) - mesh identity
  - `Endpoints` (List<string>) - connection endpoints
  - `NatType` (string?) - NAT classification
  - `RelayRequired` (bool)
  - `TimestampUnixMs` (long)
- **Issues**: No signature field, no verification mechanism visible

#### 2. Hash DB Peer Model
- **Location**: `src/slskd/HashDb/Models/Peer.cs`
- **Current Structure**:
  - `PeerId` (string) - **Currently Soulseek username**
  - `Caps` (int) - capability flags
  - `ClientVersion` (string)
  - `LastSeen` (long)
- **Issue**: PeerId property name suggests mesh ID but comment says "Soulseek username (primary key)"

#### 3. DHT Rendezvous Service
- **Location**: `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- **Current Behavior**:
  - Discovers peers via BitTorrent DHT
  - Connects to overlay network
  - Uses `MeshNeighborRegistry` for tracking
- **Issue**: Need to verify if it gates mesh operations on Soulseek presence

### Soulseek-Gating Points Identified

1. **WishlistService** - Uses `ISoulseekClient`
2. **AutoReplaceService** - Uses `ISoulseekClient`
3. **Hash DB** - Peer model uses "Soulseek username" as primary key
4. **Need to audit**:
   - `HashDbService` - Check if hash sync requires Soulseek username
   - `SwarmDownloadOrchestrator` - Check peer selection logic
   - `PeerReputation` - Check if keyed by username
   - `MeshOverlayConnector` - Check connection initiation logic

## Implementation Plan

### Epic 1: Make Mesh/DHT Identity First-Class

#### Phase 1.1: Create Core Abstractions

**File**: `src/slskd/Mesh/Identity/MeshPeerId.cs` (NEW)
```csharp
namespace slskd.Mesh.Identity;

/// <summary>
/// Unique identifier for a mesh peer derived from their Ed25519 public key.
/// </summary>
public readonly record struct MeshPeerId
{
    public string Value { get; init; }
    
    public static MeshPeerId FromPublicKey(byte[] publicKey) 
        => new() { Value = Convert.ToBase64Url(SHA256.HashData(publicKey)[..16]) };
    
    public static MeshPeerId Parse(string value) 
        => new() { Value = value };
    
    public override string ToString() => Value;
}
```

**File**: `src/slskd/Mesh/Identity/IMeshPeerRegistry.cs` (NEW)
```csharp
namespace slskd.Mesh.Identity;

public interface IMeshPeerRegistry
{
    Task RegisterOrUpdateAsync(MeshPeerDescriptor descriptor, CancellationToken ct = default);
    Task<MeshPeer?> TryGetAsync(MeshPeerId id, CancellationToken ct = default);
    IAsyncEnumerable<MeshPeer> GetAllAsync(CancellationToken ct = default);
    Task<bool> IsVerifiedAsync(MeshPeerId id, CancellationToken ct = default);
}

public record MeshPeer
{
    public required MeshPeerId Id { get; init; }
    public required MeshPeerDescriptor Descriptor { get; init; }
    public bool IsVerified { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public string? SoulseekUsername { get; init; } // Optional alias
}
```

**File**: `src/slskd/Mesh/Identity/ISoulseekMeshIdentityMapper.cs` (NEW)
```csharp
namespace slskd.Mesh.Identity;

public interface ISoulseekMeshIdentityMapper
{
    Task MapAsync(string soulseekUsername, MeshPeerId meshPeerId, CancellationToken ct = default);
    Task<MeshPeerId?> TryGetMeshPeerIdAsync(string soulseekUsername, CancellationToken ct = default);
    Task<string?> TryGetSoulseekUsernameAsync(MeshPeerId meshPeerId, CancellationToken ct = default);
}
```

#### Phase 1.2: Update MeshPeerDescriptor

**File**: `src/slskd/Mesh/Dht/MeshPeerDescriptor.cs` (MODIFY)
- Add signature fields for verification
- Add public key field
- Add capabilities field

#### Phase 1.3: Implement Registry and Mapper

**File**: `src/slskd/Mesh/Identity/MeshPeerRegistry.cs` (NEW)
- SQLite-backed implementation
- Verify signatures on registration
- Integrate with SecurityCore for ban checks

**File**: `src/slskd/Mesh/Identity/SoulseekMeshIdentityMapper.cs` (NEW)
- SQLite-backed mapping table
- Bidirectional lookups

### Epic 2: Auto-Start on Descriptor Discovery

#### Phase 2.1: Hook DHT Discovery

**File**: `src/slskd/DhtRendezvous/DhtRendezvousService.cs` (MODIFY)
- Add handler for `MeshPeerDescriptor` discovery
- Verify descriptor signature
- Register with `IMeshPeerRegistry`
- Trigger overlay connection + hash sync

#### Phase 2.2: Add BT Extension Support

**File**: `src/slskd/DhtRendezvous/BtExtensionHandler.cs` (NEW)
- Parse custom BT extension handshake
- Extract mesh identity
- Feed into same discovery flow

### Epic 3: Make Soulseek Optional

#### Phase 3.1: Refactor Hash DB

**File**: `src/slskd/HashDb/Models/Peer.cs` (MODIFY)
- Rename `PeerId` to `MeshPeerId` (type: `MeshPeerId`)
- Add `SoulseekUsername` (string?, nullable)
- Update database schema/migrations

**File**: `src/slskd/HashDb/HashDbService.cs` (MODIFY)
- Key operations by `MeshPeerId` instead of username
- Use mapper when Soulseek username needed for UI/logs

#### Phase 3.2: Refactor Swarm

**File**: `src/slskd/Swarm/SwarmJobModels.cs` (MODIFY)
- Key peers by `MeshPeerId` instead of username

#### Phase 3.3: Refactor Security

**File**: `src/slskd/Common/Security/PeerReputation.cs` (MODIFY)
- Key reputation by `MeshPeerId`
- Add overload for username-based lookups (uses mapper)

## Configuration

Add to `appsettings.json`:
```json
{
  "Mesh": {
    "EnableDhtFirstJoin": true,
    "RequireDescriptorSignature": true
  }
}
```

## Testing Strategy

1. **Unit Tests**:
   - MeshPeerId creation and parsing
   - Registry registration and lookup
   - Identity mapper bidirectional mapping

2. **Integration Tests**:
   - Mesh-only peer discovery via DHT
   - Overlay connection without Soulseek
   - Hash sync with mesh-only peer

3. **Regression Tests**:
   - Existing Soulseek flows still work
   - Mixed mesh+Soulseek peers work
   - No duplicate peer entries

## Risks and Mitigation

### Risk 1: Breaking existing Soulseek flows
**Mitigation**: Make changes additive, keep Soulseek paths intact

### Risk 2: Performance impact of dual lookups
**Mitigation**: Cache mappings, index properly

### Risk 3: Security vulnerabilities in mesh-only path
**Mitigation**: Apply same SecurityCore checks, verify all signatures

## Files to Create

- [x] `docs/MESH_IDENTITY_REFACTOR.md` (this file)
- [ ] `src/slskd/Mesh/Identity/MeshPeerId.cs`
- [ ] `src/slskd/Mesh/Identity/IMeshPeerRegistry.cs`
- [ ] `src/slskd/Mesh/Identity/MeshPeerRegistry.cs`
- [ ] `src/slskd/Mesh/Identity/ISoulseekMeshIdentityMapper.cs`
- [ ] `src/slskd/Mesh/Identity/SoulseekMeshIdentityMapper.cs`
- [ ] `src/slskd/Mesh/Identity/MeshIdentityOptions.cs`
- [ ] `src/slskd/DhtRendezvous/BtExtensionHandler.cs`
- [ ] Database migration scripts

## Files to Modify

- [ ] `src/slskd/Mesh/Dht/MeshPeerDescriptor.cs`
- [ ] `src/slskd/HashDb/Models/Peer.cs`
- [ ] `src/slskd/HashDb/HashDbService.cs`
- [ ] `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- [ ] `src/slskd/Swarm/SwarmJobModels.cs`
- [ ] `src/slskd/Common/Security/PeerReputation.cs`
- [ ] `src/slskd/Program.cs` (DI registration)

## Progress

- [x] Epic 1.1: Analysis and documentation
- [ ] Epic 1.2: Core abstractions (MeshPeerId, registries)
- [ ] Epic 1.3: Feature gate changes
- [ ] Epic 2.1: DHT discovery hooks
- [ ] Epic 2.2: BT extension support
- [ ] Epic 3.1: Hash DB refactor
- [ ] Epic 3.2: Swarm refactor
- [ ] Epic 3.3: Security refactor














