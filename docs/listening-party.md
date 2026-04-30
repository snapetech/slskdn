# Listening Party

Last updated: 2026-04-30

slskdN listening parties are metadata-synchronized playback sessions for pods. They intentionally do not relay music bytes through the host.

## Layer 1: Listen Along

The first implementation ships a persistent web player and a pod listen-along protocol:

- The host plays a local/shared item by `ContentId` through `GET /api/v0/streams/{contentId}`.
- The player updates `NowPlayingService` directly, so the Soulseek profile reflects playback started from the web UI.
- The host can publish pod-scoped playback metadata: `play`, `pause`, `seek`, and `stop`.
- Listeners receive metadata over SignalR and load their own stream for the same `ContentId`.
- Playback messages are stored as pod messages and routed through the existing pod message router.

The protocol payload is JSON in the pod message body:

```json
{
  "kind": "slskdn.listenAlong.v1",
  "podId": "pod:example",
  "channelId": "general",
  "hostPeerId": "alice",
  "action": "play",
  "contentId": "content:audio:file:...",
  "title": "Track title",
  "artist": "Artist",
  "album": "Album",
  "positionSeconds": 15.2,
  "serverTimeUnixMs": 1777500746000,
  "sequence": 42
}
```

`serverTimeUnixMs` and `positionSeconds` let listeners compensate for elapsed time when joining a currently playing party. Clients should treat host state as advisory and keep user control local: following a party can be toggled off without leaving the pod.

## Network And Rights Boundary

Layer 1 is deliberately conservative. It broadcasts only metadata and relies on the existing stream endpoint and authorization boundary for bytes:

- Normal authenticated users can stream content their node can locate.
- Share tokens still control shared collection streaming.
- Pod messages do not grant a new right to bytes.
- No peer is asked to browse, probe, download, or relay media automatically.

This keeps listening parties aligned with slskdN network-health rules: user-triggered playback, no aggressive scanning, and no surprise bandwidth fan-out from the host.

## Deferred: Live Mic / Host Commentary

Live microphone or host audio broadcast is a later layer. It should use opt-in WebRTC media with SDP/ICE signaling carried by pod messages, and it needs a separate rights and moderation review before public/listed pods can expose it.

