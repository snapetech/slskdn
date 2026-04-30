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

## Web Player

The Web UI player is part of slskdN itself. It is not a separate streaming server, and it does not require an external media service.

### Playback Sources

The player can start playback from:

- Collection rows and collection-item play buttons.
- The player empty-state **Collections** browser, which opens a two-pane modal with collection list and playable items.
- The player empty-state **Files** browser, which opens a searchable modal over shared and downloaded local audio.
- Pod/listening-party follow actions that resolve the announced `ContentId`.

All normal playback uses `GET /api/v0/streams/{contentId}`. That endpoint supports byte ranges, seeking, content-type detection, authenticated access, share-token access where applicable, and per-user stream limiting.

For local browser picking, slskdN can resolve streamable IDs from configured non-excluded share directories and the configured downloads directory. This keeps downloaded/shared audio playable even before a row has been persisted into `content_items`, while still keeping file access scoped to configured local roots.

### Player Controls

The persistent player is docked above the fixed footer and can collapse into a small drawer bar. It should never cover the footer.

Controls include:

- Play/pause and stop.
- Previous/next queue navigation.
- Rewind and fast-forward.
- Browser-local mute. This mutes only the current browser or installed PWA; it does not stop the stream or mute other listeners.
- Browser Media Session metadata and transport handlers for supported mobile/PWA lock-screen controls.
- Optional MilkDrop visualizer, lightweight analyzer, equalizer, synced lyrics, crossfade, karaoke-style center-channel reduction, and ListenBrainz now-playing/scrobble submission when the relevant player controls are enabled.

The browser owns audio output. A listener can keep following a party while locally muted, and the host can keep playing locally while publishing metadata.

### External Visualizers

The built-in player visualizer uses Butterchurn in the browser. slskdN can also expose an opt-in external visualizer launcher for host-side tools such as MilkDrop3. Configure `player.external_visualizer` in `slskd.yml`, then use **Player Integrations** to check status or launch it.

External launches run on the slskdN host process, not on the browser device. This works best when slskdN runs in the same desktop session as the visualizer and the visualizer can capture system audio or a virtual audio device. Docker, service, SSH, and headless deployments usually need a wrapper script or will not have a display/audio session.

The browser cannot provide executable paths or arguments. The launch endpoint only uses configured values from `slskd.yml`, is disabled by default, requires normal API auth, and does not contact Soulseek peers.

## Network And Rights Boundary

Layer 1 is deliberately conservative. It broadcasts only metadata and relies on the existing stream endpoint and authorization boundary for bytes:

- Normal authenticated users can stream content their node can locate.
- Share tokens still control shared collection streaming.
- Pod messages do not grant a new right to bytes.
- No peer is asked to browse, probe, download, or relay media automatically.

This keeps listening parties aligned with slskdN network-health rules: user-triggered playback, no aggressive scanning, and no surprise bandwidth fan-out from the host.

## Layer 1.5: Global Radio Registry

Hosts can explicitly opt in to listing a party in the slskdN radio directory. This is still integrated into slskdN:

- Directory API: `GET /api/v0/listening-party`
- Host publish API: `POST /api/v0/listening-party/{podId}/{channelId}`
- Integrated radio stream: `GET /api/v0/listening-party/radio/{partyId}/{contentId}`

The host controls two separate toggles:

- **List globally** publishes a `slskdn.listeningParty.announce.v1` announcement into the mesh/DHT-backed party index.
- **Mesh streaming** allows listeners who find the listing to stream the current track directly from the host's slskdN node.

The registry announcement is TTL-based and contains metadata plus a relative stream path when mesh streaming is enabled:

```json
{
  "kind": "slskdn.listeningParty.announce.v1",
  "partyId": "party:...",
  "podId": "pod:...",
  "channelId": "general",
  "hostPeerId": "alice",
  "title": "Track title",
  "artist": "Artist",
  "contentId": "content:audio:file:...",
  "allowMeshStreaming": true,
  "streamPath": "/api/v0/listening-party/radio/party%3A.../content%3Aaudio%3Afile%3A...",
  "expiresAtUnixMs": 1777501646000
}
```

The integrated radio endpoint only serves the active party's current `ContentId`, only while the host has both listing and mesh streaming enabled, and only while the normal streaming feature is enabled. It uses the same content locator and stream session limiter pattern as `/api/v0/streams/{contentId}`.

## Deferred: Live Mic / Host Commentary

Live microphone or host audio broadcast is a later layer. It should use opt-in WebRTC media with SDP/ICE signaling carried by pod messages, and it needs a separate rights and moderation review before public/listed pods can expose it.
