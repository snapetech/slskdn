# Soulseek Type-1 Obfuscation

slskdN treats Soulseek type-1 peer-message obfuscation as a first-class feature option. The option defaults on in `compatibility` mode, so the regular peer-message path remains available and obfuscated reachability is added. The option is intentionally conservative: it is configurable, validated, visible in the Network tab, and keeps legacy-client fallback enabled.

## What We Know

The research shows enough to design and expose the feature:

- The public server accepts and returns obfuscation type and obfuscated-port metadata.
- Type-1 obfuscated peer-message streams can be accepted by an obfuscated listener.
- Direct obfuscated peer-message connections can succeed across separate public endpoints.
- Indirect peer-message connection flow can carry enough metadata for the target to choose the obfuscated port.
- Obfuscated-only reachability can work between compatible implementations, but slskdN does not enable that posture while broad legacy compatibility is the default.

This is stronger than a local-only prototype. It is enough to justify product support for compatibility and prefer modes while reserving only mode for a later explicit compatibility break.

## Current Runtime Status

slskdN’s vendored runtime exposes the wire path:

- SetListenPort obfuscation type and obfuscated-port advertisement.
- Type-1 obfuscated peer-message listener support.
- Type-1 obfuscated outbound peer-message dialing.
- Obfuscation fields on peer-address and indirect-connect responses.

When enabled with a valid obfuscated listener port, slskdN reports type-1 obfuscation as `active`.

## Modes

`compatibility` mode is the broad-client default. It advertises regular and obfuscated peer-message reachability together. This mode must not block or replace the normal peer-message path.

`prefer` mode is the enhanced posture. It prefers type-1 obfuscated outbound peer-message dials when the peer advertises compatible metadata and keeps regular fallback for other clients.

`only` mode is reserved. The current runtime rejects obfuscated-only advertising because slskdN preserves the regular peer-message path for legacy clients.

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

Type-1 obfuscation must remain peer-message focused until file-transfer and distributed-network paths are independently proven. Implementations must preserve regular fallback in `compatibility` and `prefer` modes and rate-limit connection retries.

The feature is not encryption. It should not be described as anonymous, secure, or confidential transport. The correct description is obfuscated peer-message connectivity for compatible peers.

## Validation Work

Runtime support is active. Ongoing validation should include public-server advertisement tests, direct compatible-peer tests, indirect compatible-peer tests, regular fallback tests, and negative tests proving plain traffic is rejected by the obfuscated listener.
