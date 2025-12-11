# Signal System Configuration

## Overview

The signal system provides multi-channel control signaling between slskdn peers. Configuration is available via:

1. **YAML Configuration File** - `SignalSystem` section in `slskd.yml`
2. **Environment Variables** - `SLSKD_SIGNALSYSTEM_*` prefix
3. **API Endpoints** - `GET /api/v0/signals/config` and `GET /api/v0/signals/status`

## Configuration Schema

### YAML Example

```yaml
SignalSystem:
  Enabled: true
  DeduplicationCacheSize: 10000
  DefaultTtl: "00:05:00"  # TimeSpan format: HH:mm:ss
  MeshChannel:
    Enabled: true
    Priority: 1
    RequireActiveSession: false
  BtExtensionChannel:
    Enabled: true
    Priority: 2
    RequireActiveSession: true
```

### Environment Variables

```bash
SLSKD_SIGNALSYSTEM_ENABLED=true
SLSKD_SIGNALSYSTEM_DEDUPLICATIONCACHESIZE=10000
SLSKD_SIGNALSYSTEM_DEFAULTTTL=00:05:00
SLSKD_SIGNALSYSTEM_MESHCHANNEL_ENABLED=true
SLSKD_SIGNALSYSTEM_MESHCHANNEL_PRIORITY=1
SLSKD_SIGNALSYSTEM_BTEXTENSIONCHANNEL_ENABLED=true
SLSKD_SIGNALSYSTEM_BTEXTENSIONCHANNEL_PRIORITY=2
SLSKD_SIGNALSYSTEM_BTEXTENSIONCHANNEL_REQUIREACTIVESESSION=true
```

## Configuration Options

### SignalSystem

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enable the signal system |
| `DeduplicationCacheSize` | `int` | `10000` | Maximum size of SignalId LRU cache (100-1000000) |
| `DefaultTtl` | `TimeSpan` | `00:05:00` | Default time-to-live for signals |

### MeshChannel

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enable Mesh overlay channel |
| `Priority` | `int` | `1` | Channel priority (1-10, lower = higher priority) |
| `RequireActiveSession` | `bool` | `false` | Require active Mesh session (not applicable for Mesh) |

### BtExtensionChannel

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enable BT extension channel |
| `Priority` | `int` | `2` | Channel priority (1-10, lower = higher priority) |
| `RequireActiveSession` | `bool` | `true` | Require active BT session before using this channel |

## API Endpoints

### GET /api/v0/signals/config

Returns current signal system configuration.

**Response:**
```json
{
  "enabled": true,
  "deduplication_cache_size": 10000,
  "default_ttl_seconds": 300,
  "mesh_channel": {
    "enabled": true,
    "priority": 1,
    "require_active_session": false
  },
  "bt_extension_channel": {
    "enabled": true,
    "priority": 2,
    "require_active_session": true
  }
}
```

### GET /api/v0/signals/status

Returns signal system status and statistics.

**Response:**
```json
{
  "enabled": true,
  "active_channels": ["mesh", "bt_extension"],
  "statistics": {
    "signals_sent": 0,
    "signals_received": 0,
    "duplicate_signals_dropped": 0,
    "expired_signals_dropped": 0
  }
}
```

## Usage

### Enabling Signal System

The signal system is enabled by default. To disable:

```yaml
SignalSystem:
  Enabled: false
```

### Channel Priority

Signals are sent via channels in priority order (lower number = higher priority). If the first channel fails, the system automatically tries the next channel.

Example: If `MeshChannel.Priority = 1` and `BtExtensionChannel.Priority = 2`, signals will:
1. First try Mesh channel
2. If Mesh fails, try BT extension channel
3. If both fail, signal delivery fails

### BT Extension Channel

The BT extension channel requires an active BitTorrent session with the target peer. Set `RequireActiveSession: false` to allow sending even without an active session (not recommended, as delivery will likely fail).

## Integration

The signal system is automatically initialized when the application starts if:
- `SignalSystem.Enabled = true`
- At least one channel is enabled
- Required dependencies (MeshCore/BitTorrentBackend) are available

To manually initialize:

```csharp
await serviceProvider.InitializeSignalSystemAsync(localPeerId, cancellationToken);
```

## See Also

- [Signal System Design](signal-request-bt-fallback.md) - End-to-end signal example
- [Phase 12 Design](../phase12-adversarial-resilience-design.md#8-signal-system-for-multi-channel-communication) - Signal system architecture

