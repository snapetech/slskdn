# Soulseek Type-1 Obfuscation

slskdN treats Soulseek type-1 peer-message obfuscation as a first-class feature option. The option defaults on in `compatibility` mode, so the regular peer-message path remains available and obfuscated reachability is added when the runtime can honor it. The option is intentionally conservative: it is configurable, validated, visible in the Network tab, and documented as a runtime plan before the current Soulseek.NET dependency can activate the wire path.

## What We Know

The research shows enough to design and expose the feature:

- The public server accepts and returns obfuscation type and obfuscated-port metadata.
- Type-1 obfuscated peer-message streams can be accepted by an obfuscated listener.
- Direct obfuscated peer-message connections can succeed across separate public endpoints.
- Indirect peer-message connection flow can carry enough metadata for the target to choose the obfuscated port.
- An obfuscated-only posture works between compatible implementations when the regular port is intentionally unreachable and the obfuscated port is correctly advertised.

This is stronger than a local-only prototype. It is enough to justify product support for compatibility, prefer, and explicit only modes.

## Current Runtime Status

The current slskdN runtime uses Soulseek.NET. The packaged public API does not expose:

- SetWaitPort fields for obfuscation type and obfuscated port advertisement.
- A type-1 obfuscated peer-message listener.
- A type-1 obfuscated outbound peer-message dialer.
- Obfuscation fields on peer-address or indirect-connect responses.

Because of that limitation, slskdN currently reports type-1 obfuscation as `configured_pending_runtime` when enabled. The options are real, validated, and default-on in compatibility mode, but the wire path is not active until Soulseek.NET support or a slskdN transport adapter lands.

## Modes

`compatibility` mode is the broad-client default. When runtime support exists, it should advertise regular and obfuscated peer-message reachability together. This mode must not block or replace the normal peer-message path.

`prefer` mode is the enhanced posture. When runtime support exists, it should prefer type-1 obfuscated outbound peer-message dials when the peer advertises compatible metadata and keep regular fallback for other clients.

`only` mode is the strict posture. It requires an explicit obfuscated listen port and disables regular-port advertisement. This can break clients that ignore obfuscated metadata and should remain an explicit opt-in.

## Configuration

```yaml
soulseek:
  listen_port: 50300
  obfuscation:
    enabled: true
    mode: compatibility
    listen_port: 0
    advertise_regular_port: true
    prefer_outbound: true
```

CLI and environment equivalents are documented in `docs/config.md`.

## Network Health Rules

Type-1 obfuscation must remain peer-message focused until file-transfer and distributed-network paths are independently proven. Implementations must preserve regular fallback in `compatibility` and `prefer` modes, rate-limit connection retries, and make `only` mode visibly explicit because it reduces interoperability.

The feature is not encryption. It should not be described as anonymous, secure, or confidential transport. The correct description is obfuscated peer-message connectivity for compatible peers.

## Runtime Work Required

To activate this beyond configuration and status reporting, slskdN needs one of these runtime paths:

1. Add the missing public hooks to Soulseek.NET and wire slskdN options into them.
2. Add a slskdN-owned peer-message transport adapter for SetWaitPort metadata, obfuscated listener accept, and obfuscated outbound dial.

The first runtime activation ticket should include public-server advertisement tests, direct compatible-peer tests, indirect compatible-peer tests, regular fallback tests, and negative tests proving plain traffic is rejected by the obfuscated listener.
