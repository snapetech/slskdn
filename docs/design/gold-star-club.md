# Gold Star Club Design

## Overview

The **Gold Star Club** is a special pod that automatically joins the first 250 users of the slskdN network. Once membership reaches 250, no new members can be added, even if existing members leave. The cohort is used for realm governance bootstrap, early network testing, and high-signal feedback.

## Requirements

1. **Auto-join**: Network members automatically join on first connection unless they opt out
2. **Limit**: Maximum 250 members
3. **One-time**: Once full, no new members can be added (even if people leave)
4. **Revocable**: A user can leave later to revoke local Gold Star status
5. **Exclusive**: Only the first 250 users get this privilege

## Implementation

### Service: `GoldStarClubService`

Located in: `src/slskd/PodCore/GoldStarClubService.cs`

**Key Features**:
- Creates the pod on first startup (if it doesn't exist)
- Auto-joins users when they first connect
- Enforces 250-member limit
- Records a local revocation marker when the user leaves, so default-on auto-join does not rejoin them on restart
- Caches membership status to avoid repeated checks

### Pod Details

- **Pod ID**: `pod:901d57a2c1bb4e5d90d57a2c1bb4e5d0` (fixed, not random)
- **Name**: "Gold Star Club ⭐"
- **Visibility**: Listed (discoverable)
- **Tags**: `gold-star`, `first-250`, `realm-governance`, `testing`
- **Default Channel**: `general`

### Auto-Join Logic

1. **On Startup**: `GoldStarClubService` runs as a `BackgroundService`
2. **Wait for Connection**: Waits up to 30 seconds for Soulseek client to connect
3. **Ensure Pod Exists**: Creates the pod if it doesn't exist
4. **Check Eligibility**: 
   - Checks if user is already a member
   - Checks if local auto-join is disabled with `SLSKDN_POD_GOLD_STAR_CLUB_AUTOJOIN=false`
   - Checks if local membership was revoked by a previous leave action
   - Checks if membership count < 250
   - Checks current count again before joining (race condition protection)
5. **Join**: Adds user as a regular "member" with signed membership record

### Membership Limit Enforcement

- **Check Before Join**: Always checks current membership count before allowing join
- **Race Condition Protection**: Re-fetches members list right before joining
- **Caching**: Caches `isAcceptingMembers` status to avoid repeated DHT queries
- **One-Time**: Once limit is reached, `isAcceptingMembers` is permanently set to `false`

### Opt-Out and Revocation

- **Before startup**: set `SLSKDN_POD_GOLD_STAR_CLUB_AUTOJOIN=false`.
- **After joining**: leave the Gold Star Club pod from the Pods page. The server writes a local `gold-star-club.revoked` marker in the app directory so the next restart does not auto-join again.
- **Rejoin after revocation**: remove the local revocation marker and restart while membership is still below 250.

### Edge Cases Handled

1. **Race Conditions**: Multiple users trying to join simultaneously
   - Solution: Re-check membership count right before joining
   
2. **Pod Already Exists**: If pod was created manually
   - Solution: Check for existing pod before creating
   
3. **User Already Member**: If user was manually added
   - Solution: Check membership before attempting join
   
4. **Connection Delays**: Soulseek client not connected immediately
   - Solution: Wait up to 30 seconds with polling

## API

### `IGoldStarClubService`

```csharp
public interface IGoldStarClubService
{
    string GoldStarClubPodId { get; }
    int MaxMembership { get; }
    Task<bool> IsAcceptingMembersAsync(CancellationToken ct = default);
    Task<int> GetMembershipCountAsync(CancellationToken ct = default);
    Task<bool> TryAutoJoinAsync(string peerId, CancellationToken ct = default);
    Task RecordRevocationAsync(string peerId, CancellationToken ct = default);
    Task EnsurePodExistsAsync(CancellationToken ct = default);
}
```

## Configuration

The service is enabled by default and runs automatically. Set `SLSKDN_POD_GOLD_STAR_CLUB_AUTOJOIN=false` in the daemon environment to opt out before startup.

## Testing

Unit tests in: `tests/slskd.Tests.Unit/PodCore/GoldStarClubServiceTests.cs`

**Test Coverage**:
- Pod creation (if not exists)
- Pod existence check (if exists)
- Membership count retrieval
- Accepting members check (under/at/over limit)
- Auto-join success (under limit)
- Auto-join rejection (at limit)
- Already-member check
- Race condition handling

## Future Enhancements

Potential future features:
- WebGUI indicator showing Gold Star Club status
- Badge/icon for Gold Star Club members
- Special channel or privileges for Gold Star Club members
- Statistics/metrics on Gold Star Club membership

---

**Status**: ✅ Implemented and tested
