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
- Added FTP completed-download upload settings in System Integrations with
  connection, credential, encryption, retry, overwrite, certificate, runtime
  apply, YAML save, reset, warning, and tooltip affordances.
- Added System Policies for guided YAML-backed webhooks/scripts, transfer
  policy, security/access, search/network, DHT/rescue, and retention/storage
  settings.
- Added System Experience for browser-local Search, Acquisition Review, Player,
  and Messages preferences.

## Strongly Surfaced Already

- System status, diagnostics, logs, jobs, metrics, events, network, shares, files,
  security, source providers, integrations, automation dry runs, library health,
  mesh status, and MediaCore all have dedicated admin surfaces.
- Wishlist, Discovery Inbox, Import Staging, Search, Transfers, Browse, Users,
  Collections, Pods, Rooms/Chat, and Player are user-facing first-class areas.
- Raw YAML options are available in System Options when remote configuration is
  enabled.
- System Policies and System Experience now cover the broad admin/user
  preference gaps as passive configuration surfaces.

## High-Value Missing Admin Knobs

- Live execution backfills remain for some settings: webhook test-send/dry-run,
  script dry-run, hook failure state, group/leecher transfer policy detail,
  relay controller/agent detail, warm-cache/local-state impact previews, and
  richer restart/reload signalling.
- Metadata provider guided settings exist for Chromaprint, AcoustID,
  MusicBrainz, and Lidarr; remaining depth is live credential/provider checks
  and specialized SongID/audio reanalysis controls.

## High-Value Missing General-User Affordances

- Page-level consumption remains for some System Experience preferences: Search,
  Acquisition Review, Player, and Messages can now read a common browser-local
  posture, but individual pages still need follow-up where behavior is not yet
  wired.

## Recommended Follow-Up Order

1. Wire page-specific consumers for System Experience preferences.
2. Add explicit hook/script test-send dry-runs and failure status once backend
   execution contracts exist.
3. Add deeper transfer group/leecher and relay controller/agent editors if those
   policies need to move beyond YAML basics.
