# Web UI Surface Audit - 2026-04-30

This audit compares the current backend/config feature surface with operator and
general-user Web UI affordances. It is intentionally scoped to surfaced controls,
status signalling, and safe knobs/levers.

## Implemented In This Pass

- Merged the Chat and Rooms entry points into a unified Messages workspace.
- Added multi-panel message windows for direct chats and joined rooms.
- Added collapse/restore dock behavior so multiple conversations can stay open
  without taking over the whole page.
- Kept `/chat` and `/rooms` as compatibility entry points into the Messages
  workspace.
- Added source-feed provider settings in System Integrations for Spotify,
  YouTube, and Last.fm with toggles, masked secrets, warnings, runtime apply,
  YAML save, reset, and tooltips.
- Added notification provider settings in System Integrations for Pushbullet,
  Ntfy, and Pushover with delivery toggles, private-message and room-mention
  triggers, masked secret replacement, warnings, runtime apply, YAML save,
  reset, and tooltips.

## Strongly Surfaced Already

- System status, diagnostics, logs, jobs, metrics, events, network, shares, files,
  security, source providers, integrations, automation dry runs, library health,
  mesh status, and MediaCore all have dedicated admin surfaces.
- Wishlist, Discovery Inbox, Import Staging, Search, Transfers, Browse, Users,
  Collections, Pods, Rooms/Chat, and Player are user-facing first-class areas.
- Raw YAML options are available in System Options when remote configuration is
  enabled.

## High-Value Missing Admin Knobs

- Remaining notification/action integrations: webhooks, scripts, and FTP are
  configured in YAML but lack a guided admin UI for enablement, event selection,
  test-send/dry-run, retry policy, remote target visibility, and failure state.
- Identity and access: API keys, JWT TTL/key status, passthrough auth, HTTPS
  certificate settings, force HTTPS, and rate limits are mostly YAML/raw-options
  surfaces. These need a security admin editor with redacted secret replacement
  and explicit restart/reload signalling.
- Transfer policy: upload/download slots, speed limits, scheduled speed limits,
  auto-replace, retry policy, group limits, leecher thresholds, and per-group
  policies are not exposed as a cohesive transfer policy editor.
- Search/network policy: incoming search throttling, search filters, blacklist
  file enablement/path, Scene Pod Bridge enablement, DHT bootstrap/router
  controls, relay mode/controller/agent settings, and rescue mode need clear
  status plus conservative enable/test controls.
- Metadata providers: MusicBrainz, AcoustID, Chromaprint, SongID concurrency,
  audio hash/reanalysis flags, and external visualizer launch settings need
  provider cards with enable/test/limits/status instead of only feature panels.
- Retention/storage: transfer/file retention, share-cache mode/workers/retention,
  warm cache, data cleanup, and local state size should have admin controls and
  impact previews.

## High-Value Missing General-User Affordances

- Discovery Inbox and Import Staging should surface clearer "why this candidate"
  explanations, stale age, source confidence, and one-click filters for provider,
  profile, and approval state.
- Search should make saved ranking preferences, duplicate folding state,
  preferred conditions, and planned-action previews more discoverable as user
  preferences, not only per-search controls.
- Player local-only preferences now have many capabilities but need a compact
  preferences drawer for queue, radio, rating, history, scrobble, visualizer, and
  keyboard behavior.
- Messages now has multi-panel behavior, but future work should add unread counts
  per panel, pinned panels, per-room user filtering, message search, and optional
  compact/dense display preference.

## Recommended Follow-Up Order

1. Extend System Notifications/Actions to cover webhooks, scripts, and FTP
   because these are operator-facing, testable, and still fragmented.
2. Build a Transfer Policy panel for global/group speed, slot, retry, scheduled,
   and auto-replace controls because these directly affect network citizenship.
3. Build a Security/Auth settings panel for API keys, JWT, HTTPS, passthrough,
   and rate limiting with careful redaction and restart signalling.
4. Add compact preference drawers for Search, Discovery Inbox, Player, and
   Messages after the admin surfaces are no longer YAML-only.
