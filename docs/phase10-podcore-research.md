#
# Phase 10: PodCore & Soulseek Chat Bridge — Research Summary
#
# Date: 2025-12-10
# Branch: `experimental/brainz`
# Scope: T-1000..T-1031, T-1100..1101

## Goals
- Define pods as social/micro-community containers with signed membership, channels, and opinions.
- Bridge Soulseek rooms to pods (read-only and mirrored modes) with safety guardrails.
- Provide UI/UX patterns and trust/moderation boundaries.

## Decisions & Findings

### Pod Identity & Metadata
- PodId: `pod:<base32(sha256(pubkey))[0:12]>`
- Pod metadata (name, visibility, tags, focus/content links) signed by pod owner.
- Membership records are signed by inviter/owner; include role (owner/mod/member), ban flag, and timestamp.

### Discovery
- Listed pods discoverable via DHT keys: `pod:discover:name:<slug>`, `pod:discover:tag:<tag>`.
- Unlisted/private pods require invite or explicit PodId.

### Messaging
- PodMessage: channelId, messageId, senderPeerId, body, signature, ts.
- Deduplicate by messageId; signatures verified; enforce membership and bans.
- Channels: general + custom; bound channels for Soulseek rooms (see bridge).

### Content-Linked Pods
- FocusType=ContentId to bind to MB recording/release/artist; used for opinions and “collection vs pod” view.
- PodVariantOpinion: (ContentId, VariantHash, Score, Note, SenderPeerId, Signature).
- Opinions are signed and aggregated locally; not published to public DHT without consent.

### Trust & Moderation
- PodAffinity scoring: engagement (messages/participation), peer trust (reuse SecurityCore).
- Owner/mod actions: kick/ban via signed membership update.
- Abuse signals can feed back into global reputation (optional).

### UI/UX Guardrails
- No auto-linkifying magnets/URLs in chat; clear indicators for bound channels (direction of flow).
- “Collection vs pod” dashboard shows gaps and recommendations; no auto-download links.

### Soulseek Chat Bridge
- ExternalBinding on pod metadata: kind `soulseek-room`, mode `readonly` or `mirror`.
- Bound channel creation: Soulseek room → pod channel, with safety badge.
- Two-way mirroring only in `mirror` mode; default `readonly`.
- Soulseek identity mapping: synthetic PeerIds `soulseek:<username>`, optional verification/linking.
- Presence/status is advisory; avoid leaking PII.

### Domain Apps
- Keep domain apps (e.g., Soulbeet) as thin UIs over PodCore + MediaCore; no PII in descriptors.
- Extensibility to other media domains remains gated behind MediaCore/ContentID.

## Risks & Guardrails
- Always signed membership/messages/opinions; reject unsigned.
- No PII (no paths, no emails). Soulseek usernames only mapped to synthetic PeerIds.
- Bound channels default to read-only; mirrored mode opt-in and clearly indicated.
- Size limits and TTL for any DHT-published pod metadata; keep chat off DHT (overlay/pubsub only).

## Next Steps (implementation)
- Implement Pod models + in-memory PodService (create/list/join/leave, membership validation).
- Implement PodMessaging stub with signature checks and dedupe.
- Implement SoulseekChatBridge stub with readonly/mirror modes and identity mapping.
- Add UI stubs/hooks for pod list/detail, chat, and collection-vs-pod dashboard.
