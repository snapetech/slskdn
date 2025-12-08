# Security Configuration Guide

This document describes how to configure slskdN security features.

## Quick Start

Add a `Security` section to your `appsettings.yaml` or `appsettings.json`:

### YAML (appsettings.yaml)

```yaml
Security:
  Enabled: true
  Profile: Standard  # Minimal, Standard, Maximum, or Custom

  NetworkGuard:
    MaxConnectionsPerIp: 3
    MaxGlobalConnections: 100
    MaxMessagesPerMinute: 60
    MaxMessageSize: 65536

  ContentSafety:
    VerifyMagicBytes: true
    QuarantineSuspicious: true
    BlockExecutables: true

  PeerReputation:
    Enabled: true
    TrustedThreshold: 70
    UntrustedThreshold: 20
```

### JSON (appsettings.json)

```json
{
  "Security": {
    "Enabled": true,
    "Profile": "Standard",
    
    "NetworkGuard": {
      "Enabled": true,
      "MaxConnectionsPerIp": 3,
      "MaxGlobalConnections": 100,
      "MaxMessagesPerMinute": 60,
      "MaxMessageSize": 65536,
      "MaxPendingRequestsPerIp": 10
    },

    "PathGuard": {
      "Enabled": true,
      "MaxPathLength": 512,
      "MaxPathDepth": 20,
      "BlockedExtensions": [".exe", ".bat", ".cmd", ".ps1"]
    },

    "ContentSafety": {
      "Enabled": true,
      "VerifyMagicBytes": true,
      "QuarantineSuspicious": true,
      "QuarantineDirectory": "/var/lib/slskd/quarantine",
      "BlockExecutables": true
    },

    "PeerReputation": {
      "Enabled": true,
      "TrustedThreshold": 70,
      "UntrustedThreshold": 20,
      "PersistReputation": true
    },

    "ViolationTracker": {
      "Enabled": true,
      "ViolationWindowMinutes": 60,
      "ViolationsBeforeAutoBan": 5,
      "AutoBansBeforePermanent": 3,
      "BaseBanDurationMinutes": 60
    },

    "ParanoidMode": {
      "Enabled": false,
      "LogServerMessages": false,
      "ValidateServerResponses": true,
      "MaxSearchResults": 1000
    },

    "PrivacyMode": {
      "Enabled": false,
      "MinimizeMetadata": true,
      "UseGenericClientId": true,
      "AvoidPublicRooms": false
    },

    "Events": {
      "MaxEventsInMemory": 10000,
      "MinLogSeverity": "Medium",
      "PersistEvents": false
    }
  }
}
```

## Security Profiles

| Profile | Description |
|---------|-------------|
| **Minimal** | Only critical protections - path traversal, rate limiting |
| **Standard** | Balanced protection - recommended for most users |
| **Maximum** | All features enabled including paranoid mode |
| **Custom** | Use individual settings for full control |

## Network Guard

Controls connection and message rate limiting.

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxConnectionsPerIp` | 3 | Max simultaneous connections from one IP |
| `MaxGlobalConnections` | 100 | Max total connections |
| `MaxMessagesPerMinute` | 60 | Rate limit per IP |
| `MaxMessageSize` | 65536 | Max message size in bytes (64KB) |
| `MaxPendingRequestsPerIp` | 10 | Max queued requests per IP |

## Path Guard

Prevents directory traversal attacks.

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxPathLength` | 512 | Maximum path length |
| `MaxPathDepth` | 20 | Maximum directory depth |
| `BlockedExtensions` | [list] | Extensions to block |

## Content Safety

Detects malicious file content.

| Setting | Default | Description |
|---------|---------|-------------|
| `VerifyMagicBytes` | true | Check file magic bytes match extension |
| `QuarantineSuspicious` | true | Move suspicious files to quarantine |
| `QuarantineDirectory` | "" | Path for quarantined files |
| `BlockExecutables` | true | Block executable files |

## Peer Reputation

Tracks peer behavior and reliability.

| Setting | Default | Description |
|---------|---------|-------------|
| `TrustedThreshold` | 70 | Score above which peers are trusted |
| `UntrustedThreshold` | 20 | Score below which peers are untrusted |
| `PersistReputation` | true | Save reputation to disk |

## Violation Tracker

Auto-escalating bans for bad actors.

| Setting | Default | Description |
|---------|---------|-------------|
| `ViolationWindowMinutes` | 60 | Time window for counting violations |
| `ViolationsBeforeAutoBan` | 5 | Violations before auto-ban |
| `AutoBansBeforePermanent` | 3 | Temp bans before permanent |
| `BaseBanDurationMinutes` | 60 | Initial ban duration |

## Paranoid Mode

Extra validation for server interactions.

| Setting | Default | Description |
|---------|---------|-------------|
| `LogServerMessages` | false | Log all server messages |
| `ValidateServerResponses` | true | Validate server responses |
| `MaxSearchResults` | 1000 | Max search results to accept |
| `BlockedIpRanges` | [] | IP ranges to block |

## Privacy Mode

Minimize information disclosure.

| Setting | Default | Description |
|---------|---------|-------------|
| `MinimizeMetadata` | true | Strip unnecessary metadata |
| `UseGenericClientId` | true | Use generic client identifier |
| `AvoidPublicRooms` | false | Don't auto-join public rooms |
| `StripSharePaths` | false | Strip local paths from shares |

## API Endpoints

Security features expose a REST API for monitoring:

```
GET  /api/v0/security/dashboard     # Overview stats
GET  /api/v0/security/events        # Recent events
GET  /api/v0/security/bans          # Active bans
POST /api/v0/security/bans/ip       # Ban an IP
POST /api/v0/security/bans/username # Ban a username
GET  /api/v0/security/reputation/{username}  # Get reputation
```

## Hardened Deployment

For production deployments, see the included `etc/systemd/slskd-hardened.service` 
for a systemd unit file with security sandboxing enabled.

Key systemd features:
- `NoNewPrivileges=yes`
- `ProtectSystem=strict`
- `ProtectHome=yes`
- `PrivateTmp=yes`
- `ReadWritePaths` restricted to data directories

