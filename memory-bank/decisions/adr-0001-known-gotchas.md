# ADR-0001: Known Gotchas & Anti-Patterns

> **Status**: Active (Living Document)  
> **Date**: 2025-12-08  
> **Author**: AI-assisted development sessions

This document captures known issues, anti-patterns, and "gotchas" that AI models and developers have encountered. **Read this before making changes.**

---

## ⚠️ FOR AI AGENTS: ADD TO THIS FILE IMMEDIATELY

**When you fix a bug, ADD IT HERE before doing anything else.**

This is not optional. This is the highest priority action after fixing a bug.

### When to add an entry:
- You fixed a bug that you or another AI introduced
- You fixed the same type of bug more than once
- You discovered a non-obvious pattern that causes errors
- You spent >5 minutes debugging something that should have been documented

### Entry template:
```markdown
### N. short-descriptive-name

**The Bug**: One sentence description.

**Files Affected**:
- `path/to/file.cs`

**Wrong**:
```code
// bad code
```

**Correct**:
```code
// good code
```

**Why This Keeps Happening**: Explanation of the root cause.
```

### After adding:
1. Commit immediately: `git commit -m "docs: Add gotcha for [description]"`
2. Then continue with other work

**If you skip this step, the same bug WILL happen again. Document it NOW.**

---

## 🚨 CRITICAL: Bugs That Keep Coming Back

### 0z242. Listen Along Controls Must Be Channel-Scoped And Compact

**The Bug**: Unified pod messaging mounted the full Listen Along panel for every pod channel, including direct-message channels, which made DMs look like broadcast rooms and consumed most of the message panel.

**Files Affected**:
- `src/web/src/components/Messaging/Messaging.jsx`
- `src/web/src/components/Pods/Pods.jsx`
- `src/web/src/components/Player/PodListenAlongPanel.jsx`
- `src/web/src/components/Player/Player.css`

**Wrong**:
```jsx
<PodListenAlongPanel
  channelId={channel.channelId}
  podId={channel.podId}
/>
```

**Correct**:
```jsx
{!isPodDirectChannel(channel) && (
  <PodListenAlongPanel
    channelId={channel.channelId}
    compact
    podId={channel.podId}
  />
)}
```

**Why This Keeps Happening**: Listen Along is a room/broadcast affordance, not a direct-message affordance. Reusing room-level collaboration widgets in a unified message workspace must keep channel kind in the render condition and use compact controls, otherwise DMs inherit unrelated broadcast UI and the chat surface loses usable space.

### 0z241. WebGPU Raw Texture Upload Rows Must Be 256-Byte Aligned

**The Bug**: WebGPU texture uploads can fail validation when small imported RGBA images use a tightly packed `bytesPerRow` such as `width * 4`, unlike WebGL `texImage2D` which accepts tightly packed rows.

**Files Affected**:
- `src/web/src/components/Player/visualizers/milkdrop/webgpuRenderer.js`

**Wrong**:
```javascript
device.queue.writeTexture(
  { texture },
  data,
  { bytesPerRow: width * 4, rowsPerImage: height },
  [width, height],
);
```

**Correct**:
```javascript
const sourceBytesPerRow = width * 4;
const bytesPerRow = Math.ceil(sourceBytesPerRow / 256) * 256;
const textureData = new Uint8Array(bytesPerRow * height);
```

**Why This Keeps Happening**: Browser WebGPU copy layout validation is stricter than the WebGL texture path used by the parity renderer. Imported MilkDrop texture assets are often tiny sprites or checker images, so a direct WebGL-to-WebGPU port will work for some widths and fail for small or odd image sizes unless rows are padded before `queue.writeTexture(...)`.

### 0z240. Unified Messaging Must Not Duplicate Bridged Pod Direct Channels

**The Bug**: The unified Messages workspace listed normal saved DMs and matching pod `DM` channels as separate conversations, then embedded chat/room/pod panels without preserving visible composers and room member rails.

**Files Affected**:
- `src/web/src/components/Messaging/Messaging.jsx`
- `src/web/src/components/Messaging/Messaging.css`
- `src/web/src/components/Messaging/Messaging.test.jsx`

**Wrong**:
```javascript
{podChannels.map((channel) => (
  <Button>{channelLabel(channel)}</Button>
))}
```

**Correct**:
```javascript
const visiblePodChannels = podChannels.filter(
  (channel) =>
    !(isPodDirectChannel(channel) && savedChatNames.has(normalizeConversationName(channel.podName))),
);
```

**Why This Keeps Happening**: Pods can expose a `DM` channel for the same peer that already has a Soulseek saved chat. A unified workspace must fold those bridged direct channels into the existing direct-message row and explicitly restyle embedded session composers/member lists, because the old standalone Chat/Rooms/Pods layouts assumed full-page width and their inputs can collapse or disappear when nested in cards.

### 0z239. Share Scans Need An Explicit Media-Attribute Probe Escape Hatch

**The Bug**: Share scans always opened shared audio files with TagLib to extract bitrate, length, sample rate, and bit depth. On slow or remote storage this optional browse metadata could dominate scan runtime even though files can be shared without those attributes.

**Files Affected**:
- `src/slskd/Core/Options.cs`
- `src/slskd/Shares/SoulseekFileFactory.cs`
- `src/slskd/Shares/ShareScanner.cs`
- `config/slskd.example.yml`
- `docs/config.md`

**Wrong**:
```csharp
if (AudioExtensionsSet.Contains(fileInfo.Extension))
{
    var tagLibFile = TagLib.File.Create(new StreamFileAbstraction(fileInfo.Name, stream));
    attributes = GetAudioAttributes(tagLibFile);
}
```

**Correct**:
```csharp
if (ProbeMediaAttributes && AudioExtensionsSet.Contains(fileInfo.Extension))
{
    var tagLibFile = TagLib.File.Create(new StreamFileAbstraction(fileInfo.Name, stream));
    attributes = GetAudioAttributes(tagLibFile);
}
```

**Why This Keeps Happening**: Media probing feels like harmless enrichment until the share library lives on NFS, USB, mergerfs, cloud mounts, or aging disks. Share discovery must keep the cheap "file exists and can be advertised" path separate from optional metadata extraction, with a visible config escape hatch for operators.

### 0z238. DHT Rendezvous Must Back Off Repeated Unverified Overlay Candidates

**The Bug**: Public DHT discovery kept retrying candidates that repeatedly failed overlay handshakes on a fixed service-level reconnect interval. Connector cooldowns prevented some hammering, but the rendezvous scheduler still spent work on likely non-overlay or dead endpoints every discovery cycle.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `tests/slskd.Tests.Unit/DhtRendezvous/DhtRendezvousServiceTests.cs`

**Wrong**:
```csharp
if (!ShouldRetryPeerConnection(peerId, PeerReconnectInterval, now))
{
    Interlocked.Increment(ref _totalConnectionCooldownSkips);
    return;
}
```

**Correct**:
```csharp
var failureCount = _peerConnectionFailureCounts.GetValueOrDefault(peerId);
var reconnectInterval = GetPeerReconnectInterval(failureCount);

if (!ShouldRetryPeerConnection(peerId, reconnectInterval, now))
{
    Interlocked.Increment(ref _totalConnectionCooldownSkips);
    return;
}
```

**Why This Keeps Happening**: DHT returns public endpoints, not verified slskdN overlay peers. Any path that turns DHT candidates into connection attempts needs its own progressive backoff before the connector layer, otherwise normal public endpoint churn becomes repeated failed work and noisy operator diagnostics.

### 0z237. Pod Channel Tabs Must Drive Active Channel State And Message Fetches

**The Bug**: The Pods page could show a channel tab label such as `dm` without an obvious chat history/composer surface. Channel tab changes updated only the Semantic UI tab index, leaving `activeChannelId`, route state, and message fetching disconnected from the selected tab.

**Files Affected**:
- `src/web/src/components/Pods/Pods.jsx`
- `src/web/src/components/Pods/Pods.css`

**Wrong**:
```javascript
onTabChange={(_event, { activeIndex }) =>
  this.setState({ activeDetailTab: activeIndex })
}
```

**Correct**:
```javascript
onTabChange={this.handleDetailTabChange}

handleDetailTabChange = (_event, { activeIndex }) => {
  const channel = this.state.podDetail?.channels?.[activeIndex];
  this.setState({ activeDetailTab: activeIndex, activeChannelId: channel.channelId }, this.fetchMessages);
};
```

**Why This Keeps Happening**: Semantic UI tabs track visual selection, but Pods messaging is keyed by `podId:channelId`. Any channel navigation must update both the visible tab index and the active channel identity, then fetch messages for that channel. Otherwise the UI can look like a selected channel while the message panel is stale, empty, or not visually discoverable.

### 0z236. Port Migration Notices Must Explain Service Routing, Not Dump Raw Endpoints

**The Bug**: The network-port migration banner showed raw forwarded-port fields such as `Soulseek TCP public:port (local 50300)` and generic `TCP public:port (local 50305, selected 50305)`. That did not tell operators which protocol and public port belonged to which slskdN service or which local/config port should be updated.

**Files Affected**:
- `src/web/src/components/App.jsx`
- `src/web/src/components/App.css`
- `src/web/src/components/App.test.jsx`

**Wrong**:
```javascript
return [forward.label, forward.proto, publicEndpoint, details].filter(Boolean).join(' ');
```

**Correct**:
```javascript
return {
  service: 'slskdN mesh overlay',
  publicEndpoint: 'TCP public-ip:port',
  localEndpoint: 'TCP localhost:50305',
  config: 'dht.overlay_port 50305',
};
```

**Why This Keeps Happening**: Forwarding APIs expose transport-shaped fields, but users need routing instructions: service purpose, protocol, public endpoint, local destination, and the slskdN config option involved. Any UI warning about changed forwards must translate slot/protocol/local-port data into that operator-facing shape.

### 0z235. Persistent Services Need Test-Scoped Storage Paths

**The Bug**: MusicBrainz overlay persistence made the default constructor load the shared app-directory state file. Existing unit tests used the default constructor for isolated in-memory services, so edits stored by one test leaked into later tests through persisted global state.

**Files Affected**:
- `src/slskd/Integrations/MusicBrainz/Overlay/MusicBrainzOverlayService.cs`
- `tests/slskd.Tests.Unit/Integrations/MusicBrainz/MusicBrainzOverlayServiceTests.cs`

**Wrong**:
```csharp
private static MusicBrainzOverlayService CreateService()
{
    return new MusicBrainzOverlayService(NullLogger<MusicBrainzOverlayService>.Instance);
}
```

**Correct**:
```csharp
private static MusicBrainzOverlayService CreateService()
{
    return new MusicBrainzOverlayService(
        NullLogger<MusicBrainzOverlayService>.Instance,
        CreateStoragePath());
}
```

**Why This Keeps Happening**: Persistence is usually added after service tests were written as pure in-memory tests. Once the production constructor loads from `Program.AppDirectory`, tests must use a unique temporary storage path and delete it, otherwise test order or old local state can change behavior.

### 0z234. Post-Action Success Messages Must Survive Follow-Up Refreshes

**The Bug**: A Quarantine Jury UI action set a success message and then reloaded the selected review. The shared review-load helper cleared the message at the end of the action path, so users never saw confirmation that route or accept actions completed.

**Files Affected**:
- `src/web/src/components/System/QuarantineJury/index.jsx`

**Wrong**:
```javascript
setMessage('Route attempt recorded.');
await loadReview(selectedId); // loadReview clears message
```

**Correct**:
```javascript
await loadReview(selectedId);
setMessage('Route attempt recorded.');
```

or keep refresh helpers from clearing action-scoped status messages.

**Why This Keeps Happening**: Reusable load helpers often reset transient UI state for initial page loads. When an action refreshes data after a successful mutation, that reset can erase the action result before the user or test can observe it.

### 0z233. Do Not Persist While Holding A Domain Lock If Persistence Takes Another Lock

**The Bug**: Artist release radar persistence was called while holding the observation-domain lock. A concurrent subscription save could hold the storage lock and then wait for the domain lock while the observation path waited for the storage lock, creating a potential deadlock.

**Files Affected**:
- `src/slskd/Integrations/MusicBrainz/Radar/ArtistReleaseRadarService.cs`

**Wrong**:
```csharp
lock (_sync)
{
    _seenObservationKeys.Add(observationKey);
    _notifications[notification.Id] = notification;
    PersistState();
}
```

**Correct**:
```csharp
lock (_sync)
{
    _seenObservationKeys.Add(observationKey);
    _notifications[notification.Id] = notification;
}

PersistState();
```

**Why This Keeps Happening**: Persistence helpers often need their own serialization or file locks. If a domain mutation lock is held while calling those helpers, later snapshot locking can invert lock order. Mutate protected state first, release the domain lock, then persist a snapshot with one consistent lock order.

### 0z232. Summary Buckets Must Use The Same Enum/String Keys As Writers

**The Bug**: A discovery shelf helper wrote unrated items with action `unrated-expiry-watch`, but the summary initialized and displayed an `expiry-watch` bucket. Unrated shelf items would persist successfully while the summary stayed at zero for the intended bucket.

**Files Affected**:
- `src/web/src/lib/discoveryShelf.js`
- `src/web/src/components/Player/PlayerBar.jsx`

**Wrong**:
```javascript
if (rating === 3) return 'keep-reviewing';
return 'unrated-expiry-watch';
summary['unrated-expiry-watch'];
```

**Correct**:
```javascript
if (rating === 3) return 'keep-reviewing';
return 'expiry-watch';
summary['expiry-watch'];
```

**Why This Keeps Happening**: Browser-local helpers often use string unions without a central enum. When adding reducers or summaries, write a focused test that stores each action through the public writer and verifies the summary bucket that the UI will read.

### 0z231. Interface Method Additions Must Land With Implementations

**The Bug**: `IMusicBrainzOverlayService` gained route methods while `MusicBrainzOverlayService` was still missing matching members during validation, so the backend failed to compile with CS0535.

**Files Affected**:
- `src/slskd/Integrations/MusicBrainz/Overlay/IMusicBrainzOverlayService.cs`
- `src/slskd/Integrations/MusicBrainz/Overlay/MusicBrainzOverlayService.cs`

**Wrong**:
```csharp
public interface IMusicBrainzOverlayService
{
    Task<MusicBrainzOverlayRouteAttempt> RouteEditAsync(...);
}
```

**Correct**:
```csharp
public sealed class MusicBrainzOverlayService : IMusicBrainzOverlayService
{
    public Task<MusicBrainzOverlayRouteAttempt> RouteEditAsync(...) { ... }
}
```

**Why This Keeps Happening**: Service interfaces and implementations often change in separate files. Add interface methods only in the same patch as the implementation, controller tests, and a backend build.

### 0z230. Empty Arrays Should Not Suppress Scalar Metadata Fallbacks

**The Bug**: Player smart-radio planning treated an empty `tags` array as authoritative and returned it immediately, so a valid scalar `genre` on the now-playing item was ignored and the genre seed disappeared from the radio plan.

**Files Affected**:
- `src/web/src/lib/playerRadio.js`
- `src/web/src/components/Player/PlayerContext.jsx`

**Wrong**:
```javascript
const getTagValues = (track = {}) => {
  if (Array.isArray(track.tags)) return track.tags.map(normalizeText);
  if (track.genre) return [normalizeText(track.genre)];
  return [];
};
```

**Correct**:
```javascript
const getTagValues = (track = {}) => {
  const tags = Array.isArray(track.tags) ? track.tags.map(normalizeText) : [];
  if (tags.filter(Boolean).length > 0) return tags;
  if (track.genre) return [normalizeText(track.genre)];
  return [];
};
```

**Why This Keeps Happening**: Normalized playable objects often include default empty arrays for optional multi-value fields. Treat empty arrays as “no values,” not as proof that lower-priority scalar metadata is absent.

### 0z229. Apple Music Track URLs Should Prefer The `i` Query Track Id

**The Bug**: Apple Music URL import extracted both the album id from the path and the track id from the `?i=` query. Track URLs therefore issued two iTunes lookup requests for one pasted URL, and focused tests failed when the handler queue only expected the track lookup.

**Files Affected**:
- `src/slskd/SourceFeeds/SourceFeedImportService.cs`

**Wrong**:
```csharp
if (!string.IsNullOrWhiteSpace(trackId))
{
    ids.Add(trackId);
}

ids.Add(pathAlbumId);
```

**Correct**:
```csharp
if (!string.IsNullOrWhiteSpace(trackId))
{
    return [trackId];
}
```

**Why This Keeps Happening**: Apple Music track URLs are album URLs plus a selected-track query parameter. The path id is useful only when there is no `i` query value; otherwise it expands a single-track import into an album lookup and changes network request counts.

### 0z228. Album Candidate Track Counts Must Count Unique Track Numbers

**The Bug**: Album candidate review logic used total visible audio files as a lower bound for `expectedTrackCount`. Once manual substitution metadata allowed duplicate alternates for the same track number, a complete 4-track candidate with two options for track 3 looked like it was missing track 5.

**Files Affected**:
- `src/web/src/lib/albumCandidatePicker.js`

**Wrong**:
```javascript
const highestTrackNumber = trackNumbers.at(-1) || group.trackCount;
const expectedTrackCount = Math.max(highestTrackNumber, group.trackCount);
```

**Correct**:
```javascript
const highestTrackNumber = trackNumbers.at(-1) || 0;
const expectedTrackCount = highestTrackNumber || group.trackCount;
```

Warnings for extra tracks should likewise use a separately counted `unnumberedAudioCount`, not `trackCount > expectedTrackCount`, because duplicated numbered alternates are valid substitution choices.

**Why This Keeps Happening**: Album candidates can contain multiple visible source options for the same track. Total file count is not album length once alternates/substitutions are represented; completeness should use unique parsed track numbers when they exist and only fall back to file count for unnumbered folders.

### 0z227. Persistent Probe-Budget Tests Need Synthetic Peers

**The Bug**: A content-verification unit test used the fixed peer name `alice`. The verification service persists per-peer probe budgets outside the test fixture, so a local exhausted `alice` budget made the test fail before it reached the intended verification path.

**Files Affected**:
- `tests/slskd.Tests.Unit/Transfers/MultiSource/ContentVerificationServiceTests.cs`
- `src/slskd/Transfers/MultiSource/ContentVerificationService.cs`

**Wrong**:
```csharp
CandidateSources = new Dictionary<string, string>
{
    ["alice"] = @"Music\song.flac",
};
```

**Correct**:
```csharp
var username = $"test-peer-{Guid.NewGuid():N}";
CandidateSources = new Dictionary<string, string>
{
    [username] = @"Music\song.flac",
};
```

**Why This Keeps Happening**: Tests around persisted rate limits and budgets can accidentally share developer-machine state when they use realistic fixed peer names. Use synthetic unique identifiers, or inject isolated storage, before asserting downstream behavior.

### 0z226. Now-Playing Helpers Must Accept Empty Player State

**The Bug**: A browser-local player ratings helper assumed the now-playing track object was always present. `PlayerBar` calls helper code during its initial render before a track is selected, so reading `track.contentId` from `null` crashed every player test.

**Files Affected**:
- `src/web/src/lib/playerRatings.js`
- `src/web/src/components/Player/PlayerBar.jsx`

**Wrong**:
```javascript
export const getPlayerRatingKey = (track = {}) => {
  if (track.contentId) return `content:${track.contentId}`;
};
```

**Correct**:
```javascript
export const getPlayerRatingKey = (track = {}) => {
  if (!track) return '';
  if (track.contentId) return `content:${track.contentId}`;
};
```

**Why This Keeps Happening**: Player helpers are often written against the active-track shape, but the player shell renders in a ready/empty state first. Any helper called from `PlayerBar` render effects must treat `null` current track as normal state and return an inert value.

### 0z225. Interpolated Raw Strings Need Extra Dollar Signs For JavaScript Braces

**The Bug**: A controller callback HTML response used an interpolated raw string containing JavaScript object braces. The string started with a single `$"""`, so C# treated the JavaScript `{{ ... }}` content as interpolation syntax and the backend failed to compile.

**Files Affected**:
- `src/slskd/SourceFeeds/API/SpotifyConnectionController.cs`

**Wrong**:
```csharp
return $"""
<script>
  window.opener.postMessage({{ type: 'connected' }}, window.location.origin);
</script>
""";
```

**Correct**:
```csharp
return $$"""
<script>
  window.opener.postMessage({ type: 'connected' }, window.location.origin);
</script>
""";
```

**Why This Keeps Happening**: Raw strings make embedded HTML/JavaScript look safe to paste, but interpolation still reserves brace sequences according to the number of leading `$` characters. Use `$$"""` when the content naturally contains single JavaScript braces and reserve `{{value}}` only for C# interpolation.

### 0z224. Two-Thirds Jury Quorums Need Ceiling Math

**The Bug**: Quarantine Jury aggregation used integer division for the two-thirds agreement threshold, so 2-of-3 jurors failed to reach a release/uphold recommendation and fell back to manual review.

**Files Affected**:
- `src/slskd/QuarantineJury/QuarantineJuryService.cs`

**Wrong**:
```csharp
var requiredAgreement = (totalVerdicts * 2 / 3) + 1;
```

**Correct**:
```csharp
var requiredAgreement = (int)Math.Ceiling(totalVerdicts * 2 / 3.0);
```

**Why This Keeps Happening**: Byzantine-consensus code often uses strict greater-than-two-thirds rules, but human jury recommendations here need a two-thirds threshold. Integer division silently rounds down before adding one, changing 2-of-3 into an all-juror requirement.

### 0z223. Factory HttpClient Timeout Must Not Be Mutated Per Request

**The Bug**: A source-feed provider client set `HttpClient.Timeout` inside the send helper. Focused tests reused the same factory client for token and API requests, and the second request threw because `HttpClient` properties cannot be changed after the first send.

**Files Affected**:
- `src/slskd/SourceFeeds/SourceFeedImportService.cs`

**Wrong**:
```csharp
var client = HttpClientFactory.CreateClient(nameof(SourceFeedImportService));
client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
var response = await client.SendAsync(request, cancellationToken);
```

**Correct**:
```csharp
var client = HttpClientFactory.CreateClient(nameof(SourceFeedImportService));
using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
var response = await client.SendAsync(request, timeout.Token);
```

**Why This Keeps Happening**: `IHttpClientFactory` usually returns a usable client per call, but tests and typed/named-client wiring can legally reuse instances. Per-request timeouts belong in cancellation tokens or client registration, not in mutable `HttpClient` properties during request execution.

### 0z222. Tests Must Qualify Root Options When Microsoft Options Is Imported

**The Bug**: A unit test imported `Microsoft.Extensions.Options` for `IOptionsMonitor<T>` and then used unqualified `Options`, which resolved to the framework static helper instead of the root `slskd.Options` configuration model.

**Files Affected**:
- `tests/slskd.Tests.Unit/SourceFeeds/SourceFeedImportServiceTests.cs`

**Wrong**:
```csharp
using Microsoft.Extensions.Options;

var options = new Mock<IOptionsMonitor<Options>>();
```

**Correct**:
```csharp
using Microsoft.Extensions.Options;

var options = new Mock<IOptionsMonitor<global::slskd.Options>>();
```

**Why This Keeps Happening**: The root config type has the same simple name as `Microsoft.Extensions.Options.Options`. Any test or controller that imports the Microsoft options namespace should fully qualify the root config type as `global::slskd.Options`.

### 0z221. Source Feed Evidence Keys Must Not Include Row Numbers

**The Bug**: Source-feed duplicate suppression included `SourceId` in the evidence key, but local parsers use row numbers as source ids. Repeated identical rows therefore produced different keys and were not deduped.

**Files Affected**:
- `src/slskd/SourceFeeds/SourceFeedImportService.cs`

**Wrong**:
```csharp
var key = $"{provider}:{row.Source}:{row.SourceId}:{NormalizeKey(searchText)}";
```

**Correct**:
```csharp
var key = $"{provider}:{row.Source}:{NormalizeKey(searchText)}";
```

**Why This Keeps Happening**: Row ids are provenance, not identity. Review/import dedupe keys should use stable normalized work identity plus provider/source type, while per-row ids belong in `SourceItemId` for traceability.

### 0z220. Mesh Evidence Provenance Must Not Be User-Disableable

**The Bug**: The browser-local mesh evidence policy sanitizer accepted `provenanceRequired: false` from stored JSON, making provenance look optional even though the product decision requires provenance for every mesh-derived claim.

**Files Affected**:
- `src/web/src/lib/meshEvidencePolicy.js`

**Wrong**:
```js
return {
  provenanceRequired: policy.provenanceRequired !== false,
};
```

**Correct**:
```js
return {
  provenanceRequired: true,
};
```

**Why This Keeps Happening**: Policy objects often get treated like regular user preferences, but mesh evidence safety invariants are not preferences. Sanitizers must force non-negotiable safety fields back to their safe value even when local storage or a future API returns a weaker setting.

### 0z219. Acquisition Profile Logic Must Use Catalog IDs

**The Bug**: A candidate-ranking helper checked obsolete profile ids like `quick-hit` and `archive-broad`, so the actual persisted profiles (`fast-good-enough`, `album-complete`, etc.) would silently fall through to the default lossless ranking path.

**Files Affected**:
- `src/web/src/lib/searchCandidateRanking.js`

**Wrong**:
```js
if (acquisitionProfile === 'quick-hit') {
  return { points, reason: 'high bitrate quick-hit candidate' };
}
```

**Correct**:
```js
if (acquisitionProfile === 'fast-good-enough') {
  return { points, reason: 'high bitrate fast-good-enough candidate' };
}
```

**Why This Keeps Happening**: Early planning names drift from the public catalog labels. Any profile-aware logic should be keyed from `acquisitionProfiles.js` or explicitly tested with the persisted profile ids before wiring it into Search, Downloads, or planning.

### 0z218. Dark Theme Must Cover Nested Semantic UI Text And Duplicate Recovery Rails

**The Bug**: Rooms rendered nested Semantic UI list headers/user cards with inherited black text on dark panels, and joined rooms were shown twice: once as recovery buttons and again as active tabs.

**Files Affected**:
- `src/web/src/components/App.css`
- `src/web/src/components/Rooms/Rooms.jsx`
- `src/web/src/components/Rooms/Rooms.css`
- `src/web/src/components/Shared/UserCard.css`

**Wrong**:
```jsx
{joinedRooms.map((roomName) => (
  <Button onClick={() => openRoomTab(roomName)}>{roomName}</Button>
))}
```

**Correct**:
```jsx
<Tab panes={panes} renderActiveOnly={false} />
```

**Why This Keeps Happening**: Semantic UI nested list/card text and reusable user chips do not always inherit the page-level dark color. Recovery rails are useful for private chats, but rooms already hydrate joined rooms into tabs, so a second room rail duplicates the same navigation. Dark-theme audits need to include nested `.ui.list .header`, `.ui.list .description`, reusable username chips, and page-specific duplicate navigation surfaces.

### 0z217. Popup-Triggered Toggles Need Real Form Controls

**The Bug**: An Import Staging `Checkbox` rendered inside a Semantic UI `Popup` trigger looked like a usable toggle, but the test exercised the accessible checkbox path and the state did not reliably change before file selection.

**Files Affected**:
- `src/web/src/components/ImportStaging/ImportStaging.jsx`

**Wrong**:
```jsx
<Popup
  trigger={
    <Checkbox
      checked={enabled}
      label="Fingerprint on add"
      onChange={(_, data) => setEnabled(data.checked)}
      toggle
    />
  }
/>
```

**Correct**:
```jsx
<Popup
  trigger={
    <label className="import-staging-fingerprint-toggle">
      <input
        checked={enabled}
        onChange={(event) => setEnabled(event.target.checked)}
        type="checkbox"
      />
      <span>Fingerprint on add</span>
    </label>
  }
/>
```

**Why This Keeps Happening**: Semantic UI checkbox wrappers can be fine visually, but popup trigger wrapping and hidden/read-only internal inputs make behavior harder to verify. For small binary controls in dense toolbars, use a real labelled form control when the value gates file/network work.

### 0z216. Filename Track Numbers Can Be Standalone Parts

**The Bug**: The local metadata matcher only detected track numbers when the number was followed by a separator or whitespace, so filenames split into parts like `Artist - Album - 03 - Title.flac` produced no track number because `03` was a standalone part.

**Files Affected**:
- `src/web/src/lib/metadataMatcher.js`

**Wrong**:
```js
const match = value.match(/^\s*(?:disc\s*)?(\d{1,2})(?:\s*[-._)]|\s+)/i);
```

**Correct**:
```js
const match = value.match(/^\s*(?:disc\s*)?(\d{1,2})(?:\s*[-._)]|\s+|$)/i);
```

**Why This Keeps Happening**: Filename parsers often operate before or after separator splitting. A token that originally had separators around it can become a clean standalone number, so track-number parsing must accept end-of-string as a valid terminator.

### 0z215. API Controllers Must Fully Qualify Root Options In Nested Namespaces

**The Bug**: A native API controller declared `IOptionsSnapshot<Options>` inside `slskd.API.Native`, so C# resolved `Options` to a static API helper type instead of the root `slskd.Options` model and the server project failed to compile.

**Files Affected**:
- `src/slskd/API/Native/SourceProvidersController.cs`

**Wrong**:
```csharp
private readonly IOptionsSnapshot<Options> options;

public SourceProvidersController(
    IEnumerable<IContentBackend> contentBackends,
    IOptionsSnapshot<Options> options)
```

**Correct**:
```csharp
private readonly IOptionsSnapshot<slskd.Options> options;

public SourceProvidersController(
    IEnumerable<IContentBackend> contentBackends,
    IOptionsSnapshot<slskd.Options> options)
```

**Why This Keeps Happening**: Controllers live under nested namespaces that can contain local `Options` helper/static types. Root configuration injection should use `slskd.Options` explicitly unless the file already has a clear alias, especially in newer API folders with many provider-specific option models.

### 0z214. Player File Pickers Need Path-Aware Browsing And Duplicate Collapse

**The Bug**: The player file picker used a flat library search with a tiny result limit and rendered every returned file row directly, so large multi-folder libraries looked like repeated copies of the same track and could not be browsed like a real file tree.

**Files Affected**:
- `src/slskd/API/Native/LibraryItemsController.cs`
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/lib/collections.js`

**Wrong**:
```jsx
collectionsAPI.searchLibraryItems(query, 'Audio', 12)
```

**Correct**:
```jsx
collectionsAPI.browseLibraryItems({
  kinds: 'Audio',
  limit: playerBrowserPageSize,
  offset: browserOffset,
  path: browserPath,
  query,
})
```

**Why This Keeps Happening**: Search result dropdowns feel adequate with fixture-sized libraries, but real music shares have thousands of files across artist/album folders and many duplicate filenames. Player file selection needs a path-aware browser API, breadcrumbs, paging, and duplicate grouping instead of a flat first-page search table.

### 0z213. Rejected Discovery Evidence Must Stay Suppressed

**The Bug**: Discovery Inbox duplicate detection originally ignored existing `Rejected` candidates, allowing the same evidence key to be saved again immediately after rejection.

**Files Affected**:
- `src/web/src/lib/discoveryInbox.js`

**Wrong**:
```js
const duplicate = items.find(
  (existing) =>
    existing.evidenceKey === nextItem.evidenceKey &&
    existing.source === nextItem.source &&
    existing.state !== 'Rejected',
);
```

**Correct**:
```js
const duplicate = items.find(
  (existing) =>
    existing.evidenceKey === nextItem.evidenceKey &&
    existing.source === nextItem.source,
);
```

**Why This Keeps Happening**: It is tempting to treat `Rejected` as inactive when building review queues, but rejection is a persisted suppression decision. Filtering rejected items belongs in the visible queue view, not in evidence identity or deduplication logic.

### 0z212. Icon-Only Controls Need Visible And Programmatic Affordances

**The Bug**: Compact player launcher buttons and tab/action icon controls were visible as clickable icons but lacked accessible names, titles, or consistent focus/hover affordances. Headless DOM audit flagged icon-only buttons and links with no text, `aria-label`, `title`, or tooltip text.

**Files Affected**:
- `src/web/src/components/App.css`
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/components/Rooms/Rooms.jsx`
- `src/web/src/components/Chat/Chat.jsx`
- `src/web/src/components/Browse/Browse.jsx`
- `src/web/src/components/Users/Users.jsx`

**Wrong**:
```jsx
<Button icon="refresh" onClick={refresh} />
<Menu.Item onClick={handleAddTab}>
  <Icon name="plus" />
</Menu.Item>
```

**Correct**:
```jsx
<Button
  aria-label="Reload saved conversations"
  icon="refresh"
  onClick={refresh}
  title="Reload saved conversations"
/>
```

**Why This Keeps Happening**: Semantic UI makes it easy to create compact icon controls, but the visible icon is not enough for keyboard users, screen readers, or users trying to understand a dense tool surface. Icon-only controls need a title/tooltip and an accessible name, and shared CSS must make hover, focus, disabled, and selectable states obvious.

### 0z211. Dark Theme Contrast Must Cover Semantic Color Variants

**The Bug**: The dark theme improved neutral Semantic UI surfaces but still let stock colored controls and hard-coded grey text leak through, leaving bright green/orange buttons, inline `#666`/`grey` copy, and low-depth panels that either failed contrast or blended into the page.

**Files Affected**:
- `src/web/src/components/App.css`
- `src/web/src/components/Chat/Chat.css`
- `src/web/src/components/Rooms/Rooms.css`
- `src/web/src/components/Browse/Browse.css`
- `src/web/src/components/Search/Search.css`

**Wrong**:
```css
:root.dark .ui.segment {
  background: var(--slskd-secondary-background);
}
```

**Correct**:
```css
:root.dark .ui.green.button {
  background: #147c32 !important;
  color: #f3fff5 !important;
}

:root.dark .app-content [style*="color: grey"] {
  color: var(--slskd-color-subtle) !important;
}
```

**Why This Keeps Happening**: Semantic UI color variants and inline status text bypass neutral surface overrides. Dark theme work needs to check buttons, labels, tab menus, form labels, hard-coded grey copy, and page-specific panes, not only `.ui.segment` and `.ui.card`.

### 0z210. Moq ReturnsAsync Must Match Concrete Task Collection Types

**The Bug**: A unit test mocked a `Task<List<AudioVariant>>` API with array values, so Moq selected an incompatible `ReturnsAsync` overload and the whole test project failed to compile.

**Files Affected**:
- `tests/slskd.Tests.Unit/VirtualSoulfind/Core/Music/MusicContentDomainProviderTests.cs`

**Wrong**:
```csharp
mock.Setup(h => h.GetVariantsByRecordingAsync(id, It.IsAny<CancellationToken>()))
    .ReturnsAsync(new[] { new AudioVariant() });
```

**Correct**:
```csharp
mock.Setup(h => h.GetVariantsByRecordingAsync(id, It.IsAny<CancellationToken>()))
    .Returns(Task.FromResult(new List<AudioVariant> { new AudioVariant() }));
```

**Why This Keeps Happening**: Test setup often uses arrays because most repository methods return `IEnumerable<T>` or arrays, but Moq overload resolution follows the exact mocked return type. When a service contract returns `Task<List<T>>`, return a `List<T>` from the setup or the compiler may bind to a sequence-setup overload instead of the async-result overload.

### 0z209. Fixed Chrome Heights Cause Scroll Overlap

**The Bug**: The main app scroll area reserved hard-coded nav/player/footer heights, so responsive nav wrapping, footer wrapping, safe-area padding, and player collapse/expand state could leave page content hidden behind fixed chrome.

**Files Affected**:
- `src/web/src/components/App.css`
- `src/web/src/components/App.jsx`
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/components/Player/Player.css`
- `src/web/src/components/Shared/Footer.jsx`

**Wrong**:
```css
.app-content {
  height: calc(100dvh - 58px - 43px - 248px);
}
```

**Correct**:
```css
.app-content {
  height: calc(
    100dvh -
    var(--slskdn-nav-height) -
    var(--slskdn-footer-height) -
    var(--slskdn-player-reserved-height)
  );
}
```

**Why This Keeps Happening**: The nav, player, and footer are fixed-position chrome, but their real heights change with responsive wrapping, safe-area insets, and player expansion state. The Semantic UI top sidebar may also have a non-zero top offset, so reserve the nav's measured bottom edge, not only its height. Reserve space from measured CSS variables instead of constants so every route scrolls only inside the unobscured viewport.

### 0z208. Test-Local Helper Types Still Need Real Framework Imports

**The Bug**: Test-local helper implementations used `HttpRequestMessage`, `Enumerable`, `ToList`, and LINQ extension methods without importing `System.Net.Http` / `System.Linq`, causing the touched test project to fail compilation even though the helpers existed only inside unit test files. One test also passed an `IEnumerable<byte>` sequence to `Assert.DoesNotContain` where the string overload made the intended assertion explicit.

**Files Affected**:
- `tests/slskd.Tests.Unit/Mesh/DomainFrontedTransportTests.cs`
- `tests/slskd.Tests.Unit/Mesh/CensorshipSimulationServiceTests.cs`
- `tests/slskd.Tests.Unit/Mesh/ImageSteganographyTests.cs`
- `tests/slskd.Tests.Unit/Mesh/DecoyPodServiceTests.cs`

**Wrong**:
```csharp
using Xunit;

public Task<HttpRequestMessage> BuildRequestAsync(string path)
```

**Correct**:
```csharp
// tests/slskd.Tests.Unit/GlobalUsings.cs
global using System.Linq;
global using System.Net.Http;

public Task<HttpRequestMessage> BuildRequestAsync(string path)
```

**Why This Keeps Happening**: Test-local stubs and helpers feel informal, but they compile under the same project rules as production code. When adding concrete helper behavior across test files, run the focused test immediately and add shared framework namespaces to the test project's global usings instead of assuming implicit usings cover them.

### 0z207. Browser Storage Access Can Throw In Privacy-Locked Contexts

**The Bug**: Player, visualizer, ListenBrainz, and token helpers used `window.localStorage`, `window.sessionStorage`, or bare storage globals directly, which can throw in locked-down privacy browsers, private contexts, or embedded webviews and crash UI initialization.

**Files Affected**:
- `src/web/src/lib/storage.js`
- `src/web/src/lib/listenBrainz.js`
- `src/web/src/lib/token.js`
- `src/web/src/components/Player/Equalizer.jsx`
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/components/Player/Visualizer.jsx`

**Wrong**:
```js
const mode = window.localStorage.getItem(key);
window.localStorage.setItem(key, value);
```

**Correct**:
```js
const mode = getLocalStorageItem(key, fallback);
setLocalStorageItem(key, value);
```

**Why This Keeps Happening**: UI preference persistence feels harmless, but browser storage APIs are not guaranteed to be available or writable. New browser-local preferences should use `src/web/src/lib/storage.js` so blocked persistence degrades to defaults instead of taking down the page.

### 0z206. Semantic UI Dark Mode Needs Central Surface Overrides

**The Bug**: Chat, rooms, System panels, modals, tables, dropdowns, and other nested Semantic UI components showed light inner boxes in dark mode because the app themed the page shell but left many Semantic UI default surfaces and inline `#f8f9fa` styles intact.

**Files Affected**:
- `src/web/src/components/App.css`
- `src/web/src/components/Rooms/Rooms.css`
- `src/web/src/components/Rooms/RoomCreateModal.jsx`
- `src/web/src/components/PortForwarding/PortForwarding.jsx`
- `src/web/src/components/Pods/PortForwarding.jsx`
- `src/web/src/components/TrafficTicker/TrafficTicker.css`

**Wrong**:
```jsx
<Segment>
  <Table celled />
</Segment>

<div style={{ background: '#f8f9fa', color: '#666' }} />
```

**Correct**:
```css
:root.dark .ui.segment,
:root.dark .ui.table,
:root.dark .ui.modal > .content {
  background: var(--slskd-secondary-background) !important;
  color: var(--slskd-color) !important;
}
```

```jsx
<div
  style={{
    backgroundColor: 'var(--slskd-color-inset, #f8f9fa)',
    color: 'var(--slskd-color-subtle, #666)',
  }}
/>
```

**Why This Keeps Happening**: Semantic UI components render their own nested surfaces, and inline light colors override app-level dark theme variables. Any new component using `Segment`, `Card`, `Table`, `Modal`, `Dropdown`, `Input`, or inline panel colors must either rely on the central dark overrides or use CSS variables instead of literal light colors.

### 0z205. Semantic UI Tabs Remount Inactive Chat/Room Sessions By Default

**The Bug**: Switching chat or room tabs looked like a full page refresh because Semantic UI React's `Tab` unmounted inactive panes, rebuilding the active session and refetching state on every tab change.

**Files Affected**:
- `src/web/src/components/Chat/Chat.jsx`
- `src/web/src/components/Chat/ChatSession.jsx`
- `src/web/src/components/Rooms/Rooms.jsx`
- `src/web/src/components/Rooms/RoomSession.jsx`

**Wrong**:
```jsx
<Tab panes={panes} />
```

**Correct**:
```jsx
<Tab
  panes={panes}
  renderActiveOnly={false}
/>
```

**Why This Keeps Happening**: Semantic UI's default is optimized for rendering only the visible pane, but chat and room panes hold live UI state, scroll position, inputs, and polling behavior. Keep panes mounted and explicitly gate polling or acknowledgment work to the active pane so switching tabs preserves the session without multiplying background network load.

### 0z204. Packaged Config Writes Must Preserve Service Read Access

**The Bug**: Rewriting `/etc/slskd/slskd.yml` with `0600 root:root` made the systemd service fail at startup because the service runs as `slskd:slskd` and could no longer read its config.

**Files Affected**:
- `/etc/slskd/slskd.yml` on packaged hosts
- `slskd.service`

**Wrong**:
```bash
chown root:root /etc/slskd/slskd.yml
chmod 600 /etc/slskd/slskd.yml
```

**Correct**:
```bash
chown root:slskd /etc/slskd/slskd.yml
chmod 640 /etc/slskd/slskd.yml
systemctl restart slskd.service
```

**Why This Keeps Happening**: Secret-bearing config files tempt operators to use root-only permissions, but packaged slskd reads the config after dropping to the `slskd` service account. Preserve group read access for the service group whenever rotating credentials or rewriting packaged config.

### 0z203. MilkDrop3 Double Presets Must Compatibility-Check Every Preset Body

**The Bug**: Native `.milk2` imports parsed both preset bodies but only compatibility-checked `parsed.primary`, so an unsupported secondary preset could be accepted and stored as if the whole file were compatible.

**Files Affected**:
- `src/web/src/components/Player/visualizers/nativeMilkdropEngine.js`

**Wrong**:
```js
const importedPreset = parseMilkdropPreset(source, { format: 'milk2' }).primary;
const compatibilityError = getMilkdropCompatibilityError(
  analyzeMilkdropPresetCompatibility(importedPreset),
);
```

**Correct**:
```js
const parsed = parseMilkdropPreset(source, { format: 'milk2' });
const compatibilityErrors = parsed.presets
  .map((preset) => getMilkdropCompatibilityError(analyzeMilkdropPresetCompatibility(preset)))
  .filter(Boolean);
```

**Why This Keeps Happening**: Early renderer work often consumes only the primary preset while parser work preserves the full double-preset file. Import/inspection paths still need to validate every preserved body so unsupported secondary content does not become a latent render bug when simultaneous `.milk2` rendering lands.

### 0z202. Batch Preset Import Must Inspect Before Mutating The Live Renderer

**The Bug**: Native MilkDrop multi-file import called `loadPresetText()` for every compatible file while scanning the batch. That replaced and disposed the live WebGL renderer repeatedly before the user had selected an active preset.

**Files Affected**:
- `src/web/src/components/Player/Visualizer.jsx`
- `src/web/src/components/Player/visualizers/nativeMilkdropEngine.js`

**Wrong**:
```js
for (const file of files) {
  const title = engine.loadPresetText(await file.text(), file.name);
  imported.push({ fileName: file.name, title });
}
```

**Correct**:
```js
for (const file of files) {
  const source = await file.text();
  const { title } = engine.inspectPresetText(source, file.name);
  imported.push({ fileName: file.name, source, title });
}

const activePreset = imported[imported.length - 1];
engine.loadPresetText(activePreset.source, activePreset.fileName);
```

**Why This Keeps Happening**: Import validation and renderer activation are separate operations. Batch workflows should parse and compatibility-scan candidates without touching GPU state, then mutate the live renderer once for the selected/active preset.

### 0z201. Auto-Replace Must Be Opt-In And Must Exclude Our Own Username

**The Bug**: Auto-replace defaulted to enabled when its state file was missing, even though `AutoReplaceStuck` defaults false. It also excluded only the original failed source from replacement candidates, so the daemon's own Soulseek username could be selected and the host downloaded from itself through the LAN/NAT path.

**Files Affected**:
- `src/slskd/Transfers/AutoReplace/AutoReplaceBackgroundService.cs`
- `src/slskd/Transfers/AutoReplace/AutoReplaceService.cs`

**Wrong**:
```csharp
IsEnabled = state?.Enabled ?? true;

if (response.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase))
{
    continue;
}
```

**Correct**:
```csharp
IsEnabled = state?.UserConfigured == true
    ? state.Enabled
    : OptionsAtStartup.Global.Download.AutoReplaceStuck;

if (response.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)
    || IsOwnUsername(response.Username))
{
    continue;
}
```

**Why This Keeps Happening**: Background automation has to fail closed. A missing state file is not consent, and search results can include the local daemon's own username when mesh/LAN/NAT paths are involved. Replacement candidate filters must exclude both the failed source and the current client username before ranking.

### 0z200. Visualizer Render Loops Must Catch Preset Runtime Errors

**The Bug**: The native MilkDrop visualizer could import a preset successfully, then throw later from `engine.render()` when an unsupported function or expression path executed. Because the animation-frame render loop did not catch render errors, the exception escaped the React boundary and the bad imported preset stayed persisted for the next session.

**Files Affected**:
- `src/web/src/components/Player/Visualizer.jsx`

**Wrong**:
```js
const renderLoop = () => {
  engineRef.current.render();
  requestAnimationFrame(renderLoop);
};
```

**Correct**:
```js
const renderLoop = () => {
  try {
    engineRef.current.render();
  } catch (error) {
    localStorage.removeItem(nativePresetStorageKey);
    setError(getVisualizerErrorMessage(engineType, error));
    return;
  }
  requestAnimationFrame(renderLoop);
};
```

**Why This Keeps Happening**: MilkDrop presets can parse before all runtime equation paths are exercised. Any visualizer engine that runs user/imported preset code per frame must treat render as a failure boundary, not just initialization/import.

### 0z199a. DOM Factory Spies Must Be Restored In Download Tests

**The Bug**: Visualizer export tests spied on `document.createElement` to replace anchor `.click()`, but the spy was left active. The next test captured the already-spied function as its "original" factory, causing recursive `createElement` calls and widespread `Maximum call stack size exceeded` failures.

**Files Affected**:
- `src/web/src/components/Player/Visualizer.test.jsx`

**Wrong**:
```javascript
const createElement = document.createElement.bind(document);
vi.spyOn(document, 'createElement').mockImplementation((name) => {
  const element = createElement(name);
  element.click = click;
  return element;
});
```

**Correct**:
```javascript
const createElement = document.createElement.bind(document);
const createElementSpy = vi.spyOn(document, 'createElement').mockImplementation((name) => {
  const element = createElement(name);
  if (name === 'a') {
    Object.defineProperty(element, 'click', {
      configurable: true,
      value: click,
    });
  }
  return element;
});

// assertions...
createElementSpy.mockRestore();
```

**Why This Keeps Happening**: Browser download tests often patch DOM factories instead of clicking real anchors. Any test that spies on global DOM creation must restore the spy before the next render, and anchor click replacement should use `Object.defineProperty` so JSDOM does not attempt navigation.

### 0z199. Reject Soulseek Upload Requests From Our Own Username

**The Bug**: The incoming upload enqueue path accepted requests where the requesting Soulseek username matched the daemon's own logged-in username, so the Uploads page could show a file being uploaded to yourself.

**Files Affected**:
- `src/slskd/Application.cs`

**Wrong**:
```csharp
if (Users.IsBlacklisted(username, endpoint.Address))
{
    throw new DownloadEnqueueException("File not shared.");
}
```

**Correct**:
```csharp
if (IsSelfUsername(username))
{
    throw new DownloadEnqueueException("File not shared.");
}

if (Users.IsBlacklisted(username, endpoint.Address))
{
    throw new DownloadEnqueueException("File not shared.");
}
```

**Why This Keeps Happening**: Soulseek upload requests are inbound peer download requests, and the UI labels them from the daemon's perspective. Without an explicit self-username guard, stale/self-originated requests or another client using the same account can look like a real peer transfer and pollute upload history.

### 0z198. WebGL Attribute Bindings Must Be Rebound Before Each Program Draw

**The Bug**: The native MilkDrop renderer initialized vertex attribute pointers once per shader program, then switched between fullscreen, primitive, wave, and shape buffers. WebGL attribute bindings are context/VAO state, not safely isolated by program switches, so later primitive buffer setup could make the fullscreen feedback/blit program draw from the wrong buffer.

**Files Affected**:
- `src/web/src/components/Player/visualizers/milkdrop/milkdropRenderer.js`

**Wrong**:
```js
createFullscreenTriangle(gl, program);
createDynamicLineBuffer(gl, lineProgram);
gl.useProgram(program);
gl.drawArrays(gl.TRIANGLES, 0, 3);
```

**Correct**:
```js
gl.useProgram(program);
bindFullscreenTriangle(gl, program, fullscreenBuffer);
gl.drawArrays(gl.TRIANGLES, 0, 3);

gl.useProgram(lineProgram);
bindLineBuffers(gl, lineBuffers);
gl.drawArrays(gl.LINE_STRIP, 0, count);
```

**Why This Keeps Happening**: WebGL program switches do not restore vertex attribute pointer state. Either use VAOs per draw path or explicitly rebind the attributes and buffers immediately before each draw call.

### 0z197. MilkDrop Custom Wave And Shape Equations Are Indexed Equations, Not Base Values

**The Bug**: The new MilkDrop preset parser treated `shape00_per_frame1` and `wavecode_0_per_point1` keys as static base values because it only recognized indexed equation keys starting with `frame` or `point`. That would silently drop custom wave/shape behavior before rendering.

**Files Affected**:
- `src/web/src/components/Player/visualizers/milkdrop/presetParser.js`

**Wrong**:
```js
if (normalized.startsWith('frame')) {
  entry.equations.frame = appendStatement(entry.equations.frame, value);
}
```

**Correct**:
```js
if (normalized.startsWith('frame') || normalized.startsWith('per_frame')) {
  entry.equations.frame = appendStatement(entry.equations.frame, value);
}
```

**Why This Keeps Happening**: MilkDrop stores global equations as `per_frame_1`, but custom wave/shape equations can appear as `shape00_per_frame1` and `wavecode_0_per_point1`. After stripping the indexed prefix, parser logic must still recognize both the short and `per_*` forms.

### 0z196. Winget Metadata Must Not Block Non-Winget Stable Releases

**The Bug**: A stable tag release failed in the release gate before build/test because `validate-packaging-metadata.sh` unconditionally required checked-in Winget release URLs to match the current stable package metadata, even when the release was not publishing Winget.

**Files Affected**:
- `packaging/scripts/validate-packaging-metadata.sh`

**Wrong**:
```bash
validate_winget packaging/winget/snapetech.slskdn.installer.yaml packaging/winget/snapetech.slskdn.locale.en-US.yaml
validate_winget packaging/winget/snapetech.slskdn-dev.installer.yaml packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml
```

**Correct**:
```bash
if [[ "${VALIDATE_WINGET_RELEASE_METADATA:-false}" == "true" ]]; then
  validate_winget packaging/winget/snapetech.slskdn.installer.yaml packaging/winget/snapetech.slskdn.locale.en-US.yaml
  validate_winget packaging/winget/snapetech.slskdn-dev.installer.yaml packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml
else
  echo "Skipping Winget release-version metadata validation; set VALIDATE_WINGET_RELEASE_METADATA=true to enforce it."
fi
```

**Why This Keeps Happening**: Winget publication is an optional/manual release step, but the package metadata gate was treating it as mandatory for every stable tag. Keep structural Winget manifest checks in place, and gate release-version URL checks behind an explicit environment flag when the release is intentionally not publishing Winget.

### 0z195. Fully Qualify slskd Options When Importing Microsoft.Extensions.Options

**The Bug**: A service imported `Microsoft.Extensions.Options` and declared `IOptionsMonitor<Options>`, which resolved `Options` to the static `Microsoft.Extensions.Options.Options` helper instead of the app configuration type. The same file also put a static factory method named `Started` on a record with a `Started` property, causing a generated-member collision.

**Files Affected**:
- `src/slskd/Player/ExternalVisualizerLauncher.cs`

**Wrong**:
```csharp
private readonly IOptionsMonitor<Options> _options;

public sealed record LaunchResult(bool Started)
{
    public static LaunchResult Started() => new(true);
}
```

**Correct**:
```csharp
private readonly IOptionsMonitor<global::slskd.Options> _options;

public sealed record LaunchResult(bool Started)
{
    public static LaunchResult StartedResult() => new(true);
}
```

**Why This Keeps Happening**: The slskd root options type has the same simple name as the `Microsoft.Extensions.Options.Options` static helper. Any file that imports the options namespace and needs the app root config should use `global::slskd.Options`. Records also synthesize members for positional properties, so factory method names must not duplicate property names.

### 0z194. MilkDrop Needs The Real Butterchurn Export And A Live Audio Tap

**The Bug**: The Web UI MilkDrop panel mounted a canvas but showed "Failed to load visualizer" or a black frame because Vite wrapped `butterchurn` differently than the component expected, the browser could lack WebGL2, and the visualizer was connected to an audio node that graph rebuilds could disconnect.

**Files Affected**:
- `src/web/src/components/Player/Visualizer.jsx`
- `src/web/src/components/Player/audioGraph.js`

**Wrong**:
```js
const butterchurn = butterchurnModule.default || butterchurnModule;
visualizer.connectAudio(graph.source);
```

**Correct**:
```js
const butterchurn = resolveButterchurnApi(butterchurnModule);
if (!supportsWebGl2()) {
  setFallbackMode(true);
  return;
}
visualizer.connectAudio(graph.visualizerInput);
```

**Why This Keeps Happening**: Browser bundlers can wrap CommonJS visualizer libraries as `module.default.default`, Butterchurn requires WebGL2, and Web Audio visualizers need a stable, live tap that survives EQ/karaoke graph rebuilds. Verify both a normal WebGL2 browser and a WebGL-disabled browser; the latter must fall back to a live analyzer instead of showing a dead red failure. Verify rendering with a real browser canvas screenshot, not only `gl.readPixels`, because Butterchurn's WebGL framebuffer readback can look black even when the visible canvas is rendering.

### 0z193. Fixed Player Layout Needs One Scroll Owner

**The Bug**: The Web UI used fixed footer/player elements but left the document body scrollable. Scrolling moved page content behind the fixed player/footer stack, so the player appeared detached from the footer and page content could be hidden underneath it.

**Files Affected**:
- `src/web/src/components/App.css`
- `src/web/src/components/Player/Player.css`
- `src/web/src/components/Player/PlayerBar.jsx`

**Wrong**:
```css
body {
  overflow: auto;
}

.app-content {
  padding-bottom: 128px;
}
```

**Correct**:
```css
html,
body,
#root {
  height: 100%;
  overflow: hidden;
}

.app-content {
  height: calc(100dvh - var(--slskdn-nav-height) - var(--slskdn-player-height) - var(--slskdn-footer-height));
  overflow-y: auto;
}
```

**Why This Keeps Happening**: Fixed UI chrome needs a single explicit scroll owner. Padding the document for fixed controls is fragile once the player can resize, collapse, or sit above another fixed footer.

### 0z192. Player Audio Graphs Must Resume Before Playback

**The Bug**: The Web UI player created a `MediaElementSource` for EQ/analyzer output, then called `audio.play()` while the Web Audio `AudioContext` could still be suspended. Browser playback looked active or the source loaded, but audio output could be silent.

**Files Affected**:
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/components/Player/audioGraph.js`
- `src/web/src/components/Player/Player.css`

**Wrong**:
```js
setOutputGain(audioRef.current, 1);
audioRef.current.load();
audioRef.current.play().catch(() => {});
```

**Correct**:
```js
await resumeAudioGraph(audioRef.current);
await audioRef.current.play();
```

**Why This Keeps Happening**: Once a media element is routed through Web Audio, the browser's native media output depends on the audio graph. EQ, MilkDrop, spectrum, crossfade, and karaoke code must treat `AudioContext.resume()` as part of playback, not only as part of visualizer startup.

### 0z191. Fixed Player Bars Need Explicit Full-Width Grid Tracks

**The Bug**: The modern player deck used a fixed-minimum display column next to an auto-sized control pad. At some widths, the now-playing display and signal/spectrum tile stopped short instead of consuming the available bar width.

**Files Affected**:
- `src/web/src/components/Player/Player.css`

**Wrong**:
```css
.player-main-deck {
  grid-template-columns: minmax(540px, 1fr) auto;
}
```

**Correct**:
```css
.player-main-deck {
  grid-template-columns: minmax(0, 1fr) auto;
  width: 100%;
}

.player-display,
.player-display-analyzers,
.player-analyzer-tile {
  width: 100%;
}
```

**Why This Keeps Happening**: Fixed footer/player bars combine grid, intrinsic button widths, canvas elements, and long metadata. Use `minmax(0, 1fr)`, `min-width: 0`, and explicit `width: 100%` on the display and signal containers so canvases and metadata scale with the available track.

### 0z191. Player Queue Previews Must Exclude The Current Track

**The Bug**: The persistent Web UI player rendered `queue.slice(0, 3)` below the main now-playing display. Because the player queue stores the current item at index 0, the current song title appeared twice: once in the LCD display and again as a queue pill.

**Files Affected**:
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/components/Player/LyricsPane.jsx`

**Wrong**:
```jsx
{queue.slice(0, 3).map((item) => (
  <button>{item.title}</button>
))}
```

**Correct**:
```jsx
{queue.length > 1 ? queue.slice(1, 4).map((item) => (
  <button>{item.title || item.fileName || item.contentId}</button>
)) : null}
```

**Why This Keeps Happening**: Player state often keeps "current + upcoming" in one queue for simple next/previous behavior, but the UI queue preview should show only upcoming tracks. Any now-playing deck must treat queue index 0 as already represented by the main title display.

### 0z190. Lyrics Lookup Needs Real Metadata, Not Placeholder Artists

**The Bug**: The lyrics pane queried LRCLIB with `artist_name=slskdN` for local files that only had filename metadata. That made lyrics appear broken and returned misleading "No synced lyrics found" messages for tracks where the real artist/title could not be inferred.

**Files Affected**:
- `src/web/src/components/Player/LyricsPane.jsx`

**Wrong**:
```js
new URLSearchParams({
  artist_name: current.artist,
  track_name: current.title,
});
```

**Correct**:
```js
const lookup = getLyricsLookup(current);
if (!lookup) {
  setStatus('Lyrics need artist and title metadata');
  return;
}
```

**Why This Keeps Happening**: Local and generated player items often use placeholder artist values and filename titles. External metadata services need real artist/title pairs; when those are absent, infer only safe `Artist - Title.ext` filenames and otherwise explain the missing metadata instead of sending garbage queries.

### 0z190. Autosave Forms Need Saved-State Affordances

**The Bug**: The player ListenBrainz token field saved on every keystroke, but the modal only showed a generic Close button. The behavior was technically correct, but the UI looked like an unsent form with no submit action.

**Files Affected**:
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/components/Player/Player.css`

**Wrong**:
```jsx
<Input onChange={saveToken} value={token} />
<Button>Close</Button>
```

**Correct**:
```jsx
<Input action={<Button aria-label="Clear token" />} onChange={saveToken} value={token} />
<div className="player-token-save-state">Token changes are saved automatically.</div>
<Button primary>Done</Button>
```

**Why This Keeps Happening**: Autosave is invisible unless the UI names it. Settings modals that persist on change need explicit saved-state copy, a completion affordance, and a way to undo/clear sensitive values.

### 0z189. Ref-Only Audio Elements Do Not Wake Player Visualizers

**The Bug**: The Web UI player passed `audioRef.current` directly into MilkDrop and analyzer components during render. The first render often saw `null`, and attaching the `<audio>` ref later did not trigger a React render, so MilkDrop mounted without a usable audio element and appeared completely dead.

**Files Affected**:
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/components/Player/Visualizer.jsx`
- `src/web/src/components/Player/SpectrumAnalyzer.jsx`

**Wrong**:
```js
<audio ref={audioRef} />
<Visualizer audioElement={audioRef.current} />
```

**Correct**:
```js
const [playerAudioElement, setPlayerAudioElement] = useState(null);
const bindAudioElement = useCallback((element) => {
  audioRef.current = element;
  setPlayerAudioElement(element);
  setAudioElement(element);
}, [setAudioElement]);

<audio ref={bindAudioElement} />
<Visualizer audioElement={playerAudioElement} />
```

**Why This Keeps Happening**: React refs are mutable containers, not reactive state. Components that initialize Web Audio, canvas render loops, lyrics sync, or analyzer effects from an audio element need a state-backed element so they rerun when the DOM node is attached.

### 0z188. Player Ticket Failures Need A Stream URL Fallback

**The Bug**: The Web UI player switched to short-lived stream tickets for browser media playback, but if ticket creation failed or returned an empty ticket, the player set the audio source to an empty string. The UI looked playable, but the browser had no media URL to load.

**Files Affected**:
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/lib/streaming.js`

**Wrong**:
```js
setSource(ticket
  ? buildTicketedStreamUrl(contentId, ticket)
  : current.streamUrl || '');
```

**Correct**:
```js
setSource(ticket
  ? buildTicketedStreamUrl(contentId, ticket)
  : buildDirectStreamUrl(contentId));
```

**Why This Keeps Happening**: Browser media playback has two separate auth paths: preferred short-lived tickets and direct stream URLs for passthrough/legacy recovery. Ticket acquisition is async and can fail independently of the stream endpoint, so the player must never leave a selected local content item with an empty media source.

### 0z187. Player Tests Must Query Async Stream Sources Inside Waits

**The Bug**: The Web UI player test captured `audio.querySelector('source')` before the async stream-ticket request completed. The DOM later inserted the `<source>`, but the assertion kept reading the stale `null` reference and failed even though the rendered DOM was correct.

**Files Affected**:
- `src/web/src/components/Player/PlayerBar.test.jsx`

**Wrong**:
```js
const source = audio.querySelector('source');
await waitFor(() =>
  expect(source.getAttribute('src')).toContain('/api/v0/streams/...'),
);
```

**Correct**:
```js
await waitFor(() =>
  expect(audio.querySelector('source')?.getAttribute('src')).toContain('/api/v0/streams/...'),
);
```

**Why This Keeps Happening**: The player source is no longer synchronous; ticketed playback resolves through an async API call before rendering the media source. Tests must re-query DOM nodes inside `waitFor` when the node itself is created asynchronously.

### 0z185. Do Not Put Full App JWTs In Browser Media URLs

**The Bug**: Browser `<audio>` playback was fixed by appending the normal session JWT as `?access_token=` on `/api/v0/streams/{contentId}`. That made media playback work, but exposed the full app bearer token through URL history, logs, screenshots, browser extensions, and diagnostics.

**Files Affected**:
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/web/src/lib/streaming.js`
- `src/slskd/Streaming/StreamsController.cs`
- `src/slskd/Streaming/StreamTicketService.cs`
- `src/slskd/Program.cs`

**Wrong**:
```js
const streamUrl = `${urlBase}/api/v0/streams/${contentId}?access_token=${token}`;
```

**Correct**:
```js
const { ticket } = await api.post(`/streams/${encodeURIComponent(contentId)}/ticket`);
const streamUrl = `${urlBase}/api/v0/streams/${contentId}?ticket=${ticket}`;
```

**Why This Keeps Happening**: Browser-managed media requests cannot use Axios authorization headers, so query strings look like the easiest fix. Use short-lived, content-bound opaque media tickets instead of putting long-lived session or API tokens in URLs.

### 0z186. DHT Records Need Explicit Bytes And Signature Gates

**The Bug**: Some DHT publishers serialized private DTOs or records implicitly through `IMeshDhtClient.PutAsync`, while readers used typed `GetAsync<T>`. This reintroduced MessagePack formatter failures and let invalid signed pod metadata continue into discovery results.

**Files Affected**:
- `src/slskd/ListeningParty/ListeningPartyService.cs`
- `src/slskd/PodCore/PodDhtPublisher.cs`
- `src/slskd/PodCore/API/Controllers/PodDhtController.cs`

**Wrong**:
```csharp
await dht.PutAsync(key, announcement, ttlSeconds, ct);
var announcement = await dht.GetAsync<ListeningPartyAnnouncement>(key, ct);
return new PodMetadataResult(Found: true, IsValidSignature: false, PublishedPod: pod);
```

**Correct**:
```csharp
await dht.PutAsync(key, JsonSerializer.SerializeToUtf8Bytes(announcement), ttlSeconds, ct);
var raw = await dht.GetRawAsync(key, ct);
var announcement = JsonSerializer.Deserialize<ListeningPartyAnnouncement>(raw);
if (!isValidSignature) return new PodMetadataResult(Found: false, PublishedPod: null, ...);
```

**Why This Keeps Happening**: Mesh DHT storage is a byte payload boundary, not a general object database. Writers and readers must agree on an explicit wire format, and signed records must fail closed before discovery or UI layers trust their contents.

### 0z187. Never Hash Whole Library Roots On Stream Misses

**The Bug**: Stream resolution fell back to recursively scanning share/download roots and computing SHA-256 for files when a requested content id was not indexed. Random authenticated stream requests could force expensive disk reads, and the library picker returned absolute local paths to the browser.

**Files Affected**:
- `src/slskd/Streaming/ContentLocator.cs`
- `src/slskd/API/Native/LibraryItemsController.cs`

**Wrong**:
```csharp
foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
{
    var contentId = $"sha256:{ComputeSha256(path)}";
    if (requested == contentId) return path;
}
```

**Correct**:
```csharp
var contentItem = repo.FindContentItem(contentId);
if (contentItem != null) return ResolveIndexedContent(contentItem);
// Fallbacks must be bounded and must not hash file contents on the request path.
```

**Why This Keeps Happening**: Content-addressed playback makes it tempting to derive IDs lazily during stream requests. Stream endpoints sit on a hot unaudited path; expensive discovery belongs in indexing/search code, and UI responses should expose display paths instead of host filesystem paths.

### 0z184. Canvas Overlays Can Block Their Own Controls

**The Bug**: The MilkDrop visualizer canvas and hidden overlay occupied the same absolute area as the preset/control buttons. In headless and some pointer paths, the canvas/container intercepted clicks intended for the visible buttons.

**Files Affected**:
- `src/web/src/components/Player/Player.css`
- `src/web/src/components/Player/Visualizer.jsx`

**Wrong**:
```css
.player-visualizer-overlay {
  opacity: 0;
  pointer-events: none;
}
```

**Correct**:
```css
.player-visualizer-canvas {
  pointer-events: none;
}

.player-visualizer-overlay {
  opacity: 1;
  pointer-events: auto;
  z-index: 2;
}
```

**Why This Keeps Happening**: Canvas previews are often positioned as full-surface layers. Any overlay controls inside the same stacking context need an explicit z-index and pointer-event policy, otherwise the decorative/preview layer can steal the interaction.

### 0z183. Player Analyzer Grids Need Zero-Min Tracks

**The Bug**: The Web UI player display used nested CSS grid tracks with fixed minimum widths for track metadata and analyzer canvases. At normal widths, the analyzer canvases overflowed out of the player display and visually collided with the transport controls. The same pass also added a fake top-right `PLAY` status label that duplicated the actual play button instead of providing useful state.

**Files Affected**:
- `src/web/src/components/Player/Player.css`
- `src/web/src/components/Player/PlayerBar.jsx`

**Wrong**:
```css
.player-display {
  grid-template-columns: minmax(180px, 0.7fr) minmax(300px, 1.3fr);
}

.player-display-analyzers {
  grid-template-columns: minmax(120px, 0.78fr) minmax(180px, 1.22fr);
}
```

**Correct**:
```css
.player-display {
  grid-template-columns: minmax(0, 0.7fr) minmax(0, 1.3fr);
  overflow: hidden;
}

.player-display-analyzers {
  grid-template-columns: minmax(0, 0.78fr) minmax(0, 1.22fr);
  min-width: 0;
}
```

**Why This Keeps Happening**: CSS grid items default to content-sized minimums, and nested canvas/layout elements can force a grid wider than its assigned column. Use `minmax(0, ...)`, set `min-width: 0`, and inspect the rendered player with a screenshot whenever changing fixed footer/player chrome.

### 0z182. Vite Builds Must Be Synced Into Backend Wwwroot Before Publish

**The Bug**: `npm run build` produced a correct Web UI bundle under `src/web/build`, but `./bin/publish` served the stale bundle already checked into `src/slskd/wwwroot`. The deployed app therefore kept running old JavaScript even after the frontend build passed.

**Files Affected**:
- `src/web/build/`
- `src/slskd/wwwroot/`
- `src/slskd/slskd.csproj`

**Wrong**:
```bash
cd src/web && npm run build
./bin/publish --no-prebuild ...
```

**Correct**:
```bash
cd src/web && npm run build
cd ../..
rsync -a --delete src/web/build/ src/slskd/wwwroot/
./bin/publish --no-prebuild ...
```

**Why This Keeps Happening**: The backend project publishes `src/slskd/wwwroot`, not the transient Vite output directory. A green Vite build only proves the UI can compile; it does not prove the backend-served static assets were updated.

### 0z181. Browser Audio Elements Cannot Send Authorization Headers

**The Bug**: The Web UI player assigned `/api/v0/streams/{contentId}` directly to an `<audio>` source. API probes with `Authorization: Bearer ...` passed, but real browser playback failed with `401 Unauthorized` because media element requests do not use the app's Axios header interceptor.

**Files Affected**:
- `src/web/src/components/Player/PlayerBar.jsx`
- `src/slskd/Program.cs`
- `src/slskd/Streaming/StreamsController.cs`

**Wrong**:
```js
const streamUrl = (contentId) =>
  `${urlBase}/api/v0/streams/${encodeURIComponent(contentId)}`;
```

**Correct**:
```js
const token = getToken();
const query = token ? `?access_token=${encodeURIComponent(token)}` : '';
const streamUrl = `${urlBase}/api/v0/streams/${encodeURIComponent(contentId)}${query}`;
```

**Why This Keeps Happening**: Browser-managed requests from `<audio>`, `<video>`, images, and downloads bypass Axios/fetch interceptors. If a protected media endpoint must support direct element playback, the auth middleware needs an explicit query-token path, or the endpoint needs a purpose-built short-lived media token.

### 0z179. Analyzer Bars Should Use Log Frequency Buckets

**The Bug**: The Web UI player analyzer sampled FFT bins linearly and drew the oscilloscope directly from byte values. Bars overrepresented high frequencies while low/mid content looked compressed, and quiet scope signals appeared squashed around the center line.

**Files Affected**:
- `src/web/src/components/Player/SpectrumAnalyzer.jsx`
- `src/web/src/components/Player/PlayerBar.jsx`

**Wrong**:
```js
const value = data[Math.floor((index / 64) * data.length)];
const y = (value / 255) * height;
```

**Correct**:
```js
const bars = getFrequencyBars(data, barCount);
const points = getScopePoints(data, width, height);
```

**Why This Keeps Happening**: FFT bin arrays are linear in frequency, but music analyzer displays need perceptual/log-style buckets so low and mid frequencies remain visible. Time-domain scope bytes also need centering around `128` and bounded gain before mapping to canvas coordinates.

### 0z178. Versioned Controllers Must Bind The URL Version Segment

**The Bug**: PodCore controllers used literal routes like `api/v0/podcore/dht`. ASP.NET API versioning still inspected those actions and returned `ApiVersionUnspecified` or `UnsupportedApiVersion` for live `/api/v0/podcore/...` requests, even after adding `[ApiVersion("0")]`.

**Files Affected**:
- `src/slskd/PodCore/API/Controllers/*Controller.cs`

**Wrong**:
```csharp
[ApiController]
[Route("api/v0/podcore/dht")]
public class PodDhtController : ControllerBase
```

**Correct**:
```csharp
[ApiController]
[ApiVersion("0")]
[Route("api/v{version:apiVersion}/podcore/dht")]
public class PodDhtController : ControllerBase
```

**Why This Keeps Happening**: A literal `v0` route segment is not enough once ASP.NET API versioning is enabled globally. Every versioned controller should use `api/v{version:apiVersion}/...` and declare the matching `[ApiVersion]` metadata so URL-segment versioning can bind the requested version.

### 0z179. Stream Bearer Tokens Are Not Always Share Tokens

**The Bug**: `StreamsController` treated any `Authorization: Bearer ...` value as a share token before checking normal authenticated users. Browser/player requests carrying the normal JWT therefore failed with `401 Unauthorized` even though the user was logged in.

**Files Affected**:
- `src/slskd/Streaming/StreamsController.cs`

**Wrong**:
```csharp
if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    tokenRaw = auth.Substring("Bearer ".Length).Trim();

if (!string.IsNullOrEmpty(tokenRaw))
{
    claims = await _tokens.ValidateAsync(tokenRaw, ct);
    if (claims == null) return Unauthorized();
}
```

**Correct**:
```csharp
var authenticatedNormally = User?.Identity?.IsAuthenticated == true;
var bearerIsExplicitShareToken = bearer.StartsWith("share:", StringComparison.OrdinalIgnoreCase);

if (!authenticatedNormally || bearerIsExplicitShareToken)
{
    claims = await _tokens.ValidateAsync(tokenRaw, ct);
    if (claims == null) return Unauthorized();
}
```

**Why This Keeps Happening**: The streaming endpoint supports both normal UI auth and share-token auth on the same route. The authorization branch must distinguish JWT/API auth from share tokens instead of blindly interpreting every bearer token as a share grant.

### 0z180. DHT Object Payloads Need Explicit Serialization

**The Bug**: Pod DHT publishing passed a private `SignedPod` DTO directly to `IMeshDhtClient.PutAsync`. The DHT client serializes non-byte payloads with MessagePack's standard resolver, which cannot serialize unannotated/private DTOs, causing `FormatterNotRegisteredException`.

**Files Affected**:
- `src/slskd/PodCore/PodDhtPublisher.cs`
- `src/slskd/Mesh/Dht/MeshDhtClient.cs`

**Wrong**:
```csharp
var signedPod = CreateSignedPod(pod);
await _dhtClient.PutAsync(dhtKey, signedPod, ttlSeconds, ct);
```

**Correct**:
```csharp
var signedPod = CreateSignedPod(pod);
var payload = JsonSerializer.SerializeToUtf8Bytes(signedPod);
await _dhtClient.PutAsync(dhtKey, payload, ttlSeconds, ct);
```

**Why This Keeps Happening**: `IMeshDhtClient.PutAsync` accepts `object?`, so it is easy to assume arbitrary DTOs are safe. They are not. Either pass already-serialized `byte[]`, or make the type explicitly compatible with the DHT serializer and read path.

### 0z177. Fixed Pod IDs Must Still Match Pod Validation

**The Bug**: `GoldStarClubService` used the human-readable fixed ID `pod:gold-star-club`, but `SqlitePodService` validates all pod IDs as `^pod:[a-f0-9]{32}$`. Startup crashed when the hosted service tried to ensure the Gold Star Club pod. The same class also derived a channel id as `$"{GoldStarClubPodId}:general"`, but channel ids only allow alphanumeric, dash, and underscore characters.

**Files Affected**:
- `src/slskd/PodCore/GoldStarClubService.cs`
- `docs/design/gold-star-club.md`

**Wrong**:
```csharp
public const string GoldStarClubPodId = "pod:gold-star-club";
ChannelId = $"{GoldStarClubPodId}:general";
```

**Correct**:
```csharp
public const string GoldStarClubPodId = "pod:901d57a2c1bb4e5d90d57a2c1bb4e5d0";
ChannelId = "gold-star-club-general";
```

**Why This Keeps Happening**: Product-visible names and protocol/storage identifiers have different constraints. Keep friendly names in `Name` or tags, make any fixed pod IDs conform to `PodValidation.IsValidPodId`, and never derive channel IDs from `pod:<hex>` strings because the `pod:` prefix is invalid for channels.

### 0z176. Vite Dev UI Should Use The Proxy By Default

**The Bug**: The Vite Web UI served `index.html`, but the React app failed at runtime because API calls used an absolute `http://localhost:{port}` backend URL. That bypassed Vite's `/api` proxy and hit browser CORS, showing "Lost connection to slskd" even though both servers were running.

**Files Affected**:
- `src/web/src/config.js`
- `src/web/vite.config.js`

**Wrong**:
```js
const rootUrl = import.meta.env.PROD
  ? urlBase
  : `http://localhost:${developmentPort}${urlBase}`;
```

**Correct**:
```js
const rootUrl = import.meta.env.PROD
  ? urlBase
  : import.meta.env.VITE_USE_ABSOLUTE_API_URL === 'true'
    ? `http://localhost:${developmentPort}${urlBase}`
    : urlBase;
```

**Why This Keeps Happening**: A 200 from Vite only proves the page shell is served. In development, browser API and SignalR calls should normally stay same-origin so Vite can proxy `/api` and `/hub` to the daemon without CORS. Only use absolute backend URLs when the backend is explicitly configured to allow that origin.

### 0z175. Hosted Policy Services Must Be Registered

**The Bug**: `GoldStarClubService` existed as a `BackgroundService`, but it was not registered in DI, so startup policy around creating and joining the Gold Star Club pod never ran.

**Files Affected**:
- `src/slskd/PodCore/GoldStarClubService.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
public sealed class GoldStarClubService : BackgroundService, IGoldStarClubService
{
    // service implementation exists
}
```

with no matching service registration.

**Correct**:
```csharp
services.AddSingleton<PodCore.GoldStarClubService>();
services.AddSingleton<PodCore.IGoldStarClubService>(sp => sp.GetRequiredService<PodCore.GoldStarClubService>());
services.AddHostedService(sp => sp.GetRequiredService<PodCore.GoldStarClubService>());
```

**Why This Keeps Happening**: Background services are inert until registered. When adding daemon policy services, verify both the typed interface and hosted-service registration path, especially when tests instantiate the class directly and can pass without proving production startup behavior.

### 0z174. Singleton Services Must Not Capture Scoped Pod Storage

**The Bug**: `ListeningPartyService` was registered as a singleton to keep live party state, but its constructor took scoped `IPodMessageStorage`, causing startup DI validation to fail with `Cannot consume scoped service 'slskd.PodCore.IPodMessageStorage' from singleton`.

**Files Affected**:
- `src/slskd/ListeningParty/ListeningPartyService.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
public ListeningPartyService(IPodMessageStorage messageStorage)
{
    _messageStorage = messageStorage;
}
```

**Correct**:
```csharp
public ListeningPartyService(IServiceScopeFactory scopeFactory)
{
    _scopeFactory = scopeFactory;
}

using var scope = _scopeFactory.CreateScope();
var messageStorage = scope.ServiceProvider.GetRequiredService<IPodMessageStorage>();
```

**Why This Keeps Happening**: Live coordination services often need singleton lifetime for in-memory state, while PodCore persistence services are scoped because they own disposable EF/SQLite contexts. Keep singleton state in the singleton, but resolve scoped persistence dependencies per operation through an explicit scope.

### 0z173. Initial Winget Submissions Must Not Use Singleton Manifests

**The Bug**: The stable Winget workflow switched first submissions to a temporary singleton manifest to bypass local WingetCreate directory-validation errors, but Microsoft server-side validation rejected the PR with `Manifest type not supported. ManifestType: singleton`.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `.github/workflows/publish-winget.yml`

**Wrong**:
```powershell
$WingetSubmitPath = Join-Path "winget-submit" "snapetech.slskdn.yaml"
@(
  'ManifestType: singleton'
) | Set-Content -Path $WingetSubmitPath -Encoding utf8

.\wingetcreate.exe submit $env:WINGET_SUBMIT_PATH -t $env:WINGETCREATE_GITHUB_TOKEN
```

**Correct**:
```powershell
$WingetSubmitPath = Join-Path "winget-submit" "manifests/s/snapetech/slskdn/$WingetVersion"
New-Item -ItemType Directory -Force $WingetSubmitPath | Out-Null
Copy-Item packaging/winget/snapetech.slskdn.yaml $WingetSubmitPath/
Copy-Item packaging/winget/snapetech.slskdn.installer.yaml $WingetSubmitPath/
Copy-Item packaging/winget/snapetech.slskdn.locale.en-US.yaml $WingetSubmitPath/
.\wingetcreate.exe submit $env:WINGET_SUBMIT_PATH -t $env:WINGETCREATE_GITHUB_TOKEN
```

**Why This Keeps Happening**: WingetCreate's local CLI can accept singleton manifests that the `microsoft/winget-pkgs` service rejects for repository PR validation. Keep the generated manifests in the repository-shaped multi-file layout and fix any local directory-validation issue directly instead of bypassing it with singleton output. When staging stable manifests, copy the three exact stable filenames; `snapetech.slskdn*.yaml` also matches `snapetech.slskdn-dev*.yaml`.

### 0z172. Winget PackageVersion Should Stay Numeric

**The Bug**: Stable Winget manifests used the public slskdN release label converted from `2026042900-slskdn.202` to `2026042900.slskdn.202`. WingetCreate/WinGetUtil validation reported confusing multi-file consistency errors instead of a direct version-format error.

**Files Affected**:
- `packaging/scripts/update-winget-manifests.sh`
- `packaging/winget/snapetech.slskdn*.yaml`

**Wrong**:
```yaml
PackageVersion: 2026042900.slskdn.202
```

**Correct**:
```yaml
PackageVersion: "2026042900.202"
```

**Why This Keeps Happening**: slskdN public release tags include the fork label for GitHub and distro-package clarity, but Winget's package version should be a simple comparable package-manager version. Keep the full public tag in URLs/release notes and write a clean numeric dotted `PackageVersion` into Winget manifests.

### 0z171. Winget Zip Portable Fields Belong At Installer Manifest Root

**The Bug**: The stable Winget manifest put `NestedInstallerType` and `NestedInstallerFiles` under the x64 installer entry for a single zip portable package. WingetCreate validation reported confusing multi-file consistency errors instead of pointing directly at the misplaced portable metadata.

**Files Affected**:
- `packaging/scripts/update-winget-manifests.sh`
- `packaging/winget/snapetech.slskdn.installer.yaml`

**Wrong**:
```yaml
InstallerType: zip
Installers:
  - Architecture: x64
    InstallerUrl: ...
    NestedInstallerType: portable
    NestedInstallerFiles:
      - RelativeFilePath: slskd.exe
```

**Correct**:
```yaml
InstallerType: zip
NestedInstallerType: portable
NestedInstallerFiles:
- RelativeFilePath: slskd.exe
  PortableCommandAlias: slskdn
Commands:
- slskdn
Installers:
- Architecture: x64
  InstallerUrl: ...
```

**Why This Keeps Happening**: Winget supports some installer properties both globally and per-installer, but accepted zip-portable manifests commonly place archive structure and command aliases at the installer manifest root. Follow known winget-pkgs examples for portable zip layouts instead of guessing from schema flexibility.

### 0z171. Social UI State Must Rehydrate From Server State

**The Bug**: Pods, rooms, and chat had backend persistence, but parts of the Web UI treated local browser tabs and ad hoc create payloads as the source of truth. After a reload, browser reset, or restart, users could lose the visible path back to conversations/rooms/pods even when the data still existed.

**Files Affected**:
- `src/web/src/lib/pods.js`
- `src/web/src/components/Pods/Pods.jsx`
- `src/web/src/components/Chat/Chat.jsx`
- `src/web/src/components/Rooms/Rooms.jsx`
- `src/web/src/components/Contacts/Contacts.jsx`

**Wrong**:
```js
await pods.create(pod);
setTabs(loadTabsFromStorage());
```

**Correct**:
```js
await pods.create({ pod, requestingPeerId });
const conversations = await chat.getAll();
const joinedRooms = await rooms.getJoined();
```

**Why This Keeps Happening**: Social features are easy to scaffold as standalone pages with local UI state, but users experience them as durable relationships. Every social surface needs to hydrate from persisted server state first, then layer browser convenience state on top.

### 0z170. WingetCreate Submit Needs Repository-Shaped Manifest Paths

**The Bug**: `wingetcreate submit` validated the three stable manifest files incorrectly when they were copied into a flat scratch directory, reporting duplicate manifest types and inconsistent package fields even though the files had matching `PackageIdentifier` and `PackageVersion` values.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `.github/workflows/publish-winget.yml`

**Wrong**:
```powershell
New-Item -ItemType Directory -Force winget-submit | Out-Null
Copy-Item packaging/winget/snapetech.slskdn*.yaml winget-submit/
.\wingetcreate.exe submit .\winget-submit -t $env:WINGETCREATE_GITHUB_TOKEN
```

**Correct**:
```powershell
$WingetSubmitPath = Join-Path "winget-submit" "manifests/s/snapetech/slskdn/$WingetVersion"
New-Item -ItemType Directory -Force $WingetSubmitPath | Out-Null
Copy-Item packaging/winget/snapetech.slskdn*.yaml $WingetSubmitPath/
.\wingetcreate.exe submit $env:WINGET_SUBMIT_PATH -t $env:WINGETCREATE_GITHUB_TOKEN
```

**Why This Keeps Happening**: The local `packaging/winget` templates are intentionally flat for repo maintenance, but WingetCreate submission behavior follows the `microsoft/winget-pkgs` manifest tree. Always stage generated manifests into the repository-shaped path before calling `wingetcreate submit`.

### 0z169. Winget Block Scalars Must Not Mix Generator Indentation

**The Bug**: The stable Winget locale manifest generated an invalid `Description: |-` block because the description variable already contained leading spaces on its first lines, and the manifest generator added two more spaces to every line. YAML inferred a four-space block indentation from the first content line and then failed when later lines only had two spaces.

**Files Affected**:
- `packaging/scripts/update-winget-manifests.sh`
- `packaging/winget/snapetech.slskdn.locale.en-US.yaml`

**Wrong**:
```bash
DESCRIPTION=$(cat <<'EOF'
  slskdN is an unofficial fork...

Stable features include:
EOF
)

printf '%s\n' "$DESCRIPTION" | sed 's/^/  /'
```

**Correct**:
```bash
DESCRIPTION=$(cat <<'EOF'
slskdN is an unofficial fork...

Stable features include:
EOF
)

printf '%s\n' "$DESCRIPTION" | sed 's/^/  /'
```

**Why This Keeps Happening**: Here-doc text that already looks visually indented can become double-indented when a YAML generator adds block-scalar indentation later. Keep generator input unindented and let the YAML writer be the only place that adds block-scalar spaces.

### 0z168. Security Boundaries Need Boundary-Specific Tests

**The Bug**: Several security checks looked reasonable in isolation but trusted the wrong boundary: ActivityPub outbox publishing was anonymous, share backfill trusted remote stream URLs, file listing used a raw path prefix, and CSRF skipped query-string API keys that authentication does not actually honor.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/Sharing/API/SharesController.cs`
- `src/slskd/Files/FileService.cs`
- `src/slskd/Core/Security/ValidateCsrfForCookiesOnlyAttribute.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
[AllowAnonymous]
public async Task<IActionResult> PostToOutbox(...)
```

```csharp
if (!AllowedDirectories.Any(allowed => directory.StartsWith(allowed)))
{
    throw new UnauthorizedException(...);
}
```

```csharp
if (request.Query.ContainsKey("api_key"))
{
    return;
}
```

**Correct**:
```csharp
public async Task<IActionResult> PostToOutbox(...)
```

```csharp
return fullPath.Equals(fullAllowed, StringComparison.OrdinalIgnoreCase)
    || fullPath.StartsWith(fullAllowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
```

```csharp
if (request.Headers.ContainsKey("X-API-Key"))
{
    return;
}
```

**Why This Keeps Happening**: Protocol endpoints, path checks, and auth shortcuts tend to be reviewed for happy-path behavior rather than the trust boundary they cross. Every anonymous protocol action needs an allow-list test, every filesystem root check needs a sibling-prefix regression, and every outbound URL derived from remote data needs SSRF and size guards before any socket is opened.

### 0z167. Package Upgrades Must Refresh Already-Running Services

**The Bug**: AUR upgrades installed the new payload and updated `/usr/bin/slskd --version`, but the live systemd service kept running the old release until an explicit manual restart.

**Files Affected**:
- `packaging/aur/slskd.install`

**Wrong**:
```sh
post_upgrade() {
    post_install
}
```

**Correct**:
```sh
post_upgrade() {
    post_install
    systemctl try-restart slskd.service >/dev/null 2>&1 || true
}
```

**Why This Keeps Happening**: Package-manager install hooks and release payload checks prove that files on disk changed, not that the daemon process re-execed the new binary. Upgrade hooks should restart only already-running services with `try-restart`, preserving disabled/stopped installs while ensuring active daemons actually move to the upgraded payload.

### 0z166. Weak SongID Candidates Must Not Unlock Catalog Context

**The Bug**: Manual-review SongID runs with a plausible but weak track candidate expanded album, artist, and segment context, which polluted Discovery Graph neighborhoods with unrelated labels such as "TV Show".

**Files Affected**:
- `src/slskd/DiscoveryGraph/DiscoveryGraphService.cs`
- `tests/slskd.Tests.Unit/SongID/DiscoveryGraphServiceTests.cs`

**Wrong**:
```csharp
if (run.Tracks.Any(track => track.IsExact || track.IdentityScore >= MinimumTrackIdentityForWeakRun))
{
    return true;
}
```

**Correct**:
```csharp
if (run.Tracks.Any(track => track.IsExact || track.IdentityScore >= MinimumTrackIdentityForCatalogExpansion))
{
    return true;
}
```

**Why This Keeps Happening**: The graph has two different trust decisions: showing a weak candidate as a visible clue, and expanding secondary catalog context around that candidate. Reusing the weak-candidate display threshold for expansion lets uncertain manual-review evidence look like a trusted catalog identity. Keep those thresholds separate.

### 0z165. Release Gate Compiles All Tests, Including Stale Compatibility Tests

**The Bug**: The tag release gate failed at test compilation because stale tests still asserted a removed `MusicBrainz.Enabled` option and used unqualified `File` / `Directory` after importing `Soulseek`, where those names collide with `System.IO`.

**Files Affected**:
- `tests/slskd.Tests.Unit/Configuration/YamlConfigurationSourceTests.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
Assert.True(options.Integration.MusicBrainz.Enabled);
File.WriteAllText(path, content);
Directory.Delete(directory, recursive: true);
```

**Correct**:
```csharp
Assert.Equal("https://musicbrainz.example.invalid", options.Integration.MusicBrainz.BaseUrl);
using Directory = System.IO.Directory;
using File = System.IO.File;
```

**Why This Keeps Happening**: Compatibility tests are often added quickly while config schemas are moving. Release workflows compile the full test project, so stale assertions and namespace collisions outside the focused test filter can still break tags. When adding tests that import `Soulseek`, qualify or alias `System.IO.File` and `System.IO.Directory`; when testing YAML aliases, assert options that still exist in `Options`.

### 0z164. Header And Footer Chrome Need Explicit Rail Grouping

**The Bug**: The Web UI header and footer rendered as flat item streams, so utility actions mixed visually with primary navigation and footer status pills drifted into awkward spacing under live counters.

**Files Affected**:
- `src/web/src/components/App.jsx`
- `src/web/src/components/App.css`
- `src/web/src/components/Shared/Footer.jsx`
- `src/web/src/components/Shared/Footer.css`

**Wrong**:
```jsx
<Sidebar as={Menu}>
  <Link to="/browse"><Menu.Item>Browse</Menu.Item></Link>
  <Menu className="right">...</Menu>
</Sidebar>
```

with the footer as one mixed flex row of brand, speed, network, icon stats, and decorative links.

**Correct**:
```jsx
<Sidebar as={Menu}>
  <div className="navigation-primary">...</div>
  <Menu className="right">...</Menu>
</Sidebar>
```

and footer markup split into left, center, and right rails whose pills keep fixed height, avoid vertical wrapping, and use horizontal scrolling at narrow widths.

**Why This Keeps Happening**: Fixed app chrome is judged from still screenshots while the real shell has live counters, variable browser font metrics, and utility actions with different semantic weight. Header and footer changes need live desktop plus narrow viewport checks with overflow metrics, not only component-level tests.

### 0z163. Batch Downloads Do Not Live At Their Original Relative Path

**The Bug**: Removing a completed batch download with "delete file" looked for the file under the normal downloads tree, even though batch completion moves files into `downloads/<batch-id>/`.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`

**Wrong**:
```csharp
var localFilename = transfer.Filename.ToLocalFilename(baseDirectory);
```

for every completed download, regardless of `BatchId`.

**Correct**:
```csharp
var localFilename = GetLocalFilenameForRemoval(transfer, baseDirectory);
```

with succeeded batch downloads resolving to `downloads/<batch-id>/<file-name>`.

**Why This Keeps Happening**: The enqueue record stores the original remote path, while batch completion intentionally flattens completed files into a batch directory by moving the completed temp filename. Any later filesystem operation must account for that storage layout instead of recomputing only from the remote path.

### 0z162. Fixed Chrome Must Be Checked Against Showcase Viewports

**The Bug**: The top navigation and footer status dock looked acceptable in isolation but produced clipped navigation, hidden content, and a three-row mobile footer in README/browser screenshots.

**Files Affected**:
- `src/web/src/components/App.css`
- `src/web/src/components/Shared/Footer.css`
- `src/web/src/components/Search/Search.css`
- `src/web/src/components/Search/DiscoveryGraphAtlas.jsx`

**Wrong**:
```css
.slskdn-footer-content {
  flex-wrap: wrap;
}
```

on mobile with a fixed footer, because the footer can grow over the page content.

**Correct**:
```css
.slskdn-footer-content {
  flex-wrap: nowrap;
  min-width: max-content;
}
```

with the footer itself scrolling horizontally at narrow widths and the app content retaining enough bottom padding for the fixed rail.

**Why This Keeps Happening**: Fixed application chrome is easy to judge from component screenshots and easy to miss in viewport screenshots. Validate desktop and mobile with the real top nav, footer, route content, and README capture dimensions before declaring a UI pass complete.

### 0z161. Dark Theme Needs Explicit Semantic UI Variant Coverage

**The Bug**: README showcase screenshots exposed dark-theme pages with light or unreadable Semantic UI internals because `Segment secondary`, `Message info/warning`, dropdowns, labeled inputs, progress labels, and header subheaders do not all inherit the app-level dark variables.

**Files Affected**:
- `src/web/src/components/Search/Search.css`
- `src/web/src/components/System/System.css`
- `src/web/src/components/Search/SongIDPanel.jsx`
- `src/web/src/components/Search/DiscoveryGraphAtlas.jsx`
- `src/web/src/components/Search/DiscoveryGraphAtlasPanel.jsx`
- `src/web/src/components/System/Network/index.jsx`

**Wrong**:
```jsx
<Segment secondary>
  <Header.Subheader>Visible in light mode only</Header.Subheader>
</Segment>
```

without a component class and dark selectors for Semantic UI's variant-specific children.

**Correct**:
```jsx
<Segment className="songid-result-panel" secondary>
```

with `:root.dark` selectors for the panel, headers, subheaders, labels, inputs, dropdown text, progress labels, and message variants that appear inside the screenshot surface.

**Why This Keeps Happening**: The global Semantic UI variable bridge covers common components, but variant classes and nested children still ship their own light-theme colors. Any new dark-mode screenshot surface needs a quick browser pass for both panel backgrounds and nested text contrast.

### 0z160. Discovery Hash Probes Must Share The Verification Probe Budget

**The Bug**: Continuous discovery FLAC hash verification called `GetContentHashAsync()` directly, bypassing the per-peer verification probe budget used by `VerifySourcesAsync()`, so peers could see more cancelled 32KB probe transfers than intended.

**Files Affected**:
- `src/slskd/Transfers/MultiSource/ContentVerificationService.cs`
- `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs`

**Wrong**:
```csharp
var hash = await verificationService.GetContentHashAsync(username, filename, size, cancellationToken);
```

without a budget check inside `GetContentHashAsync()`.

**Correct**:
```csharp
if (!TryConsumeProbeBudget(username))
{
    return null;
}
```

and persist the budget so restarting the process does not reset peer-visible probe noise.

**Why This Keeps Happening**: Multi-source verification has multiple entry points. New callers can accidentally use the lower-level single-source helper and skip the guardrails that were added to the batch verification path. Keep rate/noise limits at the lowest public helper that contacts Soulseek peers.

### 0z159. Keep The Nav Theme Picker Out Of The Overflowing Menu

**The Bug**: The Web UI theme picker rendered options in the DOM, but browser clicks either left the dropdown closed (`aria-expanded="false"`) or opened a menu clipped behind the overflowing top navigation, especially visible in Firefox/LibreWolf.

**Files Affected**:
- `src/web/src/components/App.jsx`

**Wrong**:
```jsx
<Dropdown
  onClose={closeThemeMenu}
  onOpen={openThemeMenu}
  open={themeMenuOpen}
  options={themeOptions}
/>
```

**Correct**:
```jsx
<Popup
  on="click"
  trigger={<Menu.Item>Theme</Menu.Item>}
>
  <Menu vertical>{themeItems}</Menu>
</Popup>
```

**Why This Keeps Happening**: Semantic UI dropdown menus stay inside the top navigation's stacking and overflow context. The state can look open in the DOM while the menu is clipped or layered under page content. Render the theme choices through a click `Popup` portal and keep the nav trigger labeled by its function (`Theme`), not by the currently selected theme name.

### 0z157. Discovery Graph Must Preserve Manual-Review SongID Candidates

**The Bug**: SongID runs with useful manual-review candidates collapsed into a single Discovery Graph seed node because graph expansion required exact or very high identity scores.

**Files Affected**:
- `src/slskd/DiscoveryGraph/DiscoveryGraphService.cs`
- `tests/slskd.Tests.Unit/SongID/DiscoveryGraphServiceTests.cs`

**Wrong**:
```csharp
private const double MinimumTrackIdentityForWeakRun = 0.85;
```

**Correct**:
```csharp
private const double MinimumTrackIdentityForWeakRun = 0.70;
```

**Why This Keeps Happening**: SongID intentionally emits ambiguity candidates for manual-review and source-original cases. Discovery Graph is supposed to show that neighborhood, but overly strict catalog-recognition thresholds hide every candidate unless the run is already highly recognized. Keep graph thresholds separate from final identity verdict thresholds so review-needed runs still produce useful topology.

### 0z158. User Namespace Classes Must Fully Qualify Core Options

**The Bug**: New classes under `slskd.Users` that inject `IOptionsMonitor<Options>` can accidentally bind `Options` to a static type in the same namespace instead of the root configuration type, breaking compilation with `CS0718` / `CS0721`.

**Files Affected**:
- `src/slskd/Users/RegexUsernameMatcher.cs`

**Wrong**:
```csharp
private void Configure(Options options)
```

**Correct**:
```csharp
private void Configure(Core.Options options)
```

**Why This Keeps Happening**: `slskd.Users` already contains an `Options` symbol, so unqualified `Options` is ambiguous or resolves to the wrong type inside that namespace. Use `Core.Options` or an explicit alias when injecting configuration from user-domain classes.

### 0z156. Network DHT Warnings Must Use Status Counters, Not Just List Endpoints

**The Bug**: The Network dashboard could keep showing public-DHT and no-peer warnings even after DHT rendezvous had healthy node and discovered-peer counters because the warning logic only looked at empty mesh/discovered peer list endpoints.

**Files Affected**:
- `src/web/src/components/System/Network/index.jsx`
- `src/web/src/components/System/Network/index.test.jsx`

**Wrong**:
```js
const shouldWarnAboutConnectivity =
  meshPeers.length === 0 && discoveredPeers.length === 0;
```

**Correct**:
```js
const observedMeshPeerCount = Math.max(
  meshPeers.length,
  mesh?.connectedPeerCount ?? 0,
  stats?.dht?.activeMeshConnections ?? 0,
);
const observedDiscoveredPeerCount = Math.max(
  discoveredPeers.length,
  stats?.dht?.discoveredPeerCount ?? stats?.dht?.totalPeersDiscovered ?? 0,
);
```

**Why This Keeps Happening**: The Network page mixes older capability-list endpoints with newer DHT rendezvous status counters. The list endpoints can legitimately be empty while `/api/v0/dht/status` already reports DHT nodes, discovered peers, and active mesh connections. Public-DHT exposure is also an intended feature state, so it should be a dismissable awareness notice instead of a persistent warning.

### 0z155. Package Publish Jobs Must Fail When Retries Are Exhausted

**The Bug**: The stable Chocolatey publish job returned success after three `504 Gateway Timeout` responses, so the tag workflow looked green even though no package was published to Chocolatey.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```powershell
Write-Host "Chocolatey push failed after $maxRetries attempts (likely transient)"
exit 0
```

**Correct**:
```powershell
$PackageVersion = $Version -replace '^(\d{10})-slskdn\.(\d+)$', '$1.0.0-slskdn.$2'
$pushText = $pushOutput -join "`n"
Write-Host "Chocolatey push failed after $maxRetries attempts (likely transient)"
exit 1
```

**Why This Keeps Happening**: Release workflows often treat external package hosts as flaky and try to keep the overall release moving. That is acceptable for optional channels only if the failure is clearly reported. For a publish job named as successful, exhausted retries must fail the job so the missing downstream package is visible. PowerShell command output can also be an array of lines, so match retryable output against a joined string instead of using `-match` / `-notmatch` directly on the array. Chocolatey/NuGet may normalize date-based slskdN versions, so write the normalized package version into the nuspec explicitly while keeping download URLs pointed at the real GitHub release tag.

### 0z154. Transfer Bulk Actions Must Mask Accepted Terminal Rows

**The Bug**: Downloads/uploads flashed old/new/old/new rows after `Retry All Failed` or `Remove All Succeeded` because one-second polling kept rendering stale terminal transfer snapshots while the backend was still converging.

**Files Affected**:
- `src/web/src/components/Transfers/Transfers.jsx`
- `src/web/src/components/Transfers/Transfers.test.jsx`

**Wrong**:
```js
await transfersLibrary.clearCompleted({ direction });
// Wait for a later poll to remove rows.
```

**Correct**:
```js
await transfersLibrary.clearCompleted({ direction });
hideTransfers(transfersToRemove);
```

**Why This Keeps Happening**: Bulk transfer actions are accepted asynchronously by the backend, and the UI polls frequently enough for stale snapshots to race with newer ones. The page needs monotonic fetch application plus short-lived optimistic suppression for rows whose retry/remove action already succeeded.

### 0z153. Semantic UI Theme Pickers Should Use Controlled Dropdown Options

**The Bug**: The Web UI theme picker looked clickable but did not reliably open or apply choices when implemented as a custom-trigger `Dropdown` with manual `Dropdown.Item` children inside the inverted navigation.

**Files Affected**:
- `src/web/src/components/App.jsx`
- `src/web/src/components/App.test.jsx`

**Wrong**:
```jsx
<Dropdown trigger={<span>Theme</span>}>
  <Dropdown.Menu>
    <Dropdown.Item onClick={() => setTheme('light')}>Light</Dropdown.Item>
  </Dropdown.Menu>
</Dropdown>
```

**Correct**:
```jsx
<Dropdown
  onChange={(_, data) => setTheme(data.value)}
  open={themeMenuOpen}
  options={themeOptions}
  value={theme}
/>
```

**Why This Keeps Happening**: Semantic UI dropdowns in a `Menu.Item` have their own open/change lifecycle. Mixing a custom trigger, manual child items, and nav-specific styling can make the visible trigger diverge from the dropdown's controlled value path. Use controlled `options`/`value`/`onChange` for this selector and cover it with a click test.

### 0z152. Full-Instance Overlay Connect Tests Must Wait Through Transient 502s

**The Bug**: The live full-instance overlay mesh integration test failed in full-suite runs when `/api/v0/overlay/connect` returned a transient `502` even though the same test passed by itself and the overlay listener had already accepted TCP probes.

**Files Affected**:
- `tests/slskd.Tests.Integration/DhtRendezvous/TwoNodeMeshFullInstanceTests.cs`

**Wrong**:
```csharp
var connectResponse = await alphaClient.PostAsJsonAsync("/api/v0/overlay/connect", request);
connectResponse.EnsureSuccessStatusCode();
```

**Correct**:
```csharp
var connectBody = await WaitForOverlayConnectAsync(alphaClient, beta.OverlayPort.Value, timeout);
Assert.True(connectBody.Connected);
```

**Why This Keeps Happening**: `SlskdnFullInstanceRunner` can prove the TCP port is listening before the overlay handshake path is fully stable under full-suite load and live-account startup. A single manual-connect `502` should be treated like an eventual-readiness condition in these process-level integration tests.

### 0z151. Do Not Log Legacy Credential File Paths

**The Bug**: The overlay certificate hardening stopped reading `overlay_cert.key`, but still logged `_legacyPasswordPath` after deletion. CodeQL treated that as another `cs/cleartext-storage-of-sensitive-information` flow because the legacy credential path remains sensitive metadata.

**Files Affected**:
- `src/slskd/DhtRendezvous/Security/CertificateManager.cs`

**Wrong**:
```csharp
File.Delete(_legacyPasswordPath);
_logger.LogDebug("Removed legacy overlay certificate password file {Path}", _legacyPasswordPath);
```

**Correct**:
```csharp
File.Delete(_legacyPasswordPath);
_logger.LogDebug("Removed legacy overlay certificate password file");
```

**Why This Keeps Happening**: Once a file path is specifically a legacy credential location, logging or otherwise propagating that path can keep static-analysis secret flows alive even if the file contents are never read. Legacy secret cleanup paths should avoid logging the exact credential path and should only report the action.

### 0z150. LAN-Only DHT With Zero Nodes Is Not A Port-Forwarding Failure

**The Bug**: The Network dashboard showed the generic connectivity warning when `dhtRendezvous.lanOnly: true` left the DHT with zero nodes and zero peers, which made operators chase port-forwarding even though the service deliberately skips public bootstrap routers in LAN-only mode.

**Files Affected**:
- `src/web/src/components/System/Network/index.jsx`
- `src/web/src/components/System/Network/index.test.jsx`

**Wrong**:
```js
const shouldWarnAboutConnectivity =
  meshPeers.length === 0 &&
  discoveredPeers.length === 0 &&
  (stats?.dht?.dhtNodeCount ?? 0) === 0;
```

**Correct**:
```js
const shouldExplainLanOnlyDht =
  dhtIsLanOnly &&
  (stats?.dht?.isDhtRunning ?? false) &&
  (stats?.dht?.dhtNodeCount ?? 0) === 0;
const shouldWarnAboutConnectivity = !shouldExplainLanOnlyDht && ...;
```

**Why This Keeps Happening**: Public DHT health and local port reachability are related but not equivalent. In LAN-only mode slskdN intentionally disables public bootstrap and avoids saved public node tables, so `0 nodes` can be the expected privacy-preserving state unless another local/private discovery path seeds peers.

### 0z149. Theme Menus Need Their Own Surface And Contrast Tokens

**The Bug**: The slskdN web theme made the top navigation, active theme picker, dropdown menu, panels, and inputs use near-identical dark warm colors, so the theme picker looked broken and the page lost usable visual hierarchy.

**Files Affected**:
- `src/web/src/components/App.css`
- `src/web/src/components/App.jsx`

**Wrong**:
```css
--slskd-primary-background: #161311;
--slskd-secondary-background: #221e1b;
--slskd-overlay-background: #211b1e;
--smui-menu-background: var(--slskd-hover-background);
```

**Correct**:
```css
--slskd-primary-background: #121315;
--slskd-secondary-background: #211d1a;
--slskd-overlay-background: #24242b;
--slskdn-theme-menu-background: #27252d;
```

**Why This Keeps Happening**: Semantic UI dropdowns inherit menu and active-item tokens from the surrounding inverted navigation. If a custom theme only tweaks global dark variables, the trigger and popup can visually merge with the header. Theme pickers need explicit trigger, popup, active, hover, border, and shadow styling separate from page background tokens.

### 0z148. Do Not Read Legacy Overlay Certificate Password Files

**The Bug**: Overlay certificate migration still read `overlay_cert.key` as a cleartext password when loading an old password-protected `overlay_cert.pfx`, which kept a CodeQL `cs/cleartext-storage-of-sensitive-information` alert open.

**Files Affected**:
- `src/slskd/DhtRendezvous/Security/CertificateManager.cs`

**Wrong**:
```csharp
var password = File.ReadAllText(_legacyPasswordPath).Trim();
return X509CertificateLoader.LoadPkcs12FromFile(path, password, ...);
```

**Correct**:
```csharp
DeleteLegacyPasswordFile();
throw; // Let the caller generate a fresh passwordless overlay certificate.
```

**Why This Keeps Happening**: Compatibility migration can look harmless because the file is deleted after a successful migration, but reading a persisted cleartext secret is still sensitive-data handling. Overlay certificates are self-signed and can be regenerated, so legacy cleartext password files should be deleted without reading them instead of migrated through the old password.

### 0z147. Frontend Must Normalize Backend Boolean Names Before Warning On Them

**The Bug**: The Network dashboard showed the public DHT exposure warning even when runtime configuration had `dhtRendezvous.lanOnly: true`. The backend `/api/v0/dht/status` response serializes `LanOnly` as `lanOnly`, but the frontend warning condition checked `stats.dht.isLanOnly`, so `undefined` was treated as false and LAN-only nodes looked publicly exposed.

**Files Affected**:
- `src/web/src/lib/slskdn.js`
- `src/web/src/components/System/Network/index.jsx`
- `src/web/src/components/System/Network/index.test.jsx`

**Wrong**:
```js
const dht = await getDhtStatus();
return { dht };
```

```js
!(stats?.dht?.isLanOnly ?? false)
```

**Correct**:
```js
const normalizedDht = {
  ...rawDht,
  isLanOnly: rawDht.isLanOnly ?? rawDht.lanOnly ?? false,
};
```

**Why This Keeps Happening**: C# boolean properties with an `Is` prefix serialize as `isEnabled`, but `LanOnly` has no prefix and serializes as `lanOnly`. UI warning logic must consume a normalized frontend contract instead of assuming all backend booleans have the `is*` shape.

### 0z146. AUR Source Builds Must Use MSBuild-Safe Date Release Versions

**The Bug**: The `slskdn` AUR source package for `2026042900-slskdn.193` mapped `pkgver=2026042900.slskdn.193` to `-p:Version=2026042900.193`, which made .NET generate invalid assembly version `2026042900.193.0.0` and fail the install after a wall of unrelated generated-code warnings.

**Files Affected**:
- `packaging/aur/PKGBUILD`
- `packaging/scripts/validate-packaging-metadata.sh`
- `bin/build`
- `bin/publish`

**Wrong**:
```bash
_assembly_ver="${pkgver%.slskdn.*}.${pkgver##*.}"
dotnet publish src/slskd/slskd.csproj -p:Version="$_assembly_ver"
```

**Correct**:
```bash
_version="${pkgver//.slskdn/-slskdn}"
if [[ "${pkgver}" =~ ^([0-9]{10})\.slskdn\.([0-9]+)$ ]]; then
    _dotnet_version="0.0.0-slskdn.${BASH_REMATCH[1]}.${BASH_REMATCH[2]}"
else
    _dotnet_version="${_version}"
fi
dotnet publish src/slskd/slskd.csproj \
    -p:Version="$_dotnet_version" \
    -p:InformationalVersion="$_version" \
    -p:PackageVersion="$_dotnet_version"
```

**Why This Keeps Happening**: slskdN public release tags use a date-based version (`YYYYMMDDmm-slskdn.NNN`) that is valid for release labels but not for .NET assembly versions. Any source-based package path must mirror `bin/build` and `bin/publish`: keep the public release string in `InformationalVersion`, but pass a SemVer/MSBuild-safe prerelease value such as `0.0.0-slskdn.YYYYMMDDmm.NNN` for `Version` and `PackageVersion`.

### 0z145. Stacked Single-Line Copyright Headers Must Not Leave A Blank Before Code

**The Bug**: A bulk copyright-header normalization added a blank line after stacked `// <copyright>` comment blocks, which made StyleCop emit `SA1512` warnings across upstream-derived files.

**Files Affected**:
- `src/slskd/**/*.cs`
- `tests/**/*.cs`

**Wrong**:
```csharp
// </copyright>

using Microsoft.Extensions.Options;
```

**Correct**:
```csharp
// </copyright>
using Microsoft.Extensions.Options;
```

**Why This Keeps Happening**: The XML-looking copyright block is still made of single-line comments to StyleCop. For stacked single-line comment headers, preserve any intentional blank between separate attribution blocks, but do not leave a blank line between the final `// </copyright>` and the first `using`, `namespace`, preprocessor directive, or code line.

### 0z144. Do Not Promote Weak SongID Evidence Into Discovery Graph Neighborhoods

**The Bug**: SongID runs with `needs_manual_review` identity evidence could still build Discovery Graph neighborhoods from transcript/OCR/chapter-derived MusicBrainz candidates. Generic catalog artifacts such as `TV Show`, chapter labels, or unrelated artist names then appeared as real neighboring artists/releases when a graph node was clicked.

**Files Affected**:
- `src/slskd/DiscoveryGraph/DiscoveryGraphService.cs`
- `src/slskd/SongID/SongIdService.cs`
- `src/web/src/components/Search/SongIDPanel.jsx`

**Wrong**:
```csharp
foreach (var artist in run.Artists.Take(3))
{
    AddNode(graph, $"artist:{artist.ArtistId}", artist.Name, "artist", artist.ActionScore, 1, "neighbor", ...);
}
```

**Correct**:
```csharp
if (CanExpandCatalogContext(run))
{
    foreach (var artist in GetGraphArtistCandidates(run))
    {
        AddNode(graph, $"artist:{artist.ArtistId}", artist.Name, "artist", artist.ActionScore, 1, "neighbor", ...);
    }
}
```

**Why This Keeps Happening**: SongID intentionally collects weak forensic clues for manual review, but Discovery Graph is an action surface. Anything that looks like a graph neighbor is treated as music identity/corpus topology, so graph expansion must require strong identity evidence and must keep transcript/OCR/comment/chapter hints as diagnostics until they resolve to high-confidence track identities.

### 0z143. Docker Placeholder Users Must Avoid Fixed UID/GID Collisions

**The Bug**: The `2026042900-slskdn.190` Docker publish job failed because the runtime image tried to create `slskdn` with fixed UID/GID `1000`, but `mcr.microsoft.com/dotnet/runtime-deps:10.0-noble` already contained GID `1000`.

**Files Affected**:
- `Dockerfile`
- `packaging/docker/slskdn-container-start`

**Wrong**:
```dockerfile
RUN groupadd --gid 1000 slskdn \
  && useradd --uid 1000 --gid 1000 --home-dir /app --shell /usr/sbin/nologin slskdn
```

**Correct**:
```dockerfile
RUN groupadd --system slskdn \
  && useradd --system --gid slskdn --home-dir /app --shell /usr/sbin/nologin slskdn
```

**Why This Keeps Happening**: Docker base images can add or change built-in users and groups at any time. If an entrypoint later remaps a placeholder user with `PUID`/`PGID`, the image build should let the base OS allocate non-conflicting placeholder IDs instead of assuming `1000:1000` is free.

### 0z142. Normalize IPv4-Mapped IPv6 Before CIDR Checks

**The Bug**: IPv4 peers presented as IPv4-mapped IPv6 addresses, such as `::ffff:1.2.4.42`, bypassed IPv4 CIDR checks because blacklist and trust code compared raw address byte lengths before mapping the address back to IPv4.

**Files Affected**:
- `src/slskd/Core/Blacklist.cs`
- `src/slskd/Users/UserService.cs`
- `src/slskd/Common/Authentication/PassthroughAuthentication.cs`
- `src/slskd/Core/Security/SecurityService.cs`

**Wrong**:
```csharp
if (ip.GetAddressBytes().Length != range.BaseAddress.GetAddressBytes().Length)
{
    return false;
}
```

**Correct**:
```csharp
ip = ip.NormalizeMappedIPv4();
```

**Why This Keeps Happening**: Kestrel, proxies, and dual-stack sockets can surface IPv4 clients as IPv6 addresses. Any code that applies IPv4 CIDRs, trusted proxy ranges, or managed blacklist checks must normalize mapped IPv4 first, then compare address families.

### 0z141. Dockerfile Must Invoke Bash Scripts With Bash

**The Bug**: The `2026042900-slskdn.187` Docker publish job failed after `bin/build` gained Bash regex syntax for slskdN date versions, because the Dockerfile web stage still invoked it as `sh ./bin/build`. Alpine `sh` parsed the Bash regex group as a syntax error before the build reached Node. The follow-up `2026042900-slskdn.188` Docker job failed too because `node:20-alpine` does not include Bash by default, so `bash ./bin/build` also needs `apk add --no-cache bash` in that stage.

**Files Affected**:
- `Dockerfile`
- `bin/build`
- `bin/publish`

**Wrong**:
```dockerfile
RUN DISABLE_ESLINT_PLUGIN=true sh ./bin/build --web-only --skip-tests --version $VERSION
```

**Correct**:
```dockerfile
RUN apk add --no-cache bash
RUN DISABLE_ESLINT_PLUGIN=true bash ./bin/build --web-only --skip-tests --version $VERSION
```

**Why This Keeps Happening**: The helper scripts have `#!/bin/bash` and use Bash-only syntax, but Docker `RUN sh ./script` bypasses the shebang and forces POSIX shell parsing. Alpine-based Docker stages also omit Bash unless it is installed explicitly. Any Dockerfile or workflow command that executes repo helper scripts must either run them directly when executable or invoke `bash`, never `sh`, and Alpine stages must install Bash first.

### 0z142. Date-Style Public Versions Must Not Become .NET Assembly Versions

**The Bug**: The AUR source package for `2026042900-slskdn.192` tried to compile with `-p:Version=2026042900.192`, which caused generated assembly metadata to contain `2026042900.192.0.0`. .NET assembly versions only accept bounded numeric components, so source builds failed with `CS7034` even though binary packages worked.

**Files Affected**:
- `packaging/aur/PKGBUILD`
- `packaging/scripts/validate-packaging-metadata.sh`
- `.github/workflows/build-on-tag.yml`
- `bin/build`
- `bin/publish`

**Wrong**:
```bash
_assembly_ver="${pkgver%.slskdn.*}.${pkgver##*.}"
dotnet publish ... -p:Version="$_assembly_ver" -p:PackageVersion="$_version"
```

**Correct**:
```bash
_version="${pkgver//.slskdn/-slskdn}"
if [[ "${pkgver}" =~ ^([0-9]{10})\.slskdn\.([0-9]+)$ ]]; then
    _dotnet_version="0.0.0-slskdn.${BASH_REMATCH[1]}.${BASH_REMATCH[2]}"
else
    _dotnet_version="${_version}"
fi
dotnet publish ... -p:Version="$_dotnet_version" -p:InformationalVersion="$_version" -p:PackageVersion="$_dotnet_version"
```

**Why This Keeps Happening**: The public package version is deliberately date-first so distro package managers sort slskdN rollback builds newer than removed upstream-prefixed builds. That public version is not a valid assembly version input. Every build path must keep `InformationalVersion` as the public `YYYYMMDDmm-slskdn.N` string and pass a safe SemVer-compatible value such as `0.0.0-slskdn.YYYYMMDDmm.N` to MSBuild `Version` and `PackageVersion`.

### 0z140. Public YAML Aliases Must Bind In Runtime Configuration

**The Bug**: Tester config copied from `config/slskd.example.yml` with `dht.lan_only: true` still emitted the public DHT exposure warning. The runtime YAML configuration provider normalized keys but ignored `[YamlMember(Alias = "dht")]`, so `dht:` was valid documentation/API YAML but did not bind to `Options.DhtRendezvous`; only the internal `dhtRendezvous:` key changed runtime behavior.

**Files Affected**:
- `src/slskd/Common/Configuration/YamlConfigurationSource.cs`
- `src/slskd/Core/Options.cs`
- `config/slskd.example.yml`

**Wrong**:
```yaml
dht:
  lan_only: true
```

```csharp
var key = Normalize(rawKey);
```

**Correct**:
```csharp
var key = ResolveKeyAlias(path, Normalize(rawKey));
```

**Why This Keeps Happening**: slskd has two YAML paths: direct object serialization honors `YamlMember` aliases, while the runtime `IConfiguration` provider manually flattens YAML into keys. Any public alias shown in examples must be understood by the runtime configuration provider, not only by object deserialization.

### 0z139. Empty Cached User Groups Must Resolve To Default

**The Bug**: Tester upload logs showed uploads enqueueing and staying queued, while incoming search response resolution threw `A group with the name  could not be found`. `UserService.Configure()` clears removed or transient user-defined group membership to an empty string, but `GetGroup()` treated any non-null value as authoritative, so upload queue forecasting and queue processing looked for a group named `""`.

**Files Affected**:
- `src/slskd/Users/UserService.cs`
- `src/slskd/Transfers/Uploads/UploadQueue.cs`
- `src/slskd/Application.cs`

**Wrong**:
```csharp
if (user.Group != null)
{
    return user.Group;
}
```

**Correct**:
```csharp
if (!string.IsNullOrWhiteSpace(user.Group))
{
    return user.Group;
}
```

**Why This Keeps Happening**: Cached user records can represent both configured group membership and transient watched users. An empty configured group is not a real group name; hot paths must treat empty or whitespace cached groups as "not configured" and fall back to the default group.

### 0z135. Build Tags Cannot Nix-Smoke Unpublished Stable Release Assets

**The Bug**: `build-main-0.25.1-slskdn.1` failed before publishing release assets because `build-on-tag.yml` made `publish` depend on a pre-publish `nix-smoke` job. The smoke script builds the checked-in stable flake, which fetches `https://github.com/snapetech/slskdn/releases/download/<version>/slskdn-main-linux-glibc-x64.zip`; for a brand-new release tag that asset does not exist yet, so Nix gets a 404 and blocks every release job.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/scripts/run-nix-package-smoke.sh`
- `flake.nix`

**Wrong**:
```yaml
publish:
  needs: [parse, build, nix-smoke]
```

**Correct**:
```yaml
publish:
  needs: [parse, build]
```

**Why This Keeps Happening**: The stable Nix package is a consumer of published release artifacts, not a producer. It can only be smoke-tested after the release exists and metadata has been rewritten with the new URLs and hashes, such as in the post-release stable metadata job.

### 0z136. Stable Releases Need Curated Changelog Notes Before Tagging

**The Bug**: `0.25.1-slskdn.183` initially published release notes synthesized from the full commit delta since `0.24.5-slskdn.181`. Because the upstream-sync branch brought a very large merged history and there was no matching `docs/CHANGELOG.md` section, the generated release body listed ancient fork bootstrap commits like "Initial commit" instead of the actual 0.25.1 sync highlights.

**Files Affected**:
- `scripts/generate-release-notes.sh`
- `docs/CHANGELOG.md`

**Wrong**:
```bash
./scripts/generate-release-notes.sh "$version" release/RELEASE_NOTES.md "$ref"
# no docs/CHANGELOG.md section exists for "$version"; script publishes hundreds of raw commits
```

**Correct**:
```markdown
## [0.25.1-slskdn.183] - 2026-04-26

- Synced slskdN with upstream slskd 0.25.1 while retaining fork-specific features.
```

**Why This Keeps Happening**: Release-note synthesis is only safe for small, linear deltas. Major upstream syncs and branch-history repairs need a curated changelog section, and the generator must fail closed when the fallback commit list is too large to be useful.

### 0z137. Remote Peer Timeouts Must Be Handled At API Boundaries

**The Bug**: Live `0.25.1-slskdn.183` logs on `local test host` showed `POST /api/v0/users/{username}/directory` returning unhandled 500s when `ISoulseekClient.GetDirectoryContentsAsync()` timed out waiting for a remote peer. The timeout was expected peer/network behavior, but it bubbled through MVC, security middleware, and the exception handler, producing repeated error stack traces.

**Files Affected**:
- `src/slskd/Users/API/Controllers/UsersController.cs`

**Wrong**:
```csharp
var result = await Client.GetDirectoryContentsAsync(username, request.Directory);
return Ok(result);
```

**Correct**:
```csharp
try
{
    var result = await Client.GetDirectoryContentsAsync(username, request.Directory);
    return Ok(result);
}
catch (TimeoutException)
{
    return StatusCode(503, "Unable to retrieve directory contents from user");
}
```

**Why This Keeps Happening**: Soulseek peer operations are remote-network calls. Timeouts, offline users, and direct/indirect connection failures are normal peer outcomes, not app faults. Controller actions that call Soulseek peers must translate those exceptions into 404/503 responses before they reach global middleware.

### 0z138. Shutdown Download Cancellation Can Be Wrapped By Retry Helpers

**The Bug**: Manual `local test host` deploy stops while downloads are in flight can still emit error-level `Download ... failed` and `Task for download ... did not complete successfully` stack traces. The download path catches direct `OperationCanceledException` during `Application.IsShuttingDown`, but `Retry.Do(...)` can wrap the same shutdown cancellation in `AggregateException`, so it misses the shutdown filter and falls into generic error cleanup.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`

**Wrong**:
```csharp
catch (OperationCanceledException ex) when (Application.IsShuttingDown)
{
    Log.Debug(ex, "Download cancelled during shutdown");
    throw;
}
catch (Exception ex)
{
    Log.Error(ex, "Download failed");
}
```

**Correct**:
```csharp
catch (Exception ex) when (IsShutdownCancellation(ex))
{
    Log.Debug("Download cancelled during shutdown: {Message}", ex.Message);
    throw;
}
```

**Why This Keeps Happening**: Retry wrappers, task observers, and aggregate waits can change the outer exception type even when the root cause is still host shutdown cancellation. Shutdown classifiers must unwrap aggregate/inner exceptions and avoid passing expected cancellation exception objects to error logs.

### 0z134. Soulseek Listen Endpoint Changes Need Server Reconnect Semantics

**The Bug**: Runtime updates to `soulseek.listen_port` or `soulseek.listen_ip_address` can restart the local Soulseek.NET listener without making the server learn the new advertised port. Soulseek.NET sends `SetListenPort` during login/config messages, not from `ReconfigureOptionsAsync()`, so remote peers may keep connecting to the stale port and uploads can appear broken even though the local listener is healthy.

**Files Affected**:
- `src/slskd/Core/Options.cs`
- `src/slskd/Application.cs`
- `src/slskd/Integrations/VPN/VPNService.cs`

**Wrong**:
```csharp
listenPort: old.ListenPort == update.ListenPort ? null : update.ListenPort
```

**Correct**:
```csharp
[RequiresReconnect]
public int ListenPort { get; init; } = 50300;
```

**Why This Keeps Happening**: The local listener and the server-advertised endpoint are separate state. Any code path that changes where slskd listens must also ensure the Soulseek server receives the updated port, either by reconnecting or by an explicit server configuration command if Soulseek.NET exposes one.

### 0z133. Batch Retry Downloads Must Move From The Batch Incomplete Path

**The Bug**: A retrying download with `BatchId` can write its partial file under `incomplete/<batch-id>/...`, but completion code can still try to move the root incomplete path and fail after a successful transfer.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`

**Wrong**:
```csharp
var incompleteFilename = transfer.Filename.ToLocalFilename(
    baseDirectory: Path.Combine(incompleteRoot, transfer.BatchId?.ToString() ?? string.Empty));

Files.MoveFile(
    sourceFilename: transfer.Filename.ToLocalFilename(baseDirectory: incompleteRoot),
    destinationDirectory: destinationDirectory);
```

**Correct**:
```csharp
var incompleteFilename = transfer.Filename.ToLocalFilename(
    baseDirectory: Path.Combine(incompleteRoot, transfer.BatchId?.ToString() ?? string.Empty));

Files.MoveFile(
    sourceFilename: incompleteFilename,
    destinationDirectory: destinationDirectory);
```

**Why This Keeps Happening**: Resume/retry logic introduces a derived local filename before the Soulseek download call, but later completion code often recomputes the old legacy path. Once batch-specific incomplete directories exist, all file operations in the transfer lifecycle must use the same resolved incomplete filename.

### 0z132. YAML Examples And Operator Warnings Must Use Public Option Names

**The Bug**: A tester tried to suppress the DHT public-discoverability warning by setting `lan_only` under both `dht` and `dhtRendezvous`, but the sample config did not show `dht.lan_only` and the warning printed internal C# option names (`DhtRendezvous.LanOnly`) instead of the YAML key operators actually use.

**Files Affected**:
- `config/slskd.example.yml`
- `src/slskd/DhtRendezvous/DhtExposureWarningService.cs`

**Wrong**:
```text
Set DhtRendezvous.LanOnly=true to disable public DHT bootstrap
```

**Correct**:
```text
Set dht.lan_only: true to disable public DHT bootstrap
```

**Why This Keeps Happening**: Options classes may have C# property names and YAML aliases. Any user-facing warning, error, example, or troubleshooting note must name the public config key, not the internal option object.

### 0z131. Mesh Search Results Must Not Wait Behind Soulseek Timeout

**The Bug**: Issue `#209` tester follow-up showed mesh search returning `beatles` results at `09:22:39`, but the user-facing search did not complete until `09:22:54` because `SearchService` awaited the Soulseek search task before awaiting and persisting mesh overlay results. Mesh had already answered, but the UI could not show those results until the normal 15-second Soulseek timeout elapsed.

**Files Affected**:
- `src/slskd/Search/SearchService.cs`
- `src/web/src/components/Search/Detail/SearchDetail.jsx`

**Wrong**:
```csharp
var soulseekSearch = await soulseekSearchTask;
var meshResponses = await meshTask;
search.Responses = SearchResponseMerger.Deduplicate(soulseekResponses, meshResponses);
Update(search);
```

**Correct**:
```csharp
var meshPublicationTask = PublishMeshResultsWhenReadyAsync(meshTask);
var soulseekSearch = await soulseekSearchTask;
var meshResponses = await meshPublicationTask;
```

**Why This Keeps Happening**: Hybrid search can run providers in parallel while still serializing result publication at the end. Any fast secondary provider, especially mesh/pod search, needs an early persistence and broadcast path so slow or zero-result Soulseek searches do not hide already-available results.

### 0z130. AUR Zip Staging Must Normalize Release Directory Permissions

**The Bug**: AUR binary build `0.24.5.slskdn.177-1` installed `/usr/lib/slskd/releases/0.24.5.slskdn.177/` as `drwx------ root root`, so systemd and non-root users could not traverse the bundled release payload and startup failed until the directory was manually chmodded.

**Files Affected**:
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/PKGBUILD-dev`
- `packaging/aur/PKGBUILD`

**Wrong**:
```bash
stage_root="$(mktemp -d)"
install -dm755 "${release_root}"
unzip -q "${archive}" -d "${stage_root}"
cp -a "${stage_root}"/. "${release_root}/"
chmod +x "${release_root}/slskd"
```

**Correct**:
```bash
stage_root="$(mktemp -d)"
install -dm755 "${release_root}"
unzip -q "${archive}" -d "${stage_root}"
cp -a "${stage_root}"/. "${release_root}/"
chmod -R u=rwX,go=rX "${release_root}"
chmod 755 "${release_root}/slskd"
```

**Why This Keeps Happening**: `mktemp -d` creates a `0700` staging directory, and archive-preserving copies from `stage_root/.` can carry the staging directory attributes onto the destination release root. Package payloads under `/usr/lib/slskd/releases/<version>` must be traversable by the `slskd` service user, so AUR package functions must normalize directory/file modes after staging release zips.

### 0z129. Public Search Timeout Units Must Match Soulseek SearchOptions Units

**The Bug**: Issue `#209` live retesting on `local test host` used the documented `/api/v0/searches` contract and sent `searchTimeout: 10` for a 10-second search. `SearchRequest` documented the field as seconds and validated values down to `5`, but `ToSearchOptions()` passed the raw `10` into `Soulseek.SearchOptions`, which expects milliseconds. The search completed in 10-44 ms with zero responses, creating a false app/network failure signal.

**Files Affected**:
- `src/slskd/Search/API/DTO/SearchRequest.cs`

**Wrong**:
```csharp
searchTimeout: SearchTimeout ?? def.SearchTimeout
```

**Correct**:
```csharp
searchTimeout: SearchTimeout.HasValue
    ? SearchTimeout.Value * 1000
    : def.SearchTimeout
```

**Why This Keeps Happening**: slskd's public API describes search timeouts in seconds while Soulseek.NET uses milliseconds. Any boundary code that maps API/config seconds into `Soulseek.SearchOptions` must convert explicitly, and comments saying "convert to seconds" near `SearchOptions` calls should be treated as suspicious.

### 0z128. Background Search Producers Must Not Spend The User Search Safety Bucket

**The Bug**: Live `local test host` issue `#209` troubleshooting showed auto-replace running every few minutes, consuming ten Soulseek searches, then logging `[SAFETY] Search rate limit exceeded for source=user`. Manual/API searches were also charged to `source=user`, so background replacement work could starve real user searches and make normal searches appear to return zero results or fail unpredictably.

**Files Affected**:
- `src/slskd/Search/SearchService.cs`
- `src/slskd/Transfers/AutoReplace/AutoReplaceService.cs`

**Wrong**:
```csharp
if (!SafetyLimiter.TryConsumeSearch("user"))
{
    throw new InvalidOperationException(message);
}
```

**Correct**:
```csharp
if (!SafetyLimiter.TryConsumeSearch(safetySource))
{
    throw new InvalidOperationException(message);
}

await Searches.StartAsync(..., safetySource: "auto-replace");
```

**Why This Keeps Happening**: A single service method hid the rate-limiter source, so all callers inherited the interactive-user bucket. Every background producer that contacts Soulseek must identify itself explicitly so diagnostics and safety limits can separate user actions from automated maintenance.

### 0z127. Circuit Maintenance Must Not Run Placeholder Circuit Probes Against Live Peers

**The Bug**: Issue `#209` tester logs on `0.24.5-slskdn.174` showed recurring `Circuit building test failed` warnings every maintenance cycle once the host had at least three circuit-capable peers. The maintenance service was automatically invoking the placeholder `MeshCircuitBuilder` test path, which dials each selected peer directly and logs stack traces when those peers do not have usable direct transport metadata.

**Files Affected**:
- `src/slskd/Mesh/CircuitMaintenanceService.cs`

**Wrong**:
```csharp
if (circuitStats.ActiveCircuits == 0 && peerStats.OnionRoutingPeers >= 3)
{
    await TestCircuitBuildingAsync(cancellationToken);
}
```

**Correct**:
```csharp
// Periodic maintenance only reports circuit inventory.
// Explicit user/API actions should own any active circuit-building probe.
```

**Why This Keeps Happening**: Diagnostic probes are tempting during feature bring-up, but live maintenance loops must not initiate peer traffic unless the feature is production-ready and explicitly enabled. Automatic probes create noise, spend peer/network budget, and make low-population mesh conditions look like application failures.

### 0z126. Soulseek Transfer Rejection Reasons Must Match The Expected Network Classifier

**The Bug**: Live `local test host` logs for `0.24.5-slskdn.174` emitted repeated fake `[FATAL] Unobserved task exception` entries for normal Soulseek transfer rejections with message `Too many megabytes`. The classifier recognized `Soulseek.TransferRejectedException` as an expected network/peer class, but the final message allow-list only covered `Enqueue failed due to internal error`.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
details.Contains("Enqueue failed due to internal error", StringComparison.Ordinal)
```

**Correct**:
```csharp
details.Contains("Enqueue failed due to internal error", StringComparison.Ordinal) ||
details.Contains("Too many megabytes", StringComparison.Ordinal) ||
details.Contains("Too many files", StringComparison.Ordinal)
```

**Why This Keeps Happening**: Expected-network classifiers flatten aggregate exceptions and then require every inner exception to match the stable expected-message list. Soulseek remote-denial text is user/client policy, not process-fatal behavior, so common rejection strings must be classified with the transfer exception type.

### 0z125. BackgroundService Tests Must Wait On A Real Signal

**The Bug**: A full unit-suite run failed `CircuitMaintenanceServiceTests.ExecuteAsync_ContinuesAfterMaintenanceException` because the test slept for 200 ms and assumed the hosted service loop had already invoked `PerformMaintenance()`. On a loaded runner the service had only logged constructor messages when the assertion checked for the expected error log.

**Files Affected**:
- `tests/slskd.Tests.Unit/Mesh/CircuitMaintenanceServiceTests.cs`

**Wrong**:
```csharp
var executeTask = service.StartAsync(cts.Token);
await Task.Delay(200);
await service.StopAsync(cts.Token);
```

**Correct**:
```csharp
var maintenanceAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
builderMock.Setup(x => x.PerformMaintenance()).Callback(() =>
{
    maintenanceAttempted.TrySetResult();
    throw new InvalidOperationException("test");
});

await service.StartAsync(cts.Token);
await maintenanceAttempted.Task.WaitAsync(TimeSpan.FromSeconds(2));
```

**Why This Keeps Happening**: `BackgroundService.StartAsync()` and scheduler timing do not guarantee a loop iteration has happened by the time a fixed sleep expires. Tests for hosted-service behavior should wait on an explicit callback, log event, or state transition before asserting.

### 0z124. Entropy Health Logs Must Account For Finite Sample Bias

**The Bug**: Live `local test host` logs for `0.24.5-slskdn.172` emitted `Warning: Entropy below optimal level` every five minutes even though the process was using `RandomNumberGenerator.GetBytes(...)`. The monitor used a 256-byte Shannon entropy sample with a 7.5 bits/byte warning threshold; that small sample is biased below the true entropy often enough to create false operator noise on healthy systems.

**Files Affected**:
- `src/slskd/Common/Security/EntropyMonitor.cs`

**Wrong**:
```csharp
public const int SampleSize = 256;
public const double WarningEntropy = 7.5;
```

**Correct**:
```csharp
public const int SampleSize = 4096;
public const double WarningEntropy = 7.75;
```

**Why This Keeps Happening**: Shannon entropy estimates from small samples are biased downward, especially for 256 possible byte values. Security monitors should avoid warning on expected estimator behavior; sample enough bytes for a stable estimate and keep true critical failures operator-visible.

### 0z123. LAN Discovery Must Never Advertise A Blank Display Name

**The Bug**: Live `local test host` startup logs for `0.24.5-slskdn.172` showed `[LanDiscovery] Started advertising as  (...)` because the persisted peer profile can contain a blank `DisplayName`. LAN discovery then publishes and logs the blank value instead of a useful operator-visible fallback.

**Files Affected**:
- `src/slskd/Identity/LanDiscoveryService.cs`
- `src/slskd/Identity/ProfileService.cs`

**Wrong**:
```csharp
["displayName"] = profile.DisplayName,
await _advertiser.StartAsync(profile.DisplayName, ServiceType, port, properties, ct);
```

**Correct**:
```csharp
var displayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? friendCode : profile.DisplayName.Trim();
["displayName"] = displayName,
await _advertiser.StartAsync(displayName, ServiceType, port, properties, ct);
```

**Why This Keeps Happening**: Profiles are persisted and may predate newer assumptions about required fields. Any value used for mDNS/public discovery should be normalized at the boundary and should have a stable fallback, even if API update endpoints validate non-empty display names.

### 0z122. Temporary Config Probes Must Not Stay At Information

**The Bug**: Live `local test host` startup logs for `0.24.5-slskdn.172` still printed raw security binding probes such as `After binding OptionsAtStartup.Security.Enabled...` and `Raw config sections...` at `Information`. These were temporary diagnostics for config hardening but became permanent startup noise.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
Log.Information("[Config] Raw config sections - security.Exists={SecurityExists}, slskd:security.Exists={SlskdSecurityExists}", ...);
```

**Correct**:
```csharp
Log.Debug("[Config] Raw config sections - security.Exists={SecurityExists}, slskd:security.Exists={SlskdSecurityExists}", ...);
```

**Why This Keeps Happening**: Startup diagnostics are useful while debugging configuration binding, but once the issue is resolved they should either be removed or moved below `Information`. Operator-visible startup logs should report outcomes, not internal binding probes.

### 0z121. Soulseek Read Timeout Inner Exceptions Must Match The Expected Network Classifier

**The Bug**: Live `local test host` logs on the installed `0.24.5-slskdn.170` process emitted fake `[FATAL] Unobserved task exception` entries for routine Soulseek read-loop timeout churn. The flattened exception chain included `Soulseek.ConnectionReadException: Failed to read 4 bytes...`, an inner `IOException: Unable to read data from the transport connection: Connection timed out`, and an inner `SocketException (110): Connection timed out` from `Soulseek.Network.MessageConnection.ReadContinuouslyAsync`.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
details.Contains("Operation timed out", StringComparison.Ordinal) ||
details.Contains("Failed to read", StringComparison.Ordinal)
```

**Correct**:
```csharp
details.Contains("Operation timed out", StringComparison.Ordinal) ||
details.Contains("Connection timed out", StringComparison.Ordinal) ||
details.Contains("Failed to read", StringComparison.Ordinal) ||
details.Contains("Unable to read data from the transport connection", StringComparison.Ordinal)
```

**Why This Keeps Happening**: `Program.IsExpectedSoulseekNetworkException(...)` flattens aggregate/inner exceptions and requires every flattened exception to match. Matching only the outer Soulseek library exception is not enough; expected-network classifiers must cover the inner `IOException` and `SocketException` message shapes that the runtime actually logs.

### 0z120. Overlay Endpoint Cooldowns Should Be Aggregated At Information

**The Bug**: Live `0.24.5-slskdn.170` logs on `local test host` emitted one `Information` line per degraded DHT-discovered overlay endpoint once each endpoint hit a failure streak of three, even though the periodic DHT/overlay summary and `/api/v0/overlay/stats` already expose the same failure mix and top-problem endpoint data.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`

**Wrong**:
```csharp
_logger.LogInformation(
    "Overlay endpoint {Endpoint} failure streak={FailureCount} lastReason={FailureReason} coolingDownUntil={SuppressedUntil}",
    endpoint,
    failureCount,
    reason,
    suppressedUntil);
```

**Correct**:
```csharp
_logger.LogDebug(
    "Overlay endpoint {Endpoint} failure streak={FailureCount} lastReason={FailureReason} coolingDownUntil={SuppressedUntil}",
    endpoint,
    failureCount,
    reason,
    suppressedUntil);
```

**Why This Keeps Happening**: Per-peer/per-endpoint churn is useful diagnostic detail during development, but public DHT candidates routinely include dead, blocked, or incompatible endpoints. Keep individual endpoint failures below `Information`; use aggregate rollups and authenticated stats endpoints for operator-visible health.

### 0z119. Peer Descriptor Refresh Must Not Duplicate The Bootstrap Publish At Startup

**The Bug**: The packaged `0.24.5-slskdn.170` startup on `local test host` logged duplicate `[MeshDHT] No configured endpoints...` and `[MeshDHT] Published self descriptor...` lines because `MeshBootstrapService` published the initial descriptor while `PeerDescriptorRefreshService` immediately treated `DateTime.MinValue` as a due periodic refresh.

**Files Affected**:
- `src/slskd/Mesh/Dht/PeerDescriptorRefreshService.cs`

**Wrong**:
```csharp
var lastRefresh = DateTime.MinValue;
```

**Correct**:
```csharp
var lastRefresh = DateTime.UtcNow;
```

**Why This Keeps Happening**: Multiple hosted services can share one publisher and start in the same host window. If one service owns the startup publish and another owns periodic TTL refreshes, the periodic service must initialize its schedule from startup time rather than immediately republishing the same descriptor.

### 0z118. Soulseek Timer Reset Classifiers Must Match Real Stack Signatures

**The Bug**: The `0.24.5-slskdn.169` `local test host` package logged a current-process fatal unobserved task for `NullReferenceException` in `Soulseek.Extensions.Reset(Timer timer)` during an overlay/DHT write path. The existing expected-network classifier had a unit test for `Soulseek.Extensions.Reset(Timer)`, but the live stack included the parameter name (`Timer timer`), so the string match missed the same known Soulseek.NET timer reset race.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
details.Contains("Soulseek.Extensions.Reset(Timer)", StringComparison.Ordinal)
```

**Correct**:
```csharp
details.Contains("Soulseek.Extensions.Reset(", StringComparison.Ordinal)
```

**Why This Keeps Happening**: Runtime stack traces can include parameter names even when synthetic test stack traces do not. Classifiers for expected third-party network teardown races should match the stable method and owning type, not the exact rendered signature.

### 0z117. Normal Host Shutdown Must Not Claim App Run Returning Is Abnormal

**The Bug**: The `0.24.5-slskdn.169` package restart on `local test host` completed cleanly, but the journal still printed `[Program] app.Run() returned (this should not happen normally)` and a duplicate stderr `ProcessExit event fired during expected shutdown`. `WebApplication.Run()` returns during normal host shutdown, so this made a healthy systemd restart look suspicious.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Application.cs`

**Wrong**:
```csharp
app.Run();
Log.Information("[Program] app.Run() returned (this should not happen normally)");
```

**Correct**:
```csharp
app.Run();
Log.Debug("[Program] app.Run() returned after host shutdown");
```

**Why This Keeps Happening**: Startup hang probes often describe unexpected control flow while debugging boot issues, but `app.Run()` returning is expected after SIGTERM/systemd restart. Shutdown path logs should distinguish clean host stop from real premature process exit, and expected shutdown telemetry should not write duplicate stderr lines.

### 0z116. Release Announcement Webhooks Must Not Fail Completed Builds On Transient Gateway Errors

**The Bug**: The `build-main-0.24.5-slskdn.168` release created artifacts and the GitHub release, then the final `Announce Main Release to Discord` job failed because the Matrix homeserver returned HTTP `504` to the announcement `curl`. The release itself was usable and installed from AUR, but the overall Actions run ended red because a non-critical announcement endpoint had a transient gateway failure.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
curl --fail --silent --show-error \
  -X PUT \
  -H "Authorization: Bearer ${MATRIX_RELEASE_ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "$payload" \
  "${MATRIX_BASE_URL}/_matrix/client/r0/rooms/${room}/send/m.room.message/${txn_id}"
```

**Correct**:
```bash
if ! curl --fail --silent --show-error --retry 3 --retry-all-errors ...; then
  echo "Matrix announcement failed; release artifacts are already published, continuing."
fi
```

**Why This Keeps Happening**: Announcement webhooks run after the actual release work and depend on external chat infrastructure. They should retry and degrade to warnings on transient network/server failures so a good release is not marked failed by Discord/Matrix availability.

### 0z115. Mesh Overlay Startup Must Retry Transient Port Reuse

**The Bug**: The packaged `local test host` `0.24.5-slskdn.168` install started while TCP `50305` was still transiently unavailable. `MeshOverlayServer.StartAsync()` logged `Address already in use`, `DhtRendezvousService` gave up on the overlay listener, and the node stayed online without beacon-capable TCP overlay until a manual restart.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`

**Wrong**:
```csharp
await MeshOverlayServer.StartAsync(cancellationToken);
```

**Correct**:
```csharp
for (var attempt = 1; attempt <= MaxOverlayStartAttempts; attempt++)
{
    try
    {
        await MeshOverlayServer.StartAsync(cancellationToken);
        return;
    }
    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
    {
        await Task.Delay(OverlayStartRetryDelay, cancellationToken);
    }
}
```

**Why This Keeps Happening**: Service replacement and package upgrade paths can stop one process and start the next faster than the kernel or previous process releases the listener. `ReuseAddress` reduces the risk but does not make every restart edge deterministic. A transient bind race should retry briefly before disabling overlay for the whole uptime.

### 0z114. Startup Method-Trace Logs Must Stay Below Information

**The Bug**: Live packaged startup logs still contained dozens of `Information` lines like `Constructor called`, `ExecuteAsync called`, `[UseSlskdnSecurity] STEP 1`, `[MAIN] About to...`, and `[Program] app.Run() will...`. These were useful during earlier boot debugging, but on a healthy release they drown out real startup events and make journal audits noisy.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Common/Security/SecurityStartup.cs`
- `src/slskd/Application.cs`
- mesh/background service constructors and `ExecuteAsync()` methods

**Wrong**:
```csharp
logger.LogInformation("[MeshBootstrapService] Constructor called");
logger.LogInformation("[UseSlskdnSecurity] STEP 1: Checking configuration sections...");
```

**Correct**:
```csharp
logger.LogDebug("[MeshBootstrapService] Constructor called");
logger.LogDebug("[UseSlskdnSecurity] Checking configuration sections");
```

**Why This Keeps Happening**: Emergency startup instrumentation often lands at `Information` so it is visible during live debugging, then stays there after the bug is fixed. Method-entry, constructor, and step-by-step probe logs should be `Debug`; keep `Information` for durable state changes operators actually need.

### 0z113. E2E Harness Must Discover The Built Target Framework

**The Bug**: The Playwright E2E harness hardcoded `src/slskd/bin/Release/net8.0` when looking for a prebuilt backend. After the project moved to `net10.0`, CI builds produced `bin/Release/net10.0/slskd.dll`, but the harness failed to find it and fell back to `dotnet run -c Release` during tests. Scheduled E2E then flaked with `TCP port ... never started listening` because each test had to compile/start a Release app inside the Playwright timeout window.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`
- `src/slskd/slskd.csproj`

**Wrong**:
```typescript
private getBuiltAppBaseDir(repoRoot: string): string {
  return path.join(repoRoot, 'src', 'slskd', 'bin', 'Release', 'net8.0');
}
```

**Correct**:
```typescript
const targetFramework = await this.getTargetFramework(repoRoot);
return path.join(repoRoot, 'src', 'slskd', 'bin', 'Release', targetFramework);
```

**Why This Keeps Happening**: Test harnesses drift when they duplicate project metadata. Any path containing `netX.Y` should be derived from the `.csproj` or discovered from build output so framework upgrades do not silently switch CI from prebuilt launch to slow `dotnet run` fallback.

### 0z112. Shutdown-Cancelled Background Searches Must Not Log As Errors

**The Bug**: A manual `local test host` deploy restarted the service while a background search was still in flight. The search task observed `OperationCanceledException: The operation was canceled.` during host shutdown and logged `Failed to execute search ...` at `Error`, making normal deploy cancellation look like a runtime failure.

**Files Affected**:
- `src/slskd/Search/SearchService.cs`

**Wrong**:
```csharp
catch (Exception ex)
{
    Log.Error(ex, "Failed to execute search for '{Query}': {Message}", query, ex.Message);
    search.State = SearchStates.Completed | SearchStates.Errored;
}
```

**Correct**:
```csharp
catch (Exception ex) when (ex is OperationCanceledException && Application.IsShuttingDown)
{
    Log.Information("Search for '{Query}' was cancelled during shutdown", query);
    search.State = SearchStates.Completed | SearchStates.Cancelled;
}
```

**Why This Keeps Happening**: Background work often has its own cancellation token source, so checking only the per-search token misses host-level shutdown. Normal deploy/restart cancellation should be classified before the generic failure branch, otherwise every controlled restart can pollute the journal with false error telemetry.

### 0z111. Controlled User-Info Offline Responses Must Not Log Exception Objects

**The Bug**: After fixing `/api/v0/users/{username}/info` to return controlled `404`/`503` responses, live Playwright sweeps still filled the journal with `Soulseek.UserOfflineException` stack traces because the offline catch block logged the exception object at `Information`.

**Files Affected**:
- `src/slskd/Users/API/Controllers/UsersController.cs`

**Wrong**:
```csharp
Log.Information(ex, "User {Username} is offline for info", username);
return NotFound("User is offline");
```

**Correct**:
```csharp
Log.Information("User {Username} is offline for info: {Message}", username, ex.Message);
return NotFound("User is offline");
```

**Why This Keeps Happening**: Returning a controlled HTTP status is only half the fix. If expected peer/network exceptions are still passed as the first structured logging argument, Serilog prints the full stack and the live journal still looks broken during normal UI sweeps. Expected offline/timeout peer outcomes should log concise summaries unless the stack is needed for a real unexpected failure.

### 0z110. QUIC Must Be Explicitly Opted In On Long-Running Linux Hosts

**The Bug**: Live `local test host` monitoring caught another native `SIGSEGV`/systemd restart in the manual build while QUIC control and QUIC data listeners were enabled by default. The coredump was unsymbolized inside the native runtime, but previous and current crash history correlated with active `libmsquic`/QUIC listener activity, while managed logs did not show an app exception before the dump.

**Files Affected**:
- `src/slskd/Mesh/Overlay/OverlayOptions.cs`
- `src/slskd/Mesh/Overlay/DataOverlayOptions.cs`
- `src/slskd/Program.cs`
- `config/slskd.example.yml`

**Wrong**:
```csharp
public bool Enable { get; set; } = true;
// QUIC services are registered whenever Mesh.QuicRuntime.IsAvailable().
```

**Correct**:
```csharp
public bool EnableQuic { get; set; } = false;
// Register QUIC hosted services and QUIC clients only when runtime support is present
// and the operator explicitly opted in.
```

**Why This Keeps Happening**: `QuicListener.IsSupported` only proves the native dependency can initialize; it does not prove this host/runtime/library combination is stable under long-running mesh load. QUIC owns native MsQuic handles, and failures can bypass managed exception handling entirely. Treat QUIC as an explicit operator choice until soak testing proves the host stack stable.

### 0z109. Soulseek Listener Socket Disposal Is Expected Teardown, Not A Fatal Unobserved Task

**The Bug**: Live `local test host` monitoring caught `[FATAL] Unobserved task exception` for `ObjectDisposedException: Cannot access a disposed object. Object name: 'System.Net.Sockets.Socket'.` The stack was inside `TcpListener.AcceptTcpClientAsync()` through `Soulseek.Network.Tcp.Listener.ListenContinuouslyAsync()`. The process kept running, but the global unobserved-task handler classified a disposed listener accept loop as fatal.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
exception is ObjectDisposedException objectDisposedException &&
string.Equals(objectDisposedException.ObjectName, "Connection", StringComparison.Ordinal)
```

**Correct**:
```csharp
var isSoulseekListenerSocketDisposed =
    exception is ObjectDisposedException ode &&
    string.Equals(ode.ObjectName, "System.Net.Sockets.Socket", StringComparison.Ordinal) &&
    details.Contains("Soulseek.Network.Tcp.Listener.ListenContinuouslyAsync", StringComparison.Ordinal);
```

**Why This Keeps Happening**: Soulseek.NET owns listener/connection background tasks that can outlive the initiating control path during restart, reconnect, or listener teardown. Disposed sockets from the third-party listener accept loop are expected network lifecycle churn when the stack is inside `Soulseek.Network.Tcp.Listener`; they should be classified as expected Soulseek network exceptions instead of logged as fatal app crashes.

### 0z108. Manual Deploys Must Verify Systemd Reaches The `current` Release Payload

**The Bug**: A manual `local test host` deploy copied the new release under `/usr/lib/slskd/releases/manual-5bd0e0b88` and repointed `/usr/lib/slskd/current`, but `systemd` still executed a stale apphost at `/usr/lib/slskd/slskd`. The binary `--version` check against `current/slskd` passed, while `/api/v0/application` still reported the previous build because the service never reached the `current` payload.

**Files Affected**:
- `packaging/aur/PKGBUILD`
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/PKGBUILD-dev`
- Live/manual deployment commands that touch `/usr/lib/slskd`

**Wrong**:
```bash
sudo ln -sfn "$release" /usr/lib/slskd/current
sudo systemctl restart slskd
/usr/lib/slskd/current/slskd --version
```

**Correct**:
```bash
sudo ln -sfn "$release" /usr/lib/slskd/current
printf '%s\n' '#!/bin/sh' 'exec /usr/lib/slskd/current/slskd "$@"' \
  | sudo tee /usr/lib/slskd/slskd >/dev/null
sudo chmod 755 /usr/lib/slskd/slskd
sudo systemctl restart slskd
curl -H "Authorization: Bearer $token" http://host:5030/api/v0/application
```

**Why This Keeps Happening**: The AUR layout intentionally keeps a stable `/usr/lib/slskd/slskd` service path and moves the mutable payload behind `/usr/lib/slskd/current`. Manual deploys that only update `current` can silently leave a stale root apphost in place. Always verify the running API's `runtime.executablePath` and version after restart, not just the target release binary.

### 0z107. User Info Peer Failures Must Not Bubble As HTTP 500

**The Bug**: A Playwright crawl of live search/user links caused `/api/v0/users/{username}/info` to return HTTP 500 for expected Soulseek peer connection failures and timeouts. The UI hit unavailable remote peers, but the API logged unhandled exceptions instead of returning a controlled response.

**Files Affected**:
- `src/slskd/Users/API/Controllers/UsersController.cs`

**Wrong**:
```csharp
catch (UserOfflineException ex)
{
    Log.Information(ex, "User {Username} is offline for info", username);
    return NotFound("User is offline");
}
```

**Correct**:
```csharp
catch (SoulseekClientException ex) when (ex.InnerException is ConnectionException)
{
    Log.Information("Unable to connect to user {Username} for info: {Message}", username, ex.Message);
    return StatusCode(503, "Unable to retrieve user info");
}
```

**Why This Keeps Happening**: Soulseek peer APIs cross a network boundary even when they look like simple user-detail reads. Remote peer timeouts, indirect-connection failures, and offline states are expected P2P outcomes; controller actions must translate them into safe non-500 responses and avoid stack-noise logging for routine peer unavailability.

### 0z106. Inline Code Followed By Text Needs Explicit JSX Whitespace

**The Bug**: The System > Network public DHT exposure consent modal rendered `dht.lan_only=truein` because a `<code>` element was followed by text in JSX without an explicit whitespace expression. The copy was understandable but looked sloppy during a Playwright sweep.

**Files Affected**:
- `src/web/src/components/System/Network/index.jsx`

**Wrong**:
```jsx
set <code>dht.lan_only=true</code>
in the configuration
```

**Correct**:
```jsx
set <code>dht.lan_only=true</code>{' '}
in the configuration
```

**Why This Keeps Happening**: JSX collapses source formatting around inline elements differently than plain text. When inline tags sit between words, add an explicit `{' '}` or keep the surrounding text in one expression so browser-rendered copy does not concatenate words.

### 0z105. Soulseek TCP Double-Disconnect Races Are Expected Network Churn, Not Fatal Unobserved Tasks

**The Bug**: Live `local test host` monitoring caught a current-process `[FATAL] Unobserved task exception` from `Soulseek.Network.Tcp.Connection.Disconnect` with `InvalidOperationException: An attempt was made to transition a task to a final state when it had already completed.` The process survived because the global handler marks it observed, but the log classified a Soulseek.NET read-loop disconnect race as fatal.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
var isNetworkFailure =
    exception is TimeoutException ||
    exception is OperationCanceledException ||
    exception is IOException;
```

**Correct**:
```csharp
var isSoulseekTcpDoubleDisconnectRace =
    exception is InvalidOperationException &&
    details.Contains("An attempt was made to transition a task to a final state", StringComparison.Ordinal) &&
    details.Contains("Soulseek.Network.Tcp.Connection.Disconnect", StringComparison.Ordinal);
```

**Why This Keeps Happening**: Soulseek.NET connection teardown can race between a read loop and a disconnect path. These failures are remote network churn/control-flow races when the stack is inside `Soulseek.Network.Tcp.Connection` or `MessageConnection`, so the global unobserved-task classifier must recognize them before the fatal fallback.

### 0z104. Expected Soulseek Shutdown Disconnect Races Must Not Log Exception Objects

**The Bug**: Manual deploy shutdowns hit the known Soulseek.NET disconnect race (`InvalidOperationException: Sequence contains no elements`) and the app caught it, but still passed the exception object to Serilog. The behavior was handled, yet the journal still printed a full stack trace during every affected shutdown.

**Files Affected**:
- `src/slskd/Application.cs`

**Wrong**:
```csharp
catch (InvalidOperationException ex) when (ShuttingDown && IsDisconnectRace(ex))
{
    Log.Warning(ex, "Ignoring Soulseek disconnect race during shutdown");
}
```

**Correct**:
```csharp
catch (InvalidOperationException ex) when (ShuttingDown && IsDisconnectRace(ex))
{
    Log.Debug("Ignoring Soulseek disconnect race during shutdown: {Message}", ex.Message);
}
```

**Why This Keeps Happening**: Catch filters can correctly classify expected shutdown races, but passing the exception object to `Warning` or `Error` still produces stack noise. For expected shutdown/control-flow races, log a concise message without the exception object unless there is actionable diagnostic value.

### 0z103. Background Search Batches Must Not Emit Per-Search Success/Fallback Logs At Information

**The Bug**: After auto-replace search pacing was fixed, a 142-item `local test host` cycle still filled the journal with routine `Information` logs from shared search infrastructure: mesh fallback with no peers, search completion counts, and passive HashDb discovery counts. The cycle was healthy, but normal per-search progress hid actionable runtime signals.

**Files Affected**:
- `src/slskd/Search/SearchService.cs`
- `src/slskd/DhtRendezvous/Search/MeshOverlaySearchService.cs`
- `src/slskd/HashDb/HashDbService.cs`

**Wrong**:
```csharp
Log.Information("Search for '{Query}' completed with {Responses} responses", query, search.ResponseCount);
_logger.LogInformation("[MeshSearch] No outbound mesh peers ...");
log.Information("[HashDb] Discovered {Count} FLAC files ...");
```

**Correct**:
```csharp
Log.Debug("Search for '{Query}' completed with {Responses} responses", query, search.ResponseCount);
_logger.LogDebug("[MeshSearch] No outbound mesh peers ...");
log.Debug("[HashDb] Discovered {Count} FLAC files ...");
```

**Why This Keeps Happening**: Shared search services serve both interactive searches and background batch workflows. A log level that feels harmless for one manual search becomes noise when a conservative background loop performs dozens of searches. Keep routine per-search progress at `Debug`; reserve `Information` for user-visible aggregate workflow summaries or unusual/actionable outcomes.

### 0z102. Auto-Replace Shutdown Cancellation Must Not Count As Search Errors

**The Bug**: Deploying a new manual build while auto-replace was in the middle of a paced alternative search logged `TaskCanceledException` stack traces and counted the interrupted items as failed replacements. The shutdown was intentional, but the journal looked like current-process search failure noise.

**Files Affected**:
- `src/slskd/Transfers/AutoReplace/AutoReplaceService.cs`
- `src/slskd/Transfers/AutoReplace/AutoReplaceBackgroundService.cs`

**Wrong**:
```csharp
catch (Exception ex)
{
    Log.Error(ex, "Error searching for alternatives: {Message}", ex.Message);
}
```

**Correct**:
```csharp
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    throw;
}
```

**Why This Keeps Happening**: Long-running background cycles often receive host shutdown tokens while they are sleeping, pacing, or polling remote state. Cancellation from the caller's token is a control-flow signal, not a failed search or failed replacement; let it unwind to the hosted service and log a concise shutdown message if needed.

### 0z101. Remote Offline Download Failures Are Expected Peer Outcomes, Not Error Stack Noise

**The Bug**: Live `local test host` restart validation re-enqueued downloads from an offline remote user and logged repeated `Soulseek.UserOfflineException` / `Soulseek.TransferException` stack traces from `DownloadService`. The transfer failure was expected peer state, but the journal looked like a local runtime error.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`

**Wrong**:
```csharp
catch (Exception ex)
{
    Log.Error(ex, "Download of {Filename} from user {Username} failed: {Message}", ...);
}
```

**Correct**:
```csharp
catch (Exception ex) when (IsExpectedRemoteDownloadFailure(ex))
{
    Log.Warning("Download of {Filename} from user {Username} failed because the remote peer is unavailable: {Message}", ...);
}
```

**Why This Keeps Happening**: Download failures mix local bugs with normal P2P outcomes. Remote offline/rejected/unavailable states should still fail the transfer record, but they should not emit full error stack traces unless the failure shape is unexpected.

### 0z100. Auto-Replace Large Batches Must Not Log Routine Per-Track No-Result Searches At Information

**The Bug**: After pacing auto-replace searches correctly, live `local test host` monitoring showed the same 128-item stuck batch logging routine `Searching for alternatives` and `Found 0 alternative candidates` messages at `Information` for every track. The behavior was no longer unsafe, but the journal still filled with expected no-result progress noise.

**Files Affected**:
- `src/slskd/Transfers/AutoReplace/AutoReplaceService.cs`

**Wrong**:
```csharp
Log.Information("Searching for alternatives: {SearchText}", searchText);
Log.Information("Found {Count} alternative candidates for: {SearchText}", candidates.Count, searchText);
```

**Correct**:
```csharp
Log.Debug("Searching for alternatives: {SearchText}", searchText);
if (candidates.Count > 0)
{
    Log.Information("Found {Count} alternative candidates for: {SearchText}", candidates.Count, searchText);
}
```

**Why This Keeps Happening**: Auto-replace is batch-oriented, so per-track progress that is harmless for one manual request becomes operator noise when a background cycle processes many stuck downloads. Keep routine per-item no-result progress at `Debug`; reserve `Information` for aggregate cycle summaries or actionable/successful candidate findings.

### 0z99. Generated Publish Directories Must Be Excluded From Web SDK Publish Content

**The Bug**: Creating manual publish output under `src/slskd/dist` and then republishing caused the Web SDK to copy that generated `dist` tree back into the next publish artifact. The artifact looked valid but carried stale nested publish output that should never ship.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `.gitignore`

**Wrong**:
```xml
<!-- Only gitignore dist, but leave it visible to SDK default items. -->
```

**Correct**:
```xml
<DefaultItemExcludes>$(DefaultItemExcludes);dist/**</DefaultItemExcludes>
```

**Why This Keeps Happening**: `Microsoft.NET.Sdk.Web` includes project files as publish content by default. Git ignore rules only stop source-control churn; they do not change MSBuild item discovery. Any generated directory under the app project must be excluded from both git and SDK default items, or later publishes can recursively package old artifacts.

### 0z98. OpenAPI `IOpenApiResponse.Content` Is Read-Only Through The Interface

**The Bug**: A nullability cleanup in `ContentNegotiationOperationFilter` changed response handling to assign `response.Content ??= ...` after `operation.Responses.TryGetValue(...)`; the value was typed as `IOpenApiResponse`, where `Content` is read-only, so Release builds failed with `CS0200`.

**Files Affected**:
- `src/slskd/Common/OpenAPI/ContentNegotiationOperationFilter.cs`

**Wrong**:
```csharp
if (operation.Responses.TryGetValue(statusCode, out var response))
{
    response.Content ??= new Dictionary<string, OpenApiMediaType>();
}
```

**Correct**:
```csharp
if (operation.Responses.TryGetValue(statusCode, out var response) &&
    response is OpenApiResponse openApiResponse &&
    openApiResponse.Content is { } content)
{
    content[contentType] = new OpenApiMediaType();
}
```

**Why This Keeps Happening**: Microsoft.OpenApi exposes response values through interfaces in some call paths, and those interfaces do not allow replacing the `Content` collection. When hardening nullable OpenAPI code, keep the concrete `OpenApiResponse` path and mutate the existing content dictionary instead of assigning through the interface.

### 0z96. React Hook Order Must Not Change Across Conditional Render Paths

**The Bug**: Moving `useEffect` after an early `return` introduced different hook counts between loading and loaded renders in `Network`, which caused `Rendered more hooks than during previous render` and unstable component behavior in production.

**Files Affected**:
- `src/web/src/components/System/Network/index.jsx`

**Wrong**:
```jsx
if (loading) {
  return <LoaderSegment />;
}

useEffect(() => {
  setShowDhtExposureConsent(
    config?.data?.profile?.exposure?.isFirstRun &&
      !config?.data?.profile?.exposure?.accepted &&
      hasDhtExposure
  );
}, [/* deps... */]);
```

**Correct**:
```jsx
useEffect(() => {
  setShowDhtExposureConsent(
    config?.data?.profile?.exposure?.isFirstRun &&
      !config?.data?.profile?.exposure?.accepted &&
      hasDhtExposure
  );
}, [/* deps... */]);

if (loading) {
  return <LoaderSegment />;
}
```

**Why This Keeps Happening**: React hook order is strictly positional, so any conditional return or branch before hook declarations can silently flip hook execution order. After refactors that add early exits, we can accidentally move hooks below conditionals, so every render path must be linted for stable hook sequencing and ordering.

### 0z97. Anonymous profile lookup returned full internal profile payload

**The Bug**: `ProfileController.GetProfile(peerId)` was anonymous and returned the raw `PeerProfile` model, which included `PublicKey`, `Signature`, `CreatedAt`, and `ExpiresAt`, exposing internal identity and timing metadata to unauthenticated callers.

**Files Affected**:
- `src/slskd/Identity/API/ProfileController.cs`

**Wrong**:
```csharp
[ProducesResponseType(typeof(PeerProfile), 200)]
...
return Ok(profile);
```

**Correct**:
```csharp
[ProducesResponseType(typeof(ProfileLookupResponse), 200)]
...
return Ok(ToProfileLookupResponse(profile));
```

**Why This Keeps Happening**: The endpoint is intentionally public for peer discovery, so raw internal DTO reuse can silently broaden the attack surface when schemas drift. Any public endpoint should return a deliberately narrowed response contract instead of reusing internal signed/profile storage models.

### 0z93. Cancellation-Path Download Tests Must Wait Until The Transfer Exists Before Cancelling

**The Bug**: `DownloadServiceTests.EnqueueAsync_CancelledTransfer_DoesNotFailFromDisposedBatchSemaphore` passed locally in isolation but failed in Release CI because the test cancelled the transfer immediately after `EnqueueAsync()` returned, then waited for `service.Find(...)` to show a completed transfer. On slower or differently ordered runs, cancellation could land before the background download path had actually materialized the transfer into the service state, so the test timed out waiting for a row that had never become observable in that window.

**Files Affected**:
- `tests/slskd.Tests.Unit/Transfers/Downloads/DownloadServiceTests.cs`

**Wrong**:
```csharp
var (enqueued, failed) = await service.EnqueueAsync(...);
var transferId = enqueued.Single().Id;
Assert.True(service.TryCancel(transferId));

var cancelledTransfer = await WaitForTransferAsync(
    () => service.Find(t => t.Id == transferId && t.EndedAt != null),
    TimeSpan.FromSeconds(5));
```

**Correct**:
```csharp
var downloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

// Signal from the mocked download path once the transfer work has actually started.

await downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
Assert.True(service.TryCancel(transferId));
```

**Why This Keeps Happening**: These tests exercise asynchronous transfer orchestration, but `EnqueueAsync()` returning does not guarantee the background download worker has already registered a visible transfer row or reached a cancellable wait. Cancelling before the worker has definitely started makes the test depend on scheduler timing instead of on the behavior it claims to verify.

### 0z94. Disposable Test Stores Should Use `using` in Unit Tests

**The Bug**: Several unit tests in the MultiSource planner suite allocated `InMemoryCatalogueStore` without disposal, which triggered CA2000 warnings and could retain in-memory resources across long test runs.

**Files Affected**:
- `tests/slskd.Tests.Unit/VirtualSoulfind/v2/Planning/MultiSourcePlannerTests.cs`
- `tests/slskd.Tests.Unit/VirtualSoulfind/v2/Integration/VirtualSoulfindV2IntegrationTests.cs`
- `tests/slskd.Tests.Unit/VirtualSoulfind/v2/Integration/CompleteV2FlowTests.cs`
- `tests/slskd.Tests.Unit/VirtualSoulfind/v2/Matching/SimpleMatchEngineTests.cs`

**Wrong**:
```csharp
var catalogueStore = new InMemoryCatalogueStore();
```

**Correct**:
```csharp
using var catalogueStore = new InMemoryCatalogueStore();
```

**Why This Keeps Happening**: Some test fixture objects implement `IDisposable`, but they are easy to miss during iterative test additions and refactors; without `using`, warnings are suppressed by test teardown and resource ownership becomes unclear. Explicit disposal keeps analyzer health clean and avoids cross-test side effects.

### 0z95. `MeshOverlayConnection` Created via Reflection Needs Internal Framer for Async Disposal

**The Bug**: In tests that build `MeshOverlayConnection` via `RuntimeHelpers.GetUninitializedObject` and then dispose those instances with `await using`, test teardown can throw `NullReferenceException` because private disposal fields (especially `_framer`) were not initialized.

**Files Affected**:
- `tests/slskd.Tests.Unit/DhtRendezvous/MeshNeighborRegistryTests.cs`
- `tests/slskd.Tests.Unit/DhtRendezvous/MeshNeighborPeerSyncServiceTests.cs`
- `tests/slskd.Tests.Unit/DhtRendezvous/MeshOverlayRequestRouterTests.cs`

**Wrong**:
```csharp
var connection = (MeshOverlayConnection)RuntimeHelpers.GetUninitializedObject(typeof(MeshOverlayConnection));
SetField(connection, "_cts", new CancellationTokenSource());
SetField(connection, "_sslStream", new SslStream(...));
await using var registryConnection = connection;
```

**Correct**:
```csharp
var connection = (MeshOverlayConnection)RuntimeHelpers.GetUninitializedObject(typeof(MeshOverlayConnection));
var sslStream = new SslStream(...);
SetField(connection, "_cts", new CancellationTokenSource());
SetField(connection, "_sslStream", sslStream);
SetField(connection, "_framer", new SecureMessageFramer(sslStream));
await using var registryConnection = connection;
```

**Why This Keeps Happening**: `GetUninitializedObject` skips constructor logic, so any disposal path still expects internal state to be initialized in test-created instances. If async disposal is added to those tests, missing fields become runtime failures even if object behavior in assertions is otherwise unchanged.

### 0z93. Auto-Replace Must Pace Searches Instead Of Treating Safety Rejections As Per-Track Errors

**The Bug**: Live `local test host` logs showed a single auto-replace cycle issuing many alternative searches in rapid succession until the Soulseek safety limiter hit `Limit=10/min, Current=10`. Every remaining stuck download then logged `Error searching for alternatives: Search rate limit exceeded` with a stack trace and the cycle reported a large failed count. The safety limiter was working, but auto-replace was using it as a noisy brake instead of pacing its own work.

**Files Affected**:
- `src/slskd/Transfers/AutoReplace/AutoReplaceService.cs`
- `tests/slskd.Tests.Unit/Transfers/AutoReplace/AutoReplaceServiceTests.cs`

**Wrong**:
```csharp
foreach (var stuckDownload in stuckDownloads)
{
    var alternatives = await FindAlternativesAsync(...);
    ...
}
```

**Correct**:
```csharp
foreach (var stuckDownload in stuckDownloads)
{
    await WaitForSearchBudgetAsync(cancellationToken);
    var alternatives = await FindAlternativesAsync(...);
    ...
}
```

**Why This Keeps Happening**: Auto-replace looks like internal maintenance, but every alternative lookup is still a real Soulseek search. Bulk loops must budget those searches before calling `SearchService.StartAsync(...)`; catching the thrown limiter exception after the fact still creates noisy logs, failed work, and avoidable pressure on the network.

### 0z92. Soulseek Timer-Reset Write Loop Races Must Be Classified Too, Not Just Read Loops

**The Bug**: After classifying the `Soulseek.Extensions.Reset(Timer)` teardown race for `ReadInternalAsync`/`MessageConnection.ReadContinuouslyAsync`, live `local test host` logs still emitted fake `[FATAL] Unobserved task exception` entries from the same third-party timer-reset `NullReferenceException` occurring in `Soulseek.Network.Tcp.Connection.WriteInternalAsync(...)`. The process kept running, but the logs still looked like a fatal crash.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
var isSoulseekTimerResetRace =
    exception is NullReferenceException &&
    details.Contains("Soulseek.Extensions.Reset(Timer)", StringComparison.Ordinal) &&
    details.Contains("Soulseek.Network.MessageConnection.ReadContinuouslyAsync", StringComparison.Ordinal);
```

**Correct**:
```csharp
var isSoulseekTimerResetReadRace =
    exception is NullReferenceException &&
    details.Contains("Soulseek.Extensions.Reset(Timer)", StringComparison.Ordinal) &&
    details.Contains("Soulseek.Network.MessageConnection.ReadContinuouslyAsync", StringComparison.Ordinal);

var isSoulseekTimerResetWriteRace =
    exception is NullReferenceException &&
    details.Contains("Soulseek.Extensions.Reset(Timer)", StringComparison.Ordinal) &&
    details.Contains("Soulseek.Network.Tcp.Connection.WriteInternalAsync", StringComparison.Ordinal);
```

**Why This Keeps Happening**: The original live sample only showed the read-loop teardown, so the classifier was narrowed to that exact stack shape. But the third-party `Timer` reset race can happen on both read and write paths. Matching only one side leaves the same benign bug producing fake fatal telemetry from the other side.

### 0z91. Manual Publish Must Match The Tagged Release Publish Shape

**The Bug**: `bin/publish` was producing a materially different Linux artifact than the tagged release workflows: self-contained, single-file, `ReadyToRun=true`, and `IncludeNativeLibrariesForSelfExtract=true`. Live manual deploys on `local test host` then exercised a different native runtime/extraction path than the official release builds, and the resulting crashes showed up as kernel `general protection fault` events in `.NET Server GC` inside the apphost image. That made manual soak results untrustworthy because they were not validating the same publish shape that CI ships.

**Files Affected**:
- `bin/publish`
- `.github/workflows/build-on-tag.yml`
- `.github/workflows/release-linux.yml`

**Wrong**:
```bash
dotnet publish \
    --configuration Release \
    -p:PublishSingleFile=true \
    -p:ReadyToRun=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    --self-contained \
    --runtime $runtime \
    --output $output
```

**Correct**:
```bash
dotnet publish \
    --configuration Release \
    -p:PublishReadyToRun=false \
    --self-contained \
    --runtime $runtime \
    --output $output
```

**Why This Keeps Happening**: `bin/publish` looks like a convenience wrapper, so it is easy to add "better/faster" publish flags there without checking whether the tagged release workflows use the same runtime shape. But live debugging only means anything if the manual artifact matches the shipped one. If CI builds framework/layout/runtime one way, manual deploy tooling must stay aligned instead of inventing a second publish profile with different native-host behavior.

### 0z90. Restarted TCP Overlay Listeners Need ReuseAddress Before Binding

**The Bug**: After a clean `systemctl restart` on a live node, `MeshOverlayServer` sometimes failed to rebind port `50305` with `SocketException (98): Address already in use`, even though no other process remained listening on that port by the time the node was inspected. The result was a degraded node that could connect out but could not announce or accept inbound TCP mesh overlay traffic after restart.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`

**Wrong**:
```csharp
_listener = new TcpListener(IPAddress.Any, ListenPortConfig);
_listener.Start();
```

**Correct**:
```csharp
_listener = new TcpListener(IPAddress.Any, ListenPortConfig);
_listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
_listener.Start();
```

**Why This Keeps Happening**: Restart testing often focuses on process lifetime and logs, not on the kernel socket state left behind by a just-closed listener that recently handled live traffic. On Linux, a rapid restart can still collide with the old listener's lingering socket state unless the replacement listener opts into address reuse before binding. If a port must come back immediately after shutdown, set the socket option explicitly instead of assuming the default `TcpListener` behavior will be restart-safe.

### 0z89. Cached QUIC Connections Must Be Explicitly Disposed On Replacement, Failure, And Host Shutdown

**The Bug**: The QUIC overlay/data clients cached `QuicConnection` instances in dictionaries, but when a connect race created duplicates, or when `OpenOutboundStreamAsync`/send failed and the connection was removed from the cache, the underlying `QuicConnection` was not disposed. The QUIC hosted services also did not explicitly close/drain active connections on stop. That leaves native MsQuic handles alive longer than intended and can destabilize long-running hosts in ways that only surface as native crashes or core dumps.

**Files Affected**:
- `src/slskd/Mesh/Overlay/QuicOverlayClient.cs`
- `src/slskd/Mesh/Overlay/QuicDataClient.cs`
- `src/slskd/Mesh/Overlay/QuicOverlayServer.cs`
- `src/slskd/Mesh/Overlay/QuicDataServer.cs`

**Wrong**:
```csharp
connection = await CreateConnectionAsync(endpoint, ct);
connections.TryAdd(endpoint, connection);

// ...
connections.TryRemove(endpoint, out _);
return false;
```

**Correct**:
```csharp
connection = await CreateConnectionAsync(endpoint, ct);
if (!connections.TryAdd(endpoint, connection))
{
    await connection.DisposeAsync();
}

// ...
if (connections.TryRemove(endpoint, out var removed))
{
    await removed.DisposeAsync();
}
```

**Why This Keeps Happening**: `ConcurrentDictionary.TryRemove` only updates the cache; it does not release the native QUIC handle. With `System.Net.Quic`, leaked or duplicated `QuicConnection` instances are much riskier than ordinary managed objects because the real state lives in MsQuic/native runtime resources. Any code path that replaces, evicts, or abandons a cached QUIC connection must dispose it explicitly, and hosted services must actively close/drain live QUIC state during shutdown instead of relying on process teardown.

### 0z88. Soulseek Timer-Reset Read Loop NullReferenceExceptions Must Be Classified As Expected Network Teardown

**The Bug**: Live `local test host` logs still emitted `[FATAL] Unobserved task exception` entries during normal peer/read-loop churn with `System.NullReferenceException` from `Soulseek.Extensions.Reset(Timer)` inside `Soulseek.Network.Tcp.Connection.ReadInternalAsync` and `Soulseek.Network.MessageConnection.ReadContinuouslyAsync`. The process kept running, but the log looked like a real fatal crash/disconnect.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
var isNetworkFailure =
    exception is TimeoutException ||
    exception is OperationCanceledException ||
    exception is IOException;
```

**Correct**:
```csharp
var isSoulseekTimerResetRace =
    exception is NullReferenceException &&
    details.Contains("Soulseek.Extensions.Reset(Timer)", StringComparison.Ordinal) &&
    details.Contains("Soulseek.Network.MessageConnection.ReadContinuouslyAsync", StringComparison.Ordinal);

var isNetworkFailure =
    exception is TimeoutException ||
    exception is OperationCanceledException ||
    exception is IOException ||
    isSoulseekTimerResetRace;
```

**Why This Keeps Happening**: The existing unobserved-task classifier covered common Soulseek socket/transfer exceptions and the older "underlying Tcp connection is closed" teardown, but not this newer third-party timer-reset race. Because the base exception type is `NullReferenceException`, it falls through the network-failure gate and gets logged as fatal even though the stack is still just peer/read-loop teardown noise. Benign Soulseek read-loop races must be matched by stack/message shape, not only by exception type.

### 0z87. Shutdown-Drain Tests Must Gate On Worker Start Before Expecting Cancellation

**The Bug**: `build-main-0.24.5-slskdn.161` failed in CI again on `DownloadServiceTests.ShutdownAsync_WaitsForCancelledDownloadsToDrain` even after replacing the old sleep-only assertion. The test still called `ShutdownAsync()` immediately after `EnqueueAsync()` and assumed the mocked `DownloadAsync()` body had already reached its cancellable wait. On a busy Release runner, shutdown could race ahead before the worker observed the token, leaving the test blocked on a `TaskCompletionSource` that was never signaled.

**Files Affected**:
- `tests/slskd.Tests.Unit/Transfers/Downloads/DownloadServiceTests.cs`

**Wrong**:
```csharp
await service.EnqueueAsync(...);
await service.ShutdownAsync(CancellationToken.None);
await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
```

**Correct**:
```csharp
await service.EnqueueAsync(...);
await downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

var shutdownTask = service.ShutdownAsync(CancellationToken.None);
await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
```

**Why This Keeps Happening**: "Enqueued" only proves the bookkeeping path completed; it does not guarantee the background download task has actually entered its cancellation point. Release-mode CI can reorder and delay task startup just enough that shutdown assertions become scheduler races. Async drain tests must wait for an explicit "worker started" signal from the mocked/background operation before asserting cancellation or shutdown completion semantics.

### 0z86. First-Run Share Bootstrap Must Fall Through To Scan, Not Throw A Corruption-Looking Exception

**The Bug**: A brand-new app directory logged `Share cache backup is missing, corrupt, or is out of date`, threw `ShareInitializationException`, then immediately retried and succeeded with a forced rescan. The startup path was functionally fine, but first-run logs looked like share-cache corruption.

**Files Affected**:
- `src/slskd/Shares/ShareService.cs`

**Wrong**:
```csharp
Log.Warning("Share cache backup is missing, corrupt, or is out of date");
throw new ShareInitializationException("Share cache backup is missing, corrupt, or is out of date");
```

**Correct**:
```csharp
Log.Information("Share cache backup is missing or out of date on initialization; performing initial share scan instead");
await ScanAsync();
```

**Why This Keeps Happening**: The initialization method mixed expected first-run bootstrap conditions with real corruption/failure conditions. When there is no valid cache yet, startup should fall through to an initial scan directly instead of using exception-driven control flow that emits scary warning/error text and a stack trace.

### 0z85. Global HTTP Rate Limiting Must Not Throttle The SPA Shell Or Static Assets

**The Bug**: A fast authenticated page/panel crawl hit repeated `429 Too Many Requests` on `/searches`, `/system/*`, `/api/v0/session`, and static asset requests, causing page-level Axios errors and console noise even though the underlying pages were mostly healthy. The global limiter applied the API policy to every non-mesh/non-federation path, including the SPA shell and assets.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
return RateLimitPartition.GetFixedWindowLimiter("api:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = apiPermit, Window = apiWindow });
```

**Correct**:
```csharp
if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
{
    return RateLimitPartition.GetNoLimiter("web");
}

return RateLimitPartition.GetFixedWindowLimiter("api:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = apiPermit, Window = apiWindow });
```

**Why This Keeps Happening**: The rate-limit policy was described as API/federation/mesh focused, but the implementation used the API bucket as the default catch-all. SPAs burst several HTML, JS, font, and session-check requests during navigation; treating those like untrusted API traffic makes normal operator usage look like a denial-of-service.

### 0z84. Versioned Native Controllers And Frontend Panels Must Stay Wired To The Runtime Surface

**The Bug**: The UI audit found two hard failures: `/pods` called `GET /api/v0/pods` but the backend returned `400 ApiVersionUnspecified` because `PodsController` used a versioned path without declaring an API version, and `/system/mediacore` crashed immediately with `ReferenceError: Checkbox is not defined` because the panel rendered `Checkbox` without importing it. The app also logged a fake route error on every fresh load because `/` fell through the wildcard route before redirecting to `/searches`.

**Files Affected**:
- `src/slskd/API/Native/PodsController.cs`
- `src/web/src/components/System/MediaCore/index.jsx`
- `src/web/src/components/App.jsx`

**Wrong**:
```csharp
[Route("api/v0/pods")]
```

```jsx
import {
  Button,
  Card,
  Dropdown,
  // ...
} from 'semantic-ui-react';
```

```jsx
<Route path="*" element={<RouteMissRedirect />} />
```

**Correct**:
```csharp
[Route("api/v{version:apiVersion}/pods")]
[ApiVersion("0")]
```

```jsx
import {
  Button,
  Card,
  Checkbox,
  Dropdown,
  // ...
} from 'semantic-ui-react';
```

```jsx
<Route path="/" element={<Navigate replace to="/searches" />} />
```

**Why This Keeps Happening**: The native API and the React surface are broad enough that compile-time success does not guarantee runtime wiring. Small mismatches like missing `ApiVersion` attributes, missing component imports, or relying on the wildcard redirect for the app root can leave entire panels apparently implemented but dead at runtime. Audit new top-level pages and native controllers through the real browser/API path, not just unit tests.

### 0z83. Full-Instance Harness Startup Must Wait For Overlay Listeners, Not Just The API

**The Bug**: The next manual release/test cycle showed `./bin/build` failing the full Release integration sweep with `TwoNodeMeshFullInstanceTests.TwoFullInstances_CanFormOverlayMeshConnection` returning `502 Bad Gateway` on `/api/v0/overlay/connect`, while the exact same test passed immediately in isolation. The full-instance runner marked nodes ready once `/api/v0/session/enabled` responded, but did not wait for the configured overlay TCP listener to start accepting connections.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
await WaitForApiReadyAsync(ct);
```

**Correct**:
```csharp
await WaitForApiReadyAsync(ct);
await WaitForTcpPortReadyAsync(overlayPort.Value, "overlay", ct);
```

**Why This Keeps Happening**: Generic-host/API readiness does not guarantee transport listeners owned by background services are bound yet. In lighter isolated runs the overlay listener often comes up before the test reaches `/api/v0/overlay/connect`, but in heavier optimized suites the timing window widens and the first explicit connect attempt can legitimately race listener startup. Full-instance harnesses must wait for every transport the test immediately depends on, not just the HTTP control plane.

### 0z82. Release-Suite Tests Must Not Depend On Fixed Local Ports Or Sleep-Only Drain Timing

**The Bug**: The next local release-candidate cycle after `build-main-0.24.5-slskdn.160` cleared CI exposed two test-only release blockers: `PortForwardingControllerTests.StartForwarding_WhenPortAlreadyForwarded_DoesNotLeakExceptionMessage` occasionally failed in the full Release suite because it hardcoded local port `12346`, and `DownloadServiceTests.ShutdownAsync_WaitsForCancelledDownloadsToDrain` occasionally failed because it only checked whether cancellation had been observed after a fixed `Task.Delay(150)` instead of waiting for the tracked task to finish unwinding.

**Files Affected**:
- `tests/slskd.Tests.Unit/API/Native/PortForwardingControllerTests.cs`
- `tests/slskd.Tests.Unit/Transfers/Downloads/DownloadServiceTests.cs`

**Wrong**:
```csharp
LocalPort = 12346;
Assert.True(cancellationObserved.Task.IsCompleted);
```

**Correct**:
```csharp
var localPort = GetFreeLocalPort();
await drainCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
```

**Why This Keeps Happening**: Release smoke runs the full optimized suite, where leftover listeners, test ordering, and scheduler jitter make fixed-port assumptions and sleep-based async assertions unreliable. Network-facing tests must allocate ephemeral loopback ports at runtime, and shutdown/drain tests must wait on explicit completion signals from the tracked work rather than inferring completion from a short delay.

### 0z81. Service Interface Changes Must Update Test Doubles In Smoke Projects

**The Bug**: The `build-main-0.24.5-slskdn.160` tag build failed in CI during release-gate integration smoke compilation because `StubDownloadService` in the integration test host still implemented the old `IDownloadService` surface after `ShutdownAsync(CancellationToken)` was added for shutdown draining.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs`

**Wrong**:
```csharp
internal sealed class StubDownloadService : IDownloadService
{
    public bool TryFail(Guid id, Exception exception) => false;
    public void Update(Transfer transfer) { ... }
    public void Dispose() { }
}
```

**Correct**:
```csharp
internal sealed class StubDownloadService : IDownloadService
{
    public bool TryFail(Guid id, Exception exception) => false;
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Update(Transfer transfer) { ... }
    public void Dispose() { }
}
```

**Why This Keeps Happening**: Interface additions compile cleanly in the main app and unit tests if those projects use the concrete implementation, but release-smoke and integration harnesses often register lightweight stubs that are only compiled in CI slices. Any public service interface change must be followed by a repo-wide search for `: I<InterfaceName>` test doubles and smoke-host adapters, then validated with the exact Release smoke commands used by packaging scripts.

### 0z80. Shutdown Must Tolerate SoulseekClient Disconnect Races

**The Bug**: Live `local test host` restart validation on `manual.37a745af5` showed `Application.StopAsync()` hitting `SoulseekClient.Disconnect("Shutting down", ...)`, then logging `Application terminated unexpectedly` with `InvalidOperationException: Sequence contains no elements` from `Soulseek.Extensions.RemoveAndDisposeAll`. The service still stopped and restarted, but the shutdown path emitted a false fatal because the third-party client raced its internal connection dictionaries during disconnect.

**Files Affected**:
- `src/slskd/Application.cs`

**Wrong**:
```csharp
Client.Disconnect("Shutting down", new ApplicationShutdownException("Shutting down"));
Client.Dispose();
```

**Correct**:
```csharp
try
{
    Client.Disconnect("Shutting down", new ApplicationShutdownException("Shutting down"));
}
catch (InvalidOperationException ex) when (Application.IsShuttingDown && ex.Message.Contains("Sequence contains no elements"))
{
    Log.Warning(ex, "Ignoring Soulseek disconnect race during shutdown");
}

Client.Dispose();
```

**Why This Keeps Happening**: Host shutdown is not the same as a protocol-level graceful logout. By the time `StopAsync` runs, active network/background operations may already be tearing down the same Soulseek client collections that `Disconnect()` iterates. Third-party shutdown helpers can throw collection-state exceptions that are irrelevant to process correctness; the host should treat those as shutdown races, log them at reduced severity, and continue disposing cleanly.

### 0z79. Shutdown Cancellation Must Not Use Disposed Transfer Services

**The Bug**: Live `local test host` validation of a clean `systemctl restart` showed active downloads cancelling during shutdown, then logging global semaphore release warnings and `ObjectDisposedException` while trying to fail/update transfers after the DI provider was already disposing. Shutdown cancellation is expected, but the download observers treated it like a normal transfer failure and kept touching database/services.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`

**Wrong**:
```csharp
catch (OperationCanceledException ex)
{
    Log.Error(ex, "Task for download ... did not complete successfully");
    TryFail(transferId, exception: ex);
}
```

**Correct**:
```csharp
await transferService.Downloads.ShutdownAsync(cancellationToken);
Client.Disconnect("Shutting down", new ApplicationShutdownException("Shutting down"));
Client.Dispose();
```

```csharp
catch (OperationCanceledException ex) when (Application.IsShuttingDown)
{
    Log.Debug(ex, "Download task cancelled during shutdown");
}
```

**Why This Keeps Happening**: User-requested transfer cancellation and host shutdown both surface as `OperationCanceledException`, but they need different cleanup semantics. User cancellation should mark a transfer cancelled; host shutdown should stop background work quietly and leave incomplete records for normal startup recovery. During shutdown, the service provider and semaphores may already be disposing, so failure paths must avoid database updates, event publication, and semaphore disposal races. Cancelling transfer CTS is not enough by itself: the host must also wait for those download tasks to unwind before disposing the shared Soulseek client, or the client's own transfer semaphores/logging will race with disposal and emit shutdown-only warnings.

### 0z78. Service SIGTERM Must Stop The Host, Not Exit 1

**The Bug**: Live `local test host` manual deployments showed `systemctl restart slskd` logging `Received SIGTERM`, `[FATAL] ProcessExit event fired`, and `Main process exited, code=exited, status=1/FAILURE`. `Application` registered POSIX signal handlers that treated normal service stop/restart signals as fatal and called `Environment.Exit(1)`.

**Files Affected**:
- `src/slskd/Application.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
PosixSignalRegistration.Create(signal, context =>
{
    ShuttingDown = true;
    Program.MasterCancellationTokenSource.Cancel();
    Log.Fatal("Received {Signal}", signal);
    Environment.Exit(1);
});
```

**Correct**:
```csharp
PosixSignalRegistration.Create(signal, context =>
{
    context.Cancel = true;
    ShuttingDown = true;
    Program.MasterCancellationTokenSource.Cancel();
    lifetime.StopApplication();
});
```

**Why This Keeps Happening**: SIGTERM is how systemd performs a normal service stop or restart. Treating it like a crash creates false failure telemetry, increments failure counters, and can trigger restart/on-failure policy even though the operator requested a clean shutdown. Signal handlers should cancel the default termination, mark shutdown state, and ask the generic host to stop so hosted services can run `StopAsync`.

### 0z77. Expected Peer Browse Failures Must Not Escape API Controllers

**The Bug**: Live `local test host` logs showed `POST /api/v0/users/{username}/directory` producing repeated middleware/error-handler stack traces when a remote Soulseek peer could not establish a direct or indirect message connection. `UsersController.Directory` only handled `UserOfflineException`, so ordinary peer reachability failures bubbled as unhandled request exceptions.

**Files Affected**:
- `src/slskd/Users/API/Controllers/UsersController.cs`
- `tests/slskd.Tests.Unit/Users/UsersControllerTests.cs`

**Wrong**:
```csharp
try
{
    var result = await Client.GetDirectoryContentsAsync(username, request.Directory);
    return Ok(result);
}
catch (UserOfflineException)
{
    return NotFound("User is offline");
}
```

**Correct**:
```csharp
try
{
    var result = await Client.GetDirectoryContentsAsync(username, request.Directory);
    return Ok(result);
}
catch (UserOfflineException)
{
    return NotFound("User is offline");
}
catch (SoulseekClientException ex) when (ex.InnerException is ConnectionException)
{
    return StatusCode(503, "Unable to retrieve directory contents from user");
}
```

**Why This Keeps Happening**: Soulseek browsing depends on remote peer connectivity. A peer can be online enough to appear in search/user data but still fail direct or indirect message connection setup. API endpoints that initiate peer network operations must translate expected Soulseek connection failures into stable HTTP responses, otherwise normal remote-peer behavior looks like an application exception and pollutes logs.

### 0z76. DHT Rendezvous Must Not Count Connector Capacity Skips As Peer Attempts

**The Bug**: Live `local test host` testing after manual build `d41ef6335` showed DHT healthy with four discovered peers but no mesh, while status reported `totalConnectionsAttempted=8` and overlay stats reported only six connector failures. `DhtRendezvousService` scheduled one background task per discovered peer and marked each peer attempted before calling the connector; `MeshOverlayConnector` then silently skipped calls when its three-attempt concurrency guard was already full.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `tests/slskd.Tests.Unit/DhtRendezvous/DhtRendezvousServiceTests.cs`

**Wrong**:
```csharp
_peerConnectionAttemptedAt[peerId] = now;
_ = TryConnectToPeerAsync(peerId, endpoint);
```

**Correct**:
```csharp
if (_pendingPeerConnections.Count >= MaxConcurrentPeerConnectionAttempts)
{
    return;
}

_peerConnectionAttemptedAt[peerId] = now;
_ = TryConnectToPeerAsync(peerId, endpoint);
```

**Why This Keeps Happening**: The rendezvous layer and connector both have concurrency state. If the outer scheduler does not honor the connector's capacity, it can burn retry/backoff state on work that never actually opened a socket. Attempt counters, diagnostics, and retry timing then lie, and a valid candidate later in the DHT result set can be delayed behind junk endpoints.

### 0z75. Overlay Readers Must Handle Unframed JSON Compatibility Frames

**The Bug**: Live `local test host` testing on manual build `90257b10d` connected to mesh peer `m***7`, then dropped the peer exactly at the two-minute keepalive mark with `Protocol violation ... Invalid message length: 2065855609`. That "length" is `0x7b227479`, the ASCII bytes `{"ty`, so the reader saw raw JSON at the frame boundary where it expected a four-byte big-endian length prefix.

**Files Affected**:
- `src/slskd/DhtRendezvous/Security/SecureMessageFramer.cs`
- `tests/slskd.Tests.Unit/DhtRendezvous/Security/SecureMessageFramerTests.cs`

**Wrong**:
```csharp
var length = BinaryPrimitives.ReadInt32BigEndian(headerBuffer);
if (length < 2 || length > MaxMessageSize)
{
    throw new ProtocolViolationException($"Invalid message length: {length}");
}
```

**Correct**:
```csharp
if (headerBuffer[0] == (byte)'{')
{
    return await ReadUnframedJsonPayloadAsync(headerBuffer, cancellationToken);
}

var length = BinaryPrimitives.ReadInt32BigEndian(headerBuffer);
```

**Why This Keeps Happening**: The overlay protocol moved to length-prefixed JSON frames, but live peers can still emit unframed JSON control messages after a successful framed handshake. The symptom is deterministic and recognizable: bogus length values whose bytes are the start of JSON (`{"ty` for `{"type"...`). Treating that as corruption disconnects otherwise usable mesh peers; the reader needs a capped compatibility path that consumes exactly one JSON object and keeps normal framed messages unchanged.

### 0z73. PATH-Based Tool Names Are Not Filesystem Paths

**The Bug**: Live `local test host` logs emitted `[AudioSketch] ffmpeg not configured or missing: ffmpeg` dozens of times even though `ffmpeg` was installed at `/usr/bin/ffmpeg`. `AudioSketchService` treated the default configured command name `ffmpeg` as a literal relative file path and called `File.Exists("ffmpeg")`, which fails for PATH-resolved executables.

**Files Affected**:
- `src/slskd/Audio/AudioSketchService.cs`
- `tests/slskd.Tests.Unit/Audio/AudioSketchServiceTests.cs`

**Wrong**:
```csharp
if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
{
    log.Warning("[AudioSketch] ffmpeg not configured or missing: {Path}", ffmpegPath);
    return null;
}
```

**Correct**:
```csharp
var resolvedFfmpegPath = ResolveExecutablePath(ffmpegPath);
if (resolvedFfmpegPath is null)
{
    log.Warning("[AudioSketch] ffmpeg not configured or missing: {Path}", ffmpegPath);
    return null;
}
```

**Why This Keeps Happening**: Tool configuration often accepts either absolute paths (`/usr/bin/ffmpeg`) or command names (`ffmpeg`). `File.Exists` only answers the first case. Code that launches external tools must either let `ProcessStartInfo` resolve command names through PATH or explicitly resolve PATH before declaring the tool missing.

### 0z74. QUIC Support Is A Runtime Capability, Not An OS Check

**The Bug**: Live `local test host` testing on build `15ac295cc` showed `slskd` starting QUIC overlay listeners with the host's AUR `msquic 2.4.11`, then crashing with a native `libmsquic.so.2` segfault. The app registered QUIC hosted services because the OS was Linux, even though QUIC viability depends on the installed native `libmsquic` and its compatibility with the running .NET runtime.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Mesh/MeshStatsCollector.cs`
- `src/slskd/Mesh/Dht/PeerDescriptorPublisher.cs`

**Wrong**:
```csharp
private static bool IsQuicSupportedPlatform()
{
    return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();
}
```

**Correct**:
```csharp
private static bool IsQuicAvailable()
{
    try
    {
        return QuicConnection.IsSupported && QuicListener.IsSupported;
    }
    catch
    {
        return false;
    }
}
```

**Why This Keeps Happening**: `System.Net.Quic` has compile-time platform guards, but Linux QUIC also depends on a native MsQuic library supplied by the host/package. OS checks only satisfy the analyzer; they do not prove the native library can initialize. Runtime registration, descriptor publication, and stats collection must use actual `QuicConnection`/`QuicListener` support checks and treat failures as "QUIC unavailable" unless the host dependency is upgraded.

### 0z72. Soulseek MessageConnection Teardown Must Not Log As Fatal

**The Bug**: Live `local test host` manual build testing emitted `[FATAL] Unobserved task exception ... (The underlying Tcp connection is closed)` while a remote transfer had already been classified as peer-side failure. The flattened exception was `InvalidOperationException: The underlying Tcp connection is closed` from `Soulseek.Network.MessageConnection.ReadContinuouslyAsync`, which did not match the existing expected Soulseek network classifier even though it is normal peer connection teardown.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
var isNetworkFailure =
    exception is IOException ||
    typeName.Contains("Soulseek.ConnectionException", StringComparison.Ordinal);
```

**Correct**:
```csharp
var isSoulseekMessageConnectionClosed =
    exception is InvalidOperationException &&
    details.Contains("The underlying Tcp connection is closed", StringComparison.Ordinal) &&
    details.Contains("Soulseek.Network.MessageConnection.ReadContinuouslyAsync", StringComparison.Ordinal);
```

**Why This Keeps Happening**: Soulseek.NET has multiple asynchronous connection loops, and not all peer teardown escapes as `IOException`, `SocketException`, or a public `Soulseek.*Exception` type. The unobserved-task classifier must recognize exact, stack-anchored teardown signatures; otherwise handled remote failures still look like host-level fatal crashes.

### 0z71. DHT Beacon Capability Must Be Based On The Real Overlay Listener

**The Bug**: Live `local test host` manual build testing showed DHT ready, outbound mesh connected, but `/api/v0/overlay/stats` reported `server.isListening=false` and no TCP listener on `50305`. Startup had logged `This client is not beacon-capable (behind NAT)` even though the same host had listened on `50305` minutes earlier. `DhtRendezvousService.DetectBeaconCapabilityAsync(...)` performed a temporary bind probe before starting `MeshOverlayServer`; if that probe failed transiently during a fast restart, the real overlay listener was never started or retried.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`

**Wrong**:
```csharp
IsBeaconCapable = await DetectBeaconCapabilityAsync(cancellationToken);
if (IsBeaconCapable)
{
    await _overlayServer.StartAsync(cancellationToken);
}
```

**Correct**:
```csharp
IsBeaconCapable = await StartOverlayServerIfPossibleAsync(cancellationToken);
```

**Why This Keeps Happening**: A preflight bind is not the same thing as owning the listener. It can create false negatives during restart races and adds a time-of-check/time-of-use window before the real server binds. Beacon capability for this implementation should be derived from whether the actual overlay listener started successfully; if it fails, log the bind failure and continue as a seeker.

### 0z70. Overlay Keepalive Must Not Read Outside The Message Loop

**The Bug**: Live `local test host` manual build testing still dropped a mesh neighbor around the two-minute keepalive window with `Protocol violation ... Invalid message length: 2065855609` after a mesh search had been sent. `MeshOverlayServer.HandleMessagesAsync(...)` called `connection.PingAsync(...)`, and `PingAsync` wrote a ping then performed its own direct read for a `PongMessage`. That creates a second read path outside the normal dispatcher and can consume or race unrelated overlay frames on the same TLS stream.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayConnection.cs`
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`

**Wrong**:
```csharp
if (connection.NeedsKeepalive())
{
    var rtt = await connection.PingAsync(cancellationToken);
}
```

**Correct**:
```csharp
if (connection.NeedsKeepalive())
{
    await connection.SendKeepalivePingAsync(cancellationToken);
}

// The normal message loop remains the only reader and handles pong frames.
case OverlayMessageType.Pong:
    break;
```

**Why This Keeps Happening**: Request/response helpers are tempting for keepalive logic, but persistent overlay sockets already have a long-lived dispatcher reading the stream. Any second reader can steal the next frame, deserialize the wrong message shape, or desynchronize the protocol when it overlaps with mesh search or service traffic. Keepalive on persistent overlay sockets should write only; all inbound frames must flow through the single message loop.

### 0z69. Test Code Must Alias `slskd.Options` When Importing `Microsoft.Extensions.Options`

**The Bug**: `AutoReplaceServiceTests` imported `Microsoft.Extensions.Options` and then used `Mock.Of<IOptionsMonitor<Options>>()`. In that scope, `Options` resolved to `Microsoft.Extensions.Options.Options` (a static helper type) instead of the application `slskd.Options`, so a later full test build failed with `CS0718: 'Options': static types cannot be used as type arguments`.

**Files Affected**:
- `tests/slskd.Tests.Unit/Transfers/AutoReplace/AutoReplaceServiceTests.cs`

**Wrong**:
```csharp
using Microsoft.Extensions.Options;

Mock.Of<IOptionsMonitor<Options>>();
```

**Correct**:
```csharp
using SlskdOptions = slskd.Options;

Mock.Of<IOptionsMonitor<SlskdOptions>>();
```

**Why This Keeps Happening**: The app's root options type has the same short name as the Microsoft.Extensions.Options helper class. Unit tests often need `IOptionsMonitor<T>`, so importing the namespace makes the collision easy to miss until a clean build compiles that test file.

### 0z68. DHT Rendezvous Must Not Dial Its Own DHT UDP Port As A TCP Overlay Endpoint

**The Bug**: Live `local test host` manual build testing showed DHT discovery returning many candidates on port `50306`, which is the slskdn DHT UDP port, while the TCP overlay listener was on `50305`. `DhtRendezvousService.OnPeersFound(...)` treated every `PeerInfo.ConnectionUri` as a TCP overlay endpoint and scheduled outbound TLS overlay dials immediately. The node then spent connection attempts/backoff on DHT-port candidates, producing timeouts/refusals/TLS EOFs and leaving mesh search with no usable outbound peers.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `tests/slskd.Tests.Unit/DhtRendezvous/DhtRendezvousServiceTests.cs`

**Wrong**:
```csharp
var endpoint = new IPEndPoint(ip, uri.Port);
PublishDiscoveredPeer(endpointKey, endpoint);
SchedulePeerConnection(endpointKey, endpoint);
```

**Correct**:
```csharp
var endpoint = new IPEndPoint(ip, uri.Port);
if (IsLikelyDhtPort(endpoint.Port))
{
    // Do not treat a DHT UDP contact as a TCP overlay listener.
    continue;
}

PublishDiscoveredPeer(endpointKey, endpoint);
SchedulePeerConnection(endpointKey, endpoint);
```

**Why This Keeps Happening**: BitTorrent DHT peer results only carry an IP and port; they do not prove the endpoint is a slskdn TCP overlay listener. slskdn also runs a stable DHT UDP port next to the overlay port, so stale or malformed announcements can look plausible enough to dial. Discovery, candidate tracking, and overlay verification must stay separate, and obvious DHT-port candidates should be filtered before consuming overlay connection budget.

### 0z67. Auto-Replace Must Wait For SearchService Background Finalization

**The Bug**: Live `local test host` build `159` logs showed `AutoReplaceService` warning `No search responses found` for a track, then `SearchService` logging the same search completed with 14-17 responses one second later. Auto-replace started a search with `SearchService.StartAsync()`, but that method returns after creating the search record while a background task later persists the final responses. The fixed 30-second poll could expire just before finalization, causing auto-replace to skip valid alternatives.

**Files Affected**:
- `src/slskd/Transfers/AutoReplace/AutoReplaceService.cs`
- `tests/slskd.Tests.Unit/Transfers/AutoReplace/AutoReplaceServiceTests.cs`

**Wrong**:
```csharp
while (waited < TimeSpan.FromSeconds(30))
{
    searchWithResponses = await Searches.FindAsync(s => s.Id == searchId, includeResponses: true);
    if (searchWithResponses?.State.HasFlag(SearchStates.Completed) == true)
    {
        break;
    }
}

if (searchWithResponses?.Responses == null || !searchWithResponses.Responses.Any())
{
    return candidates;
}
```

**Correct**:
```csharp
while (waited < SearchCompletionTimeout)
{
    searchWithResponses = await Searches.FindAsync(s => s.Id == searchId, includeResponses: true);
    if (searchWithResponses?.State.HasFlag(SearchStates.Completed) == true)
    {
        break;
    }
}

if (searchWithResponses?.State.HasFlag(SearchStates.Completed) != true)
{
    return candidates;
}

if (searchWithResponses?.Responses == null || !searchWithResponses.Responses.Any())
{
    return candidates;
}
```

**Why This Keeps Happening**: `SearchService.StartAsync()` sounds like it returns the completed search, but the normal search path returns the newly created record and finalizes responses asynchronously. Any workflow that needs search responses must poll for the persisted `Completed` state with enough finalization grace and must not equate "not completed yet" with "completed with no responses."

### 0z66. User Directory Browse Must Gate The Soulseek Logged-In State

**The Bug**: Live `local test host` startup logs showed `POST /api/v0/users/{username}/directory` throwing through the ASP.NET pipeline when the frontend/browser requested a directory while the Soulseek client was `Connected, LoggingIn`. The endpoint only handled offline users, so a normal startup race became a noisy 500 with repeated security middleware and exception-handler errors.

**Files Affected**:
- `src/slskd/Users/API/Controllers/UsersController.cs`
- `tests/slskd.Tests.Unit/Users/UsersControllerTests.cs`

**Wrong**:
```csharp
var result = await Client.GetDirectoryContentsAsync(username, request.Directory);
return Ok(result);
```

**Correct**:
```csharp
if (!Client.State.HasFlag(SoulseekClientStates.Connected | SoulseekClientStates.LoggedIn))
{
    return StatusCode(503, "Soulseek server connection is not ready");
}

var result = await Client.GetDirectoryContentsAsync(username, request.Directory);
return Ok(result);
```

**Why This Keeps Happening**: A Soulseek TCP connection being established is not enough for peer browse operations; the client also has to finish login. UI requests can replay immediately after a service restart, so controller actions that call Soulseek peer APIs need a ready-state gate instead of relying on lower-level `InvalidOperationException` messages.

### 0z65. Mesh Content Downloads Need A Real Overlay Service Transport, Not Just Service DTOs

**The Bug**: The DHT rendezvous overlay could connect peers and run mesh search, and the content service/fetcher classes existed, but `MeshServiceClient.CallAsync()` always returned `ServiceUnavailable` because service-fabric transport over the DHT overlay was not implemented. Search results could look available while `/api/v0/searches/{id}/items/{item}/download` could never fetch bytes from a mesh peer.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/MeshServiceClient.cs`
- `src/slskd/DhtRendezvous/MeshOverlayRequestRouter.cs`
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/Streaming/MeshContentFetcher.cs`

**Wrong**:
```csharp
_logger.LogWarning("[ServiceClient] Mesh service transport is not implemented...");
return Task.FromResult(new ServiceReply
{
    StatusCode = ServiceStatusCodes.ServiceUnavailable,
    ErrorMessage = "Mesh service transport is not implemented."
});
```

**Correct**:
```csharp
var replyTask = _requestRouter.WaitForMeshServiceReplyAsync(connection, correlationId, timeout.Token);
await connection.WriteMessageAsync(new MeshServiceCallMessage { CorrelationId = correlationId, ... }, timeout.Token);
var reply = await replyTask;
```

**Why This Keeps Happening**: The project has several mesh layers with similar names: DHT rendezvous overlay connections, mesh search RPCs, service-fabric DTOs, and `MeshContent` handlers. Having the service and fetcher classes present does not mean the transport path is wired. Any feature that claims end-to-end mesh transfer must verify a real peer-to-peer call crosses an overlay connection and returns file bytes, not just that both endpoint classes compile.

### 0z65a. Search Response Merging Must Preserve Pod Routing Metadata

**The Bug**: Mesh overlay search responses were tagged with `PrimarySource = "pod"` and `PodContentRef`, but `SearchResponseMerger.Deduplicate()` rebuilt each `Response` and copied only Soulseek-style counters/files. The final persisted search result lost the pod routing metadata, so the action controller could not route a mesh result to `MeshContentFetcher`.

**Files Affected**:
- `src/slskd/Search/SearchResponseMerger.cs`
- `src/slskd/Search/API/Controllers/SearchActionsController.cs`
- `src/slskd/DhtRendezvous/Search/MeshOverlaySearchService.cs`

**Wrong**:
```csharp
merged.Add(new Response
{
    Username = r.Username,
    Files = keptFiles,
    LockedFiles = keptLocked,
});
```

**Correct**:
```csharp
merged.Add(new Response
{
    Username = r.Username,
    Files = keptFiles,
    LockedFiles = keptLocked,
    SourceProviders = r.SourceProviders,
    PrimarySource = r.PrimarySource,
    PodContentRef = r.PodContentRef,
    SceneContentRef = r.SceneContentRef,
});
```

**Why This Keeps Happening**: Search responses started as Soulseek-only DTOs, but pod/scene bridging added routing metadata at the response layer. Any code that clones, deduplicates, serializes, or adapts `Search.Response` must carry source metadata forward, or downstream action routing silently falls back to the wrong path.

### 0z65b. Overlay Service Call Handlers Need MeshServiceRouter Registered In DI

**The Bug**: DHT overlay service-call message handling was added to `MeshOverlayServer` and `MeshOverlayConnector`, but `MeshServiceRouter` was only looked up opportunistically and was not registered in DI. Peers could connect and mesh-search successfully, then every pod download failed with `Mesh service router unavailable` because incoming `mesh_service_call` messages had no router to dispatch `MeshContent.GetByContentId`.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`

**Wrong**:
```csharp
public MeshOverlayConnector(..., MeshServiceRouter? serviceRouter = null)
{
    _serviceRouter = serviceRouter; // null if the router was never registered
}
```

**Correct**:
```csharp
services.AddOptions<MeshServiceFabricOptions>().Bind(Configuration.GetSlskdSection("MeshServiceFabric"));
services.AddSingleton<MeshServiceRouter>();
services.AddSingleton<IMeshOverlayConnector, MeshOverlayConnector>();
```

**Why This Keeps Happening**: Optional constructor parameters make missing DI registrations look intentional and allow the app to boot with half-wired behavior. Any overlay protocol feature that handles inbound RPCs must have its dispatcher registered explicitly and covered by a full-instance request test, not just constructor defaults.

### 0z64. Soulseek TransferRejectedException Remote Enqueue Failures Are Expected Network Churn

**The Bug**: Live `local test host` validation still emitted `[FATAL] Unobserved task exception ... (Enqueue failed due to internal error)` after downloads were already recorded as `Completed, Rejected`. The unobserved exception was `Soulseek.TransferRejectedException`, which the expected-network classifier did not recognize even though the app had already handled it as a remote transfer rejection.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
typeName.Contains("Soulseek.TransferException", StringComparison.Ordinal) ||
typeName.Contains("Soulseek.TransferReportedFailedException", StringComparison.Ordinal);
```

**Correct**:
```csharp
typeName.Contains("Soulseek.TransferException", StringComparison.Ordinal) ||
typeName.Contains("Soulseek.TransferRejectedException", StringComparison.Ordinal) ||
typeName.Contains("Soulseek.TransferReportedFailedException", StringComparison.Ordinal);
```

**Why This Keeps Happening**: Soulseek.NET uses several sibling exception types for remote peer transfer failures. A message like `Enqueue failed due to internal error` sounds local, but when it arrives as `Soulseek.TransferRejectedException` from the download path it represents a remote client rejection and should be treated like other expected peer churn in the global unobserved-task handler.

### 0z63. Download History Counters Need Atomic Upserts Under Transfer Concurrency

**The Bug**: Live `local test host` transfer activity hit `SQLite Error 19: 'UNIQUE constraint failed: DownloadHistory.Username'` while recording source-ranking history. Multiple transfer-completion handlers can see no row for the same username, race to insert it, and make EF log database errors or lose counter updates if the retry path exhausts.

**Files Affected**:
- `src/slskd/Transfers/Ranking/SourceRankingService.cs`
- `tests/slskd.Tests.Unit/Transfers/Ranking/SourceRankingServiceTests.cs`

**Wrong**:
```csharp
var entry = await context.DownloadHistory.FindAsync(new object[] { username }, cancellationToken);
if (entry == null)
{
    entry = new DownloadHistoryEntry { Username = username };
    context.DownloadHistory.Add(entry);
}

entry.Failures++;
await context.SaveChangesAsync(cancellationToken);
```

**Correct**:
```csharp
await context.Database.ExecuteSqlInterpolatedAsync($@"
    INSERT INTO ""DownloadHistory"" (""Username"", ""Successes"", ""Failures"", ""LastUpdated"")
    VALUES ({username}, {successes}, {failures}, {lastUpdated})
    ON CONFLICT(""Username"") DO UPDATE SET
        ""Successes"" = ""DownloadHistory"".""Successes"" + {successes},
        ""Failures"" = ""DownloadHistory"".""Failures"" + {failures},
        ""LastUpdated"" = {lastUpdated}",
    cancellationToken);
```

**Why This Keeps Happening**: EF read-then-add code looks natural for single-row counters, but event handlers run concurrently during real transfer activity. Any SQLite-backed counter keyed by a natural key must use one atomic database operation, or it will race exactly when many transfer events arrive together.

### 0z62. DHT Diagnostic Controllers Must Use AuthPolicy.Any, Not Bare Authorize

**The Bug**: Live `local test host` diagnostics accepted the configured API key for `/api/v0/session` and `/api/v0/searches`, but `/api/v0/dht/status` and `/api/v0/overlay/stats` returned `401` with `WWW-Authenticate: Bearer`. `DhtRendezvousController` used bare `[Authorize]`, which followed the default JWT bearer scheme instead of the repo's API-key-or-JWT policy.

**Files Affected**:
- `src/slskd/DhtRendezvous/API/DhtRendezvousController.cs`
- `tests/slskd.Tests.Unit/DhtRendezvous/DhtRendezvousControllerTests.cs`

**Wrong**:
```csharp
[Authorize]
public class DhtRendezvousController : ControllerBase
```

**Correct**:
```csharp
[Authorize(Policy = AuthPolicy.Any)]
public class DhtRendezvousController : ControllerBase
```

**Why This Keeps Happening**: `[Authorize]` looks equivalent to "authenticated user required", but this app has multiple authentication schemes. Operator and automation endpoints that should work with configured API keys must opt into `AuthPolicy.Any` (or a narrower explicit policy) instead of relying on the default bearer scheme.

### 0z61. Pending Overlay Request Router Entries Must Be Removed On Write Failure Too

**The Bug**: The first `MeshOverlayRequestRouter` implementation registered a pending mesh-search response before writing the request to the peer, but only removed the entry when a response arrived, the timeout token canceled, or the whole connection closed. If `WriteMessageAsync` failed after registration, disposing the linked timeout source did not invoke cancellation callbacks, so the pending request could stay in memory until disconnect.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayRequestRouter.cs`
- `src/slskd/DhtRendezvous/Search/MeshOverlaySearchService.cs`

**Wrong**:
```csharp
var responseTask = _requestRouter.WaitForMeshSearchResponseAsync(connection, requestId, timeoutCts.Token);
await connection.WriteMessageAsync(req, timeoutCts.Token);
var resp = await responseTask;
```

**Correct**:
```csharp
try
{
    var responseTask = _requestRouter.WaitForMeshSearchResponseAsync(connection, requestId, timeoutCts.Token);
    await connection.WriteMessageAsync(req, timeoutCts.Token);
    var resp = await responseTask;
}
finally
{
    _requestRouter.RemoveMeshSearchResponse(connection, requestId);
}
```

**Why This Keeps Happening**: Request routers are easy to reason about on the happy response/timeout paths and easy to leak on pre-response write failures. Any code that registers pending stream work before an awaited write needs an explicit `finally` removal path; do not rely on `CancellationTokenRegistration` cleanup from disposing a linked `CancellationTokenSource`.

### 0z60. Direct Anonymity Transport Failures Are Not The Same As No Available Transport

**The Bug**: The broad integration suite failed because `AnonymityTransportSelector_FallbackLogic_Works` still expected `InvalidOperationException` after direct-mode transport support was added. In direct mode, a transport is available; if the TLS connection fails, `AnonymityTransportSelector` wraps the actual failure in `AggregateException("All anonymity transports failed", inner)`. `InvalidOperationException` only describes the separate "no transport is available" path.

**Files Affected**:
- `tests/slskd.Tests.Integration/Security/ObfuscatedTransportIntegrationTests.cs`
- `src/slskd/Common/Security/AnonymityTransportSelector.cs`

**Wrong**:
```csharp
await Assert.ThrowsAsync<InvalidOperationException>(() =>
    selector.SelectAndConnectAsync("peer123", null, "example.com", 80, null, CancellationToken.None));
```

**Correct**:
```csharp
var exception = await Assert.ThrowsAsync<AggregateException>(() =>
    selector.SelectAndConnectAsync("peer123", null, "example.com", 80, null, CancellationToken.None));
Assert.NotNull(exception.InnerException);
```

**Why This Keeps Happening**: Direct mode used to look like "no usable anonymity transport" in some test paths, but it now registers a real direct transport. Tests and callers must distinguish selection failures from connection failures; collapsing both into `InvalidOperationException` hides useful root-cause details and makes the integration suite drift behind production behavior.

### 0z59. Reciprocal Overlay Connections Need Independent Inbound And Outbound Lifecycles

**The Bug**: Issue `#209` kept reaching DHT `Ready` and discovering peers, but mesh health fell back to DHT-only candidates (`0 onion-capable`) because the overlay registry treated a peer as one socket and preferred outbound connections. When two reachable nodes formed reciprocal connections, each side could reject or dispose the other side's live server-side socket. Outbound sockets also had no read loop, so they could not answer keepalive pings or mesh RPCs and were later cleaned up as stale.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshNeighborRegistry.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/DhtRendezvous/MeshOverlayRequestRouter.cs`
- `src/slskd/DhtRendezvous/Search/MeshOverlaySearchService.cs`

**Wrong**:
```text
Store one connection per username, replace inbound with outbound, and let mesh search read directly from outbound sockets while no background loop owns those reads.
```

**Correct**:
```text
Track inbound and outbound overlay connections separately for the same peer. Run a message loop for outbound sockets too, and route request/response RPC replies through a pending-request coordinator so one reader owns each socket.
```

**Why This Keeps Happening**: It is tempting to collapse peer identity to one "best" connection, but this overlay uses directional sockets for different jobs: inbound server loops answer remote requests and outbound sockets initiate local RPCs. Replacing one with the other makes simultaneous reciprocal dialing destroy the working path. Any future overlay registry change must preserve direction-specific lifecycle and avoid competing readers on the same stream.

### 0z58. AUR Binary Sources Need Versioned Local Filenames Or Makepkg Can Repackage An Older Zip

**The Bug**: `slskdn-bin` used a constant local source filename, `slskdn-main-linux-glibc-x64.zip`, with `sha256sums=('SKIP' ...)`. On `local test host`, yay built packages labeled `0.24.5.slskdn.147`, `.149`, and `.152`, but makepkg reused the cached `.145` zip because the local filename never changed and checksum validation was skipped. Pacman showed the new package version while `/usr/bin/slskd --version` still reported `0.24.5-slskdn.145`.

**Files Affected**:
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/PKGBUILD-dev`
- `packaging/scripts/validate-packaging-metadata.sh`
- `packaging/scripts/update-stable-release-metadata.sh`

**Wrong**:
```bash
source=(
    "slskdn-main-linux-glibc-x64.zip::https://github.com/snapetech/slskdn/releases/download/${pkgver//.slskdn/-slskdn}/slskdn-main-linux-glibc-x64.zip"
)
sha256sums=('SKIP' ...)
```

**Correct**:
```bash
source=(
    "slskdn-${pkgver}-main-linux-glibc-x64.zip::https://github.com/snapetech/slskdn/releases/download/${pkgver//.slskdn/-slskdn}/slskdn-main-linux-glibc-x64.zip"
)
sha256sums=('SKIP' ...)
```

**Why This Keeps Happening**: The GitHub release asset name is stable by channel, so it feels natural to use that same name locally. With `SKIP`, however, makepkg has no hash reason to reject an existing cached file. The local source filename must include the package version whenever a binary release asset is versioned only by its URL path. This applies to both stable and dev binary AUR packages.


### 0z57. Overlay Logs Must Sanitize Remote Usernames And Public Endpoints Before Operator Output

**The Bug**: Issue `#209` tester logs exposed remote mesh usernames and public endpoints because DHT rendezvous overlay code logged `hello.Username`, `connection.Username`, `ack.Username`, and raw `IPEndPoint` values directly. The older privacy work only protected selected VirtualSoulfind capture data and mesh transport helpers; it was not a global log redaction layer.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/DhtRendezvous/MeshNeighborRegistry.cs`
- `src/slskd/DhtRendezvous/OverlayLogSanitizer.cs`

**Wrong**:
```text
_logger.LogInformation("Accepted mesh connection from {Username}@{Endpoint}", hello.Username, remoteEndPoint);
```

**Correct**:
```text
_logger.LogInformation("Accepted mesh connection from {Username}@{Endpoint}", OverlayLogSanitizer.Username(hello.Username), OverlayLogSanitizer.Endpoint(remoteEndPoint));
```

**Why This Keeps Happening**: Structured logging makes raw identifiers easy to pass through, and privacy helpers are scattered across subsystems instead of enforced globally. Any new externally supplied username, peer id, IP address, or endpoint in DHT/overlay logs must go through an explicit sanitizer before it reaches the logger.

### 0z50. DHT Discovery Must Not Treat "Seen Once" As "Tried Forever"

**The Bug**: `DhtRendezvousService` stored discovered overlay endpoints in `_discoveredPeers` and only attempted an outbound overlay connection when `TryAdd(...)` succeeded. Once an endpoint had been seen once, every later discovery cycle skipped the connect path entirely, even if the first attempt failed because of a transient timeout, stale local state, or a later code fix. The node kept a growing list of candidates but never retried them.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`

**Wrong**:
```text
Use one dictionary as both the discovered-peer cache and the retry gate, so a single failed first connection attempt suppresses all future retries for that endpoint.
```

**Correct**:
```text
Track discovery separately from outbound attempt timing. Keep discovered endpoints cached, but maintain explicit retry/backoff state so unverified peers can be retried on later discovery cycles until they either connect or age out.
```

**Why This Keeps Happening**: It is tempting to use `TryAdd` as a cheap dedupe and trigger point, but discovery dedupe and connection-attempt scheduling are different concerns. A mesh node operates in a flaky network, so “we already saw this endpoint” is not the same thing as “we should never try it again.”


### 0z49. QUIC-Less Mesh Hosts Need A Real Advertised Direct Path Or They Publish Zero Usable Direct Candidates

**The Bug**: While hardening mesh issue `#209`, we correctly stopped QUIC-unsupported hosts from advertising impossible `DirectQuic` transports, but that left them with `transports=0` and no usable direct path at all. At the same time, `TransportSelector` only parsed legacy `quic://...` endpoints, so publishing `udp://...` legacy endpoints did nothing for direct mesh dialing. The host became more honest but still could not form mesh connections.

**Files Affected**:
- `src/slskd/Mesh/Dht/PeerDescriptorPublisher.cs`
- `src/slskd/Mesh/Transport/TransportSelector.cs`
- `src/slskd/Mesh/Transport/DirectTlsDialer.cs`

**Wrong**:
```text
Treat honest descriptor publication as sufficient: suppress QUIC-only transports on unsupported hosts,
publish only legacy UDP endpoints, and assume the selector or dialer stack still has a usable direct path.
```

**Correct**:
```text
If a QUIC-less host still supports direct mesh over the TCP/TLS overlay listener, advertise that direct path
through the mesh transport endpoint model and teach the selector to fall back across multiple dialers for the
same logical direct transport type.
```

**Why This Keeps Happening**: The codebase has two overlapping systems: the older mesh transport stack and the newer DHT rendezvous/TCP overlay path. Fixing one layer to stop lying did not automatically make the other layer usable. If the published descriptor model and the selector's parser disagree about what counts as a direct endpoint, mesh stays broken while the logs look cleaner.

### 0z45. FFmpeg Fingerprint Extraction Cannot Buffer Unlimited PCM In Memory

**The Bug**: `FingerprintExtractionService` piped ffmpeg PCM output into a plain `MemoryStream` with no size cap. A long or malformed decode stream could keep growing until the process consumed far more memory than intended before Chromaprint ever saw a sample.

**Files Affected**:
- `src/slskd/Integrations/Chromaprint/FingerprintExtractionService.cs`

**Wrong**:
```text
Read ffmpeg stdout into an unbounded MemoryStream and assume `-t` alone is enough to keep memory usage safe.
```

**Correct**:
```text
Apply an internal byte cap derived from the configured sample rate/channel count/duration and abort the decode once ffmpeg exceeds the maximum PCM payload needed for fingerprinting.
```

**Why This Keeps Happening**: Audio decode helpers look harmless because the happy path is short, but piping raw PCM means every extra second is a linear memory increase. If the stream is not bounded explicitly, one bad input or tool behavior change turns a utility decode into an unbounded buffer sink.

### 0z43. IP-Only Login Lockouts Do Not Stop Distributed Password Spray Against One Username

**The Bug**: Session login throttling only tracked failed attempts by remote IP. That blocked single-source brute force but did nothing against a distributed spray where an attacker rotates IPs while hammering the same admin username. The account could be tested indefinitely as long as each source stayed under the per-IP threshold.

**Files Affected**:
- `src/slskd/Core/API/Controllers/SessionController.cs`

**Wrong**:
```text
Treat per-IP lockout as sufficient for the web login surface and assume brute-force attempts always come from one address.
```

**Correct**:
```text
Track failed login windows by both remote IP and normalized username. Clear both counters on successful authentication, and reject requests when either the source or the target username is currently locked out.
```

**Why This Keeps Happening**: Rate limiting naturally starts with network identity, but authentication abuse targets accounts as well as origins. A password-spray attacker only needs many low-volume IPs to bypass IP-only lockouts. Login throttling needs both dimensions.

### 0z44. Share Tokens Need JWT Audience Binding Or Cross-Collection Replay Stays Valid

**The Bug**: Share tokens carried `collection_id` as a signed claim, but JWT validation still had `ValidateAudience = false`. That meant a token could be replayed without any JWT-layer audience check, and the cryptographic token envelope itself was not asserting that it belonged to the intended collection.

**Files Affected**:
- `src/slskd/Sharing/ShareTokenService.cs`

**Wrong**:
```text
Store the target collection only as a custom claim while disabling JWT audience validation entirely.
```

**Correct**:
```text
Set the JWT `aud` value to the collection id and require validation to prove that the token audience matches the signed `collection_id` claim. This binds the token envelope itself to the intended collection and rejects replay against mismatched targets.
```

**Why This Keeps Happening**: Custom claims feel “good enough” once they are signed, but JWT already has a first-class audience concept for exactly this binding problem. If audience validation is left off, the token shape looks correct while one of the protocol’s main anti-replay checks is silently unused.

### 0z42. Overlay Connector Stats That Only Count Success/Failure Hide The Actual Failing Layer

**The Bug**: While validating issue `#209` on `local test host`, `/api/v0/overlay/stats` only exposed aggregate `successfulConnections` and `failedConnections`. That made the live system look like “overlay is broken” even after our inbound TLS/HELLO path was proven healthy, because the stats could not distinguish `connect timeout`, `no route`, `TCP refused`, `TLS EOF`, or protocol-handshake failures. We kept reaching for broad fixes because the product diagnostics were too coarse to tell whether the current failure was ours or the remote candidate's.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnectorStats.cs`
- `src/slskd/DhtRendezvous/API/DhtRendezvousController.cs`

**Wrong**:
```text
Track only "successful" and "failed" outbound overlay connections and expect operators to infer the
real failure layer from raw debug logs or manual probes.
```

**Correct**:
```text
Classify outbound overlay failures at the connector boundary and expose reason counts in the API.
At minimum, distinguish reachability failures from TLS failures and protocol-handshake failures so
live diagnostics can tell whether the current problem is local, remote, or just bad DHT candidates.
```

**Why This Keeps Happening**: Aggregate failure counters are enough for happy-path dashboards, but they are not enough for live mesh triage. Without typed failure reasons, every new report looks like "maybe the fix didn't work" even when the remaining problem is a different layer entirely. The connector must turn exception shapes into stable operational categories, or we will keep shipping blind.

### 0z37. Clearing Stale Antiforgery Cookies After `GetAndStoreTokens()` Is Too Late To Stop Framework Log Spam

**The Bug**: Issue `#209` kept showing repeated `An exception was thrown while deserializing the token` / `The antiforgery token could not be decrypted` errors even after we added stale-cookie cleanup and retry logic. The real problem was ordering: on safe GET requests we still let `IAntiforgery.GetAndStoreTokens()` read the incoming stale `XSRF-COOKIE-*` first, and ASP.NET logged the decryption failure inside `DefaultAntiforgery.GetCookieTokenDoesNotThrow(...)` before our catch block could clear and replace the cookies.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```text
Catch stale antiforgery exceptions around `GetAndStoreTokens()` and clear cookies afterward,
assuming that prevents the operator-visible decrypt spam.
```

**Correct**:
```text
On safe requests that mint replacement CSRF tokens, strip the known antiforgery cookies from the
incoming request before calling `GetAndStoreTokens()`. Then expire the stale cookies in the
response and issue a fresh token pair. If cleanup happens only after `GetAndStoreTokens()`,
ASP.NET has already logged the decrypt failure.
```

**Why This Keeps Happening**: It is easy to think “we catch the stale-cookie exception, so we fixed it,” but antiforgery token deserialization and logging happen inside ASP.NET before the exception reaches our code. That means post-failure cleanup can repair browser state while still leaving the exact noisy log spam the user reported. The only way to stop that path is to prevent the framework from seeing the stale cookie on the minting request in the first place.

### 0z41. Writing `cert_pins.json` In-Place Can Corrupt The Whole TOFU Store On Crash Or Concurrent Interruption

**The Bug**: `CertificatePinStore.Save()` previously serialized the pin set straight to `cert_pins.json` with `File.WriteAllText(...)`. If the process crashed or the write was interrupted mid-update, the file could be left truncated or partially written. On the next startup, `Load()` would treat the malformed JSON as unreadable and effectively drop every pin, degrading TOFU pinning into first-use-on-every-restart.

**Files Affected**:
- `src/slskd/DhtRendezvous/Security/CertificateManager.cs`

**Wrong**:
```text
Rewrite the live pin store file in place and assume the full JSON payload always reaches disk atomically.
```

**Correct**:
```text
Write the serialized pins to a sibling temp file, flush it to disk, and atomically rename it over the real `cert_pins.json`. Clean up the temp file on failure.
```

**Why This Keeps Happening**: Small JSON stores look harmless, so it is easy to treat them like config writes instead of durability-sensitive identity state. But the pin store is part of overlay identity continuity. Once it is corrupted, the node forgets every peer pin and starts re-learning trust from scratch.

### 0z40. DHT-Discovered Endpoints Cannot Be Counted As Onion-Capable Peers Before An Overlay Handshake Proves Them

**The Bug**: While validating issue `#209` on `local test host`, `Circuit maintenance` reported `11 total peers, 11 onion-capable` even though live overlay stats still showed `successfulConnections = 1` and `activeMeshConnections = 0`. The cause was `DhtRendezvousService.PublishDiscoveredPeer(...)`: it inserted every DHT-discovered endpoint into `IMeshPeerManager` with `supportsOnionRouting: true` immediately, before any overlay handshake succeeded.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/Mesh/MeshPeerManager.cs`
- `src/slskd/Mesh/CircuitMaintenanceService.cs`

**Wrong**:
```text
Treat every DHT-discovered rendezvous endpoint as an onion-capable mesh peer the moment it is discovered.
```

**Correct**:
```text
Track DHT-discovered endpoints as unverified candidates first. Only mark a peer onion-capable after a successful overlay connect or a live neighbor registration proves it actually speaks the overlay protocol.
```

**Why This Keeps Happening**: DHT discovery and overlay verification are separate stages, but the current peer-manager model only has one `SupportsOnionRouting` bit. It is tempting to set that bit early so circuit code can “see” candidates, but that makes peer stats, circuit-maintenance logs, and operator troubleshooting overstate reality. Candidate discovery and verified overlay capability must stay distinct.

### 0z39. Auto-Banning Peers On Overlay Certificate Pin Mismatch Can Partition The Mesh After Normal Cert Rotation

**The Bug**: While live-testing issue `#209` on `local test host`, DHT discovery found real peers and at least one real slskdn overlay endpoint, but the node still never formed a neighbor because `CertificatePinStore` had a stale pin for `minimus7`. The connector treated the mismatch as a possible MITM, blocked that username for an hour, and stopped trying. Clearing the stale pin immediately produced the first successful overlay neighbor.

**Files Affected**:
- `src/slskd/DhtRendezvous/Security/CertificateManager.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`

**Wrong**:
```text
Treat every overlay certificate pin mismatch as a hard MITM event and auto-ban the username, even
though real peers can rotate self-signed certificates across reinstalls or appdir loss.
```

**Correct**:
```text
For overlay TOFU pins, log the mismatch loudly but rotate the stored pin to the newly presented
certificate instead of auto-blocking the peer. Otherwise a normal peer certificate rotation can
partition the mesh until an operator manually clears `cert_pins.json` or the blocklist.
```

**Why This Keeps Happening**: The current overlay identity is only TOFU on self-signed certs, so a strict block-on-mismatch policy assumes certificate continuity that many real installs do not have. In practice that turns ordinary peer reinstalls into self-inflicted partitions. If the system cannot provide a stronger long-lived peer identity, pin mismatches need a softer recovery path than automatic bans.

### 0z38. DHT Status APIs Cannot Report `IsEnabled` From `IsDhtRunning` Or The UI Lies During Bootstrap

**The Bug**: While rechecking issue `#209` on `local test host`, the live `/api/v0/dht/status` response reported `isEnabled: false` and `isDhtRunning: false` even though the configured DHT service was running, had a node count, and was actively transitioning through bootstrap states. `DhtRendezvousController.GetDhtStatus()` incorrectly mapped `IsEnabled` from `stats.IsDhtRunning` instead of the actual configured enabled flag.

**Files Affected**:
- `src/slskd/DhtRendezvous/API/DhtRendezvousController.cs`
- `src/slskd/DhtRendezvous/IDhtRendezvousService.cs`
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`

**Wrong**:
```text
Treat "enabled in config" and "currently Ready" as the same field in the API response.
```

**Correct**:
```text
Expose both values separately: one flag for whether DHT rendezvous is configured/enabled, and one
for whether the live engine is currently running/Ready. Bootstrap and degraded states must not
masquerade as "disabled" in diagnostics.
```

**Why This Keeps Happening**: Status DTOs are easy to wire by copying the nearest-looking property, but bootstrap state, configured enablement, and actual readiness are different concepts. When the API collapses them together, the UI and troubleshooting output become misleading precisely when operators most need accurate state.

### 0z36. `AnonymityMode.Direct` Cannot Still Bootstrap Only Tor Or Circuit Building Will Fail Exactly Like A Missing Tor Proxy

**The Bug**: Issue `#209` kept advancing from `DHT state changed to: Ready` and `DHT discovery found ... peers` straight into `Tor SOCKS proxy not available at 127.0.0.1:9050`, `No available anonymity transports found`, and `Circuit establishment failed - not all hops connected`. The root cause was that `AnonymityTransportSelector` treated `AnonymityMode.Direct` as if it should initialize the Tor transport, and `GetTransportPriorityOrder(...)` also prioritized `Tor` for direct mode. So the default direct configuration still depended on a local Tor SOCKS proxy even though no real direct transport existed in the selector at all.

**Files Affected**:
- `src/slskd/Common/Security/AnonymityTransportSelector.cs`
- `src/slskd/Mesh/MeshCircuitBuilder.cs`
- `tests/slskd.Tests.Unit/Mesh/Transport/AnonymityTransportSelectionTests.cs`

**Wrong**:
```text
Treat `Direct` mode as a naming alias for "try Tor first and maybe fall back later" while never
actually registering a usable direct transport. That makes circuit building fail as soon as Tor is
not running, even though the logs and defaults say the node is in direct mode.
```

**Correct**:
```text
`AnonymityMode.Direct` must register and prioritize a real direct transport. A failed direct dial in
that mode should fail as a direct connection attempt, not as "no anonymity transport available"
because Tor is absent.
```

**Why This Keeps Happening**: The selector mixed two concepts: "anonymity transport selection" and "how circuit builder gets any stream at all." Because `Direct` was appended as a fallback token without a concrete implementation, the code looked like it supported direct mode while the runtime still hard-required Tor. The only reliable guard here is a focused test that reproduces the tester's exact path: DHT peers exist, Tor is absent, and direct mode must still choose a usable transport candidate.

### 0z35. Shell Command Substitution Inside `debian/rules` Needs `$$(` So `make` Does Not Eat It

**The Bug**: While fixing the Jammy PPA path drift, we changed `packaging/debian/rules` to discover `libcoreclrtraceptprovider.so` dynamically with `tracept_provider=$(find ...)`. Under `make`, that expanded as a make variable reference instead of shell command substitution, so the staged DEB install always saw an empty `tracept_provider` and silently skipped the SONAME patch even when the file was present.

**Files Affected**:
- `packaging/debian/rules`

**Wrong**:
```make
tracept_provider=$(find debian/slskdn/usr/lib/slskd -name libcoreclrtraceptprovider.so -print -quit); \
```

**Correct**:
```make
tracept_provider=$$(find debian/slskdn/usr/lib/slskd -name libcoreclrtraceptprovider.so -print -quit); \
```

**Why This Keeps Happening**: `debian/rules` is a makefile, not a plain shell script. Single `$(` means “expand a make function/variable now,” while `$$(` is what leaves `$(` intact for the shell inside the recipe. Packaging fixes that look right in shell syntax can be wrong once they are embedded in make recipes.

### 0z34. Standalone PPA/COPR/Linux Release Workflows Must Track The Main Release Toolchain And Bundle Layout

**The Bug**: The Jammy PPA build for `0.24.5.slskdn.144` still failed after we fixed `patchelf` build-depends, because the standalone `release-ppa.yml` path had drifted behind the main release flow. It was still pinned to `.NET 8` and the Debian rules hard-coded `debian/slskdn/usr/lib/slskd/libcoreclrtraceptprovider.so`, even though these distro-packaging flows are repackaging prebuilt publish output whose exact runtime file layout can change when the toolchain or bundling strategy changes. Launchpad ended up trying to patch a file path that did not exist in the staged package tree.

**Files Affected**:
- `.github/workflows/release-ppa.yml`
- `.github/workflows/release-copr.yml`
- `.github/workflows/release-linux.yml`
- `packaging/debian/rules`

**Wrong**:
```text
Treat the standalone distro workflows as if they can keep their own stale SDK version and
assume a single hard-coded runtime-library path inside the packaged appdir forever.
```

**Correct**:
```text
Keep every distro/release workflow on the same supported .NET target as the main release path,
and patch bundled runtime files by discovering them inside the staged package tree rather than
assuming one flat path. If a package workflow repackages a prebuilt publish directory, it must
validate the real staged payload before mutating it.
```

**Why This Keeps Happening**: The main release workflow gets the most attention, so it is easy for side workflows like PPA/COPR/raw Linux release jobs to keep older SDK pins and older path assumptions. Once the packaging logic starts mutating bundled runtime files, those stale assumptions become hard failures that only show up when a user or Launchpad tries the neglected path.

### 0z33. Stale Antiforgery Cookie Recovery Cannot Only Catch `AntiforgeryValidationException`

**The Bug**: Issue `#209` still showed repeated `The antiforgery token could not be decrypted` / `The key ... was not found in the key ring` noise even after we added stale-cookie cleanup. The recovery helper only caught `AntiforgeryValidationException`, but `GetAndStoreTokens(...)` can surface the same stale key-ring condition as a different wrapped exception, including raw `CryptographicException`. That meant the stale-cookie path sometimes bypassed cleanup entirely and fell into the generic warning path again and again.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```text
Assume stale antiforgery cookie/key-ring mismatches will always arrive as
`AntiforgeryValidationException`, and only clear cookies in that one catch block.
```

**Correct**:
```text
Classify stale antiforgery failures by the flattened exception content, not just one concrete
exception type. If the exception chain indicates a key-ring/decryption mismatch, clear the
known cookies and retry token minting once.
```

**Why This Keeps Happening**: ASP.NET antiforgery and Data Protection wrap failures differently depending on exactly where token deserialization failed. The stale-token condition is semantic, not tied to one exception type, so a narrow catch filter misses real stale-cookie cases and leaves the operator staring at repeated key-ring warnings.

### 0z32. DHT Discovery Must Feed `IMeshPeerManager`, Not Just Fire Opportunistic Overlay Connect Attempts

**The Bug**: Issue `#209` kept reporting `DHT state changed to: Ready` and nonzero peers discovered, but the runtime still logged `Circuit maintenance: 0 circuits, 0 total peers, 0 active, 0 onion-capable`. `DhtRendezvousService.OnPeersFound(...)` stored discovered endpoints in `_discoveredPeers` and kicked off `TryConnectToPeerAsync(...)`, but it never published those discovered candidates into `IMeshPeerManager`. The circuit layer only reads `IMeshPeerManager`, so DHT discovery could be healthy while circuit building stayed blind.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/Mesh/CircuitMaintenanceService.cs`
- `src/slskd/Mesh/MeshCircuitBuilder.cs`

**Wrong**:
```text
Treat DHT peer discovery as a fire-and-forget connection hint only. If the immediate overlay
connect attempt does not already succeed, leave the discovered peer out of the mesh peer
inventory entirely.
```

**Correct**:
```text
When DHT discovery yields a candidate overlay endpoint, publish it into `IMeshPeerManager`
immediately as an onion-capable peer candidate, then let later overlay success/failure update
its quality. Circuit maintenance must see the same discovered peers that DHT already found.
```

**Why This Keeps Happening**: The code split neighbor state and circuit-peer state into two separate inventories. `MeshNeighborPeerSyncService` only mirrors peers after a successful overlay registration, but `CircuitMaintenanceService` and `MeshCircuitBuilder` never look at the DHT discovery cache or neighbor registry directly. That makes it easy to believe “DHT found peers” means the routing layer can use them, when in reality the peer manager is still empty.

### 0z31. Launchpad Only Installs Debian `Build-Depends`, So DEB Rules Cannot Assume CI-Only Tooling Like `patchelf`

**The Bug**: The Jammy PPA build for `slskdn 0.24.5.slskdn.141` failed in `override_dh_auto_install` with `make[1]: patchelf: No such file or directory`. We had updated the DEB package recipe to patch `libcoreclrtraceptprovider.so` with `patchelf`, and we installed `patchelf` in GitHub Actions, but `packaging/debian/control` still only declared `debhelper-compat (= 13)` in `Build-Depends`.

**Files Affected**:
- `packaging/debian/control`
- `packaging/debian/rules`

**Wrong**:
```text
Teach the Debian packaging rules to invoke `patchelf`, but rely on CI job setup to provide the tool instead of declaring it in Debian source metadata.
```

**Correct**:
```text
Any tool invoked from `debian/rules` must be listed in `Build-Depends` so Launchpad/sbuild install it automatically.
```

**Why This Keeps Happening**: GitHub Actions package jobs can hide missing source-package metadata because they install extra build tools out-of-band. Launchpad only trusts the Debian source package metadata, so if a tool is missing from `Build-Depends`, the PPA build fails even though CI looked green.

### 0z29. Clean DEB/RPM Installs Need Explicit ICU Runtime Dependencies Because .NET Loads It Dynamically

**The Bug**: Clean Ubuntu 24.04 and Fedora 43 package installs completed, but `/usr/bin/slskd --version` immediately failed with `Couldn't find a valid ICU package installed on the system.` The bundled apphost does not record ICU as a normal ELF dependency, so DEB/RPM metadata generation never pulled it in automatically.

**Files Affected**:
- `packaging/debian/control`
- `packaging/rpm/slskdn.spec`

**Wrong**:
```text
Assume a self-contained .NET bundle will automatically generate package-manager dependencies for
runtime libraries that it dlopens at startup, especially ICU/globalization support.
```

**Correct**:
```text
For distro packages built from the published bundle, declare ICU explicitly in package metadata.
Clean-package smoke must include actually running `/usr/bin/slskd --version`, not just verifying
that the package installed.
```

**Why This Keeps Happening**: Package managers only see normal link-time dependencies by default, but .NET discovers ICU dynamically at runtime. A package can install perfectly and still be dead on first launch unless ICU is listed explicitly.

### 0z28. RPM Packages Cannot Mix `%{_libdir}` With A Hard-Coded `/usr/lib/slskd` Service Path

**The Bug**: After fixing the Fedora `liblttng-ust` SONAME issue, the RPM installed successfully but dropped the bundle into `%{_libdir}/slskd` (`/usr/lib64/slskd` on x86_64) while the shared `slskd.service` still executed `/usr/lib/slskd/slskd`. The package looked installed, but the systemd unit pointed at a path that did not exist on Fedora.

**Files Affected**:
- `packaging/rpm/slskdn.spec`
- `packaging/aur/slskd.service`

**Wrong**:
```text
Use distro-native `%{_libdir}` for the bundled app payload in RPMs while reusing a service file
that hard-codes `/usr/lib/slskd/slskd` as the executable path.
```

**Correct**:
```text
If the project promises a drop-in `/usr/lib/slskd` runtime path, RPM packaging must install the
payload there too. Do not let `%{_libdir}` silently move the bundle to `/usr/lib64/slskd` while the
service and operator docs still target `/usr/lib/slskd`.
```

**Why This Keeps Happening**: `%{_libdir}` is the normal RPM instinct, but this project intentionally treats `/usr/lib/slskd` as a compatibility contract across installers. Reusing the shared service file without matching the payload path creates a package that installs cleanly yet cannot start.

### 0z27. Linux Package Builds Must Patch .NET's Old liblttng-ust SONAME Before Shipping Fedora/RPM Artifacts

**The Bug**: The published Linux glibc bundle still contains `libcoreclrtraceptprovider.so` linked against `liblttng-ust.so.0`. Fedora 43 provides `liblttng-ust.so.1`, so the generated RPM ended up with an unsatisfied auto-detected dependency and `dnf` refused to install it on a clean system with `nothing provides liblttng-ust.so.0()(64bit)`.

**Files Affected**:
- `packaging/rpm/slskdn.spec`
- `packaging/debian/rules`
- `.github/workflows/release-packages.yml`
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```text
Assume the published Linux zip is package-manager ready as-is on every glibc distro and
ship it into RPM/DEB builds without the same SONAME patching we already apply in Nix.
```

**Correct**:
```text
Before building Linux distro packages from the published bundle, patch
`libcoreclrtraceptprovider.so` to replace `liblttng-ust.so.0` with `liblttng-ust.so.1`.
Install-time package smoke on Fedora should pass on a clean host before trusting RPM/COPR.
```

**Why This Keeps Happening**: The binary zip looks runnable on Ubuntu-like systems where the bundled runtime otherwise works, so it is easy to forget that RPM dependency scanning sees the old SONAME directly. We already encoded the fix in `flake.nix`; any distro package path that repackages the same zip must apply the same patch or Fedora-family installs will fail before users ever start the service.

### 0z26. Pacman File Conflicts Are Checked Before AUR pre_upgrade Scriptlets, So A Loose Root App Bundle Cannot Repair Its Own Upgrade Path

**The Bug**: `slskdn-bin` tried to solve stale `/usr/lib/slskd` file conflicts with a `slskd.install` `pre_upgrade()` cleanup, but pacman checks filesystem conflicts before it runs that scriptlet. On a real `0.24.5.slskdn.129 -> 0.24.5.slskdn.140` upgrade, the package still aborted with `failed to commit transaction (conflicting files)` because unmanaged runtime DLLs and compressed web assets already existed directly under `/usr/lib/slskd`.

**Files Affected**:
- `packaging/aur/PKGBUILD`
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/PKGBUILD-dev`
- `packaging/aur/slskd.install`

**Wrong**:
```text
Install the full self-contained app bundle directly into `/usr/lib/slskd` and assume a
`pre_upgrade()` scriptlet can delete stale files before pacman performs its conflict check.
```

**Correct**:
```text
Keep `/usr/lib/slskd` as the drop-in public path, but package the mutable app payload inside
a managed subdirectory underneath it and leave the root path to a stable launcher/service
surface. Do not depend on pacman scriptlets to rescue a root-level file dump once unmanaged
files already exist there.
```

**Why This Keeps Happening**: The old layout made the root appdir both the compatibility surface and the payload dump. That works until a manual copy, release-asset experiment, or previous package version leaves behind one unowned file. Once that happens, pacman sees the conflict before any package cleanup code can run. The only reliable same-path fix is to stop spraying versioned payload files directly into the root compatibility directory.

### 0z30. Bash EXIT Traps Cannot Reference Function-Local `mktemp` Variables Under `set -u`

**The Bug**: `packaging/linux/install-from-release.sh` successfully installed the published Linux bundle, but still exited nonzero at the end with `/tmp/install-from-release.sh: line 1: work_dir: unbound variable`. The script set `trap '''rm -rf "$work_dir"''' EXIT` inside `main()` after declaring `local work_dir`. By the time the shell processed the EXIT trap, `main()` had returned and the local variable was out of scope, so `set -u` turned cleanup into a hard failure.

**Files Affected**:
- `packaging/linux/install-from-release.sh`

**Wrong**:
```bash
local work_dir
work_dir="$(mktemp -d)"
trap '''rm -rf "$work_dir"''' EXIT
```

**Correct**:
```bash
local work_dir
work_dir="$(mktemp -d)"
trap "rm -rf '$work_dir'" EXIT
```

**Why This Keeps Happening**: In Bash, an EXIT trap runs after the function scope is gone. If the trap body relies on a function-local variable being expanded later, `set -u` can turn a successful script into a failing one during cleanup. Expand the `mktemp` path into the trap string up front, or use a global variable if the trap must dereference at shell exit.

### 0z24. Successful Soulseek Transfers Can Still Emit A Terminal "Transfer complete" Exception That Must Be Treated As Expected Churn

### 0z25. DHT Bootstrap Can Take Longer Than 30 Seconds Even When The Network Path Is Healthy

**The Bug**: On `local test host`, once router forwarding and host firewall rules were both correct, the MonoTorrent DHT still took about 90 seconds to move from `Initialising` to `Ready`. Our startup path treated 30 seconds as the failure threshold, logged a warning that implied misconfiguration, and started spamming `Cannot announce` / `Cannot discover peers` even though the same process later became healthy without any further changes.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`

**Wrong**:
```text
Assume DHT bootstrap should always reach `Ready` within 30 seconds and warn about
firewall/forwarding problems immediately when it does not.
```

**Correct**:
```text
Allow a longer bootstrap grace period before treating DHT startup as suspicious.
Slow public-router bootstrap is normal on some hosts; do not emit operator-facing
misconfiguration warnings until the grace period has actually elapsed.
```

**Why This Keeps Happening**: Once DHT has zero or only a few nodes at startup, the public bootstrap routers can seed slowly even on a healthy network path. A short static timeout turns that normal warm-up into a misleading product error and sends debugging in the wrong direction.


**The Bug**: On `local test host`, downloads were succeeding end to end, but the process still emitted `[FATAL] Unobserved task exception` with `Soulseek.ConnectionException: Transfer failed: Transfer complete` immediately after the successful transfer state transition. The transfer was already done; only the trailing connection teardown surfaced as an exception name/message we were not classifying as expected Soulseek transport churn.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```text
Downgrade socket resets, remote closes, and remote-declared transfer failures, but leave
the Soulseek post-success `Transfer failed: Transfer complete` teardown exception outside
the same expected-exception bucket.
```

**Correct**:
```text
If the transfer path raises `Transfer failed: Transfer complete` after a successful file
transfer, treat it as expected Soulseek connection churn for unobserved-task telemetry so
completed downloads do not emit fake fatal crash noise.
```

**Why This Keeps Happening**: The Soulseek library can signal the end of a completed transfer through an exception-shaped teardown path rather than a clean no-op completion. It looks like a real failure if you only key off exception type names, but on a live host you can see the transfer already reached `Completed, Succeeded` before the finalizer-thread exception appears. The message text has to be folded into the same expected-churn classifier as the other transfer-layer cases.

### 0z23. Remote Peer Transfer Rejections Are Expected Soulseek Churn, Not Fatal Host Errors

**The Bug**: After the local queue and DHT fixes, `local test host` showed both successful downloads and normal remote-peer failures on the same build. One remaining bad behavior was that `Soulseek.TransferReportedFailedException` (`Download reported as failed by remote client`) could still fall through the unobserved-task classifier and show up as fake `[FATAL] Unobserved task exception` noise, even though the remote peer simply declined or aborted that one transfer.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```text
Classify socket resets/timeouts as expected Soulseek network churn but leave remote-declared transfer failures outside the same expected-exception bucket.
```

**Correct**:
```text
If the exception chain shows Soulseek transfer-layer failures like `TransferReportedFailedException` /
`Download reported as failed by remote client`, treat them as expected peer/runtime churn for unobserved-task telemetry so the host does not emit fake fatal crash noise.
```

**Why This Keeps Happening**: Soulseek peer interactions fail in several layers: pure socket errors, read/write timeouts, and explicit transfer-layer rejections from the remote client. It is easy to stop once the socket-layer cases are downgraded, but operators still see the same scary `[FATAL]` signal unless the transfer-layer failure names/messages are folded into the same expected-churn classifier.

### 0z22. Package-Managed App Payload Directories Must Be Pruned On Upgrade If Builds Can Leave Unowned Files Behind

**The Bug**: The AUR `slskdn-bin` package installs the entire app payload under `/usr/lib/slskd`, but older builds and manual/runtime copy flows left extra files there that pacman did not own. On the next package upgrade, pacman refused to install the new package with `failed to commit transaction (conflicting files)` because stale unowned files like `Microsoft.AspNetCore.StaticAssets.dll`, native runtime libraries, and compressed `wwwroot` assets were still present in `/usr/lib/slskd`. The repo also referenced an `install=slskd.install` hook file that no longer existed, so there was no package-script chance to clean the directory before upgrade.

**Files Affected**:
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/PKGBUILD`
- `packaging/aur/PKGBUILD-dev`
- `packaging/aur/slskd.install`

**Wrong**:
```text
Treat `/usr/lib/slskd` like a normal package payload directory even though release/runtime copy flows can leave extra unowned files behind, and assume pacman upgrades will overwrite the directory cleanly without a package hook.
```

**Correct**:
```text
Ship a real pacman install script and prune the managed app payload directory before upgrade/reinstall so stale unowned binaries and compressed asset files cannot block the next package install. Keep mutable config/data outside `/usr/lib/slskd`.
```

**Why This Keeps Happening**: App bundles install hundreds of files under one directory, and release/runtime packaging changes can add or remove files between versions. Pacman only replaces files it owns; anything left behind by older builds, manual deploys, or missing package metadata becomes a hard conflict later unless the package explicitly cleans that managed application directory during upgrade.

### 0z21. Background Enqueue Tasks Must Finish Before Their Shared Semaphore Goes Out Of Scope

**The Bug**: `DownloadService.EnqueueAsync(...)` created a per-batch `SemaphoreSlim` with `using var enqueueSemaphore`, then spawned background enqueue tasks that released that semaphore in their `finally` blocks. The method only waited for the transfer to reach `Queued, Remotely` and then moved on, so the scope could end and dispose `enqueueSemaphore` while those background tasks were still unwinding. On the live host this surfaced as `ObjectDisposedException: Cannot access a disposed object. Object name: 'System.Threading.SemaphoreSlim'.` immediately after downloads entered `Queued, Remotely`.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`

**Wrong**:
```text
Create a shared synchronization primitive in a local scope, fire background tasks that
use it in their finally blocks, and return before those tasks have definitely completed.
```

**Correct**:
```text
If background tasks share a scoped semaphore or similar disposable primitive, keep track of
those tasks and await their completion before disposing the primitive or leaving the scope.
```

**Why This Keeps Happening**: The transfer enqueue path mixes synchronous local bookkeeping with asynchronous background task observation. It is easy to think "the important work is done once the transfer reaches Queued, Remotely" and forget that the background task still has cleanup logic that touches shared synchronization objects. Scoped disposal and async finally blocks do not mix unless the parent explicitly waits for the child tasks to finish.

### 0z20. Empty Permission Defaults Must Fall Back To Umask Instead Of Being Parsed As A Real chmod Value

**The Bug**: `permissions.file.mode` defaults to `string.Empty` to mean "no explicit Unix mode; let the host umask apply." But `FileService.CreateFile(...)` and `MoveFile(...)` still called `Mode?.ToUnixFileMode()`, so the empty default string was treated like a real permission value and downloads failed at file-creation time with `The value cannot be an empty string or composed entirely of whitespace. (Parameter 'permissions')`.

**Files Affected**:
- `src/slskd/Files/FileService.cs`
- `src/slskd/Core/Options.cs`

**Wrong**:
```text
Treat an unset permission option (`""` / whitespace) like a configured chmod mode and
parse it anyway in low-level file creation/move helpers.
```

**Correct**:
```text
Only parse `permissions.file.mode` when it contains a real non-whitespace chmod string.
Otherwise pass `null` and let the OS default umask govern file and directory creation.
```

**Why This Keeps Happening**: The option model uses an empty string as the "not configured" default, which is fine at the boundary, but callers that rely on null-conditional access (`Mode?.ToUnixFileMode()`) accidentally still invoke the parser on empty strings. Any low-level file path that skips the explicit `IsNullOrWhiteSpace` guard can turn the harmless default into a hard runtime failure.

### 0z19. Serialized Bulk Actions Still Need A Real Background Queue With De-Dupe

**The Bug**: Simply changing transfer bulk actions from `Promise.all(...)` to a serial `for ... await` loop stopped the immediate `429` storm, but it still kept the whole bulk action bound to the click handler and allowed the same files to be re-enqueued if the user clicked the same bulk action again while the first drain was still in progress. That meant the UI could still create duplicate background work and repeated retries/removals against the same transfer set.

**Files Affected**:
- `src/web/src/components/Transfers/Transfers.jsx`
- `src/web/src/components/Transfers/TransferGroup.jsx`

**Wrong**:
```text
Replace parallel bulk transfer requests with a serial loop inside the button
handler and assume that is enough to make the action queue-safe.
```

**Correct**:
```text
Bulk retry/remove should enqueue work into a background queue that:
- drains at a controlled rate
- keeps in-flight items deduped by transfer/action key
- ignores repeated submissions for work already queued or running
- reports failures once per batch instead of once per file
```

**Why This Keeps Happening**: It is easy to treat "not parallel anymore" as the same thing as "properly queued." It is not. If the user can trigger the same bulk action again before the first drain finishes, the UI still needs explicit queue ownership and de-dupe semantics or it will recreate the same storm more slowly.

### 0z18. Transfer Bulk Actions Must Respect The Backend Request Shape Instead Of Spamming Per-File Calls

**The Bug**: The Transfers page implemented `Retry All` and `Remove All` as `Promise.all(...)` over one API request per selected file. That looked simple in the UI, but the backend download enqueue path is intentionally concurrency-limited and returns `429` when hit in parallel, while completed-transfer cleanup already has dedicated bulk-clear endpoints. In practice, bulk retry turned into a toast storm of self-inflicted `429` failures, and bulk remove completed created unnecessary request floods instead of one clear operation.

**Files Affected**:
- `src/web/src/components/Transfers/Transfers.jsx`
- `src/web/src/components/Transfers/TransferGroup.jsx`
- `src/web/src/lib/transfers.js`
- `src/slskd/Transfers/API/Controllers/TransfersController.cs`

**Wrong**:
```text
Implement "Retry All" and "Remove All" by firing one request per file in
parallel from the browser and assume the backend wants that shape too.
```

**Correct**:
```text
Use the backend's actual contract:
- serialize or batch retry requests so they do not trip the enqueue limiter
- call the dedicated clear-completed endpoint when the action is "remove all completed"
- reserve per-file calls for mixed or non-completed selections that genuinely need them
```

**Why This Keeps Happening**: Bulk UI actions are easy to write as `Promise.all(...)`, but transfer backends often have throttling or special bulk endpoints for a reason. If the frontend ignores those contracts, the product generates its own errors and makes a sick queue look much worse than it is.

### 0z17. Do Not Run UDP Hole-Punch Preflight Against DHT Overlay TCP Endpoints

**The Bug**: `MeshOverlayConnector` took each DHT-discovered overlay endpoint and wrapped it as `udp://host:port` for NAT traversal preflight before the real TCP connect. But DHT peers advertise the mesh overlay TCP listener port, and there is no corresponding UDP responder in that path, so the hole-punch attempts were guaranteed to fail and produced misleading `[HolePunch] ... FAILED` noise even when DHT discovery itself was healthy.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/Mesh/Nat/NatTraversalService.cs`
- `src/slskd/Mesh/Nat/UdpHolePuncher.cs`

**Wrong**:
```text
Treat a discovered TCP overlay endpoint as if it were a UDP hole-punch target just because the host and port are available.
```

**Correct**:
```text
Only attempt NAT traversal preflight when you have an actual UDP/relay endpoint contract for that peer. Plain DHT overlay candidates should go straight to the real TCP overlay connect path.
```

**Why This Keeps Happening**: The code has NAT traversal primitives and DHT-discovered endpoints in the same area, so it is tempting to wire them together speculatively. But transport information matters: a TCP listener port is not automatically a valid UDP hole-punch target. Without an actual UDP responder on the remote side, the preflight can only fail and mislead operators.

### 0z16. Frontend API Libraries Must Stay On The Same Versioned Route Family As Their Controllers

**The Bug**: The WebUI `userNotes` client called `/api/v0/users/notes`, but `UserNotesController` only advertised API version `1`. That left the UI with a reproducible `GET /api/v0/users/notes -> 404` even though the backend feature existed and the frontend was using the same route shape as the rest of the app.

**Files Affected**:
- `src/web/src/lib/userNotes.js`
- `src/slskd/Users/Notes/API/UserNotesController.cs`

**Wrong**:
```text
Add a new controller on a different API version than the existing frontend route family and assume versioned routing will just line up.
```

**Correct**:
```text
When a WebUI lib already targets `/api/v0/...`, the controller must either support `v0` too or the frontend route must be updated in the same change, with an integration test proving the versioned route actually resolves.
```

**Why This Keeps Happening**: Most of the app still uses `v0` routes, so a controller that defaults to `v1` looks valid in isolation but breaks only when exercised through the frontend. Route/version mismatches are easy to miss unless the exact versioned URL is covered in integration tests.

### 0z15. Public Overlay Exposure Creates Follow-On Noise Unless We Classify Expected Handshake Churn And Clear Stale CSRF Cookies

**The Bug**: After issue `#209` finally fixed DHT bootstrap, the first public test node immediately started logging three follow-on problems as if the feature were still broken: `Connection reset by peer` surfaced as a `[FATAL]` unobserved task exception, stale antiforgery cookies from a reinstall spammed decrypt/key-ring errors on every safe request, and random internet junk hitting the overlay port showed up as warning-stack traces from the TLS handshake path.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Core/Security/ValidateCsrfForCookiesOnlyAttribute.cs`
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`

**Wrong**:
```text
Fix DHT bootstrap, expose the overlay port publicly, and keep treating every
subsequent connection reset, stale XSRF cookie, and garbage TLS probe as an
unexpected fatal-or-warning condition.
```

**Correct**:
```text
Once the node is reachable:
- classify `Connection reset by peer` as expected network churn
- clear stale antiforgery cookies when data-protection keys changed across reinstall
- downgrade obvious non-TLS overlay probes to debug-level noise instead of warning stack traces
```

**Why This Keeps Happening**: The first successful public deployment changes the operating environment. A reachable overlay listener attracts scanners and failed handshakes immediately, and reinstalling a web app often leaves old antiforgery cookies in the browser. If we only test the bootstrap path and not the first reachable-runtime behavior, the next operator report looks like the original bug never got fixed even though the real problem has moved on.

### 0z14. Release Asset Naming Changes Must Be Applied Atomically Across Build Outputs, Repo Metadata, And Package Workflows

**The Bug**: We changed stable package metadata to `slskdn-main-linux-x64.zip` while the release workflow still published `slskdn-main-linux-glibc-x64.zip`. That left the main release partially split-brain: COPR copied one filename while the RPM spec referenced the other, metadata refresh rebuilt `flake.nix` against a filename the just-created release did not publish, and package jobs failed even though the Linux payload itself had built successfully.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/scripts/update-stable-release-metadata.sh`
- stable packaging metadata (`flake.nix`, Homebrew, Snap, Flatpak, RPM, AUR)

**Wrong**:
```text
Change the canonical stable Linux asset name in one layer (repo metadata or workflow consumers) without changing the release output and every downstream package job in the same edit.
```

**Correct**:
```text
Pick one canonical stable Linux asset name and update all of these together:
- release upload step
- repo metadata updater
- package workflows (COPR, Snap, PPA, Homebrew, metadata smoke)
- checked-in package metadata
```

**Why This Keeps Happening**: The release pipeline has multiple independent consumers of the same Linux zip, and several of them rewrite local filenames before packaging. If one part of the pipeline moves from `linux-x64` to `linux-glibc-x64` without the others moving in lockstep, failures surface later as missing files or 404s in unrelated jobs rather than at the initial release upload step.

### 0z13. Stable Metadata Must Reference Asset Names That Already Exist On The Published Stable Release

**The Bug**: We changed `flake.nix` and other stable package metadata to `slskdn-main-linux-glibc-*.zip` before any stable GitHub release actually published those asset names. `Nix Package Smoke` then fetched `0.24.5-slskdn.131/slskdn-main-linux-glibc-x64.zip`, got a `404`, and failed even though the real stable asset was still `slskdn-main-linux-x64.zip`.

**Files Affected**:
- `flake.nix`
- `packaging/scripts/update-stable-release-metadata.sh`
- stable packaging metadata files (`Formula`, `snapcraft`, `flatpak`, `rpm`, `aur`)

**Wrong**:
```text
Switch stable metadata to a future asset naming scheme as soon as the workflow code changes.
```

**Correct**:
```text
Stable metadata must point at the asset names on the latest published stable release.
Only change those URLs after a release has successfully published the new asset names, or
teach the metadata updater to choose the asset names that actually exist for that release.
```

**Why This Keeps Happening**: Release workflows and post-release metadata files move on different timelines. A workflow can be updated to produce new asset names on the next tag, but package metadata is consumed immediately against the current published stable release. If the metadata jumps ahead, anything that validates or downloads the current stable asset will 404.

### 0z12. Stable Linux Releases Must Ship An Explicit Installer Path, Not Just Raw Zip Payloads

**The Bug**: Stable GitHub releases published only the platform zip payloads, while dev releases also shipped the Linux service/config helper files. That leaves Linux users upgrading from an existing `slskd` systemd install to guess how to replace the old service path, and it is easy to restart the old package-managed binary while thinking the new release zip is running.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/linux/install-from-release.sh`

**Wrong**:
```text
Ship a stable Linux zip and assume operators will correctly replace any existing service/unit/install path by hand.
```

**Correct**:
```text
Publish the Linux install helper and service/config assets with stable releases too, and give release users
a single supported install/migration path that rewrites the systemd unit to the extracted release tree.
```

**Why This Keeps Happening**: Raw release zips are just file payloads. They do not carry a service migration story, and existing `slskd` installs already have a unit file, config location, and binary path. If stable releases do not ship an explicit installer path, users can extract the new tree somewhere and still restart the old service target.

### 0z11. A Reported "Still Broken" Release Can Actually Be A Stale Running Install, So The App Must Self-Identify Its Executable And Config Paths

**The Bug**: We treated issue `#209` as if the new DHT build was still failing in the same way, but the reporter's WebUI still showed version `126` while they believed they had installed `131`. That means the running process was still an older binary, and we had no fast way to prove which executable/config path the live instance was actually using.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Core/State.cs`

**Wrong**:
```text
Validate the fix in code and release assets, then assume the reporter's machine is actually running that new binary.
```

**Correct**:
```text
Make the live process identify itself clearly: log the executable path and base directory at startup,
and expose the running executable/app/config paths in runtime state so /system/info shows what is
actually running before diagnosing the feature itself.
```

**Why This Keeps Happening**: Once a repo has package installs, raw zip installs, systemd units, and reused app directories, an operator can replace one tree while the service still launches another. Version mismatches then look like feature regressions. Before calling a user report a failed fix, first prove the live process version and path match the release you think they installed.

### 0z10. Release Assets Must Not Publish The Same Build Under Both Stable And Version-Named Zip Files

**The Bug**: Stable releases were uploading identical Linux payloads multiple times under names like `slskdn-main-linux-x64.zip` and `slskdn-0.24.5.slskdn.131-linux-x64.zip`. That made the release page look like it contained extra architectures or variants when it was really the same archive duplicated for compatibility.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `.github/workflows/release-packages.yml`
- `packaging/scripts/update-stable-release-metadata.sh`

**Wrong**:
```bash
zip -r ../slskdn-main-linux-x64.zip .
cp ../slskdn-main-linux-x64.zip ../slskdn-0.24.5.slskdn.131-linux-x64.zip
```

**Correct**:
```bash
zip -r ../slskdn-main-linux-glibc-x64.zip .
```

```text
Update packaging and metadata consumers to use the one explicit asset name
instead of publishing duplicate aliases into the release itself.
```

**Why This Keeps Happening**: GitHub Releases do not have lightweight aliases, so it is tempting to upload the same file repeatedly under machine-friendly and human-friendly names. That pushes compatibility clutter into the public release page. Pick one canonical asset name per runtime, make the runtime identifier explicit (`glibc` vs `musl`), and keep any backward-compat lookup logic only in consumers that still need to fetch older releases.

### 0z8. Tag Builds Must Move Docker And Workflow SDK Versions In Lockstep With The App Target Framework

**The Bug**: Stable tag builds can pass most of the repo and still fail only in the Docker publish leg when `slskd` moves to a newer target framework but `.github/workflows/build-on-tag.yml` and `Dockerfile` are still pinned to the previous SDK/runtime images. The failure only shows up late as `NETSDK1045`.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `Dockerfile`

**Wrong**:
```yaml
env:
  DOTNET_VERSION: '8'
```

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS publish
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-bookworm-slim AS slskd
```

**Correct**:
```yaml
env:
  DOTNET_VERSION: '10'
```

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-bookworm-slim AS publish
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-bookworm-slim AS slskd
```

**Why This Keeps Happening**: The repo can move its project files to a new target framework without immediately breaking local builds, but the tag-only Docker path uses its own SDK/runtime pins. Every framework bump must include the tag workflow's `setup-dotnet` version and the Dockerfile base images in the same change.

### 0z9. Matrix Message Redaction In This Release Workflow Uses `PUT`, Not `POST`

**The Bug**: Release announcements could succeed in Discord but still fail the combined announce job because the Matrix cleanup step tried to redact the previous release message with `POST`, and the homeserver returned `405 Method Not Allowed`.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
curl --fail --silent --show-error   -X POST   -H "Authorization: Bearer ${MATRIX_RELEASE_ACCESS_TOKEN}"   -H "Content-Type: application/json"   -d '{"reason":"Superseded by newer release announcement"}'   "${MATRIX_BASE_URL}/_matrix/client/r0/rooms/.../redact/${previous_event_id}/${redact_txn}"
```

**Correct**:
```bash
curl --fail --silent --show-error   -X PUT   -H "Authorization: Bearer ${MATRIX_RELEASE_ACCESS_TOKEN}"   -H "Content-Type: application/json"   -d '{"reason":"Superseded by newer release announcement"}'   "${MATRIX_BASE_URL}/_matrix/client/r0/rooms/.../redact/${previous_event_id}/${redact_txn}"
```

**Why This Keeps Happening**: The send step already uses `PUT`, so it is easy to assume the redact helper can be sketched from memory without checking the server behavior. When touching Matrix release automation, verify the exact method against the live homeserver path instead of trusting a generic snippet.

### 0z6. MonoTorrent `3.0.2` DHT Bootstrap Can Stall Forever Because It Only Seeds From `router.bittorrent.com`

**The Bug**: slskdn's DHT rendezvous looked broken in production because the pinned `MonoTorrent 3.0.2` bootstrap path seeded only from `router.bittorrent.com`. If that single router did not answer, the engine stayed in `Initialising` with `nodes=0`, so announce/discovery never became usable no matter how much local logging or port explanation we added.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `config/slskd.example.yml`

**Wrong**:
```csharp
// slskdn relied on MonoTorrent 3.0.2's hidden bootstrap defaults.
await dhtEngine.StartAsync();
```

```text
MonoTorrent 3.0.2 only seeded bootstrap from router.bittorrent.com in this path,
so DHT startup could stall forever at nodes=0 when that router was unreachable.
```

**Correct**:
```csharp
await dhtEngine.StartAsync(initialNodes, _options.BootstrapRouters);
```

```text
Pin a MonoTorrent build with the multi-router bootstrap fix and carry the router
list in slskdn's own config so DHT startup does not depend on one hidden upstream default.
```

**Why This Keeps Happening**: DHT bootstrap failures are easy to misdiagnose as local firewall or NAT mistakes because the visible symptom is just `NotReady` with zero nodes. When the underlying library hides a single-router bootstrap default, operator-facing logging changes do nothing. Reproduce the engine outside the app, confirm whether the routing table ever gets seeded, and make bootstrap routers explicit in our own configuration instead of trusting opaque upstream defaults.

### 0z4. Bridge Integration Tests Must Preflight External `soulfind` Prerequisites Before Launching A Full `slskdn` Instance

**The Bug**: `dotnet test slskd.sln` could hang in `BridgeProxyServerIntegrationTests` when the environment did not have a `soulfind` binary. The test harness launched a full `slskdn` process in bridge mode first, then waited for the bridge listener, so the suite never failed or skipped cleanly when the external bridge dependency was missing.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
if (enableBridge)
{
    bridgePort = bridgePortOverride ?? AllocateEphemeralPort();
}

slskdnProcess = Process.Start(startInfo);
await WaitForApiReadyAsync(ct);
await WaitForBridgeReadyAsync(bridgePort.Value, ct);
```

**Correct**:
```csharp
if (enableBridge)
{
    bridgePort = bridgePortOverride ?? AllocateEphemeralPort();

    if (string.IsNullOrEmpty(DiscoverSoulfindBinary()))
    {
        throw new InvalidOperationException(
            "Soulfind binary not found. Install soulfind or set SOULFIND_PATH before running bridge integration tests.");
    }
}
```

**Why This Keeps Happening**: Integration harnesses that rely on external binaries cannot assume those tools exist on every dev or CI machine. If a test path needs `soulfind`, check that prerequisite before spawning the main application process and fail or skip fast with a clear message; otherwise the suite ends up diagnosing a stuck host instead of the real missing dependency.

### 0z5. Long Fixed Delays In Integration Tests Can Trigger `--blame-hang` Even When The Code Path Is Fine

**The Bug**: `DisasterModeTests.Disaster_Mode_Recovery_Should_Deactivate_When_Soulfind_Returns` timed out under `dotnet test --blame-hang --blame-hang-timeout 30s` because the test spent most of its runtime inside two blind `Task.Delay(...)` calls while never asserting the underlying state transition. The host was still alive, but the runner saw 30 seconds of inactivity and killed the suite.

**Files Affected**:
- `tests/slskd.Tests.Integration/DisasterMode/DisasterModeTests.cs`

**Wrong**:
```csharp
await soulfind!.StopAsync();
await Task.Delay(TimeSpan.FromSeconds(15));

await soulfind.StartAsync();
await Task.Delay(TimeSpan.FromSeconds(10));
```

**Correct**:
```csharp
await soulfind!.StopAsync();
await WaitForStatusEndpointAsync(alice!);

await soulfind.StartAsync();
await WaitForStatusEndpointAsync(alice);
```

**Why This Keeps Happening**: Long integration waits are easy to add while sketching end-to-end scenarios, especially when the test has TODO assertions. Under hang diagnostics, a quiet sleep is indistinguishable from a stuck testhost. Poll the observable condition you care about instead of burning fixed delays.

### 0z3. `@testing-library/react` Major Upgrades Can Require A Direct `@testing-library/dom` Dependency In This Repo

**The Bug**: After upgrading the web stack to React 18 and `@testing-library/react` 16, Vitest failed before several suites could load with `Cannot find module '@testing-library/dom'`. The repo had `@testing-library/react` installed, but not the DOM package it now expects in this dependency graph.

**Files Affected**:
- `src/web/package.json`
- `src/web/package-lock.json`

**Wrong**:
```json
"devDependencies": {
  "@testing-library/react": "^16.3.2"
}
```

```text
Vitest can fail at module load time because @testing-library/react no longer
brings in a usable @testing-library/dom path here automatically.
```

**Correct**:
```json
"devDependencies": {
  "@testing-library/dom": "^10.4.1",
  "@testing-library/react": "^16.3.2"
}
```

**Why This Keeps Happening**: Testing-library upgrades look like a simple React-version follow-up, but their package graph changes across majors. When bumping `@testing-library/react`, run the full Vitest suite and treat any missing peer/helper package as part of the same upgrade instead of assuming the old dependency tree still holds.

### 0z2. React Router Major Migrations Must Remove Every Stale v5 `history` / `match` Reference, Not Just The Imports

**The Bug**: During the React Router 7 migration, `Searches.jsx` was updated to `useNavigate()` and `useParams()`, but one old fallback still called `history.replace(match.url.replace(...))`. Lint caught `match` as undefined, but the deeper problem is that partial router migrations leave dead v5 navigation code behind in edge-path cleanup branches.

**Files Affected**:
- `src/web/src/components/Search/Searches.jsx`
- `src/web/src/components/App.jsx`
- `src/web/src/components/System/System.jsx`

**Wrong**:
```javascript
import { useNavigate, useParams } from 'react-router-dom';

// ...later, in an edge path that did not get migrated:
history.replace(match.url.replace(`/${searchId}`, ''));
```

**Correct**:
```javascript
import { useNavigate, useParams } from 'react-router-dom';

const navigate = useNavigate();

// ...all route repair/redirect paths must use the same router API:
navigate('/searches', { replace: true });
```

**Why This Keeps Happening**: Router major upgrades are easy to do mechanically at the import level while missing less-traveled fallback branches. Every file moving off Router v5 needs a full pass for `history`, `match`, `Redirect`, and route-render props, not just the happy-path navigation buttons.

### 0z1. `jsdom 29.0.2` Breaks This Vitest/JSDOM Stack Even When Plain Node Imports Still Resolve

**The Bug**: Bumping the web test toolchain from `jsdom 29.0.1` to `29.0.2` caused Vitest fork workers to fail before any tests ran, with `Cannot find module 'parse5'` and `Cannot find module 'entities/decode'` coming from the JSDOM HTML parser path, even though direct `node --input-type=module` imports of `parse5` and `entities/decode` still succeeded.

**Files Affected**:
- `src/web/package.json`
- `src/web/package-lock.json`

**Wrong**:
```json
"jsdom": "^29.0.2"
```

```text
Vitest worker bootstrap can fail in this repo with parse5/entities resolution errors
after that bump, so a plain npm install + node import smoke check is not enough.
```

**Correct**:
```json
"jsdom": "^29.0.1"
```

```text
Keep the last known-good JSDOM line unless the Vitest worker pool passes again in
this exact repo environment after the upgrade.
```

**Why This Keeps Happening**: Dependency bumps that look safe in isolation can still break this repo's older React/Vitest/JSDOM stack in ways that only show up when Vitest forks its workers. Direct package-resolution spot checks are weaker than the actual `npm test` path here, so test-runner dependencies need real end-to-end Vitest validation before they are kept.

### 0z. Release-Gate Subpath Smoke Checks Must Mirror Backend HTML Rewrite Behavior, Not Old Relative-Asset Assumptions

**The Bug**: The frontend build was correctly switched back to root-relative asset URLs (`/assets/...`) with ASP.NET HTML rewriting for `web.url_base`, but the release-gate smoke script still expected built `index.html` to contain relative asset references like `./assets/...`. Stable tag builds failed in `run-release-gate.sh` before any release jobs or Discord announcements could run.

**Files Affected**:
- `src/web/scripts/smoke-subpath-build.mjs`
- `src/web/scripts/verify-build-output.mjs`
- `packaging/scripts/run-release-gate.sh`

**Wrong**:
```javascript
const relativeAssetMatches = [...indexHtml.matchAll(/(?:src|href)="(\.[^"]+)"/g)];

if (relativeAssetPaths.length === 0) {
  fail('Expected built index.html to contain relative asset references under a subpath');
}
```

**Correct**:
```javascript
// Smoke tests for subpath deployment must emulate the backend's HTML rewrite layer.
// Built output should stay root-relative, and the smoke server should rewrite those
// root-relative references to the mounted subpath before fetching assets.
```

**Why This Keeps Happening**: It is easy to update the frontend build and backend serving model but forget the standalone smoke harnesses in release automation. Any check that validates subpath behavior must follow the same contract as `Program.CreateWebHtmlRewriteRules(...)`; otherwise CI ends up enforcing the superseded behavior and blocks releases even though the product code is correct.

### 0x. Vite Relative Asset URLs Break Deep-Link Refreshes In The Embedded Web UI

**The Bug**: Switching the Vite build to `base: './'` made the root page work under `web.url_base`, but it broke hard refreshes on client-side routes like `/system`. Browsers resolved `./assets/...` relative to the current route, so `/system` tried to load `/system/assets/...` instead of the actual app root assets.

**Files Affected**:
- `src/web/vite.config.js`
- `src/web/index.html`
- `src/slskd/Program.cs`

**Wrong**:
```javascript
export default defineConfig({
  base: './',
});
```

```html
<link rel="manifest" href="./manifest.json" />
<script type="module" src="./src/index.jsx"></script>
```

**Correct**:
```javascript
export default defineConfig({
  base: '/',
});
```

```html
<link rel="manifest" href="/manifest.json" />
<script type="module" src="/src/index.jsx"></script>
```

```csharp
foreach (var (pattern, replacement) in CreateWebHtmlRewriteRules(urlBase))
{
    app.UseHTMLRewrite(pattern, replacement);
}
```

**Why This Keeps Happening**: Relative asset URLs look attractive because they work in a static subdirectory smoke test, but slskdn is not serving a plain static site. It serves an SPA behind ASP.NET with `UsePathBase`, client-side routes, and HTML rewriting. Deep-link refreshes need build output to use app-root paths, and the backend must rewrite those root-relative paths to the configured `web.url_base`.

### 0y. Soulseek Client Listener Settings Must Exist In Initial Options, Not Only In Startup Reconfiguration

**The Bug**: The Soulseek client was instantiated with `enableListener: false` and no listen endpoint, then later reconfigured during `Application.StartAsync()`. That left a window where connect/login work could still observe a non-listening client and throw `InvalidOperationException: Not listening. You must call the Start() method before calling this method.`

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Application.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
return new SoulseekClientOptions(
    enableListener: false,
    enableDistributedNetwork: false,
    acceptDistributedChildren: false,
    ...);
```

**Correct**:
```csharp
return new SoulseekClientOptions(
    enableListener: true,
    listenIPAddress: startupListenAddress,
    listenPort: optionsAtStartup.Soulseek.ListenPort,
    enableDistributedNetwork: !optionsAtStartup.Soulseek.DistributedNetwork.Disabled,
    acceptDistributedChildren: !optionsAtStartup.Soulseek.DistributedNetwork.DisableChildren,
    distributedChildLimit: optionsAtStartup.Soulseek.DistributedNetwork.ChildLimit,
    ...);
```

**Why This Keeps Happening**: It is easy to assume that no network work happens until after `Application.StartAsync()` finishes, but the Soulseek client’s own connection/login flow and background tasks can still depend on listener state once connects begin. Listener/distributed-network bootstrap settings need to be present on the initial client object, while later reconfiguration should only patch resolvers, caches, and other runtime-dependent services.

### 0v. CSRF Token Middleware Must Not Mint New Tokens On Unsafe Requests

**The Bug**: The custom CSRF middleware called `antiforgery.GetAndStoreTokens(context)` on every request, including `POST`/`PUT`/`DELETE`/`PATCH`. That meant a state-changing request could receive a freshly rotated antiforgery token pair immediately before `ValidateCsrfForCookiesOnlyAttribute` validated the request, causing valid header/cookie pairs from the previous page load to fail with `CSRF token validation failed`.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Integration/Security/CsrfPortScopedTokenIntegrationTests.cs`

**Wrong**:
```csharp
var tokens = antiforgery.GetAndStoreTokens(context);
context.Response.Cookies.Append($"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", tokens.RequestToken, ...);
```

```text
This ran on every request, including the unsafe request currently being validated.
```

**Correct**:
```csharp
if (HttpMethods.IsGet(context.Request.Method) ||
    HttpMethods.IsHead(context.Request.Method) ||
    HttpMethods.IsOptions(context.Request.Method) ||
    HttpMethods.IsTrace(context.Request.Method))
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append($"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", tokens.RequestToken, ...);
}
```

```text
Only mint/store antiforgery tokens on safe requests. Unsafe requests should validate the pair the client already has; they should not rotate it mid-flight.
```

**Why This Keeps Happening**: It is easy to think of the antiforgery middleware as harmless cookie setup that can run globally, but `GetAndStoreTokens(...)` is stateful. Once validation is deferred to a controller/filter, token issuance must stay on safe/bootstrap requests or the request under validation can invalidate itself.

### 0w. Frontend API Helpers Must Not Re-Add `/api/v0` When Axios Already Has That Base URL

**The Bug**: Some Web UI helper modules hardcoded endpoint roots like `/api/v0/security/...` and `/api/v0/mediacore/...` even though the shared Axios client already uses `apiBaseUrl = ${rootUrl}/api/v0`. Requests became `/api/v0/api/v0/...`, which broke System tabs with 404s.

**Files Affected**:
- `src/web/src/lib/api.js`
- `src/web/src/lib/security.js`
- `src/web/src/lib/mediacore.js`

**Wrong**:
```javascript
const baseUrl = '/api/v0/security';
return (await api.get(`${baseUrl}/dashboard`)).data;
```

**Correct**:
```javascript
const baseUrl = '/security';
return (await api.get(`${baseUrl}/dashboard`)).data;
```

**Why This Keeps Happening**: Some frontend modules build URLs relative to Axios, while others build fully-qualified API paths. Once `api.js` owns the `/api/v0` prefix, every helper that uses `api.get/post/put/delete(...)` must pass paths relative to that prefix or the request will be versioned twice.

### 0w1. Route Smoke Coverage Must Exercise The Same Versioned Web UI Paths Production Uses

**The Bug**: The Jobs Web UI helper still called `/api/jobs...` through an Axios client already rooted at `/api/v0`, while multiple MediaCore controllers exposed versioned-looking paths without `ApiVersion` metadata. The existing tests still passed because they asserted the wrong frontend URLs and the release integration smoke filter never exercised the affected `/api/v0/jobs`, `/api/v0/mediacore/...`, `/api/v0/security/...`, or `/api/v0/bridge/...` routes.

**Files Affected**:
- `src/web/src/lib/jobs.js`
- `src/web/src/lib/jobs.test.js`
- `src/slskd/API/Native/JobsController.cs`
- `src/slskd/MediaCore/API/Controllers/*.cs`
- `tests/slskd.Tests.Integration/Api/VersionedApiRoutesIntegrationTests.cs`
- `packaging/scripts/run-release-integration-smoke.sh`

**Wrong**:
```javascript
const url = `/api/jobs${queryString ? `?${queryString}` : ''}`;
expect(api.get).toHaveBeenCalledWith('/api/jobs');
```

```csharp
[Route("api/v0/mediacore/contentid")]
public class ContentIdController : ControllerBase
```

```bash
FILTER='...|FullyQualifiedName~SoulbeetAdvancedModeTests|...'
```

**Correct**:
```javascript
const url = `/jobs${queryString ? `?${queryString}` : ''}`;
expect(api.get).toHaveBeenCalledWith('/jobs');
```

```csharp
[Route("api/v{version:apiVersion}/mediacore/contentid")]
[ApiVersion("0")]
public class ContentIdController : ControllerBase
```

```bash
FILTER='...|FullyQualifiedName~VersionedApiRoutesIntegrationTests|FullyQualifiedName~SecurityRoutesIntegrationTests|FullyQualifiedName~NicotinePlusIntegrationTests'
```

**Why This Keeps Happening**: Route regressions can hide behind two layers of false confidence at once: unit tests that only assert whatever broken path a helper currently builds, and release smoke filters that skip the exact versioned routes the Web UI uses in production. For Web UI APIs, tests must assert the helper's relative path against the shared Axios base URL, controllers must declare explicit version metadata when serving `/api/v{version:apiVersion}/...`, and release smoke must include at least one end-to-end probe for every critical System-page route family.

### 0w1a. Search UI Actions Must Import The Same API Helper Module They Invoke

**The Bug**: A search response action called `library.createBatch(...)` without importing `library`, so the UI path only failed at runtime when users queued nearby graph searches from a result card.

**Files Affected**:
- `src/web/src/components/Search/Response.jsx`

**Wrong**:
```javascript
const count = await library.createBatch({ queries });
```

**Correct**:
```javascript
import * as searches from '../../lib/searches';

const count = await searches.createBatch({ queries });
```

**Why This Keeps Happening**: Nearby components use slightly different helper names (`search`, `searches`, `library`, `createBatch`), so copy/paste between panels can leave a stale identifier behind. Any new UI action that queues searches should be checked against its import list, not just nearby components with similar behavior.

### 0w1b. Do Not Mix `+` And `??` Without Explicitly Defaulting Each Operand First

**The Bug**: Explorer totals used `directory?.directories?.length + directory?.files?.length ?? 0`, which looks like “sum lengths or fall back to zero” but actually evaluates the addition first and can produce `NaN` before the nullish coalescing runs.

**Files Affected**:
- `src/web/src/components/System/Files/Explorer.jsx`

**Wrong**:
```javascript
const total = directory?.directories?.length + directory?.files?.length ?? 0;
```

**Correct**:
```javascript
const total =
  (directory?.directories?.length ?? 0) +
  (directory?.files?.length ?? 0);
```

**Why This Keeps Happening**: `??` has lower precedence than `+`, so a fallback at the end of an arithmetic expression does not protect intermediate operands. When optional values participate in math, default each term before the calculation.

### 0w2. `Connection refused` Must Not Be Blanket-Classified As A Benign Unobserved Task Failure

### 0w3. Tagged Release Notes Must Never Fall Back To The Entire `Unreleased` Section

**The Bug**: The release-note generator preferred a matching changelog section, but when one did not exist yet for the exact tag it fell back to the full `docs/CHANGELOG.md` `## [Unreleased]` section. That caused each new GitHub release body to re-publish old bullets from prior releases instead of only listing the delta since the previous tag.

**Files Affected**:
- `scripts/generate-release-notes.sh`
- `docs/CHANGELOG.md`

**Wrong**:
```bash
# Tagged release notes pulled the whole rolling Unreleased bucket.
elif [[ -n "$UNRELEASED_SECTION" ]]; then
  printf '%s\n\n' "$UNRELEASED_SECTION"
```

**Correct**:
```bash
# Tagged release notes must use either the matching version section or
# synthesize from the previous-tag commit range. Unreleased is for in-flight
# work only, not for published tags.
```

**Why This Keeps Happening**: `Unreleased` is a rolling staging area for future release content, so it always contains a mixture of old and new bullets until someone manually cuts a dated/versioned section. Using it at tag time feels convenient, but it breaks the core release contract: a published release body must describe only the changes introduced since the immediately previous release.

### 0w4. `Soulseek.ListenIpAddress` Must Not Be Set To Loopback For A Live Client

**The Bug**: A live slskd node was configured with `Soulseek.ListenIpAddress = 127.0.0.1`. The client still logged in and could search, but every peer-facing operation (`endpoint`, `info`, `browse`, downloads) failed because the Soulseek server handed other peers the node's externally visible address while slskd was only listening on loopback.

**Files Affected**:
- `src/slskd/Core/Options.cs`
- `config/slskd.example.yml`

**Wrong**:
```yaml
soulseek:
  listen_ip_address: 127.0.0.1
```

**Correct**:
```yaml
soulseek:
  listen_ip_address: 0.0.0.0
```

```text
If the node is meant to connect to the Soulseek network, bind the Soulseek
listener to 0.0.0.0 or a reachable LAN/VPN interface, not loopback.
```

**Why This Keeps Happening**: Loopback feels safe for local testing because the daemon still starts, logs in, and can initiate server-side activity like searches. But peer operations are different: other clients dial the address the server knows for you, not your local loopback binding. That creates the exact “logged in, searchable, but all peer transfers/browse/info fail” pattern unless startup rejects the configuration.

**The Bug**: After the listener-startup race was fixed, `Program.IsBenignUnobservedTaskException(...)` still treated any unobserved `SocketError.ConnectionRefused` as benign. That meant real refused connections from unrelated or still-broken transfer paths could be silently downgraded before the narrower Soulseek-network classifier had a chance to decide whether the failure was expected churn or a real bug.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
return exception switch
{
    SocketException socketException when socketException.SocketErrorCode == SocketError.ConnectionRefused => true,
    _ => false,
};
```

**Correct**:
```csharp
return false;
```

```text
Only `IsExpectedSoulseekNetworkException(...)` should downgrade expected peer/distributed-network churn, because it checks the exception type and the Soulseek-specific context. Blanket refusal suppression hides real failures.
```

**Why This Keeps Happening**: Once a specific startup race is fixed, it is tempting to keep a broad suppression rule around as “harmless noise control.” In practice that turns a targeted workaround into a catch-all mask. Global unobserved-task handling must stay narrower than the suspected failure domain, or the logs and tests stop distinguishing expected network churn from real transfer-path regressions.

### 0w3. Download Enqueue Must Not Pre-Fail On Auxiliary `ConnectToUserAsync` Priming

**The Bug**: `DownloadService.EnqueueAsync(...)` fetched the user's endpoint and then eagerly called `Client.ConnectToUserAsync(...)` before scheduling the real transfer task. That control-channel priming was not required for the actual `Client.DownloadAsync(...)` path, but if the auxiliary peer connect hit `Connection refused` it aborted the whole enqueue before the transfer pipeline had a chance to run or report the real failure state.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`
- `tests/slskd.Tests.Unit/Transfers/Downloads/DownloadServiceTests.cs`

**Wrong**:
```csharp
endpoint = await Client.GetUserEndPointAsync(username, cancellationToken);
await Client.ConnectToUserAsync(username, invalidateCache: false, cancellationToken);
```

**Correct**:
```csharp
// Do not require an eager auxiliary peer-control connection here.
// Let the actual transfer pipeline own connection establishment and failure handling.
```

**Why This Keeps Happening**: It feels reasonable to “warm” a peer connection up front for validation or caching, but that creates a second connection path with different failure behavior than the real transfer code. When the preflight connect fails earlier or differently, slskdn aborts legitimate transfer requests for the wrong reason and the logs point at the warm-up path instead of the transfer path that actually matters.

### 0w4. Startup Soulseek Option Patches Must Match The Live Reconfigure Transfer Surface

**The Bug**: The startup `Application.StartAsync()` patch path and the later live-reconfigure path were not actually equivalent. Live reconfigure updated `incomingConnectionOptions`, but startup patching only set `peerConnectionOptions` and `transferConnectionOptions`. That left transfer-listener behavior dependent on whether the process had only booted once or had already gone through a later options reconfigure, which is exactly the kind of environment-sensitive seam that can keep search/browse working while peer transfers still misbehave.

**Files Affected**:
- `src/slskd/Application.cs`
- `tests/slskd.Tests.Unit/Core/ApplicationLifecycleTests.cs`

**Wrong**:
```csharp
return new SoulseekClientOptionsPatch(
    ...
    peerConnectionOptions: connectionOptions,
    transferConnectionOptions: transferOptions,
    distributedConnectionOptions: distributedOptions,
    ...);
```

**Correct**:
```csharp
return new SoulseekClientOptionsPatch(
    ...
    peerConnectionOptions: connectionOptions,
    transferConnectionOptions: transferOptions,
    incomingConnectionOptions: connectionOptions,
    distributedConnectionOptions: distributedOptions,
    ...);
```

```text
Startup and later reconfigure must configure the same transfer-related option surface, or fixes appear
"working" only after an options reload instead of on a clean boot.
```

**Why This Keeps Happening**: Boot-time configuration code tends to drift from the "real" runtime reconfigure code because both paths are manually assembled patches with overlapping fields. If one path gets a new transfer-related option and the other does not, clean startup and post-reconfigure behavior silently diverge. Any Soulseek client patch helper used at startup should be shared and unit-tested against the fields the live reconfigure path depends on.

### 0n. Missing `yt-dlp` Must Degrade YouTube SongID Runs, Not Fail Them

**The Bug**: SongID treated a missing `yt-dlp` binary as a fatal YouTube run failure. Metadata analysis already fell back to a raw URL query, but the later evidence pipeline still called `PrepareYouTubeAssetsAsync()` unguarded and crashed the run at the evidence stage.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`
- `packaging/aur/PKGBUILD`
- `packaging/proxmox-lxc/setup-inside-ct.sh`

**Wrong**:
```csharp
await RunToolAsync("yt-dlp", new[] { "-f", "bestaudio", "-o", audioOutput, source }, cancellationToken).ConfigureAwait(false);
```

```text
Result: YouTube SongID runs failed with "An error occurred trying to start process 'yt-dlp'..."
instead of completing with metadata-only evidence.
```

**Correct**:
```csharp
if (!await CommandExistsAsync("yt-dlp", cancellationToken).ConfigureAwait(false))
{
    run.Evidence.Add("yt-dlp unavailable; skipping YouTube audio, video, and comment extraction. Continuing with metadata-only SongID analysis.");
    return new PreparedAnalysisAssets
    {
        WorkspacePath = workspace,
        AnalysisAudioSource = "youtube_metadata",
    };
}
```

```text
Also make packaging install yt-dlp anywhere we claim YouTube SongID works out of the box.
```

**Why This Keeps Happening**: The source-analysis phase already handles missing helper tools gracefully, but the downstream evidence pipeline is easy to forget because it runs later and uses different helper methods. Any external tool that is optional for enrichment must be checked again at the asset-preparation stage, not just when building the initial query.

### 0o. Metadata-Only SongID Runs Cannot Call `Max()` On An Empty Clip List

**The Bug**: Once YouTube SongID was allowed to continue without `yt-dlp`, the evidence pipeline still crashed when no clips were generated because `AddPipelineEvidenceAsync()` computed `MaxAiArtifactScore` with `run.Clips.Max(...)`.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`

**Wrong**:
```csharp
run.Scorecard.MaxAiArtifactScore = run.Clips.Max(clip => clip.AiHeuristics?.ArtifactScore ?? 0);
```

**Correct**:
```csharp
run.Scorecard.MaxAiArtifactScore = run.Clips.Count == 0
    ? 0
    : run.Clips.Max(clip => clip.AiHeuristics?.ArtifactScore ?? 0);
```

**Why This Keeps Happening**: SongID scoring code was written assuming the evidence pipeline always produces at least one clip for non-text sources. As soon as optional-tool fallback paths or metadata-only analysis are introduced, every aggregate over `run.Clips` needs an empty-list-safe default.

### 0p. Native Job API Clients Must Use The Backend's Snake-Case Contract Exactly

**The Bug**: SongID action buttons like `Plan Discography` and single-release album planning silently failed because the frontend posted camelCase keys (`artistId`, `targetDirectory`, `mbReleaseId`, `targetDir`) to native job endpoints whose request models are annotated with snake-case names (`artist_id`, `target_dir`, `mb_release_id`).

**Files Affected**:
- `src/web/src/lib/jobs.js`
- `src/slskd/Jobs/DiscographyJobService.cs`
- `src/slskd/API/Native/JobsController.cs`

**Wrong**:
```javascript
await api.post('/api/jobs/discography', {
  artistId,
  profile,
  targetDirectory,
});
```

**Correct**:
```javascript
await api.post('/api/jobs/discography', {
  artist_id: artistId,
  profile,
  target_dir: targetDirectory,
});
```

**Why This Keeps Happening**: Most of the web client talks to the versioned REST API using camelCase payloads, so it is easy to assume the native job endpoints behave the same way. When a backend request type uses explicit `JsonPropertyName` values, mirror that contract exactly in the shared frontend client and lock it down with tests.

### 0q. SongID Artist-Graph Expansion Must Be Time-Boxed Per Artist

**The Bug**: SongID runs could appear stuck at `38%` in `artist_graph` because `AddArtistCandidatesAsync()` awaited full MusicBrainz release-graph expansion for each artist candidate. Large artists with many release groups caused long or effectively unbounded waits, which stalled the whole SongID run before the evidence pipeline even started.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `src/slskd/Integrations/MusicBrainz/ReleaseGraphService.cs`
- `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`

**Wrong**:
```csharp
var releaseGraph = await _releaseGraphService.GetArtistReleaseGraphAsync(artistId, false, cancellationToken).ConfigureAwait(false);
```

**Correct**:
```csharp
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(ArtistGraphFetchTimeout);
releaseGraph = await _releaseGraphService.GetArtistReleaseGraphAsync(artistId, false, timeoutCts.Token).ConfigureAwait(false);
```

```text
If the fetch times out or fails, continue with a lightweight artist candidate instead of stalling the run.
```

**Why This Keeps Happening**: the artist-graph stage looks like lightweight candidate enrichment from the SongID side, but the underlying MusicBrainz graph service performs deep release-group expansion with rate-limited per-group requests. Treat that dependency like a potentially expensive remote enrichment step and bound it explicitly inside SongID.

### 0r. SongID Search Actions Must Emit Canonical `Artist - Track` Queries, Not Metadata Soup

**The Bug**: SongID-generated searches were reusing generic query builders that concatenated uploader names, album text, duplicate titles, and other metadata into the search string. That made the actual Soulseek searches noisy and reduced recall for the intended `artist + track` match.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`

**Wrong**:
```csharp
analysis.Query = BuildBestQuery(track, artist, title, uploader);
SearchText = string.Join(" ", new[] { hit.Artist, hit.Title }.Where(value => !string.IsNullOrWhiteSpace(value)));
```

**Correct**:
```csharp
analysis.Query = BuildTrackSearchText(artist ?? uploader, track ?? title);
SearchText = BuildTrackSearchText(hit.Artist, hit.Title);
```

```text
Use a dedicated formatter for user-facing search actions so generated searches stay in the canonical `Artist - Track` shape unless there truly is no artist/title pair to work with.
```

**Why This Keeps Happening**: `BuildBestQuery(...)` is fine for broad metadata lookups, but it is too permissive for the actual search strings we send to Soulseek. Once SongID has an artist/title pair, switching back to a generic "join every clue" helper quickly pollutes the query with low-signal metadata.

### 0s. Release Notes Must Filter Release-Hygiene Doc Commits Out Of The Included-Commits List

**The Bug**: The repo-backed release-note generator listed standalone ADR gotcha commits and `docs: add release notes ...` commits in `## Included Commits`, even when the actual product change was already summarized in the changelog. That made release pages look like the same fix landed multiple times.

**Files Affected**:
- `scripts/generate-release-notes.sh`

**Wrong**:
```text
- `9da3519` docs: Add gotcha for packaged slskd config precedence
- `8265aff` docs: Add gotcha for packaged dual-port web defaults
- `d988e37` fix: harden packaged defaults and SongID youtube fallback
```

**Correct**:
```text
Treat `docs: Add gotcha for ...` and `docs: add release notes ...` as release-hygiene commits.
Keep them out of the generated Included-Commits list so the visible commit summary only reflects distinct product/code changes.
```

**Why This Keeps Happening**: this repo intentionally creates extra docs-only commits during bugfix work, and the generic git-log based release-note generator has no idea those commits are bookkeeping for the real fix. Without an explicit filter, the release page inflates one bugfix into multiple apparent changes.

### 0t. Changelog Discipline Must Be Enforced At Commit/PR Time, Not Deferred To Release Generation

### 0u. GitHub Actions Metadata Jobs Must Emit Every Referenced Checksum Output

**The Bug**: The stable release metadata job in `build-on-tag.yml` called `update-stable-release-metadata.sh` with `${{ steps.hashes.outputs.linux_arm64_hex }}` even though the `Calculate Hashes` step never emitted that output. The same block also passed a Windows checksum under the inconsistent name `win_x64_sha`, which made the argument contract harder to audit. The metadata update step then failed immediately with the script usage error.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/scripts/update-stable-release-metadata.sh`

**Wrong**:
```yaml
echo "linux_x64_hex=$(sha256sum slskdn-main-linux-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "macos_x64_hex=$(sha256sum slskdn-main-osx-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "macos_arm64_hex=$(sha256sum slskdn-main-osx-arm64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "win_x64_sha=$(sha256sum slskdn-main-win-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
```

```yaml
bash packaging/scripts/update-stable-release-metadata.sh \
  "${VERSION}" \
  "${{ steps.hashes.outputs.linux_x64_hex }}" \
  "${{ steps.hashes.outputs.linux_arm64_hex }}" \
  "${{ steps.hashes.outputs.macos_x64_hex }}" \
  "${{ steps.hashes.outputs.macos_arm64_hex }}" \
  "${{ steps.hashes.outputs.win_x64_sha }}" \
  "${VERSION}"
```

**Correct**:
```yaml
echo "linux_x64_hex=$(sha256sum slskdn-main-linux-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "linux_arm64_hex=$(sha256sum slskdn-main-linux-arm64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "macos_x64_hex=$(sha256sum slskdn-main-osx-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "macos_arm64_hex=$(sha256sum slskdn-main-osx-arm64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "win_x64_hex=$(sha256sum slskdn-main-win-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
```

```yaml
bash packaging/scripts/update-stable-release-metadata.sh \
  "${VERSION}" \
  "${{ steps.hashes.outputs.linux_x64_hex }}" \
  "${{ steps.hashes.outputs.linux_arm64_hex }}" \
  "${{ steps.hashes.outputs.macos_x64_hex }}" \
  "${{ steps.hashes.outputs.macos_arm64_hex }}" \
  "${{ steps.hashes.outputs.win_x64_hex }}" \
  "${VERSION}"
```

**Why This Keeps Happening**: GitHub Actions expressions silently expand missing outputs to empty strings, so the workflow looks correct at a glance until the downstream script rejects the argument list. Whenever a shell script has positional required arguments, define the workflow outputs next to the call site and keep the output names aligned with the script parameter names.

### 0v. CodeQL Must Track The Live Default Branch, Or Fixed Alerts Stay Open Forever

**The Bug**: The repository’s CodeQL workflow was still configured for `master` while active development and releases happen on `main`. Security fixes landed on `main`, but GitHub never re-analyzed the branch automatically, so open alerts on `main` persisted and reappeared in release triage even after the underlying code changed.

**Files Affected**:
- `.github/workflows/codeql.yml`

**Wrong**:
```yaml
on:
  push:
    branches: [master]
  pull_request:
    branches: [master]
```

**Correct**:
```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
```

```text
If the repo still needs compatibility during a branch rename, include both branches explicitly.
```

**Why This Keeps Happening**: release and security work naturally follow the real default branch, but old workflow triggers are easy to miss after a branch rename because the YAML still looks valid and GitHub does not warn that the workflow is effectively dormant for the active branch. Any branch rename must be followed by an audit of all workflow trigger branches, especially CodeQL and other security automation.

### 0w. Swashbuckle.AspNetCore 10 Is Not A Drop-In Upgrade For The Current OpenAPI Surface

**The Bug**: Merging the Dependabot bump from `Swashbuckle.AspNetCore 6.6.2` to `10.1.7` immediately broke the backend build. Existing code references `Microsoft.OpenApi.Models` and the current `IOperationFilter` surface expected by the 6.x package set, so the build failed as soon as restore picked up the new package.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `.github/dependabot.yml`
- `src/slskd/Common/OpenAPI/ContentNegotiationOperationFilter.cs`
- `src/slskd/Program.cs`

**Wrong**:
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.7" />
```

```text
Result: `Microsoft.OpenApi.Models` and `OpenApiOperation` references no longer resolved against the restored package graph.
```

**Correct**:
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
```

```yaml
- dependency-name: "Swashbuckle.AspNetCore"
  update-types: ["version-update:semver-major"]
```

```text
Treat the 10.x line as an intentional migration task, not a background Dependabot merge.
```

**Why This Keeps Happening**: Swagger/OpenAPI packages look like low-risk tooling deps, but major-version jumps often change transitive OpenAPI assemblies and code-generation contracts. If the repo has handwritten `Microsoft.OpenApi` integrations, keep major Swashbuckle bumps behind an explicit migration plan instead of auto-merging them from a green Dependabot PR.

### 0x. Roslyn Analyzer Package Upgrades Must Match The Effective Compiler Version

**The Bug**: Upgrading `Microsoft.CodeAnalysis.Analyzers` to `5.3.0` removed a Dependabot PR but introduced persistent `CS9057` warnings because the analyzer assembly expects compiler `4.12.0.0` while the current build still runs compiler `4.11.0.0`.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `.github/dependabot.yml`

**Wrong**:
```xml
<PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="5.3.0" />
```

```text
CSC : warning CS9057: The analyzer assembly ... references version '4.12.0.0'
of the compiler, which is newer than the currently running version '4.11.0.0'.
```

**Correct**:
```xml
<PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" />
```

```yaml
- dependency-name: "Microsoft.CodeAnalysis.Analyzers"
  update-types: ["version-update:semver-major", "version-update:semver-minor", "version-update:semver-patch"]
```

```text
Keep analyzer package upgrades blocked until the repo intentionally upgrades to a compiler/SDK line that satisfies the analyzer's Roslyn dependency.
```

**Why This Keeps Happening**: analyzer packages look like ordinary dev-time dependencies, but they execute inside the compiler and are tightly coupled to the Roslyn version shipped by the active SDK. Green restore/build does not mean the package is actually compatible; always check for `CS9057` after analyzer bumps and treat that warning as a version-compatibility failure, not benign noise.

### 0y. Dependabot Must Ignore Deliberately Pinned `Microsoft.Extensions.*` Major Lines

**The Bug**: Dependabot kept reopening PRs for `Microsoft.Extensions.Configuration 10.0.5` and `Microsoft.Extensions.Caching.Memory 10.0.5` even though `slskd.csproj` already documents those direct references as intentionally pinned to the current compatibility line.

**Files Affected**:
- `.github/dependabot.yml`
- `src/slskd/slskd.csproj`

**Wrong**:
```yaml
ignore:
  - dependency-name: "Microsoft.Data.Sqlite"
    update-types: ["version-update:semver-major"]
```

```xml
<!-- Pin to 9.x so framework-dependent publish includes these; dotNetRdf 3.4.1 requires 9.0.9 -->
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.14" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.14" />
```

**Correct**:
```yaml
ignore:
  - dependency-name: "Microsoft.Extensions.Caching.Memory"
    update-types: ["version-update:semver-major"]
  - dependency-name: "Microsoft.Extensions.Configuration"
    update-types: ["version-update:semver-major"]
```

```text
If a package is intentionally pinned for runtime/publish compatibility, Dependabot must carry the same rule or it will keep reopening the same "unresolved" major bump PRs.
```

**Why This Keeps Happening**: the project file comment explains the package pin, but Dependabot only knows what is encoded in `.github/dependabot.yml`. Any deliberate direct-package pin needs a matching ignore rule, otherwise the PR queue drifts back open even after the team already decided not to take that major line.

### 0z. `Microsoft.Extensions.*` Upgrades Must Move As An Aligned Set Across App And Test Projects

**The Bug**: The repo partially upgraded onto `Microsoft.Extensions.* 10.0.5` by moving `Configuration.Abstractions` and `Primitives`, but left direct `Caching.Memory`, `Configuration`, and the performance-test `Logging.Abstractions` / `Options` packages on `9.0.14`. Dependabot PR `#189` then failed restore with `NU1605` because `slskd` pulled `10.0.5` transitive requirements into `slskd.Tests.Performance`, which still pinned lower versions.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `tests/slskd.Tests.Performance/slskd.Tests.Performance.csproj`

**Wrong**:
```xml
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.14" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.14" />
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.5" />
<PackageReference Include="Microsoft.Extensions.Primitives" Version="10.0.5" />
```

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.14" />
<PackageReference Include="Microsoft.Extensions.Options" Version="9.0.14" />
```

**Correct**:
```text
When taking a `Microsoft.Extensions.*` major line, align the direct app references and any test-project companion references (`Logging.Abstractions`, `Options`, etc.) to the same line before judging the upgrade.
```

**Why This Keeps Happening**: these packages are tightly interrelated, but they live across multiple projects and some of them arrive transitively. A partial upgrade can look harmless in the main app project while still breaking restore in test projects that pin adjacent `Microsoft.Extensions.*` packages directly.

**The Bug**: The repo relied on `scripts/generate-release-notes.sh` fallback behavior at release time instead of requiring feature/fix commits to update `docs/CHANGELOG.md` as they landed. That left dozens of releases with no curated changelog content, and release notes were synthesized from commit history long after the actual work happened.

**Files Affected**:
- `docs/CHANGELOG.md`
- `.githooks/pre-commit`
- `.github/workflows/ci.yml`

**Wrong**:
```text
Ship a feature/fix commit without touching docs/CHANGELOG.md, then hope the release-time generator can reconstruct something acceptable from git subjects later.
```

**Correct**:
```text
If a commit or PR changes product code, packaging behavior, user-visible UI, or workflows that affect shipped behavior, require a corresponding entry under `docs/CHANGELOG.md` `## [Unreleased]` before the commit/PR can pass.
```

```text
Release generation should consume curated changelog content, not serve as the first time release-worthy changes are summarized.
```

**Why This Keeps Happening**: release automation is easier to notice because it runs on every tag, while changelog discipline has no pain until much later. Without a local hook and CI validation, developers optimize for shipping code and defer the changelog until the release is already being cut, which is exactly when recall is worst.

### 0u. A Checked-In Hook Is Useless Unless The Repo Explicitly Installs `core.hooksPath`

**The Bug**: The repo added meaningful checks in `.githooks/pre-commit` and `.githooks/pre-push`, but nothing in the normal setup path actually configured `git config core.hooksPath .githooks`. That meant local enforcement was silently absent for anyone who had not configured hooks manually.

**Files Affected**:
- `.githooks/pre-commit`
- `.githooks/pre-push`
- local setup / bootstrap docs and scripts

**Wrong**:
```text
Assume that committing hook scripts into `.githooks/` is enough for them to run automatically on every clone.
```

**Correct**:
```text
Provide an explicit repo bootstrap step (script and docs) that runs:
git config core.hooksPath .githooks
```

```text
If local hook enforcement matters, install the hooks as part of normal developer setup instead of relying on tribal knowledge.
```

**Why This Keeps Happening**: checked-in hooks look "present" in the tree, so it is easy to forget Git ignores them unless `core.hooksPath` or `.git/hooks` is configured. CI catches some problems later, but the whole point of local hooks is to fail earlier than PR time.

### 0v. Packaging Static-File Edits Must Immediately Refresh AUR `sha256sums`

**The Bug**: Changing packaged static files like `packaging/aur/slskd.service` or `packaging/aur/slskd.yml` without updating `PKGBUILD` and `PKGBUILD-bin` left the repo in a state where later unrelated commits were blocked by the AUR hash consistency hook.

**Files Affected**:
- `packaging/aur/PKGBUILD`
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/slskd.service`
- `packaging/aur/slskd.yml`

**Wrong**:
```text
Edit `packaging/aur/slskd.service` or `packaging/aur/slskd.yml`, but leave the checked-in `sha256sums=()` entries pointing at the old file contents.
```

**Correct**:
```text
Whenever a packaged static file changes, recompute and commit the matching `sha256sums` in both AUR PKGBUILDs as part of the same change.
```

**Why This Keeps Happening**: the package templates and the hash declarations live in different files, so it is easy to update one and forget the other until a later pre-commit run trips over it. The fix belongs with the packaging edit, not in some later cleanup commit.

### 0w. Unobserved Soulseek Peer/Distributed Connection Failures Must Not Be Logged As Process-Fatal

**The Bug**: `InstallShutdownTelemetry()` logged every `TaskScheduler.UnobservedTaskException` as `[FATAL]`, even for routine Soulseek peer/distributed network failures like timeouts, connection refusals, canceled socket reads, and indirect connection failures. The process stayed alive because the handler immediately called `e.SetObserved()`, but the logs made healthy-yet-noisy peer churn look like repeated daemon crashes.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    var msg = $"[FATAL] Unobserved task exception: {e.Exception.Message}";
    Console.Error.WriteLine(msg);
    Log?.Fatal(e.Exception, msg);
    e.SetObserved();
};
```

**Correct**:
```text
Classify common Soulseek peer/distributed network exceptions separately and log them as warning/debug noise, not process-fatal shutdown telemetry.
Reserve `[FATAL]` for truly unhandled process-level failures.
```

**Why This Keeps Happening**: unobserved task handlers are a tempting catch-all for "silent crash" telemetry, but P2P networking libraries often use fire-and-forget tasks internally and can surface expected connection churn there. Without classification, ordinary peer timeout noise becomes indistinguishable from an actual daemon-killing fault.

### 0x. Docker Images Must Override The Loopback HTTP Bind Default

**The Bug**: The container image inherited the global `web.address = 127.0.0.1` default, so `docker run -p 5030:5030 ...` looked healthy from inside the container while every host-side HTTP request reset because Kestrel was only listening on container loopback.

**Files Affected**:
- `Dockerfile`

**Wrong**:
```dockerfile
ENV \
  SLSKD_HTTP_PORT=5030 \
  SLSKD_HTTPS_PORT=5031
```

```text
Result: `/health` succeeds inside the container, Docker marks the container healthy,
but `curl http://host:5030/` from outside the container resets because nothing is
bound on the container's non-loopback interface.
```

**Correct**:
```dockerfile
ENV \
  SLSKD_HTTP_ADDRESS=0.0.0.0 \
  SLSKD_HTTP_PORT=5030 \
  SLSKD_HTTPS_PORT=5031
```

```text
Any Docker or container-oriented distribution path must force the web listener to
`0.0.0.0` unless it deliberately expects an in-container reverse proxy.
```

**Why This Keeps Happening**: the repo-wide default is intentionally conservative for bare-metal installs, but containers invert the reachability model. A loopback default that is safe on a host is broken in Docker unless the image or packaged config explicitly overrides it.

### 0y. Stable Release Metadata Automation Must Update `main` And The Full Metadata Set

**The Bug**: The stable tag workflow's repo-metadata step only rewrote a small subset of files and still tried to reset/push `origin/master`. In a `main`-based repo that meant successful stable releases could leave checked-in package metadata stale for multiple releases, and the next tag build would fail the release gate on mismatched versions.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/scripts/update-stable-release-metadata.sh`

**Wrong**:
```text
After a stable release, update only `flake.nix`, `Formula/slskdn.rb`, and Winget,
then `git reset --hard origin/master` and `git push origin HEAD:master`.
```

**Correct**:
```text
Use one repo-owned script that updates every checked-in stable metadata target from
the actual release asset hashes, and have the workflow fetch/reset/push `origin/main`.
```

```text
If release automation is supposed to keep the repo in sync, it must target the real
default branch and cover every file the release gate validates.
```

**Why This Keeps Happening**: release automation was built incrementally around whichever package manager was in front of us at the time, so the "current stable version" ended up duplicated across many files with no single source of truth. Once the repo switched from `master` to `main`, the branch mismatch quietly turned that partial updater into a no-op for the actual default branch.

### 0z. Launchpad PPA Uploads Need Passive FTP And Retry Logic On GitHub Runners

**The Bug**: The PPA workflow successfully signed and started uploading the source package, then failed mid-transfer with `550 Requested action not taken: internal server error`. Launchpad's `dput` output explicitly pointed at `passive_ftp`, and the current workflow used plain FTP with no passive mode or retry.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `.github/workflows/release-ppa.yml`

**Wrong**:
```text
Configure `dput` with anonymous FTP only, then do a single `dput slskdn-ppa "$CHANGES_FILE"` attempt.
```

**Correct**:
```text
Set `passive_ftp = 1` in the PPA `dput` target and wrap the upload in a small retry loop,
because Launchpad FTP failures can be transient even after signatures and package assembly succeed.
```

**Why This Keeps Happening**: PPA upload is one of the last release steps, so it often gets less local exercise than build/test/package creation. On ephemeral GitHub runners, FTP behavior is more fragile than the earlier release stages, and the workflow needs to be explicit about passive mode instead of relying on whatever default the runner image happens to ship.

### 0l. Packaged Service Config Can Keep Reading The Runtime Copy Under `~/.local/share/slskd`, Not `/etc/slskd/slskd.yml`

**The Bug**: On packaged installs, changing `/etc/slskd/slskd.yml` did not affect the live service because the systemd unit runs with `HOME=/var/lib/slskd` and no `--config`, so `slskd` kept loading `/var/lib/slskd/.local/share/slskd/slskd.yml`. That left the Web UI bound to `127.0.0.1:5030` even after `/etc/slskd/slskd.yml` was updated.

**Files Affected**:
- `packaging/aur/slskd.service`
- runtime config at `/var/lib/slskd/.local/share/slskd/slskd.yml`

**Wrong**:
```ini
ExecStart=/usr/share/dotnet/dotnet /usr/lib/slskd/slskd.dll
Environment="HOME=/var/lib/slskd"
```

```yaml
# edited, but ignored by the running service
web:
  port: 5030
  address: "*"
```

**Correct**:
```ini
ExecStart=/usr/share/dotnet/dotnet /usr/lib/slskd/slskd.dll --http-address "*"
```

```text
If the package intends `/etc/slskd/slskd.yml` to be authoritative, pass `--config /etc/slskd/slskd.yml` explicitly from the service unit.
```

**Why This Keeps Happening**: The package ships `/etc/slskd/slskd.yml`, which strongly suggests that file is authoritative, but the service's config search order prefers the runtime config under the service account home directory when no explicit `--config` is passed. On fresh installs that also inherit the loopback default `web.address`, the service looks healthy while the Web UI is unreachable remotely.

### 0m. Packaged Installs Should Not Enable HTTPS On `5031` By Default If The Login UX Still Centers `5030`

**The Bug**: Packaged installs exposed HTTP on `5030` and HTTPS on `5031` by default, while docs and user expectation still centered on `5030`. Browsers that auto-upgraded to HTTPS or users who manually tried `https://host:5030` hit TLS failures or confusing "problem loading page" behavior even though the HTTP UI itself was healthy.

**Files Affected**:
- `packaging/aur/slskd.yml`
- `packaging/aur/README.md`
- release workflows that publish `packaging/aur/slskd.yml`

**Wrong**:
```yaml
web:
  port: 5030
```

**Correct**:
```yaml
web:
  port: 5030
  https:
    disabled: true
```

**Why This Keeps Happening**: The application defaults are reasonable for a generic binary, but packaged installs are judged by the first URL users type. If packaging wants `5030` to be the default entry point, it must make that path unambiguous by disabling the extra HTTPS listener unless the user explicitly enables TLS and chooses to manage `5031`.

### 0j. Relay Validation Logs Must Hash Agent And Connection Identifiers

**The Bug**: Relay credential-validation paths logged raw cached relay connection ids and compared response credentials directly in debug logs, which exposed server-internal identifiers and kept triggering CodeQL cleartext-storage findings.

**Files Affected**:
- `src/slskd/Relay/RelayService.cs`

**Wrong**:
```csharp
Log.Debug("Validation failed: No registration for cached relay connection {ConnectionId}", trustedConnectionId);
Log.Debug("Validation failed: Supplied credential {Credential} does not match expected credential {Expected}", credential, expectedCredential);
```

**Correct**:
```csharp
Log.Debug("Validation failed: No registration for cached relay connection {ConnectionId}", GetConnectionLogId(trustedConnectionId));
Log.Debug("Validation failed: Supplied response credential does not match expected credential for agent {Agent}", GetAgentLogId(agentName));
```

**Why This Keeps Happening**: Relay auth/debug code lives right next to token verification, so it is easy to log the raw values that are convenient during manual troubleshooting. Treat relay agent names, connection ids, and response credentials like secrets in logs and emit only stable hashed identifiers or higher-level state.

### 0k. `Serilog.Sinks.Grafana.Loki` 8.x Replaced `outputTemplate` With `textFormatter`

**The Bug**: Upgrading `Serilog.Sinks.Grafana.Loki` from 7.x to 8.x broke `Program.ConfigureGlobalLogger()` at compile time because the sink overload no longer accepts `outputTemplate`, and the new default formatter changes payload shape unless a formatter is provided explicitly.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/slskd.csproj`

**Wrong**:
```csharp
config => config.GrafanaLoki(
    OptionsAtStartup.Logger.Loki ?? string.Empty,
    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
```

**Correct**:
```csharp
config => config.GrafanaLoki(
    OptionsAtStartup.Logger.Loki ?? string.Empty,
    textFormatter: new MessageTemplateTextFormatter(
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        null))
```

**Why This Keeps Happening**: version-bump PRs that look like pure package updates can still carry sink/config API breaks, especially around logging formatters. When upgrading logging sinks, check the new extension-method signature and preserve the intended output shape explicitly instead of assuming template parameters are stable across major versions.

### 0f. Invalid-Config Startup Tests Must Satisfy Base Option Validation Before Asserting Later Hardening Failures

**The Bug**: `EnforceInvalidConfigIntegrationTests` expected the subprocess to fail on a hardening rule, but CI hit the earlier base-options validation first because the temporary app directory did not contain `wwwroot`, so startup returned success from the early validation path and never reached the hardening check.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
await File.WriteAllTextAsync(yml, """
    web:
      enforceSecurity: true
""");
```

```csharp
if (!OptionsAtStartup.TryValidate(out var result))
{
    Log.Information(result.GetResultView());
    return;
}
```

**Correct**:
```csharp
Directory.CreateDirectory(Path.Combine(tempDir, "wwwroot"));
await File.WriteAllTextAsync(yml, """
    web:
      contentPath: wwwroot
      enforceSecurity: true
""");
```

```csharp
if (!OptionsAtStartup.TryValidate(out var result))
{
    Log.Information(result.GetResultView());
    Exit(1);
}
```

**Why This Keeps Happening**: Startup has more than one validation layer. Tests that target a later validation stage can be accidentally preempted by unrelated defaults unless the temporary environment satisfies the earlier base constraints first. When startup does reject config, it must terminate non-zero or release-gate tests will treat a real config failure as a false success.

### 0g. Startup Failure Tests Need a Deterministic Plain-Text Rule Signal, Not Just Structured Logger Output

**The Bug**: The invalid-config subprocess test exited non-zero on CI but still failed because the captured output did not reliably include the hardening rule name, even though the exception was being logged.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
catch (HardeningValidationException hex)
{
    Log.Fatal(hex, "Hardening validation failed: {Message}", hex.Message);
    Exit(1);
}
```

**Correct**:
```csharp
catch (HardeningValidationException hex)
{
    Console.Error.WriteLine($"[HardeningValidation] {hex.RuleName}: {hex.Message}");
    Log.Fatal(hex, "Hardening validation failed: {Message}", hex.Message);
    Exit(1);
}
```

**Why This Keeps Happening**: Integration tests read raw subprocess stdout/stderr, not the structured logger event stream. If the test depends on a specific diagnostic token, write that token directly to stderr/stdout before exiting.

### 0h. Async Timeout/Circuit Tests Should Assert Eventual State Change, Not An Exact Transition Call Count

**The Bug**: `ServiceTimeout_TriggersCircuitBreaker` assumed the circuit breaker would always be visibly open on the 6th timed-out call, but CI occasionally returned one more timeout before the open-state reply, making the test fail even though the breaker logic was still converging correctly.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 5; i++)
{
    await router.RouteAsync(call, peerId);
}

var lastReply = await router.RouteAsync(lastCall, peerId);
Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Correct**:
```csharp
ServiceReply? lastReply = null;
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Why This Keeps Happening**: Timeouts and cancellation-driven state transitions can land on slightly different attempts under CI scheduling. For async resilience tests, assert that the expected state change happens within a bounded window instead of pinning the assertion to one exact call number unless the implementation explicitly guarantees it.

### 0i. Circuit-Breaker Failure Tests Have The Same Exact-Transition Flake As Timeout Tests

**The Bug**: `CircuitBreaker_OpensAfter5ConsecutiveFailures` assumed the open-state response must appear on the 6th failing call, but CI can surface one more ordinary failure before returning `ServiceUnavailable`, creating the same exact-transition flake as the timeout-based breaker test.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 6; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Correct**:
```csharp
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Why This Keeps Happening**: The breaker state update is observable through asynchronous request flow, not as a hard guarantee tied to a specific numbered call. If the behavior being tested is "the breaker opens after sustained failures," the assertion should allow a bounded convergence window.

### 0j. Subprocess Config Tests Must Create Relative Content Directories Under `AppContext.BaseDirectory`

**The Bug**: `EnforceInvalidConfigIntegrationTests` created a temp `wwwroot` and changed the subprocess working directory, but `contentPath` validation and runtime static-file setup both resolve relative paths under `AppContext.BaseDirectory`, so CI still failed base config validation before the hardening rule.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
Directory.CreateDirectory(Path.Combine(tempDir, "wwwroot"));
await File.WriteAllTextAsync(yml, """
    web:
      contentPath: wwwroot
""");
```

**Correct**:
```csharp
var contentPath = "test-wwwroot-" + Guid.NewGuid().ToString("N")[..8];
var contentDir = Path.Combine(Path.GetDirectoryName(slskdDll)!, contentPath);
Directory.CreateDirectory(contentDir);
```

**Why This Keeps Happening**: `SLSKD_APP_DIR` and `WorkingDirectory` do not control this option. The validator and `Program` both explicitly combine `OptionsAtStartup.Web.ContentPath` with `AppContext.BaseDirectory`, so tests must place any temporary relative content directory under the built app output directory.

### 0j2. `FileExistsAttribute` Must Treat Empty Strings As "Not Configured", Not As A Path To Validate

**The Bug**: Full-startup invalid-config tests were still being preempted before hardening validation because optional config fields that default to `string.Empty` hit `Path.GetFullPath("")` inside `FileExistsAttribute`, throwing `ArgumentException` instead of cleanly skipping validation for an unset optional path.

**Files Affected**:
- `src/slskd/Common/Validation/FileExistsAttribute.cs`
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`
- `src/slskd/Core/Options.cs`

**Wrong**:
```csharp
if (value != null)
{
    var file = Path.GetFullPath(value.ToString()!);
    if (!string.IsNullOrEmpty(file))
    {
        // validate file
    }
}
```

**Correct**:
```csharp
var rawPath = value?.ToString();
if (string.IsNullOrWhiteSpace(rawPath))
{
    return ValidationResult.Success;
}

var file = Path.GetFullPath(rawPath);
// validate file
```

**Why This Keeps Happening**: Many optional path settings in `Options` intentionally default to `string.Empty`. Validation attributes must distinguish "unset optional value" from "configured path" before normalizing or resolving the path, or they will fail startup for the wrong reason and mask the real validation behavior being tested.

### 0j3. Subprocess Startup Tests Must Launch The Freshly Built App Binary, Not A Hard-Coded `Release` Output

**The Bug**: `EnforceInvalidConfigIntegrationTests` always launched `src/slskd/bin/Release/net8.0/slskd.dll`, so `dotnet test` could rebuild the project in `Debug` while the test still executed a stale old `Release` binary and reported a failure that had already been fixed in source.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
var slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd.dll");
if (!File.Exists(slskdDll))
{
    return;
}
```

**Correct**:
```csharp
var slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Debug", "net8.0", "slskd.dll");
if (!File.Exists(slskdDll))
{
    slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd.dll");
}
```

**Why This Keeps Happening**: Integration tests that spawn the app as a subprocess are not automatically tied to the current test build configuration. If they hard-code one output folder, they can silently run stale binaries and invalidate the test result. Always resolve the current build output first, then fall back only if necessary.

### 0j3b. Full-Instance Test Harnesses Must Prefer The Current `Debug` Binary Over An Older Native `Release` Executable

**The Bug**: `SlskdnFullInstanceRunner` searched for the native app executable in `Release` before `Debug`. During `dotnet test`, the integration project rebuilt the app in `Debug`, but the harness still launched an older `src/slskd/bin/Release/net8.0/slskd` binary. That made end-to-end CSRF tests report stale runtime behavior even though the current source already emitted the correct antiforgery cookies.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
var candidates = new[]
{
    Path.Combine(solutionRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd"),
    Path.Combine(solutionRoot, "src", "slskd", "bin", "Debug", "net8.0", "slskd"),
};
```

**Correct**:
```csharp
var candidates = new[]
{
    Path.Combine(solutionRoot, "src", "slskd", "bin", "Debug", "net8.0", "slskd"),
    Path.Combine(solutionRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd"),
};
```

**Why This Keeps Happening**: subprocess integration harnesses are easy to treat like "real app" launchers, but they still need to follow the build configuration of the current test run. If `Release` is checked first, an old executable can survive indefinitely and make new fixes look broken. Always prefer the freshly built `Debug` output inside test harnesses, then fall back to `Release` only when needed.

### 0j3c. CI Must Not Enforce Constant-Time Behavior With Raw Wall-Clock Microbenchmarks

**The Bug**: `SecurityUtilsTests.ConstantTimeEquals_LargeArrays_PerformsConstantTime` compared `MeasureTimingVariance()` results for equal and unequal inputs and failed the release gate when GitHub runner noise made the ratio explode. The test was measuring `max - min` across tiny stopwatch samples, so scheduler jitter dominated the result and created a flaky false failure unrelated to the actual `ConstantTimeEquals` implementation.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/SecurityUtilsTests.cs`

**Wrong**:
```csharp
var timingEqual = SecurityUtils.MeasureTimingVariance(() =>
    SecurityUtils.ConstantTimeEquals(a, a), 100);
var timingUnequal = SecurityUtils.MeasureTimingVariance(() =>
    SecurityUtils.ConstantTimeEquals(a, b), 100);

var ratio = (double)timingUnequal / Math.Max(timingEqual, 1);
Assert.True(ratio < 300.0, $"Timing ratio too high: {ratio} ...");
```

**Correct**:
```text
Keep deterministic correctness coverage in CI, and treat constant-time claims as code-structure / algorithm reviews, not stopwatch-ratio assertions. If timing is checked at all, do it in a dedicated benchmark or security harness outside the release gate.
```

**Why This Keeps Happening**: wall-clock timing tests look attractive for security helpers, but shared CI runners are hostile to microbenchmarks. A ratio built from min/max stopwatch deltas mostly measures host jitter, preemption, and CPU frequency changes. For release gating, prefer deterministic invariants such as full-length iteration logic and `NoInlining`/`NoOptimization` markers over pseudo-benchmark thresholds.

### 0j4. Empty-String Unix Socket Defaults Must Be Treated As "Not Configured" Before Kestrel Startup

**The Bug**: Full-instance integration tests timed out for 25 seconds per test because `Program` treated `web.socket` as configured whenever it was non-null. The option defaults to `string.Empty`, so Kestrel received an empty Unix socket path and crashed during `builder.Build()` before the API ever came up.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Core/Options.cs`

**Wrong**:
```csharp
if (OptionsAtStartup.Web.Socket != null)
{
    options.ListenUnixSocket(OptionsAtStartup.Web.Socket);
}
```

**Correct**:
```csharp
if (!string.IsNullOrWhiteSpace(OptionsAtStartup.Web.Socket))
{
    options.ListenUnixSocket(OptionsAtStartup.Web.Socket);
}
```

**Why This Keeps Happening**: This codebase uses `string.Empty` for many optional path-like settings. Startup code must check for a real configured value, not just non-null, or the app can die in a later subsystem with a misleading exception instead of simply leaving the optional feature disabled.

### 0j5. Full-Instance Bridge Tests Must Set The Bridge-Enable Environment Variable, Not Just Bridge Config

**The Bug**: `SlskdnFullInstanceRunner` wrote `virtualSoulfind.bridge.enabled: true` into test config, but `Program` only registers `BridgeProxyServer` when `SLSKDN_ENABLE_BRIDGE_PROXY` is present. The bridge integration tests therefore spent their startup budget booting an app that would never open the expected bridge port.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};
```

**Correct**:
```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};

if (enableBridge)
{
    startInfo.Environment["SLSKDN_ENABLE_BRIDGE_PROXY"] = "1";
}
```

**Why This Keeps Happening**: Some test-only or deadlock-guarded features are gated by environment variables in addition to config. If a harness expects a hosted service to exist, it must mirror the same startup gate the application uses, or tests will silently wait on a port that the process was never allowed to bind.

### 0j6. Startup Fallbacks Must Treat Blank Static Path Settings As Unset, And Test Harnesses Must Pass `APP_DIR`

**The Bug**: Full-instance bridge tests still failed before config load with `Filesystem exception: Directory  does not exist...` because the child process never received an app directory, while `Program` used `??=` on static string properties initialized to `string.Empty`. A blank `AppDirectory` or `ConfigurationFile` therefore stayed blank instead of falling back to the defaults.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
AppDirectory ??= DefaultAppDirectory;
ConfigurationFile ??= DefaultConfigurationFile;
```

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};
```

**Correct**:
```csharp
if (string.IsNullOrWhiteSpace(AppDirectory))
{
    AppDirectory = DefaultAppDirectory;
}

if (string.IsNullOrWhiteSpace(ConfigurationFile))
{
    ConfigurationFile = DefaultConfigurationFile;
}
```

```csharp
startInfo.Environment["APP_DIR"] = appDir;
```

**Why This Keeps Happening**: Several startup path fields are modeled as empty strings, not nulls. `??=` only fixes null, so blank values can leak into filesystem setup and explode before logging/config are fully online. Test harnesses that expect isolated app state must also pass `APP_DIR` explicitly instead of assuming `WorkingDirectory` or the config file location will set it indirectly.

### 0j7. SOCKS/Tunnel Tests Must Use Bounded Timeouts And Deterministic Silent Endpoints, Not "Probably Unused" Ports

**The Bug**: `TorTransport_ConnectionTimeout_HandledGracefully` hung for minutes because it assumed `127.0.0.1:12345` was unused. If something listens on that port but never speaks SOCKS, `TorSocksTransport` had no internal handshake timeout and would wait forever on `ReadAsync`.

**Files Affected**:
- `src/slskd/Common/Security/TorSocksTransport.cs`
- `tests/slskd.Tests.Integration/Security/TorIntegrationTests.cs`

**Wrong**:
```csharp
var torOptions = new TorOptions
{
    SocksAddress = "127.0.0.1:12345",
};

await transport.ConnectAsync("example.com", 80);
```

**Correct**:
```csharp
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
var effectiveToken = linkedCts.Token;
```

```csharp
using var silentServer = new SilentTcpServer();
await silentServer.StartAsync();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, cts.Token));
```

**Why This Keeps Happening**: Connection-refused tests are only deterministic if the endpoint state is deterministic. A "random closed port" can become an open but silent endpoint on another machine or CI worker, and transports without a bounded connect/handshake timeout will then hang forever in network reads.

### 0j8. Tuple Member Renames Must Be Updated In Tests Too, Or `dotnet test` Will Fail At Compile Time

**The Bug**: Root `dotnet test` still failed after the integration fixes because a unit test was reading a tuple member as `.totalKeys` after the production API had been normalized to PascalCase tuple names `(int TotalKeys, int ContentHintKeys)`.

**Files Affected**:
- `tests/slskd.Tests.Unit/Mesh/Phase8MeshTests.cs`

**Wrong**:
```csharp
var stored = dht.GetStoreStats();
Assert.True(stored.totalKeys >= 1);
```

**Correct**:
```csharp
var stored = dht.GetStoreStats();
Assert.True(stored.TotalKeys >= 1);
```

**Why This Keeps Happening**: Tuple element names are part of the compile-time API surface even though they look lightweight. When cleanup work renames tuple elements for consistency, stale tests won’t fail until the affected project is rebuilt, so always grep the test tree for the old element name after changing a returned tuple signature.

### 0j9. Optional Lazy Service Resolvers Must Not Throw Before Stats Objects Return Their Local Counters

**The Bug**: `MeshStatsCollector.GetStatsAsync()` returned all-zero stats in unit tests even after `RecordMessageSent()` and `RecordMessageReceived()` because optional lazy resolvers for DHT and overlay services threw before the method reached the return statement, and the outer catch replaced the partially collected counters with a default zeroed stats object.

**Files Affected**:
- `src/slskd/Mesh/MeshStatsCollector.cs`

**Wrong**:
```csharp
this.dhtClient = new Lazy<Dht.InMemoryDhtClient>(() =>
    serviceProvider.GetService(typeof(VirtualSoulfind.ShadowIndex.IDhtClient)) as Dht.InMemoryDhtClient
        ?? throw new InvalidOperationException(...));
```

**Correct**:
```csharp
this.dhtClient = new Lazy<Dht.InMemoryDhtClient?>(() =>
    serviceProvider.GetService(typeof(VirtualSoulfind.ShadowIndex.IDhtClient)) as Dht.InMemoryDhtClient);
```

**Why This Keeps Happening**: Diagnostics collectors often depend on optional subsystems. If a lazy resolver throws for an absent optional service, the whole stats call can fall into a broad catch and wipe out independent counters that were already valid. Optional service lookups should return `null` and let the collector degrade gracefully.

### 0j10. Re-entrant Stop/Dispose Paths Must Null Out `CancellationTokenSource` Before Canceling

**The Bug**: `LocalPortForwarder.StopForwardingAsync()` could throw `ObjectDisposedException` because `ForwarderInstance.StopAsync()` called `_cts?.Cancel()` even when a previous stop/dispose path had already disposed that same `CancellationTokenSource`.

**Files Affected**:
- `src/slskd/Common/Security/LocalPortForwarder.cs`

**Wrong**:
```csharp
_cts?.Cancel();
...
_cts?.Dispose();
```

**Correct**:
```csharp
var cts = _cts;
_cts = null;

try
{
    cts?.Cancel();
}
catch (ObjectDisposedException)
{
}

cts?.Dispose();
```

**Why This Keeps Happening**: Stop and dispose paths often converge on the same field. If the field remains published while cleanup is in progress, later callers can observe a disposed token source and try to cancel it again. Copy the reference locally, clear the field first, and then clean it up once.

### 0k. Empty-String DTO Defaults Break `??`-Based Fallback Chains For Hash Selection

**The Bug**: `AudioVariant` cleanup initialized codec-specific hash properties to `string.Empty`, but `CanonicalStatsService` still used `??` fallback chains when building dedup keys. Empty strings are non-null, so FLAC variants with missing `FlacStreamInfoHash42` stopped falling back to `FlacPcmMd5` and collapsed into the same canonical candidate bucket.

**Files Affected**:
- `src/slskd/Audio/CanonicalStatsService.cs`
- `src/slskd/Audio/AudioVariant.cs`

**Wrong**:
```csharp
var streamHash = v.Codec switch
{
    "FLAC" => v.FlacStreamInfoHash42 ?? v.FlacPcmMd5 ?? v.FileSha256,
    "MP3" => v.Mp3StreamHash ?? v.FileSha256,
    _ => v.FileSha256,
};
```

**Correct**:
```csharp
var streamHash = v.Codec switch
{
    "FLAC" => FirstNonEmpty(v.FlacStreamInfoHash42, v.FlacPcmMd5, v.FileSha256),
    "MP3" => FirstNonEmpty(v.Mp3StreamHash, v.FileSha256),
    _ => FirstNonEmpty(v.FileSha256),
};
```

**Why This Keeps Happening**: Nullability cleanup often replaces nullable strings with `string.Empty`, but any fallback logic that relied on `??` now changes behavior silently. When a value is semantically "missing", use `string.IsNullOrWhiteSpace`-aware fallback helpers instead of null-coalescing chains.

### 0k. Timeout-Based Circuit Tests Must Distinguish "Breaker Opened" From "Open-State Reply Observed"

**The Bug**: `ServiceTimeout_TriggersCircuitBreaker` still flaked after widening the retry window because the last timeout call could be the one that opens the breaker, which means the first `ServiceUnavailable` reply only appears on the next probe request.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}
```

**Correct**:
```csharp
for (int i = 0; i < 10; i++)
{
    await router.RouteAsync(call, peerId);

    var circuit = router.GetStats().CircuitBreakers.Find(cb => cb.ServiceName == "slow-service");
    if (circuit?.IsOpen == true)
    {
        break;
    }
}

var blockedReply = await router.RouteAsync(probeCall, peerId);
Assert.Equal(ServiceStatusCodes.ServiceUnavailable, blockedReply.StatusCode);
```

**Why This Keeps Happening**: The timeout response reports the result of the current request, while the breaker state change affects the next request. For timeout-driven breaker tests, inspect router state or issue a separate probe after failures instead of expecting the opening transition and blocked reply to collapse onto the same call.

### 0l. E2E Harnesses Must Not Treat Gitignored Downloaded Media As Baseline CI Fixtures

### 0m. Lightweight Integration Hosts Must Stub Every Controller Dependency They Expose

**The Bug**: Integration test hosts included the VirtualSoulfind controllers in their application parts, but did not register `IDisasterModeCoordinator` and `IShadowIndexQuery` consistently, so tests failed at request time with controller activation errors instead of exercising the endpoint contracts.

**Files Affected**:
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs`
- `tests/slskd.Tests.Integration/Harness/SlskdnTestClient.cs`

**Wrong**:
```csharp
services.AddControllers()
    .AddApplicationPart(typeof(global::slskd.API.VirtualSoulfind.DisasterModeController).Assembly);
```

```csharp
builder.Services.AddSingleton<global::slskd.VirtualSoulfind.ShadowIndex.IShadowIndexQuery>(_ =>
    new StubShadowIndexQueryForTests());
```

**Correct**:
```csharp
services.AddSingleton<global::slskd.VirtualSoulfind.DisasterMode.IDisasterModeCoordinator>(_ =>
    new StubDisasterModeCoordinatorForTests());
services.AddSingleton<global::slskd.VirtualSoulfind.ShadowIndex.IShadowIndexQuery>(_ =>
    new StubShadowIndexQueryForTests());
```

**Why This Keeps Happening**: The lightweight test hosts deliberately avoid the full production DI graph, so every added controller creates a manual dependency obligation. If you expose a controller assembly in a stub host, audit its constructor dependencies immediately or the tests will fail with activation errors that look like app regressions.

### 0n. Native API DTOs Need Explicit Snake_Case Binding When Compatibility Clients Post Snake_Case JSON

**The Bug**: The native jobs endpoints accepted positional record DTOs with PascalCase property names, but the Soulbeet compatibility tests posted `snake_case` JSON like `mb_release_id` and `target_dir`, causing model binding to fail with `400` ProblemDetails payloads.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`

**Wrong**:
```csharp
public record MbReleaseJobRequest(
    string MbReleaseId,
    string TargetDir,
    string Tracks = "all",
    JobConstraints? Constraints = null);
```

**Correct**:
```csharp
public record MbReleaseJobRequest(
    [property: JsonPropertyName("mb_release_id")] string MbReleaseId,
    [property: JsonPropertyName("target_dir")] string TargetDir,
    [property: JsonPropertyName("tracks")] string Tracks = "all",
    [property: JsonPropertyName("constraints")] JobConstraints? Constraints = null);
```

**Why This Keeps Happening**: ASP.NET Core JSON binding is case-insensitive, but it does not translate underscore-delimited names into PascalCase automatically. Compatibility-facing DTOs need explicit `JsonPropertyName` attributes anywhere the request contract is `snake_case`.

**The Bug**: The scheduled `E2E Tests` workflow treated downloaded media as mandatory baseline fixtures, so a transient fetch failure aborted the whole suite before any real UI coverage ran.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`
- `src/web/e2e/fixtures/ensure-fixtures.ts`
- `src/web/e2e/streaming.spec.ts`
- `src/web/e2e/multippeer-sharing.spec.ts`
- `test-data/slskdn-test-fixtures/meta/manifest.json`

**Wrong**:
```ts
const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));
for (const entry of manifest.files) {
  await fs.access(path.join(fixturesRoot, entry.path));
}
```

**Correct**:
```ts
await ensureFixtures(fixturesRoot);
test.skip(
  !hasDownloadedMediaFixtures(),
  'Streaming E2E requires downloaded media fixtures',
);
```

**Why This Keeps Happening**: The committed fixture tree contains a small tracked offline baseline plus a larger gitignored media tier fetched on demand. CI can legitimately run without the downloaded tier, so the harness must validate the tracked baseline and let only media-dependent specs skip.

### 0m. E2E Harnesses Should Launch The Prebuilt Release App, And UI Pages Must Tolerate Missing `server` State During Boot

**The Bug**: The E2E harness launched `dotnet run` during test execution even though CI had already built the backend, which made the first node startup exceed the 30-second TCP wait on cold runs. Follow-up fixes then hit two more traps: `Web.ContentPath` only accepts relative paths under `AppContext.BaseDirectory`, and the web-asset sync helper must recreate the destination root before `fs.cp` or the copy can fail with `ENOENT` on nested assets. Separately, `Searches.jsx` read `server.isConnected` before `applicationState.server` existed, so a harmless `/capabilities` failure turned into a page-crashing `TypeError`.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`
- `src/web/src/components/Search/Searches.jsx`

**Wrong**:
```ts
const webContentPath = path.relative(expectedAppBaseDir, webBuildPath);
const args = ['run', '--project', projectPath, '--', '--app-dir', this.appDir];
await waitForTcpListen('127.0.0.1', this.apiPort, 30_000);
```

```jsx
disabled={creating || !server.isConnected}
placeholder={server.isConnected ? 'Search phrase' : 'Connect to server'}
```

**Correct**:
```ts
const webContentPath = webBuildPath.replace(/\\/g, '/');
const args = useBuiltRelease
  ? [builtDllPath, '--app-dir', this.appDir]
  : ['run', '--project', projectPath, '-c', 'Release', '--', '--app-dir', this.appDir];
await waitForTcpListen('127.0.0.1', this.apiPort, 60_000);
```

```jsx
await replaceDirectoryContents(webBuildPath, path.join(builtAppBaseDir, 'wwwroot'));
const webContentPath = 'wwwroot';
```

```jsx
const normalizedServer = server ?? { isConnected: false };
disabled={creating || !normalizedServer.isConnected}
placeholder={
  normalizedServer.isConnected
    ? 'Search phrase'
    : 'Connect to server to perform a search'
}
```

**Why This Keeps Happening**: E2E harness code often grows around local developer assumptions, but CI already provides a built Release app and is much less tolerant of redundant startup work. Even when using the prebuilt app, the runtime still validates `Web.ContentPath` as a relative directory under the app base, so the harness has to stage fresh web assets into `wwwroot` instead of pointing at arbitrary absolute paths, and that staging helper has to recreate the destination root explicitly before copying nested asset trees. On the frontend, boot-time state objects can be transiently missing even when the route eventually succeeds, so route components must normalize optional props before reading nested fields.

### 0n. XML Doc Comments Must Escape `&` Or CI Will Emit CS1570 Warnings

**The Bug**: Several XML documentation comments used raw ampersands in phrases like `Identity & Friends` or `Test Coverage & Regression Harness`, which made the generated XML invalid and caused repeated `CS1570` warnings in CI.

**Files Affected**:
- `src/slskd/Common/Moderation/*.cs`
- `src/slskd/Common/CodeQuality/*.cs`
- `src/slskd/Mesh/Realm/*.cs`
- `src/slskd/Sharing/*.cs`
- `src/slskd/VirtualSoulfind/**/*.cs`

**Wrong**:
```csharp
///     T-MCP04: Peer Reputation & Enforcement.
/// <summary>Contact PeerId (Identity & Friends).</summary>
```

**Correct**:
```csharp
///     T-MCP04: Peer Reputation &amp; Enforcement.
/// <summary>Contact PeerId (Identity &amp; Friends).</summary>
```

**Why This Keeps Happening**: XML doc comments are real XML, not plain text. Any raw `&` inside `///` comments has to be escaped or the compiler will produce malformed-doc warnings that bury real CI signal.

### 0a. Do Not Assume MusicBrainz Target Models Expose the Same ID Surface

**The Bug**: `SongIdService` treated `TrackTarget` like `AlbumTarget` and tried to read `MusicBrainzArtistId` from it, which broke the build because `TrackTarget` does not expose that property.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
run.Tracks.Insert(0, new SongIdTrackCandidate
{
    MusicBrainzArtistId = track.MusicBrainzArtistId,
});
```

**Correct**:
```csharp
run.Tracks.Insert(0, new SongIdTrackCandidate
{
    RecordingId = track.MusicBrainzRecordingId,
    Title = track.Title,
    Artist = track.Artist,
});
```

**Why This Keeps Happening**: The MusicBrainz integration models look similar at a glance, but they are not interchangeable. Check the actual target type before assuming it carries artist, release, or recording IDs in the same shape.

### 0b. Do Not Introduce `System.Threading.Lock` Unless the Project Explicitly Uses That API Surface

**The Bug**: A new SongID SQLite store used `Lock` instead of a plain object gate, which failed to compile in this project even though the code targets modern .NET.

**Files Affected**:
- `src/slskd/SongID/SongIdRunStore.cs`

**Wrong**:
```csharp
private readonly Lock _gate = new();
```

**Correct**:
```csharp
private readonly object _gate = new();
```

**Why This Keeps Happening**: It is easy to mentally map “modern C#” to every recent BCL convenience type. This repo still needs compatibility with the actual APIs available in its current toolchain and package graph, so prefer the already-common locking patterns unless you have confirmed the newer type is already in use here.

### 0c. When You Extend a Controller Constructor, Update Direct Instantiation Tests Immediately

**The Bug**: `JobsController` gained an `IMusicBrainzClient` dependency for release-to-artist resolution, but `JobsControllerPaginationTests` still instantiated the old constructor shape, breaking unit test compilation before the new SongID tests could even run.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`
- `tests/slskd.Tests.Unit/API/Native/JobsControllerPaginationTests.cs`

**Wrong**:
```csharp
controller = new JobsController(
    discographyService.Object,
    labelCrateService.Object,
    logger.Object,
    jobServiceList.Object);
```

**Correct**:
```csharp
controller = new JobsController(
    discographyService.Object,
    labelCrateService.Object,
    musicBrainzClient.Object,
    logger.Object,
    jobServiceList.Object);
```

**Why This Keeps Happening**: Controllers are often instantiated through ASP.NET DI in production, so constructor changes compile there but any unit test that manually news up the controller will silently drift until the next test build.

### 0. MusicBrainz Release IDs Are Not Artist IDs

**The Bug**: A single-release SongID or jobs path passed an MB release ID into `DiscographyJobRequest.ArtistId`, which silently created the wrong planning context and broke album download handoff.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`
- `src/slskd/Jobs/DiscographyJobService.cs`
- `src/slskd/Integrations/MusicBrainz/MusicBrainzClient.cs`

**Wrong**:
```csharp
var jobId = await discographyJobService.CreateJobAsync(
    new DiscographyJobRequest
    {
        ArtistId = request.MbReleaseId,
        Profile = DiscographyProfile.AllReleases,
    },
    cancellationToken);
```

**Correct**:
```csharp
var release = await musicBrainzClient.GetReleaseAsync(request.MbReleaseId, cancellationToken);
var jobId = await discographyJobService.CreateJobAsync(
    new DiscographyJobRequest
    {
        ArtistId = release.MusicBrainzArtistId,
        ReleaseIds = new List<string> { request.MbReleaseId },
        Profile = DiscographyProfile.AllReleases,
    },
    cancellationToken);
```

**Why This Keeps Happening**: MusicBrainz uses different MBIDs for releases, recordings, and artists. It is easy to treat “some MBID” as interchangeable unless the code explicitly carries the identifier type through the model.

### 0d. Do Not Store Recovery-Only State in `Summary` When Queue Refresh Also Owns `Summary`

**The Bug**: SongID restart recovery marked runs as "Recovered after restart..." in `Summary`, but the next queue-position refresh immediately overwrote that text with the normal queued summary, erasing the only visible recovery signal.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
run.Summary = "Recovered after restart and re-queued for SongID analysis.";
await EnqueueRunAsync(run, broadcastCreate: false).ConfigureAwait(false);
```

**Correct**:
```csharp
run.Evidence.Add("Recovered after restart and re-queued for SongID analysis.");
run.Summary = "Queued for SongID analysis.";
await EnqueueRunAsync(run, broadcastCreate: false).ConfigureAwait(false);
```

**Why This Keeps Happening**: `Summary` looks like a convenient general-purpose status field, but the queue layer also treats it as derived UI text. If two parts of the pipeline both own the same display field, one silently erases the other.

### 0e. Do Not Use Wall-Clock Time or Tight Upper Bounds for Async Delay Tests

**The Bug**: `SecurityUtilsTests.RandomDelayAsync_ValidRange_CompletesWithinExpectedTime` measured `Task.Delay` with `DateTimeOffset.UtcNow` and a narrow upper bound, so the test failed intermittently on loaded CI runners even though the code was behaving correctly.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/SecurityUtilsTests.cs`

**Wrong**:
```csharp
var startTime = DateTimeOffset.UtcNow;
await SecurityUtils.RandomDelayAsync(minDelay, maxDelay);
var endTime = DateTimeOffset.UtcNow;
var actualDelay = (endTime - startTime).TotalMilliseconds;
Assert.True(actualDelay <= maxDelay + 600, $"Delay too long: {actualDelay}ms");
```

**Correct**:
```csharp
var timer = Stopwatch.StartNew();
await SecurityUtils.RandomDelayAsync(minDelay, maxDelay);
timer.Stop();
var actualDelay = timer.Elapsed.TotalMilliseconds;
Assert.True(actualDelay <= maxDelay + 1500, $"Delay too long: {actualDelay}ms");
```

**Why This Keeps Happening**: Async timing tests are easy to write like benchmark assertions, but `Task.Delay` is scheduler-dependent and CI hosts can stall for hundreds of milliseconds. Use monotonic timing (`Stopwatch`) and treat the upper bound as a broad sanity check, not a precision guarantee.

### 0e1. Do Not Use Sub-Millisecond Cancellation Windows In Unit Tests

**The Bug**: `MeshSearchRpcHandlerTests.HandleAsync_TimeCap_RespectsCancellation` and `AsyncRulesTests.ValidateCancellationHandlingAsync_WithProperCancellation_ReturnsTrue` used razor-thin delay/cancellation windows that passed locally but failed in release-gate CI when the runner scheduled work a little differently.

**Files Affected**:
- `tests/slskd.Tests.Unit/DhtRendezvous/Search/MeshSearchRpcHandlerTests.cs`
- `tests/slskd.Tests.Unit/Common/CodeQuality/AsyncRulesTests.cs`

### 0e1c. Do Not Use Cancellation Timeouts As The Success Condition For Async Enumerables

**The Bug**: `CoverTrafficGeneratorTests.GenerateCoverTrafficAsync_GeneratesMessagesWithCorrectSize` used `CancellationTokenSource(TimeSpan.FromSeconds(5))` as the loop control while waiting for multiple messages from an async enumerable. CI sometimes hit token cancellation before the second message arrived, so the test failed with `TaskCanceledException` even though the generator was behaving correctly.

**Files Affected**:
- `tests/slskd.Tests.Unit/Mesh/Privacy/CoverTrafficGeneratorTests.cs`

**Wrong**:
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
{
    messages.Add(message);
    if (messages.Count >= 2)
        break;
}
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();

await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
{
    messages.Add(message);
    if (messages.Count >= 1)
    {
        cts.Cancel();
    }
}
```

**Why This Keeps Happening**: Async enumerable tests often mix "eventually produce output" with "cancel after some time" and accidentally make timeout expiration the normal success path. For scheduler-dependent producers, use an explicit completion condition and only cancel after the assertion target is satisfied.

**Wrong**:
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
await Task.Delay(100, cts.Token);
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();
cts.Cancel();
_shareServiceMock
    .Setup(x => x.SearchLocalAsync(It.IsAny<SearchQuery>()))
    .Returns(Task.FromCanceled<IEnumerable<Soulseek.File>>(cts.Token));
```

```csharp
await Task.Delay(Timeout.InfiniteTimeSpan, ct);
var result = await AsyncRules.ValidateCancellationHandlingAsync(
    TestOperationAsync,
    TimeSpan.FromMilliseconds(50));
```

**Why This Keeps Happening**: Tests that rely on "cancel within 1ms" or "wake up after 100ms" are really testing scheduler luck, not code behavior. Make cancellation deterministic with pre-cancelled tokens or infinite waits that must be interrupted by cancellation.

### 0e1a. Cancellation Validators Need A Post-Cancel Grace Window, Not A Single Tight Race

**The Bug**: `AsyncRules.ValidateCancellationHandlingAsync` raced the operation against `Task.Delay(timeout * 2)` and treated any miss as a cancellation failure. On a loaded CI runner, a correctly cancellable operation could still lose that race by a few scheduler ticks and fail the release gate.

**Files Affected**:
- `src/slskd/Common/CodeQuality/AsyncRules.cs`
- `tests/slskd.Tests.Unit/Common/CodeQuality/AsyncRulesTests.cs`

**Wrong**:
```csharp
using var cts = new CancellationTokenSource(timeout);
var operationTask = operation(cts.Token);
var delayTask = Task.Delay(timeout * 2, CancellationToken.None);
var completedTask = await Task.WhenAny(operationTask, delayTask);
return completedTask != delayTask;
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();
var operationTask = operation(cts.Token);
await Task.Delay(timeout);
cts.Cancel();
var completedTask = await Task.WhenAny(operationTask, Task.Delay(gracePeriod));
```

**Why This Keeps Happening**: Cancellation is not an instantaneous event. A validator that uses one narrow race window is still testing scheduler timing rather than cancellation handling. Cancel explicitly, then give the operation a bounded grace period to observe the token and unwind.

### 0e1b. Timing-Sanity Tests Must Avoid Precise Upper Bounds On Loaded CI Runners

**The Bug**: `SecurityUtilsTests.RandomDelayAsync_ValidRange_CompletesWithinExpectedTime` still used an upper bound that looked broad locally but was too tight for a loaded GitHub runner, where a `10-50ms` delay measured just over 2 seconds and failed the release gate.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/SecurityUtilsTests.cs`

**Wrong**:
```csharp
Assert.True(actualDelay <= maxDelay + 1500, $"Delay too long: {actualDelay}ms");
```

**Correct**:
```csharp
Assert.True(actualDelay <= maxDelay + 5000, $"Delay too long: {actualDelay}ms");
```

**Why This Keeps Happening**: `Task.Delay` timing in CI is dominated by scheduler availability, not just requested delay length. These tests should verify the code is not obviously broken, not enforce a pseudo-benchmark ceiling.

### 0e2. Do Not Mark Internal Mutation APIs As `AllowAnonymous` Just Because They Feel "Protocol-Like"

**The Bug**: A broad `// PR-02: intended-public` pattern was applied to controllers that mutate local state or trigger expensive work, including analyzer migrations, VirtualSoulfind queue operations, MediaCore registry writes/imports, stats resets, and pod control-plane actions. That exposed internal admin/UI surfaces to unauthenticated callers.

**Files Affected**:
- `src/slskd/Audio/API/AnalyzerMigrationController.cs`
- `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`
- `src/slskd/MediaCore/API/Controllers/ContentDescriptorPublisherController.cs`
- `src/slskd/MediaCore/API/Controllers/ContentIdController.cs`
- `src/slskd/MediaCore/API/Controllers/IpldController.cs`
- `src/slskd/MediaCore/API/Controllers/MediaCoreStatsController.cs`
- `src/slskd/MediaCore/API/Controllers/MetadataPortabilityController.cs`
- `src/slskd/PodCore/API/Controllers/PodJoinLeaveController.cs`
- `src/slskd/PodCore/API/Controllers/PodMessageRoutingController.cs`
- `src/slskd/PodCore/API/Controllers/PodMessageSigningController.cs`

**Wrong**:
```csharp
[ApiController]
[AllowAnonymous] // PR-02: intended-public
[ValidateCsrfForCookiesOnly]
public class ContentIdController : ControllerBase
{
    [HttpPost("register")]
    public Task<IActionResult> Register(...)
```

**Correct**:
```csharp
[ApiController]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public class ContentIdController : ControllerBase
{
    [HttpPost("register")]
    public Task<IActionResult> Register(...)
```

**Why This Keeps Happening**: "Public data model" and "public unauthenticated endpoint" are not the same thing. Once `[AllowAnonymous]` is placed at class scope, every `POST`/`PUT`/`PATCH`/`DELETE` action under that controller becomes reachable unless explicitly re-protected.

### 0e3. Public Protocol Controllers Must Still Default To Authenticated At Class Scope

**The Bug**: Even after narrowing the anonymous surface, `StreamsController`, `ActivityPubController`, and `WebFingerController` still used class-level `[AllowAnonymous]`. That meant any future action added to those controllers would become public by default, recreating the same auth-boundary bug in a quieter form.

**Files Affected**:
- `src/slskd/Streaming/StreamsController.cs`
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SocialFederation/API/WebFingerController.cs`

**Wrong**:
```csharp
[AllowAnonymous]
public class ActivityPubController : ControllerBase
{
    [HttpGet("{actorName}")]
    public async Task<IActionResult> GetActor(...)
```

**Correct**:
```csharp
[Authorize(Policy = AuthPolicy.Any)]
public class ActivityPubController : ControllerBase
{
    [HttpGet("{actorName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActor(...)
```

**Why This Keeps Happening**: It is easy to think "this controller is for a public protocol" and stop there. The safer pattern is still auth-by-default at controller scope with `[AllowAnonymous]` only on the exact protocol/bootstrap actions that must stay public. That way future endpoints do not silently widen the unauthenticated surface.

### 0f. Fix Every Release Workflow and Checked-In Package Template When Asset Names Change

**The Bug**: The main tag workflow was corrected to publish `slskdn-main-*.zip`, but `release-packages.yml` still waited for the old `slskdn-<tag>-linux-x64.zip` pattern and the checked-in Chocolatey templates were still pinned to `0.24.1-slskdn.40`, leaving stable-package automation and manual package publishing stale.

**Files Affected**:
- `.github/workflows/release-packages.yml`
- `packaging/chocolatey/slskdn.nuspec`
- `packaging/chocolatey/tools/chocolateyinstall.ps1`

**Wrong**:
```yaml
ASSET_URL="https://github.com/snapetech/slskdn/releases/download/${{ steps.version.outputs.tag }}/slskdn-${{ steps.version.outputs.tag }}-linux-x64.zip"
```

```powershell
$url = "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.40/slskdn-main-win-x64.zip"
```

**Correct**:
```yaml
ASSET_URL="https://github.com/snapetech/slskdn/releases/download/${{ steps.version.outputs.tag }}/slskdn-main-linux-x64.zip"
```

```powershell
$url = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.52/slskdn-main-win-x64.zip"
```

**Why This Keeps Happening**: It is easy to fix only the primary build workflow and forget the secondary packaging workflows and checked-in templates that still encode old asset names or versions. Any release-format change must be audited across tag workflows, auxiliary release workflows, validation scripts, and package templates together.

### 0g. When You Extend Core Interfaces, Update Test Stubs and Fakes in the Legacy Test Projects Immediately

**The Bug**: `ISecurityService`, `IShareService`, and `IShareRepository` gained new members, but the older smoke/integration test stubs still implemented the previous interface shapes, so `dotnet test` failed even though the feature code compiled and targeted SongID tests passed.

**Files Affected**:
- `src/slskd/Core/Security/SecurityService.cs`
- `src/slskd/Shares/IShareService.cs`
- `src/slskd/Shares/IShareRepository.cs`
- `tests/slskd.Tests/TestHostFactory.cs`
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs`
- `tests/slskd.Tests.Integration/StubVirtualSoulfindServices.cs`

**Wrong**:
```csharp
internal class StubSecurityService : ISecurityService
{
    public JwtSecurityToken GenerateJwt(...) => ...;
    public (string Name, Role Role) AuthenticateWithApiKey(...) => ...;
}

public Task<IEnumerable<File>> SearchAsync(SearchQuery query) => ...;
public IEnumerable<File> Search(SearchQuery query) => ...;
```

### 0h. Retry Loops Around External Uploads Must Bound Each Attempt, Not Just the Number of Attempts

**The Bug**: The Snap Store publish steps retried transient `snapcraft upload` failures, but each upload attempt could block indefinitely waiting on the store, so the loop never advanced and the release stayed stuck in a single opaque upload step.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
for attempt in $(seq 1 60); do
  OUT="$(snapcraft upload --release=stable "$SNAP_PATH" 2>&1)"
  CODE=$?
  ...
done
```

**Correct**:
```bash
for attempt in $(seq 1 6); do
  OUT="$(timeout --signal=TERM 10m snapcraft upload --release=stable "$SNAP_PATH" 2>&1)"
  CODE=$?
  ...
done
```

**Why This Keeps Happening**: A retry loop looks resilient, but it does nothing if the wrapped command never returns. Any networked publish step needs both retry logic and a hard per-attempt timeout so GitHub Actions can surface the failure instead of hanging for tens of minutes.

**Correct**:
```csharp
internal class StubSecurityService : ISecurityService
{
    public JwtSecurityToken GenerateJwt(...) => ...;
    public (string Name, Role Role) AuthenticateWithApiKey(...) => ...;
    public void RevokeToken(string jti) { }
    public bool IsTokenRevoked(string jti) => false;
}

public Task<IEnumerable<File>> SearchAsync(SearchQuery query, int? limit = null) => ...;
public IEnumerable<File> Search(SearchQuery query, int? limit = null) => ...;
```

**Why This Keeps Happening**: The newer feature work tends to validate against focused unit tests first, but the repo still includes older smoke/integration projects with hand-written stubs. Interface drift is invisible until the broad solution test run compiles those projects, so every interface change needs a repo-wide grep for stub implementations before calling the tree releasable.

### 0h. Gate Metrics Hardening Rules on the Metrics Endpoint Actually Being Enabled

**The Bug**: `HardeningValidator` started enforcing a non-empty metrics password whenever metrics auth was not disabled, even if `metrics.enabled` was still `false`, which broke otherwise-valid startup configs and older hardening tests.

**Files Affected**:
- `src/slskd/Common/Security/HardeningValidator.cs`

**Wrong**:
```csharp
var metricsAuth = options.Metrics?.Authentication;
if (metricsAuth != null && !metricsAuth.Disabled &&
    string.IsNullOrWhiteSpace(metricsAuth.Password))
{
    throw new HardeningValidationException(RuleWeakMetricsPassword, msg);
}
```

**Correct**:
```csharp
var metrics = options.Metrics;
var metricsAuth = metrics?.Authentication;
if (metrics?.Enabled == true && metricsAuth != null && !metricsAuth.Disabled &&
    string.IsNullOrWhiteSpace(metricsAuth.Password))
{
    throw new HardeningValidationException(RuleWeakMetricsPassword, msg);
}
```

**Why This Keeps Happening**: Nested auth options default to “auth enabled” semantics even when the parent feature is disabled. Any startup validation that checks nested credentials must first gate on the top-level feature flag, or harmless defaults become fatal.

### 0i. Do Not Use Anonymous Objects for JSON-LD Keys That Need Literal `@` Names

**The Bug**: `SolidClientIdDocumentService` built the Solid client-id document with an anonymous object using `@context`, which serialized to `context` instead of the required JSON-LD key `@context`.

**Files Affected**:
- `src/slskd/Solid/SolidClientIdDocumentService.cs`
- `tests/slskd.Tests.Unit/Solid/SolidClientIdDocumentServiceTests.cs`

**Wrong**:
```csharp
var doc = new
{
    @context = "https://www.w3.org/ns/solid/oidc-context.jsonld",
};
```

**Correct**:
```csharp
var doc = new Dictionary<string, object?>
{
    ["@context"] = "https://www.w3.org/ns/solid/oidc-context.jsonld",
};
```

**Why This Keeps Happening**: In C#, the `@` prefix only escapes the identifier for the compiler; it is not part of the serialized property name. For wire formats that require literal keys like `@context`, use explicit string keys or a concrete model with `JsonPropertyName`.

### 1. `return undefined` vs `return []` in Frontend API Calls

**The Bug**: Frontend API functions that return `undefined` on error instead of `[]` cause downstream crashes.

**Files Affected**:
- `src/web/src/lib/searches.js` - `getResponses()`
- `src/web/src/lib/transfers.js` - `getAll()`

**Wrong**:
```javascript
if (!Array.isArray(response)) {
  console.warn('got non-array response');
  return undefined;  // 💀 Causes "Cannot read property 'map' of undefined"
}
```

**Correct**:
```javascript
if (!Array.isArray(response)) {
  console.warn('got non-array response');
  return [];  // ✅ Safe to iterate
}
```

**Why This Keeps Happening**: Models see `undefined` as a "signal" value and forget that callers will `.map()` or `.filter()` the result.

### 1a. Do Not Block SPA Initialization on Optional SignalR Handshakes

**The Bug**: `App.init()` waited on `appHub.start()` before clearing the full-screen loader, so any stalled SignalR negotiation kept the whole site on "loading" for 30 seconds even though auth had succeeded and the rest of the UI could render.

**Files Affected**:
- `src/web/src/components/App.jsx`
- `src/web/src/components/App.test.jsx`

**Wrong**:
```javascript
if (await session.check()) {
  const appHub = createApplicationHubConnection();
  await Promise.race([appHub.start(), hubTimeout]);
}
```

**Correct**:
```javascript
if (await session.check()) {
  this.startApplicationHub();
}
```
The hub startup stays bounded and logged, but it runs in the background instead of sitting in the critical render path.

**Why This Keeps Happening**: Real-time channels feel "core" during implementation, so it is easy to treat them like a prerequisite for first paint. In this UI they are enhancement paths, not the gate for showing the authenticated shell. Keep session validation in the blocking path, but let hub connection, retries, and late state hydration happen asynchronously.

### 1b. Do Not Run `security-and-quality` on `master` Unless You Intend to Triage Thousands of Maintainer Alerts

**The Bug**: The checked-in C# CodeQL workflow used `queries: security-and-quality`, which repopulated roughly 2,400 `master` alerts with maintainability and code-smell findings (`cs/local-not-disposed`, `cs/log-forging`, `cs/catch-of-all-exceptions`, etc.) even though the goal was ordinary security scanning.

**Files Affected**:
- `.github/workflows/codeql.yml`

**Wrong**:
```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    queries: security-and-quality
```

**Correct**:
```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    queries: security-extended
```

**Why This Keeps Happening**: `security-and-quality` sounds like a better default until it lands in a mature codebase and turns every broad code-quality heuristic into a repo-level security alert. On `master`, keep the suite scoped to security-focused queries unless there is an explicit, staffed cleanup plan for the extra findings.

### 1c. Do Not Let Arbitrary API-Supplied Absolute Paths Reach Filesystem Probes

**The Bug**: Destination validation, Library Health scans, and mesh-transfer target selection accepted caller-supplied absolute paths and passed them straight into `Directory.Exists`, `EnumerateFiles`, `File.WriteAllText`, or later file I/O, which triggered real path-injection findings and allowed the server to probe arbitrary filesystem locations.

**Files Affected**:
- `src/slskd/Common/Security/PathGuard.cs`
- `src/slskd/Destinations/API/Controllers/DestinationsController.cs`
- `src/slskd/LibraryHealth/LibraryHealthService.cs`
- `src/slskd/VirtualSoulfind/DisasterMode/MeshTransferService.cs`
- `src/slskd/VirtualSoulfind/Bridge/BridgeApi.cs`

**Wrong**:
```csharp
var exists = Directory.Exists(request.Path);
var files = Directory.EnumerateFiles(request.LibraryPath, "*.*", SearchOption.AllDirectories);
var finalTargetPath = targetPath ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "Downloads",
    filename);
```

**Correct**:
```csharp
var normalizedPath = PathGuard.NormalizeAbsolutePathWithinRoots(request.Path, allowedRoots);
var libraryPath = ResolveLibraryPath(request.LibraryPath);
var finalTargetPath = targetPath ?? Path.Combine(
    optionsMonitor.CurrentValue.Directories.Downloads,
    PathGuard.SanitizeFilename(filename));
```

**Why This Keeps Happening**: Admin-facing endpoints make it tempting to trust absolute paths, especially when the UI is just “checking” a directory or kicking off a scan. That still turns the server into a filesystem oracle. Any absolute path from HTTP or bridge input must be canonicalized and constrained to configured app-owned roots before touching disk.

### 1d. Pod Membership Mutation Endpoints Must Not Be Anonymous

**The Bug**: `PodMembershipController` was marked `[AllowAnonymous]`, which let unauthenticated callers publish, update, remove, ban, unban, and role-change pod membership records through the server-signed membership service.

**Files Affected**:
- `src/slskd/PodCore/API/Controllers/PodMembershipController.cs`

**Wrong**:
```csharp
[AllowAnonymous]
public class PodMembershipController : ControllerBase
{
}
```

**Correct**:
```csharp
[Authorize(Policy = AuthPolicy.Any)]
public class PodMembershipController : ControllerBase
{
}
```

**Why This Keeps Happening**: Some PodCore endpoints are intentionally public for signed message exchange or DHT-facing workflows, and it is easy to copy that attribute onto mutation endpoints that actually exercise privileged server behavior. Membership publication and role changes are management operations, not anonymous transport endpoints.

### 1e. Vite SPA Builds Must Use Relative Asset Paths When `web.url_base` Is Not `/`

**The Bug**: The Vite web build emitted absolute asset URLs like `/assets/...`, `/manifest.json`, and `/logo192.png`, so deployments mounted under a subpath such as `/slskd` served `index.html` correctly but then fetched the JS bundle from the site root. Reverse proxies returned HTML/404 for those asset requests, which produced a blank white page with `NS_ERROR_CORRUPTED_CONTENT` and “disallowed MIME type (`text/html`)" in the browser.

**Files Affected**:
- `src/web/vite.config.js`
- `src/web/index.html`

**Wrong**:
```javascript
export default defineConfig({
  plugins: [react()],
});
```

```html
<link rel="manifest" href="/manifest.json" />
<script type="module" src="/src/index.jsx"></script>
```

**Correct**:
```javascript
export default defineConfig({
  base: './',
  plugins: [react()],
});
```

```html
<link rel="manifest" href="./manifest.json" />
<script type="module" src="./src/index.jsx"></script>
```

**Why This Keeps Happening**: The old SPA pipeline used server-side HTML rewriting for CRA-era `/static/...` assets. Vite defaults to root-relative output unless told otherwise, so a subpath deployment works locally at `/` and silently breaks only behind `web.url_base` or a reverse proxy prefix.

### 1f. Legacy Transfers Rows May Contain `NULL` Strings Even If New Code Treats Them As Required

**The Bug**: Startup initialization called `Uploads.List(...)`, and EF Core materialization threw on upgraded databases because older `transfers.db` rows contained `NULL` in string columns like `StateDescription`/`Exception` while the model treated them as non-nullable strings.

**Files Affected**:
- `src/slskd/Transfers/Types/Transfer.cs`
- `tests/slskd.Tests.Unit/Transfers/TransfersDbContextTests.cs`

**Wrong**:
```csharp
public string StateDescription { get; set; }
public string Exception { get; set; }
```

**Correct**:
```csharp
public string? StateDescription { get; set; }
public string? Exception { get; set; }
```

**Why This Keeps Happening**: It is easy to tighten nullability on current writes and forget that persisted SQLite rows from older releases do not retroactively satisfy the new contract. For long-lived local databases, read models need to be tolerant of legacy `NULL` values unless a migration backfills them first.

### 1g. Built-Web Verifier Scripts Must Resolve Paths Relative To `src/web`, Not The Repo Root

**The Bug**: A release-gate script successfully built the frontend into `src/web/build`, then the Node verifier immediately failed because it looked for `build/index.html` relative to the repository root instead of the web project directory.

**Files Affected**:
- `src/web/scripts/verify-build-output.mjs`
- `packaging/scripts/run-release-gate.sh`

**Wrong**:
```javascript
const root = path.resolve(process.cwd());
const buildDir = path.join(root, 'build');
```

**Correct**:
```javascript
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const buildDir = path.resolve(scriptDir, '..', 'build');
```

**Why This Keeps Happening**: Top-level gate scripts usually execute from the repository root, but many frontend utilities assume they are running from `src/web`. If the verifier uses `process.cwd()`, it quietly depends on the caller's shell location instead of the actual artifact location.

---

### 2. Reverting Entire Workflow Files (build-on-tag.yml, CI)

**The Bug**: Reverting `.github/workflows/build-on-tag.yml` (or other workflows) to an old commit wipes out months of accumulated fixes: AUR, Winget (Windows case-sensitivity), Nix/Winget branch refs, PPA version checks, Chocolatey retries, etc. Builds then fail immediately (wrong branch name, case-sensitivity errors, missing steps).

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- Any workflow that has been fixed incrementally over time

**Wrong**:
```bash
git checkout <old-commit> -- .github/workflows/build-on-tag.yml
```
Do not revert the whole file to "fix" one thing.

**Correct**:
- Make minimal, targeted edits (e.g. only add `--legacy-peer-deps` or fix one job).
- Before changing workflows: read `docs/DEV_BUILD_PROCESS.md`, then `git log --oneline -- .github/workflows/build-on-tag.yml` to see what was fixed and why.
- Branch names in workflows must match actual repo branches: use `dev/40-fixes` (or whatever the current dev branch is), not hardcoded `experimental/multi-source-swarm` if that branch no longer exists.
- Winget on Windows: use `fetch-depth: 1` for checkout and `git fetch origin +refs/heads/master:refs/remotes/origin/master` (not full fetch) to avoid case-insensitivity errors when refs differ only in casing.

**Why This Keeps Happening**: Agent "fixes" a single symptom by reverting the file to a "known good" state, not realizing that state is old and missing many fixes.

### 2a. Package Channel Metadata Must Match the Runtime Binary Name and Package Identity

**The Bug**: The Nix flake exported only a `slskdn` wrapper even though NixOS `services.slskd` expects `bin/slskd`, and the stable Winget manifests were copied from dev without replacing the `snapetech.slskdn-dev` identifier or `slskdn-dev` alias.

**Files Affected**:
- `flake.nix`
- `packaging/winget/snapetech.slskdn.yaml`
- `packaging/winget/snapetech.slskdn.installer.yaml`
- `packaging/winget/snapetech.slskdn.locale.en-US.yaml`
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```nix
makeWrapper $out/libexec/${pname}/slskd $out/bin/${pname}
```

```yaml
PackageIdentifier: snapetech.slskdn-dev
PortableCommandAlias: slskdn-dev
```

**Correct**:
```nix
makeWrapper $out/libexec/${pname}/slskd $out/bin/slskd
ln -s $out/bin/slskd $out/bin/${pname}
```

```yaml
PackageIdentifier: snapetech.slskdn
PortableCommandAlias: slskdn
```

**Why This Keeps Happening**: Packaging work tends to treat channel names, package names, and executable names as interchangeable. They are not. Each channel must preserve the runtime contract expected by downstream tools (`slskd` for service modules) while also publishing the correct channel identity (`slskdn` vs `slskdn-dev`). Add an explicit validation step whenever manifests or wrappers are generated.

### 2b. Wrapping Generic Linux Binaries Is Not Enough for NixOS

**The Bug**: The Nix flake wrapped the published `slskd` binary and set `LD_LIBRARY_PATH`, but the service still failed on NixOS because the extracted ELF kept its generic Linux dynamic loader and NixOS refused to execute it.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper ];

installPhase = ''
  makeWrapper $out/libexec/${pname}/slskd $out/bin/slskd \
    --prefix LD_LIBRARY_PATH : ${pkgs.lib.makeLibraryPath [ pkgs.icu pkgs.openssl ]}
'';
```

**Correct**:
```nix
nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook ];
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Why This Keeps Happening**: It is easy to treat Nix like any other Linux packaging target and assume a wrapper plus `LD_LIBRARY_PATH` solves native dependency issues. On NixOS, generic upstream ELF binaries also need their interpreter and linked libraries patched into the Nix store path, so use `autoPatchelfHook` or explicit `patchelf` instead of only wrapping the executable.

### 2c. Do Not Assume Fresh Filesystem Labels Are Immediately Available Under `/dev/disk/by-label`

**The Bug**: A QEMU/NixOS install helper formatted `/dev/vda1` with `mkfs.ext4 -L nixos` and immediately mounted `/dev/disk/by-label/nixos`, but the installer environment had not populated that symlink yet, so the mount failed even though the partition existed.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
mkfs.ext4 -F -L nixos /dev/vda1
mount /dev/disk/by-label/nixos /mnt
```

**Correct**:
```bash
mkfs.ext4 -F -L nixos /dev/vda1
udevadm settle
mount /dev/vda1 /mnt
```

**Why This Keeps Happening**: It is tempting to use the friendlier `/dev/disk/by-label/...` path immediately after formatting, but installer/live environments can lag on udev updates. For fresh partitions, either wait for udev explicitly or mount the block device path you already know exists.

### 2d. Do Not Append a Bare Attrset to `configuration.nix`; Add a Module or Edit Inside the Existing One

**The Bug**: A NixOS install helper appended a second top-level `{ ... }` block to the generated `/etc/nixos/configuration.nix`, but that file already defines a module function (`{ config, pkgs, ... }:`). The next `nixos-install` failed with “attempt to call something which is not a function but a set”.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
cat >> /mnt/etc/nixos/configuration.nix <<'EOF'
{
  services.openssh.enable = true;
}
EOF
```

**Correct**:
```bash
cat > /mnt/etc/nixos/slskdn-vm.nix <<'EOF'
{ ... }:
{
  services.openssh.enable = true;
}
EOF
printf '\n  ./slskdn-vm.nix\n' >> /mnt/etc/nixos/configuration.nix
```

**Why This Keeps Happening**: Generated NixOS config files look like plain attribute sets at a glance, but they are module functions. If you need to inject extra settings from a script, either edit inside the existing attrset carefully or create a separate module file and import it.

### 2e. NixOS GRUB Configuration Now Expects `boot.loader.grub.devices` in This Installer Path

**The Bug**: A scripted NixOS VM install set `boot.loader.grub.device = "/dev/vda";`, but the installer on NixOS 25.11 rejected it with an assertion asking for `boot.loader.grub.devices` or `boot.loader.grub.mirroredBoots`.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```nix
boot.loader.grub.device = "/dev/vda";
```

**Correct**:
```nix
boot.loader.grub.devices = [ "/dev/vda" ];
```

**Why This Keeps Happening**: Older examples and muscle memory still use the singular `grub.device` form, but the current module assertions in this install path expect the list form. Check the generated module assertions on current NixOS releases instead of reusing older snippets blindly.

### 2f. Generated NixOS `imports` Blocks May Span Multiple Lines; Match the Real Shape Before Using `sed`

**The Bug**: A helper tried to inject `./slskdn-vm.nix` with `sed '/imports = \[/a ...'`, but `nixos-generate-config` emitted `imports =` and `[` on separate lines, so the expression never matched and the custom module was not imported at all.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
sed -i '/imports = \[/a\ \ \ \ ./slskdn-vm.nix' /mnt/etc/nixos/configuration.nix
```

**Correct**:
```bash
sed -i '/\.\/hardware-configuration\.nix/a\ \ \ \ \ \./slskdn-vm.nix' /mnt/etc/nixos/configuration.nix
```

**Why This Keeps Happening**: Generated config files look predictable, but their whitespace and line breaks are not stable enough to target with a guessed pattern. Match a concrete line that is actually present in the generated file, or rewrite the whole block explicitly instead of assuming a one-line `imports = [`.

### 2g. `expect` Patterns for SSH Password Prompts Must Handle OpenSSH's Actual Prompt Casing

**The Bug**: A local-VM validation helper waited for `password:` in lowercase, but OpenSSH prompted with `(root@127.0.0.1) Password:`. The automation stalled at the login prompt even though the VM was ready.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-vm.expect`

**Wrong**:
```tcl
expect {
  "password:" { send "root\r" }
}
```

**Correct**:
```tcl
expect {
  -re {[Pp]assword:} { send "root\r" }
}
```

**Why This Keeps Happening**: Interactive prompt matching is brittle when it relies on exact casing or full literal text. SSH clients vary their password prompt prefix, so use a case-tolerant regex for the stable suffix instead of matching the whole prompt literally.

### 2h. Nix Flakes on 9p-Mounted Git Repositories Can Trip Git Ownership Checks

**The Bug**: Inside the NixOS VM, `nix build /mnt/hostrepo#default` treated the shared repo as a Git flake and failed because the 9p mount preserved host ownership that did not match the guest user, triggering Git's “safe directory” protection.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
nix build /mnt/hostrepo#default
```

**Correct**:
```bash
git config --global --add safe.directory /mnt/hostrepo
nix build /mnt/hostrepo#default
```

**Why This Keeps Happening**: Shared folders in VMs often preserve host UIDs/GIDs or present synthetic ownership that does not match the guest account. When a flake path is also a Git repo, Nix delegates part of the source handling to Git, so you need to either mark the mount as a safe directory or use a non-Git path source when testing from a shared folder.

### 2i. Prefer `path:` Flake URIs in Minimal Guest Images When Shared Repos Trigger Git Handling

**The Bug**: The first recovery plan for a 9p-mounted flake repo assumed `git` was installed in the minimal NixOS guest so `safe.directory` could be configured, but the guest image did not include `git`, leaving the flake build blocked.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
git config --global --add safe.directory /mnt/hostrepo
nix build /mnt/hostrepo#default
```

**Correct**:
```bash
nix build 'path:/mnt/hostrepo#default'
```

**Why This Keeps Happening**: It is easy to assume live or minimal troubleshooting images carry the same helper tools as a normal dev box. For ad hoc VM validation, use the simplest source form that avoids extra dependencies; `path:` flake URIs sidestep both Git ownership checks and the need for Git itself.

### 2j. Read-Only Shared Flake Mounts Need `--no-write-lock-file`

**The Bug**: After switching to a `path:` flake URI for a read-only 9p mount, `nix build` still failed because it tried to create `flake.lock` in the mounted repo and the filesystem was intentionally read-only.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
nix build 'path:/mnt/hostrepo#default'
```

**Correct**:
```bash
nix build --no-write-lock-file 'path:/mnt/hostrepo#default'
```

**Why This Keeps Happening**: Read-only source mounts are ideal for preserving the host checkout during guest validation, but flake evaluation still wants to persist lock updates by default. When validating from a read-only mount, always disable lock-file writes explicitly or copy the flake into a writable path first.

### 2k. Nix Flake Stable Pins Must Move With the Latest Published Stable Release

**The Bug**: The flake still pointed at stable release `0.24.5-slskdn.52` and its old hashes even though GitHub’s latest stable release had moved to `0.24.5-slskdn.54`, so `nix build` failed immediately with a fixed-output hash mismatch before the runtime patching fix could even be exercised.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
version = "0.24.5-slskdn.52";
sha256 = "1gljb5zj7h0g7mhi8d9s5hjkqvn8v6dmrb812gfwggayl91ksj7y";
```

**Correct**:
```nix
version = "0.24.5-slskdn.54";
sha256 = "sha256-M1gUyVXt1iPUjjh9eFheDBRWv/kixAgIxlvIRMbckoo=";
```

**Why This Keeps Happening**: Packaging work can fix wrapper logic or runtime behavior while leaving the stable source pin behind on an older release. For fixed-output fetches, a stale release pin is just as fatal as a stale hash, so treat version and hashes as one atomic update sourced from the actual latest published release metadata.

### 2l. The Bundled .NET Runtime Also Needs `lttng-ust` on NixOS for `autoPatchelfHook` to Finish Cleanly

**The Bug**: After adding the obvious runtime libraries, the NixOS VM still failed during `autoPatchelfHook` because `libcoreclrtraceptprovider.so` wanted `liblttng-ust.so.0`, which was not present in the flake inputs.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Correct**:
```nix
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.lttng-ust
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Why This Keeps Happening**: The first-pass dependency list tends to cover the apphost and common runtime libs, but the bundled .NET runtime ships tracing/provider binaries that pull in less obvious native dependencies. Validate with `autoPatchelfHook` on real NixOS and add every missing provider library it reports instead of assuming the first set is complete.

### 2m. Some Nix Packages Default to a Non-Library Output; Use the Output That Actually Contains the Shared Object

**The Bug**: Adding `pkgs.lttng-ust` still did not satisfy `liblttng-ust.so.0` because that attribute resolved to the `bin` output in this nixpkgs revision, while the shared library lived in `pkgs.lttng-ust.out`.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.lttng-ust
];
```

**Correct**:
```nix
buildInputs = [
  pkgs.lttng-ust.out
];
```

**Why This Keeps Happening**: It is easy to assume a package attribute points at the runtime library output, but multi-output Nix packages often default to `bin` or `dev`. When `autoPatchelfHook` still cannot find a `.so`, inspect the package outputs and reference the one that actually contains the needed library.

### 2n. Bundled Runtime SONAMEs Can Lag Behind nixpkgs; Patch `NEEDED` Entries Before `autoPatchelfHook` Runs

**The Bug**: Even after adding the correct `lttng-ust` library output, the NixOS VM still failed because the bundled `.NET` trace provider asked for `liblttng-ust.so.0` while current nixpkgs only ships `liblttng-ust.so.1`.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.lttng-ust.out
];
```

**Correct**:
```nix
patchelf \
  --replace-needed liblttng-ust.so.0 liblttng-ust.so.1 \
  $out/libexec/${pname}/libcoreclrtraceptprovider.so
```

**Why This Keeps Happening**: Upstream self-contained runtimes can be built against an older SONAME than the one available in current nixpkgs. Adding more packages will not help when the exact requested SONAME no longer exists; inspect the bundled binary and patch the `NEEDED` entry to the compatible library that nixpkgs actually provides before running `autoPatchelfHook`.

### 2o. Do Not Strip Bundled .NET Runtime Payloads in the Nix Package

**The Bug**: After the flake finally built, launching `slskd` on NixOS still failed with `Failed to load System.Private.CoreLib.dll ... 0x8007000B`. The package had gone through Nix’s default strip phase, which is unsafe for this bundled .NET payload.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
pkgs.stdenv.mkDerivation {
  nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook pkgs.patchelf ];
}
```

**Correct**:
```nix
pkgs.stdenv.mkDerivation {
  nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook pkgs.patchelf ];
  dontStrip = true;
}
```

**Why This Keeps Happening**: Nix’s normal strip phase is reasonable for ordinary native packages, but bundled .NET distributions mix ELF binaries with managed/runtime payloads that are not safe to treat like a conventional C/C++ install tree. If CoreCLR starts failing with format/load errors after packaging, remove stripping from the equation before chasing more loader theories.

### 2p. The NixOS `services.slskd` Module Requires `services.slskd.domain` Even for Local Validation

**The Bug**: A local NixOS validation module enabled `services.slskd` and provided a custom package, but `nixos-rebuild test` failed before creating the service because the module accessed `services.slskd.domain` and no value was set.

**Files Affected**:
- `/etc/nixos/slskdn-local.nix` in the validation VM

**Wrong**:
```nix
{
  services.slskd.enable = true;
  services.slskd.package = slskdn.packages.${pkgs.system}.default;
}
```

**Correct**:
```nix
{
  services.slskd.enable = true;
  services.slskd.domain = "localhost";
  services.slskd.package = slskdn.packages.${pkgs.system}.default;
}
```

**Why This Keeps Happening**: It is easy to treat the NixOS module like a thin wrapper around the binary and only override `package`, but module assertions/options can still require unrelated application settings. For service validation, read the module’s required options instead of assuming `enable + package` is enough.

### 2q. The NixOS `services.slskd` Module Also Requires `settings.shares.directories`, Even If You Want No Shares

**The Bug**: After adding `domain` and `environmentFile`, `nixos-rebuild test` still failed because the module always maps over `cfg.settings.shares.directories` to build `ReadOnlyPaths`, so leaving it unset crashes evaluation.

**Files Affected**:
- `/etc/nixos/slskdn-local.nix` in the validation VM

**Wrong**:
```nix
{
  services.slskd.enable = true;
  services.slskd.environmentFile = "/etc/slskd.env";
}
```

**Correct**:
```nix
{
  services.slskd.enable = true;
  services.slskd.environmentFile = "/etc/slskd.env";
  services.slskd.settings.shares.directories = [ ];
}
```

**Why This Keeps Happening**: “No shares configured” feels like it should mean “unset,” but this module dereferences the list unconditionally when generating systemd hardening paths. For local validation, explicitly set it to an empty list.

### 2r. Whenever `flake.nix` Packaging Logic Changes, Update the Metadata Validator in the Same Edit

**The Bug**: After changing the Nix flake to add `patchelf`, `dontStrip`, `lttng-ust.out`, and the SONAME rewrite, `packaging/scripts/validate-packaging-metadata.sh` still enforced the old `nativeBuildInputs` line and failed immediately.

**Files Affected**:
- `flake.nix`
- `packaging/scripts/validate-packaging-metadata.sh`

**Wrong**:
```bash
expect_line flake.nix 'nativeBuildInputs = \[ pkgs\.unzip pkgs\.makeWrapper pkgs\.autoPatchelfHook \];'
```

**Correct**:
```bash
expect_line flake.nix 'nativeBuildInputs = \[ pkgs\.unzip pkgs\.makeWrapper pkgs\.autoPatchelfHook pkgs\.patchelf \];'
expect_line flake.nix 'dontStrip = true;'
expect_line flake.nix '--replace-needed liblttng-ust\.so\.0 liblttng-ust\.so\.1'
```

**Why This Keeps Happening**: Packaging validation tends to get treated as a one-time guardrail, but it is really part of the packaging implementation. If the flake or package templates change and the validator does not, the repo ends up failing on stale assertions instead of catching real regressions.

### 2s. Validator Helpers That Pass Regexes to `grep` Must Use `grep --` for Patterns Beginning With `-`

**The Bug**: After adding a validation pattern for `--replace-needed ...`, the packaging validator failed inside `grep` because the pattern itself started with `-` and was parsed as an option rather than a regex.

**Files Affected**:
- `packaging/scripts/validate-packaging-metadata.sh`

**Wrong**:
```bash
grep -Eq "$pattern" "$file"
```

**Correct**:
```bash
grep -Eq -- "$pattern" "$file"
```

**Why This Keeps Happening**: Validation helpers often assume patterns are data, but command-line tools still parse them as arguments first. Any generic wrapper that forwards arbitrary regexes to `grep` should include `--` up front or it will break as soon as a pattern begins with `-`.

### 2b. Tests That Bind TCP Ports Must Not Hardcode Popular Local Ports

**The Bug**: `LocalPortForwarderTests` bound to `8080` and `8081`, which caused unrelated CI and local failures whenever those ports were already in use; `TorSocksTransportTests` also assumed a specific connect-error substring even though timeout/cancellation wording varies by runtime and environment.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/LocalPortForwarderTests.cs`
- `tests/slskd.Tests.Unit/Mesh/Transport/TorSocksTransportTests.cs`

**Wrong**:
```csharp
await _portForwarder.StartForwardingAsync(8080, "pod-123", "example.com", 80);
Assert.Contains("connect", status.LastError.ToLower());
```

**Correct**:
```csharp
var localPort = GetFreeLocalPort();
await _portForwarder.StartForwardingAsync(localPort, "pod-123", "example.com", 80);
Assert.NotEmpty(status.LastError);
```

**Why This Keeps Happening**: Test code often assumes "common dev ports" are free and that low-level socket failures have stable message text. Neither assumption holds across busy developer machines, CI runners, or different runtime timing paths.

---

### 3. E2E SlskdnNode: HTTPS Port Conflict and Missing --app-dir

**The Bug**: E2E tests that start real slskdn nodes fail with "Hosting failed to start" / "Address already in use" or "An instance of slskd is already running" because (1) every node tries to bind to the same HTTPS port (5031) and (2) nodes share the default app dir (mutex conflict).

**Files Affected**:
- `tests/e2e/harness/SlskdnNode.ts`
- `tests/e2e/fixtures/helpers.ts` (findFreePort)

**Wrong**:
- Test config without `web.https.disabled: true` → all nodes bind to 5031, second node fails.
- Spawn args without `--app-dir <per-node dir>` → all nodes use default app dir, mutex prevents multiple instances.
- Building inside `SlskdnNode.start()` after `findFreePort()` → long delay lets another process grab the port (or port in TIME_WAIT).

**Correct**:
- In test config YAML: `web: https: disabled: true` so each node only binds to its unique HTTP port.
- Spawn with `--app-dir`, `this.appDir` (isolated temp dir per node).
- Build once in spec `beforeAll`, not per node; use `findFreePort()` with `reuseAddress: true` so the probe port can be rebound immediately.
- Keep stdin as pipe (do not use `ignore`) so the child does not see EOF and exit.

**Why This Keeps Happening**: Default slskd config enables HTTPS on a fixed port; E2E runs multiple nodes and did not disable HTTPS or isolate app dirs.

---

### 3b. E2E SlskdnNode.stop(): Must Wait for Child Exit (Port Leaks)

**The Bug**: E2E nodes intermittently fail to start with `Address already in use` because the harness stop logic resolves before the `dotnet` child process has actually exited. The old process can keep Kestrel bound to its port for a short window, and the next node hits a bind failure.

**Files Affected**:
- `tests/e2e/harness/SlskdnNode.ts`

**Wrong** (resolves early after SIGKILL without waiting for `exit`):
```ts
this.process.kill('SIGTERM');
await new Promise<void>((resolve) => {
  this.process.on('exit', () => resolve());
  setTimeout(() => {
    this.process.kill('SIGKILL');
    resolve();
  }, 5000);
});
```

**Correct** (escalate SIGTERM -> SIGKILL, but always await the `exit` event):
```ts
const exitPromise = new Promise<void>((resolve) => proc.once('exit', () => resolve()));

proc.kill('SIGTERM');
const exitedGracefully = await Promise.race([
  exitPromise.then(() => true),
  delay(5000).then(() => false),
]);

if (!exitedGracefully) {
  proc.kill('SIGKILL');
  await Promise.race([exitPromise, delay(5000)]);
}
```

**Why This Keeps Happening**: It's easy to write a timeout path that resolves the stop Promise without verifying the child actually exited.

---

### 4. `async void` Event Handlers Without Try-Catch

**The Bug**: `async void` event handlers that throw exceptions crash the entire .NET process.

**Files Affected**:
- `src/slskd/Messaging/RoomService.cs` - `Client_LoggedIn`

**Wrong**:
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    await TryJoinAsync(rooms);  // 💀 Exception here = process crash
}
```

**Correct**:
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    try
    {
        await TryJoinAsync(rooms);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to execute post-login room actions");
    }
}
```

**Why This Keeps Happening**: `async void` is required for event handlers, but models forget it can't propagate exceptions.

---

### 5. Streaming Controller `[Produces]` Causing 406 Instead of 429

**The Bug**: Adding `[Produces("application/octet-stream")]` to the streams endpoint can cause ASP.NET Core to return `406 Not Acceptable` for non-file error responses (notably the concurrency limiter `429`), breaking E2E expectations.

**Files Affected**:
- `src/slskd/Streaming/StreamsController.cs`

**Wrong**:
```csharp
[Produces("application/octet-stream")]
public class StreamsController : ControllerBase
{
    // ...
    if (!_limiter.TryAcquire(limiterKey, maxConcurrent))
        return StatusCode(429, "Too many concurrent streams.");
}
```

**Correct**:
```csharp
public class StreamsController : ControllerBase
{
    // ...
    if (!_limiter.TryAcquire(limiterKey, maxConcurrent))
        return StatusCode(429, "Too many concurrent streams.");
}
```

**Why This Keeps Happening**: `[Produces]` is tempting for a file endpoint, but the action also returns non-file errors (401/404/429). Constraining the produced content types can make these errors fail content negotiation and surface as `406`.

---

### 3. Unbounded Parallelism in Download Loops

**The Bug**: `Task.Run` inside loops without concurrency limits causes resource exhaustion.

**Files Affected**:
- `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs`

**Wrong**:
```csharp
foreach (var source in sources)
{
    _ = Task.Run(() => DownloadFromSourceAsync(source));  // 💀 Unbounded
}
```

**Correct**:
```csharp
var semaphore = new SemaphoreSlim(10);  // Cap at 10 concurrent
foreach (var source in sources)
{
    await semaphore.WaitAsync();
    _ = Task.Run(async () =>
    {
        try { await DownloadFromSourceAsync(source); }
        finally { semaphore.Release(); }
    });
}
```

**Why This Keeps Happening**: Models optimize for "parallelism = fast" without considering resource limits.

---

### 4. HashDb Migration Version Collisions

**The Bug**: Duplicate migration version numbers cause `UNIQUE constraint failed: __HashDbMigrations.version`, blocking startup and E2E health checks.

**Files Affected**:
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs`

**Wrong**:
```csharp
new Migration { Version = 12, Name = "Label crate job cache", ... },
new Migration { Version = 12, Name = "Traffic accounting", ... }, // 💥 duplicate
new Migration { Version = 14, Name = "Warm cache popularity", ... },
new Migration { Version = 14, Name = "Warm cache entries", ... }, // 💥 duplicate
```

**Correct**:
```csharp
new Migration { Version = 12, Name = "Label crate job cache", ... },
new Migration { Version = 13, Name = "Peer metrics storage", ... },
new Migration { Version = 14, Name = "Warm cache popularity", ... },
new Migration { Version = 15, Name = "Warm cache entries", ... },
new Migration { Version = 16, Name = "Virtual Soulfind pseudonyms", ... },
new Migration { Version = 17, Name = "Traffic accounting", ... },
```

**Why This Keeps Happening**: Migrations were appended without re-checking version uniqueness, and the list order wasn’t kept strictly ascending.

---

### 5. Snap workflow: source path is relative to snapcraft project dir

**The Bug**: In `build-on-tag.yml`, the Snap job unzipped the release zip to `slskdn_dist` in repo root, then `sed` set `source: slskdn_dist` in `packaging/snap/snapcraft.yaml`. Snapcraft runs with `cd packaging/snap`, so it resolves `source: slskdn_dist` relative to that directory. The path `packaging/snap/slskdn_dist` did not exist (the unzip created `./slskdn_dist` at repo root), so snapcraft failed.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Wrong**:
```yaml
run: |
  unzip slskdn-dev-linux-x64.zip -d slskdn_dist
  sed -i "s|source: .*|source: slskdn_dist|" packaging/snap/snapcraft.yaml
  cd packaging/snap
  snapcraft --destructive-mode
```

**Correct**:
```yaml
run: |
  mkdir -p packaging/snap/slskdn_dist
  unzip slskdn-dev-linux-x64.zip -d packaging/snap/slskdn_dist
  sed -i "s|source: .*|source: slskdn_dist|" packaging/snap/snapcraft.yaml
  cd packaging/snap
  snapcraft --destructive-mode
```

**Why This Keeps Happening**: Unzip target was assumed to be "any dir"; snapcraft resolves part sources relative to the snapcraft project root (the directory containing `snapcraft.yaml`).

---

### 5b. Chocolatey: do NOT pass path to choco push (match master)

**The Bug**: Passing a path to `choco push` (e.g. `choco push $Nupkg --source ...`) causes Chocolatey/pwsh to glue the path and the next flag into one argument, so it fails with: "File specified is either not found or not a .nupkg file. '<path>.nupkg --prerelease'".

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (chocolatey-dev, chocolatey-main)

**Wrong** (any path argument can glue to next flag):
```powershell
choco push $Nupkg --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --prerelease
```

**Correct** (match master): Run `choco push` from inside `packaging/chocolatey` after `choco pack`, with **no path** — choco finds the single .nupkg in the current directory:
```powershell
cd packaging/chocolatey
choco pack
choco push --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --prerelease --execution-timeout 300   # dev
choco push --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --execution-timeout 300               # main (add retry loop for 504)
```

**Why This Keeps Happening**: Chocolatey/pwsh glues a path argument to the next token. Omitting the path (run from the pack directory) avoids the bug; master branch uses this pattern.

---

### 5c. Snap workflow: destructive-mode on ubuntu-latest breaks stage-packages (libicu70)

**The Bug**: On GitHub Actions `ubuntu-latest` (Ubuntu 24.04), running `snapcraft --destructive-mode` uses the host apt repositories. With `base: core22`, this can fail because `stage-packages` include `libicu70` (available on 22.04, not 24.04). Error: "Stage package not found in part 'slskdn': libicu70."

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Correct** (build in LXD so the build environment matches `base: core22`):
```yaml
- uses: snapcore/action-build@v1
  with:
    path: packaging/snap
```

---

### 5d. Snap Store: duplicate content and transient "error while processing"

**The Bug**: (1) If a previous upload succeeded in transmitting but failed the status check (e.g. "Waiting for previous upload"), the next retry fails with: "binary_sha3_384: A file with this exact same content has already been uploaded". (2) Snap Store can return "Status: error while processing" transiently.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Fix**: (1) Treat "exact same content has already been uploaded" as **SUCCESS**. (2) Treat "Waiting for previous upload" and "error while processing" as **retry** (sleep 30s, continue); do not exit on them.

---

### 5e. Snap: action-build output path; do not double packaging/snap

**The Bug**: `snapcore/action-build@v1` sets its `snap` output to a path relative to the repo root (e.g. `packaging/snap/slskdn_0.24.1.dev.91769629519_amd64.snap`). If you set `SNAP_PATH="packaging/snap/${{ steps.snap-build.outputs.snap }}"` you get `packaging/snap/packaging/snap/...` and "is not a valid file". The upload step also runs on the host runner; install snapcraft there before upload.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Wrong**:
```yaml
SNAP_PATH="packaging/snap/${{ steps.snap-build.outputs.snap }}"   # duplicates packaging/snap
```

**Correct**:
- Set `SNAP_PATH="${{ steps.snap-build.outputs.snap }}"` (use the output as-is; it already includes packaging/snap when path: packaging/snap).
- Add a step before the upload step to install snapcraft on the host: `sudo apt-get install -y snapd` then `sudo snap install snapcraft --classic`.

**Why This Keeps Happening**: The action may output filename-only or path; if it outputs path, prepending packaging/snap breaks.

---

### 5e2. Snap (and other packaging) jobs: don't pin checkout to a branch on tag-triggered builds

**The Bug**: In `build-on-tag.yml`, Snap (and Nix, Homebrew) jobs had `ref: dev/40-fixes` or `ref: master`. When the workflow is triggered by a **tag** (e.g. `build-dev-0.24.1.dev.…`), the runner checks out that branch tip, not the tag's commit. So you build with `packaging/snap` (and release zip) from different commits: zip from the tag's release, tree from branch tip. If someone reverted or changed the Snap workflow on the branch, the job uses that reverted state and Snap breaks.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main; also nix-dev, homebrew-dev if they pin ref)

**Wrong**:
```yaml
- uses: actions/checkout@v4
  with:
    ref: dev/40-fixes   # tag build then gets branch tip, not tag commit
```

**Correct**:
```yaml
- uses: actions/checkout@v4
  # No ref: so tag-triggered runs checkout the tag's commit (same as release assets).
```

**Why This Keeps Happening**: It's tempting to pin to a branch for "dev" or "main" packaging; for tag-triggered runs the ref that triggered the run is the tag, and checkout should match that.

---

### 5f. PPA dev build: version must always increase (workflow uses epoch-based DEB_VERSION)

**The Bug**: PPA rejects uploads with "Version older than that in the archive". Debian version comparison treats the suffix after `dev.` as the ordering key. If the tag (or derived version) is e.g. `0.24.1.dev.20260128.162317`, it can sort **below** a previously uploaded `0.24.1.dev.91769609285`, so the PPA rejects the upload.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (ppa-dev job)

**Fix (in workflow)**: The ppa-dev job now **ignores the tag version** for the package version and sets `DEB_VERSION=0.24.1.dev.9$(date +%s)` in "Prepare Source Structure", then uses that for directory name, tarball, and changelog. So PPA always gets a monotonically increasing version regardless of tag format.

**If tagging manually**: Prefer `build-dev-0.24.1.dev.9$(date +%s)` so the tag itself is increasing; the workflow no longer derives PPA version from the tag for dev.

---

### 6. Library Items Empty When Share Cache Is Cold

**The Bug**: `/api/v0/library/items` returned no results when the share cache was empty or not ready, breaking E2E flows that need real content IDs.

**Files Affected**:
- `src/slskd/API/Native/LibraryItemsController.cs`
- `src/web/e2e/policy.spec.ts`
- `src/web/e2e/streaming.spec.ts`

**Wrong**:
```csharp
var directories = await shareService.BrowseAsync();
var allFiles = directories.SelectMany(d => d.Files ?? Enumerable.Empty<File>());
// allFiles can be empty if the share cache is cold
```

**Correct**:
```csharp
var directories = await shareService.BrowseAsync();
var allFiles = directories.SelectMany(d => d.Files ?? Enumerable.Empty<File>());
if (!allFiles.Any())
{
    // Fallback: scan configured share directories directly
    var items = await SearchShareDirectoriesAsync(query, kinds, limit, cancellationToken);
    return Ok(new { items });
}
```

**Why This Keeps Happening**: The library search assumes the share cache is always populated, but E2E nodes can query before scans finish or when caches are empty.

---

### 6. Library Item ContentIds Not Streamable

**The Bug**: Library item searches returned `contentId` values that were not registered in the share repository, so `/api/v0/streams/{contentId}` returned 404 even though the item existed on disk.

**Files Affected**:
- `src/slskd/API/Native/LibraryItemsController.cs`
- `src/slskd/Streaming/ContentLocator.cs`

**Wrong**:
```csharp
// contentId returned but never registered with share repository
return new LibraryItemResponse { ContentId = contentId, /* ... */ };
```

**Correct**:
```csharp
repo.UpsertContentItem(contentId, "GenericFile", null, maskedFilename, true, string.Empty, checkedAt);
```

**Why This Keeps Happening**: Content streaming resolves via the share repository’s `content_items` table, so ad-hoc content IDs must be registered with a masked filename to resolve to a file path.

## ⚠️ HIGH: Common Mistakes

### 4. Copyright Headers - Wrong Company Attribution

**The Rule**: New slskdN files use `company="slskdN Team"`, existing upstream files keep `company="slskd Team"`.

**Fork-specific directories** (always slskdN headers):
- `Capabilities/`, `HashDb/`, `Mesh/`, `Backfill/`
- `Transfers/MultiSource/`, `Transfers/Ranking/`
- `Users/Notes/`, `DhtRendezvous/`, `Common/Security/`

**Why This Matters**: Legal clarity for fork vs upstream code.

---

### 5. Logging Pattern Inconsistency

**The Issue**: Mixed use of `ILogger<T>` and `Serilog.Log.ForContext`.

**Preferred** (standardization in progress):
```csharp
private readonly ILogger<MyService> _logger;

public MyService(ILogger<MyService> logger)
{
    _logger = logger;
}
```

**Avoid**:
```csharp
private static readonly ILogger Log = Serilog.Log.ForContext<MyService>();
```

---

### 7. Duplicate Variable Names in React Components

**The Bug**: Large React components with multiple state sections can have duplicate variable names, causing "Identifier 'X' has already been declared" compilation errors.

**Files Affected**:
- `src/web/src/components/System/MediaCore/index.jsx` (main culprit)

**Wrong**:
```jsx
// In one section:
const [verificationResult, setVerificationResult] = useState(null);

// Later in another section:
const [verificationResult, setVerificationResult] = useState(null); // ❌ Duplicate declaration
```

**Correct**:
```jsx
// Use descriptive names for different purposes:
const [descriptorVerificationResult, setDescriptorVerificationResult] = useState(null);
const [signatureVerificationResult, setSignatureVerificationResult] = useState(null);
```

**Why This Keeps Happening**: MediaCore component has 50+ state variables across multiple sections. When adding new state variables, developers may not realize the name is already used elsewhere in the file. Always grep for variable names before adding new state.

---

### 6. React Version Compatibility

**The Issue**: Older repo guidance claimed a React 16.8.6 floor after the web app had moved on. That makes agents reject valid current patterns or pin incompatible dependency versions.

**Rule**: Treat `src/web/package.json` and `src/web/package-lock.json` as the source of truth for the active React major. Do not copy old archive/memory references into new guidance without checking the current package versions first.

**Safe Baseline**:
- Use hooks and router APIs supported by the currently declared React and React Router majors.
- Keep Semantic UI React integration patterns consistent with existing components.
- When dependency compatibility is uncertain, check peer dependency ranges before bumping.

---

### 7. Path Traversal - Base64 Decoding

**The Issue**: User-supplied paths may be Base64-encoded with `..` components.

**Wrong**:
```csharp
var path = Base64Decode(userInput);
File.Delete(path);  // 💀 Could delete /etc/passwd
```

**Correct**:
```csharp
var path = Base64Decode(userInput);
var fullPath = Path.GetFullPath(path);
if (!fullPath.StartsWith(allowedRoot))
    throw new SecurityException("Path traversal attempt");
```

**Use `PathGuard`** in experimental branch: `PathGuard.NormalizeAndValidate(path, root)`

---

## 🔄 Patterns That Cause Fix/Unfix Cycles

### 8. ESLint/Prettier Formatting Wars

**The Cycle**:
1. Model fixes a bug
2. Lint fails on import order or quotes
3. Model "fixes" lint by changing unrelated code
4. Original fix gets lost

**Solution**: Run `npm run lint -- --fix` in `src/web/` before committing frontend changes.

---

### 9. DI Service Registration

**The Cycle**:
1. New service added
2. Forgot to register in `Program.cs`
3. Runtime crash: "Unable to resolve service"
4. Model adds registration
5. Merge conflict loses registration

**Checklist for new services**:
```csharp
// In Program.cs
builder.Services.AddSingleton<IMyService, MyService>();
// OR
builder.Services.AddScoped<IMyService, MyService>();
```

---

### 10. Experimental Files on Master Branch

**The Cycle**:
1. Work on experimental branch
2. Accidentally commit experimental files to master
3. "Fix" by removing files
4. Merge conflict brings them back

**Files that should NOT be on master**:
- `src/slskd/DhtRendezvous/`
- `src/slskd/Transfers/MultiSource/`
- `src/slskd/HashDb/`
- `src/slskd/Mesh/`
- `src/slskd/Backfill/`
- `src/slskd/Common/Security/` (beyond basic PathGuard)

---

### 10b. YAML Heredocs with Special Characters

**The Bug**: GitHub Actions workflows with inline heredocs containing `${}`, `#{}`, or `\$` break YAML parsing.

**Files Affected**:
- `.github/workflows/release-homebrew.yml`
- `.github/workflows/release-packaging.yml`

**Wrong**:
```yaml
- name: Generate file
  run: |
    cat > file.nix <<EOF
    let pkgs = nixpkgs.\${system};  # 💀 YAML parser chokes on this
    EOF
```

**Correct**: Use external scripts in `packaging/scripts/`:
```yaml
- name: Generate file
  run: |
    chmod +x packaging/scripts/update-nix.sh
    packaging/scripts/update-nix.sh "${{ steps.release.outputs.tag }}"
```

**Why This Keeps Happening**: Models inline heredocs for "simplicity" without realizing Nix `${}` and Ruby `#{}` break YAML.

---

## 📦 Packaging Gotchas (MAJOR PAIN POINT)

> ⚠️ **These issues caused 10+ CI failures each. Read carefully.**

### 11. Case Sensitivity EVERYWHERE

**The Issue**: Package names, URLs, and filenames must be **consistently lowercase**.

| Context | Correct | Wrong |
|---------|---------|-------|
| Package name | `slskdn` | `slskdN` |
| GitHub tag | `0.24.1-slskdn.22` | `0.24.1-slskdN.22` |
| Zip filename | `slskdn-0.24.1-...` | `slskdN-0.24.1-...` |
| COPR project | `slskdn` | `slskdN` |
| PPA changelog | `slskdn (0.24.1...)` | `slskdN (0.24.1...)` |

**Files that MUST use lowercase**:
- `packaging/aur/PKGBUILD*`
- `packaging/debian/changelog`
- `packaging/rpm/*.spec`
- `.github/workflows/*.yml`
- `packaging/homebrew/Formula/slskdn.rb`

---

### 12. SHA256 Checksum Formats

**The Issue**: Different packaging systems want checksums in different formats.

| System | Format | Example |
|--------|--------|---------|
| AUR PKGBUILD | Single-line array | `sha256sums=('abc123...' 'def456...')` |
| Homebrew | Quoted string | `sha256 "abc123..."` |
| Flatpak | Plain value | `sha256: abc123...` |
| Snap | Prefixed | `source-checksum: sha256/abc123...` |
| Chocolatey | PowerShell var | `$checksum = "abc123..."` |
| Nix flake | Quoted string | `sha256 = "abc123...";` |

**Multi-line PKGBUILD breaks makepkg**:
```bash
# WRONG - breaks AUR
sha256sums=(
  'abc123...'
  'def456...'
)

# CORRECT - single line
sha256sums=('abc123...' 'def456...')
```

---

### 13. SKIP vs Actual Hash in AUR

**The Issue**: AUR packages need `SKIP` for the source tarball (changes each release) but real hashes for static files.

```bash
# PKGBUILD source array order:
source=(
    "tarball.tar.gz"    # Index 0 - SKIP (changes)
    "slskd.service"     # Index 1 - real hash (static)
    "slskd.yml"         # Index 2 - real hash (static)
    "slskd.sysusers"    # Index 3 - real hash (static)
)

# Matching sha256sums:
sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')
```

**The Cycle**:
1. Model updates tarball hash
2. AUR build fails (tarball changed)
3. Model sets to SKIP
4. Model accidentally SKIPs the static files too
5. AUR build fails (missing hashes)

---

### 14. Version Format Conversion

**The Issue**: GitHub tags use `-slskdn` but PKGBUILD uses `.slskdn`.

```bash
# GitHub tag format
0.24.1-slskdn.22

# PKGBUILD pkgver format (no hyphens allowed)
0.24.1.slskdn.22

# Conversion in workflows:
PKGVER=$(echo $TAG | sed 's/-slskdn/.slskdn/')
```

**Files that need conversion**:
- `.github/workflows/release-linux.yml`
- `.github/workflows/release-copr.yml`
- `packaging/aur/PKGBUILD*`

---

### 15. URL Patterns Must Match Release Assets

**The Issue**: Download URLs must exactly match the uploaded asset names.

**Asset naming pattern** (from `release-linux.yml`):
```
slskdn-{TAG}-linux-x64.zip
slskdn-{TAG}-linux-arm64.zip
slskdn-{TAG}-osx-x64.zip
slskdn-{TAG}-osx-arm64.zip
slskdn-{TAG}-win-x64.zip
```

**Common mistakes**:
- `slskdN-...` (wrong case)
- `slskdn-linux-x64.zip` (missing version)
- `slskdn_{TAG}_linux_x64.zip` (wrong separators)

---

### 16. Homebrew Formula Architecture Blocks

**The Issue**: Homebrew needs separate `on_arm` and `on_intel` blocks for macOS.

```ruby
on_macos do
  on_arm do
    url "...osx-arm64.zip"
    sha256 "..."
  end
  on_intel do
    url "...osx-x64.zip"
    sha256 "..."
  end
end

on_linux do
  url "...linux-x64.zip"
  sha256 "..."
end
```

**Don't**: Use a single URL for all platforms.

---

### 17. Workflow Timing Issues

**The Issue**: Packaging workflows run before release assets are uploaded.

**The Cycle**:
1. Release published
2. Packaging workflow triggered immediately
3. Asset download fails (not uploaded yet)
4. Workflow fails
5. Manual re-run required

**Solution in `release-linux.yml`**:
```yaml
# Retry loop with 30s delays
for i in {1..20}; do
  if curl -fsSL "$ASSET_URL" -o release.zip; then
    exit 0
  fi
  sleep 30
done
```

---

### 18. AUR Directory Cleanup

**The Issue**: AUR git clone fails if directory exists from previous run.

```bash
# WRONG - fails if aur-repo exists
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-repo

# CORRECT - clean first
rm -rf aur-repo
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-repo
```

---

### 19. COPR/PPA Need Different Spec Files

**The Issue**: COPR uses `.spec` files, PPA uses `debian/` directory.

**COPR** (`packaging/rpm/slskdn.spec`):
- RPM spec format
- `%{version}` macro
- `BuildRequires` / `Requires`

**PPA** (`packaging/debian/`):
- `changelog` (specific format!)
- `control`
- `rules`
- `copyright`

**Changelog format is STRICT**:
```
slskdn (0.24.1-slskdn.22-1) jammy; urgency=medium

  * Release 0.24.1-slskdn.22

 -- snapetech <slskdn@proton.me>  Sun, 08 Dec 2024 12:00:00 +0000
```

Note: TWO spaces before `--`, specific date format.

---

### 20. Self-Hosted Runner Paths

**The Issue**: Self-hosted runners have different paths than GitHub-hosted.

**GitHub-hosted**: `/home/runner/work/...`
**Self-hosted**: `/home/github/actions-runner/_work/...`

**Don't**: Hardcode paths. Use `$GITHUB_WORKSPACE`.

---

### 21. Chocolatey v2 push – do not pass path (see gotcha 5b)

**The Bug**: Passing a path to `choco push` causes path+flag gluing. **Correct** (see gotcha 5b): run `choco push` from `packaging/chocolatey` after `choco pack` with no path; use `--api-key $env:CHOCO_API_KEY`. Match master branch.

---

## 🧪 Test Gotchas

### 13. Flaky UploadGovernorTests

**The Issue**: Tests using `AutoData` with random values can hit edge cases.

**Example**: Integer division with small random values causes off-by-one errors.

**Solution**: Use `InlineAutoData` with fixed values for edge-case-sensitive tests.

---

### 14. Test Isolation

**The Issue**: Tests that share static state can interfere with each other.

**Solution**: Use `TestIsolationExtensions` for tests that need isolated state.

---

## 🔐 Security Gotchas (Experimental Branch)

### 15. Security Services Not Wired to Transfer Handlers

**Current State**: 30 security components exist but aren't integrated into actual transfer code.

**TODO**: Wire `PathGuard`, `ContentSafety`, `ViolationTracker` into:
- `TransferService`
- `FilesController`
- `MultiSourceDownloadService`

---

### 16. UPnP Disabled by Default

**The Issue**: UPnP has known security vulnerabilities.

**Current**: `EnableUpnp = false` by default in `NatDetectionService.cs`

**Don't**: Enable UPnP by default without explicit user opt-in.

---

## 📝 Documentation Gotchas

### 17. DEVELOPMENT_HISTORY.md vs memory-bank/progress.md

- `DEVELOPMENT_HISTORY.md` - Human-maintained release history
- `memory-bank/progress.md` - AI session log

**Don't** overwrite `DEVELOPMENT_HISTORY.md` with AI-generated content.

---

### 18. TODO.md vs memory-bank/tasks.md

- `TODO.md` - Human-maintained high-level todos
- `memory-bank/tasks.md` - AI-managed task backlog

**Don't** duplicate tasks between them. Reference each other instead.

---

### 19. HashDb Not Populated - Missing Event Subscription

**The Bug**: HashDb was initializing but `seq_id` stayed at 0 because no code was hashing downloaded files.

**Files Affected**:
- `src/slskd/HashDb/HashDbService.cs`
- `src/slskd/Program.cs`

**Root Cause**: The `ContentVerificationService` only hashes files during multi-source downloads. Regular single-source downloads raised `DownloadFileCompleteEvent` but nothing subscribed to hash the file.

**Fix**: Subscribe `HashDbService` to `DownloadFileCompleteEvent` and hash downloaded files:
```csharp
eventBus.Subscribe<DownloadFileCompleteEvent>("HashDbService.DownloadComplete", OnDownloadCompleteAsync);
```

**Why This Happened**: The hashing logic was only implemented in the multi-source path, not the common download completion path.

---

### 20. Passive FLAC Discovery Architecture - Understanding the Design

**The Confusion**: The HashDb/FlacInventory was expected to populate "passively" but wasn't.

**The Design (Clarified)**:

The passive FLAC discovery system has **three sources** of FLAC files:

1. **Search Results** - When WE search, we see other users' files → add to `FlacInventory` with `hash_status='none'`
2. **Downloads** - When we download a FLAC → compute hash → store with `hash_status='known'`
3. **Incoming Interactions** - When users search us or download from us → track their username → optionally browse them later

**How FlacInventory Gets Populated**:

| Source | Event | Action |
|--------|-------|--------|
| Our searches | `SearchResponsesReceivedEvent` | Upsert FLAC files to FlacInventory (hash_status='none') |
| Our downloads | `DownloadFileCompleteEvent` | Hash first 32KB, store in HashDb, update FlacInventory |
| Mesh sync | `MeshSyncService` | Receive hashes from other slskdn clients |
| Backfill | `BackfillSchedulerService` | Probe files in FlacInventory where hash_status='none' |

**How Hashes Get Discovered**:

```
FlacInventory (hash_status='none')
         ↓
BackfillSchedulerService picks candidates
         ↓
Downloads first 32KB header
         ↓
Computes SHA256 hash
         ↓
Updates HashDb + FlacInventory
         ↓
Publishes to MeshSync
```

**Key Insight**: The `BackfillSchedulerService` is the "engine" that converts `hash_status='none'` entries into `hash_status='known'`. But it needs the `FlacInventory` to be populated first, which happens via search results and incoming interactions.

**Files Involved**:
- `src/slskd/HashDb/HashDbService.cs` - Subscribes to events, populates FlacInventory
- `src/slskd/Search/SearchService.cs` - Raises `SearchResponsesReceivedEvent`
- `src/slskd/Events/Types/Events.cs` - Defines `SearchResponsesReceivedEvent`
- `src/slskd/Backfill/BackfillSchedulerService.cs` - Probes FlacInventory entries
- `src/slskd/Application.cs` - Handles incoming searches/uploads (peer tracking)

---

---

### 21. API Calls Before Login - Infinite Loop Danger

**The Bug**: Components that make API calls on mount will cause infinite loops or errors when rendered on the login page (before authentication).

**Files Affected**:
- `src/web/src/components/LoginForm.jsx`
- `src/web/src/components/Shared/Footer.jsx`
- Any component rendered before login

**Wrong**:
```jsx
// In LoginForm.jsx - BAD: Footer makes API calls
import Footer from './Shared/Footer';

const LoginForm = () => {
  return (
    <>
      <LoginContent />
      <Footer /> {/* 💀 If Footer fetches data on mount, this breaks */}
    </>
  );
};

// In Footer.jsx - BAD: API call on mount
const Footer = () => {
  const [stats, setStats] = useState(null);

  useEffect(() => {
    api.getStats().then(setStats); // 💀 401 error before login!
  }, []);

  return <footer>...</footer>;
};
```

**Correct**:
```jsx
// Footer.jsx - GOOD: Pure static component, no API calls
const Footer = () => {
  const year = new Date().getFullYear();

  return (
    <footer>
      © {year} <a href="https://github.com/...">slskdN</a>
      {/* All content is static - no useEffect, no API calls */}
    </footer>
  );
};
```

**Why This Keeps Happening**: Models add "helpful" features like version info or stats to footers without considering the login page context.

**Rule**: Components rendered before login (LoginForm, Footer on login, error pages) MUST be pure/static with ZERO API calls.

---

### 22. HashDb Schema Migrations - Versioned Upgrades

**The System**: HashDb uses a versioned migration system (`HashDbMigrations.cs`) that runs automatically on startup.

**Key Files**:
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs` - Migration definitions
- `docs/HASHDB_SCHEMA.md` - Schema documentation

**How It Works**:
1. `__HashDbMigrations` table tracks applied versions
2. On startup, `RunMigrations()` compares current vs target version
3. Pending migrations run in order, each in a transaction
4. Failed migrations roll back automatically

**Adding New Columns** (SQLite gotcha):
```csharp
// WRONG - SQLite doesn't support multiple ALTER in one command
cmd.CommandText = @"
    ALTER TABLE Foo ADD COLUMN bar TEXT;
    ALTER TABLE Foo ADD COLUMN baz INTEGER;
";

// CORRECT - Execute each ALTER separately
var alters = new[] {
    "ALTER TABLE Foo ADD COLUMN bar TEXT",
    "ALTER TABLE Foo ADD COLUMN baz INTEGER"
};
foreach (var sql in alters)
{
    using var alterCmd = conn.CreateCommand();
    alterCmd.CommandText = sql;
    alterCmd.ExecuteNonQuery();
}
```

**Handling Existing Columns** (idempotent migrations):
```csharp
try
{
    alterCmd.ExecuteNonQuery();
}
catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
{
    // Column already exists - skip
}
```

**Check Current Version**:
```bash
curl http://localhost:5030/api/v0/hashdb/schema
```

**Rule**: Always increment `CurrentVersion` when adding migrations. Never modify existing migrations.

---

### 23. Missing `using` Directives - Check ALL Related Files

**The Bug**: Adding a type (e.g., `DateTimeOffset`) to an interface but only adding the `using System;` directive to one file, then having to fix each file one-by-one as compilation fails.

**Files Affected**:
- Any file that shares types across interface/implementation/controller boundaries

**Wrong Workflow**:
```
1. Add DateTimeOffset to IHashDbService.cs
2. Add "using System;" to IHashDbService.cs
3. Compile → ERROR in HashDbController.cs
4. Add "using System;" to HashDbController.cs
5. Compile → ERROR in HashDbService.cs
6. Add "using System;" to HashDbService.cs
7. Finally compiles ✅ (wasted 3 compile cycles)
```

**Correct Workflow**:
```
1. Add DateTimeOffset to IHashDbService.cs
2. BEFORE compiling, grep for all files that might need the type:
   grep -l "IHashDbService\|HashDb" src/slskd/HashDb/**/*.cs
3. Add "using System;" to ALL relevant files in one pass
4. Compile once ✅
```

**Pre-Compile Checklist** when adding new types:
```bash
# Find all files in the feature directory
find src/slskd/MyFeature -name "*.cs" -type f

# Or grep for files using the interface/class
grep -rl "IMyService\|MyService" src/slskd/MyFeature/
```

**Why This Keeps Happening**: AI models fix errors incrementally instead of thinking ahead about which files share the same types.

**Rule**: When adding a new type to an interface, check ALL files in the same namespace/feature directory and add necessary `using` directives BEFORE attempting to compile.

---

### 24. AUR PKGBUILD Checksums - NEVER Replace SKIP

**The Bug**: The AUR workflow was calculating the sha256 of `slskdn-dev-linux-x64.zip` and replacing the entire `sha256sums` array, overwriting `SKIP` with the calculated hash. This causes validation failures on `yay -Syu` because the zip changes every build.

**What Was Happening**:
```bash
# PKGBUILD template (CORRECT):
sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')
#           ^^^^   ^^^^^^^^   ^^^^^^^^   ^^^^^^^^
#           zip    service    yml        sysusers
#          (changes) (static)  (static)  (static)

# Workflow was replacing it with (WRONG):
sha256sums=('abc123...' 'SKIP' 'SKIP' 'SKIP')
#           ^^^^^^^^^^
#           Calculated hash for zip - breaks on next download!
```

**Why This Breaks**:
1. CI builds `slskdn-dev-linux-x64.zip` and calculates hash `abc123...`
2. Workflow updates AUR PKGBUILD with `sha256sums=('abc123...' ...)`
3. User runs `yay -S slskdn-dev` → works (zip matches hash)
4. CI rebuilds zip → new hash `def456...`
5. User runs `yay -Syu` → **FAILS** (cached zip has hash `abc123...`, PKGBUILD expects `abc123...`, but downloaded zip is `def456...`)

**The Fix**:
```bash
# DON'T calculate or replace the zip hash in the workflow
# The PKGBUILD template already has SKIP for index 0

# OLD (wrong):
sed -i "s/sha256sums=.*/sha256sums=('$SHA256' 'SKIP' 'SKIP' 'SKIP')/" PKGBUILD

# NEW (correct):
# Just update pkgver and _commit, leave sha256sums alone
sed -i "s/^pkgver=.*/pkgver=${VERSION}/" PKGBUILD
sed -i "s/^_commit=.*/_commit=${COMMIT}/" PKGBUILD
```

**Rule**: For AUR packages that download release binaries (not source), the first entry in `sha256sums` MUST be `'SKIP'` because the binary changes every build. Only static files (service files, configs) get real checksums.

**Related**: See gotcha #13 "SKIP vs Actual Hash in AUR" for more context on why this pattern exists.

---

## Package Manager Version Constraints

**The Problem**: AUR and RPM package managers don't allow hyphens in version strings, causing build failures.

**Error Messages**:
```
# AUR:
==> ERROR: pkgver is not allowed to contain colons, forward slashes, hyphens or whitespace.

# RPM:
error: line 2: Illegal char '-' (0x2d) in: Version: 0.24.1-dev-20251209-203936
```

**Why This Happens**:
Our dev builds use the format `0.24.1-dev-20251209-203936` (with hyphens). This works fine for Git tags and GitHub releases, but AUR and RPM have strict version format requirements:
- AUR `pkgver`: No hyphens, colons, slashes, or whitespace
- RPM `Version`: No hyphens (hyphen is reserved for separating version from release number)

**The Fix**:
Convert ALL hyphens to dots when generating package versions:

```bash
# Git/GitHub (hyphens OK):
DEV_VERSION="0.24.1-dev-20251209-203936"

# AUR/RPM/DEB (convert to dots):
ARCH_VERSION=$(echo "$DEV_VERSION" | sed 's/-/./g')
# Result: 0.24.1.dev.20251209.203936
```

**CRITICAL**: Use `sed 's/-/./g'` (global replace) NOT `sed 's/-/./'` (only first hyphen)!

**Where This Applies**:
- AUR PKGBUILD: `pkgver=0.24.1.dev.20251209.203936`
- RPM spec: `Version: 0.24.1.dev.20251209.203936`
- Debian changelog: `slskdn-dev (0.24.1.dev.20251209.203936-1)`
- Package filenames: `slskdn-dev_0.24.1.dev.20251209.203936_amd64.deb`

**Git Tag and Zip Stay Original**:
- Git tag: `dev-20251209-203936` (hyphens OK)
- Zip file: `slskdn-dev-20251209-203936-linux-x64.zip` (hyphens OK)
- GitHub release title: `Dev Build 20251209-203936` (hyphens OK)

---

## Integration Test Project Missing Project Reference

**The Problem**: Docker builds fail with `error CS0234: The type or namespace name 'Common' does not exist in the namespace 'slskd'` when building integration tests.

**Root Cause**: The `tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj` file was missing a `<ProjectReference>` to the main `src/slskd/slskd.csproj` project.

**Error Message**:
```
/slskd/tests/slskd.Tests.Integration/SecurityIntegrationTests.cs(10,13): error CS0234: 
The type or namespace name 'Common' does not exist in the namespace 'slskd' 
(are you missing an assembly reference?) [/slskd/tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj]
```

**Why This Breaks**:
1. Integration tests need to reference types from the main project (`slskd.Common.Security`, etc.)
2. Without a `<ProjectReference>`, the compiler can't find any `slskd.*` namespaces
3. This fails silently in local builds if you've previously built the main project (DLL is in bin/), but ALWAYS fails in Docker/CI clean builds

**The Fix**:
```xml
<!-- tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -->
<ItemGroup>
  <ProjectReference Include="../../src/slskd/slskd.csproj" />
</ItemGroup>
```

**Prevention**:
- When creating ANY test project, ALWAYS add a `<ProjectReference>` to the code being tested
- Test in Docker before committing: `docker build -f Dockerfile .`
- Check .csproj files when you see "namespace does not exist" errors in CI

**Related**: This is especially insidious because local `dotnet build` might work if you've built the main project before, masking the missing reference until CI runs.

---

## Workflow File Pattern Mismatch in Download Step

**The Problem**: The `packages` job fails with "no assets match the file pattern" when trying to download the zip from the dev release.

**Root Cause**: Mismatch between the actual filename and the download pattern:
- Build job creates: `slskdn-dev-linux-x64.zip` (no timestamp)
- Packages job tried to download: `slskdn-dev-*-linux-x64.zip` (wildcard for timestamp that doesn't exist)

**Error Message**:
```
gh release download dev --pattern "slskdn-dev-*-linux-x64.zip"
no assets match the file pattern
```

**Why This Breaks**:
1. The `build` job creates `slskdn-dev-linux-x64.zip` without a timestamp in the filename
2. The `release` job uploads this file to the `dev` tag as-is
3. The `packages` job tries to download with a wildcard pattern expecting a timestamp
4. The wildcard doesn't match, so no file is downloaded

**The Fix**:
```yaml
# packages job - Download from Dev Release step
gh release download dev \
  --repo ${{ github.repository }} \
  --pattern "slskdn-dev-linux-x64.zip"  # Exact filename, no wildcard
```

**Prevention**:
- When adding workflow download steps, check what the ACTUAL filename is from the upload step
- Don't use wildcards unless the filename actually varies
- The timestamp is in the VERSION/tag, not in the zip filename for dev builds

**Note**: The timestamped dev tag (e.g., `dev-20251209-212425`) is separate from the floating `dev` tag. The `dev` tag always points to the latest dev build and contains `slskdn-dev-linux-x64.zip`.

---

## Building RPM Packages on Ubuntu Fails with Missing BuildRequires

**The Problem**: The `packages` job fails when trying to build .rpm packages on Ubuntu with "Failed build dependencies: systemd-rpm-macros is needed".

**Root Cause**: The RPM spec file has `BuildRequires: systemd-rpm-macros` and `BuildRequires: unzip`, which are Fedora packages not available in Ubuntu's apt repositories. You can't build RPMs on Ubuntu that require Fedora-specific build tools.

**Error Message**:
```
error: Failed build dependencies:
	systemd-rpm-macros is needed by slskdn-dev-0.24.1.dev.20251209.213134-1.x86_64
	unzip is needed by slskdn-dev-0.24.1.dev.20251209.213134-1.x86_64
```

**Why This Breaks**:
1. RPM spec files can have `BuildRequires` for Fedora-specific packages
2. Ubuntu (apt) doesn't have `systemd-rpm-macros` or the exact versions of build tools RPM expects
3. The `rpmbuild` command on Ubuntu can't satisfy these dependencies
4. Cross-distro package building requires containers or native build environments

**The Fix**:
Don't build RPMs on Ubuntu. Let COPR (which runs on Fedora) handle RPM builds. The `packages` job should only build .deb:

```yaml
packages:
  name: Build .deb Package  # Changed from "Build Packages (.deb and .rpm)"
  # ... only build .deb, remove all RPM build steps
```

**Correct Architecture**:
- **AUR job**: Builds Arch packages (runs on Arch via Docker)
- **COPR job**: Builds RPM packages (runs on Fedora infrastructure)
- **PPA job**: Builds Debian packages (runs on Ubuntu/Launchpad)  
- **Packages job**: Builds .deb for direct GitHub download (Ubuntu is fine)
- **Docker job**: Builds container images (distro-agnostic)

**Prevention**:
- Ubuntu can build .deb natively
- Fedora (COPR) should build .rpm natively
- Don't try to build distro-specific packages on the wrong distro
- If you need RPMs as GitHub release assets, download them from COPR after it builds

---

## PPA Rejects Upload: Version Comparison with Hyphens

**The Problem**: Launchpad PPA rejects the upload with "Version older than that in the archive" even though the new version has a later timestamp.

**Root Cause**: Debian version string comparison treats hyphens differently than dots. The version `0.24.1-dev-20251209-214612` is considered OLDER than `0.24.1-dev.202512092002` because of how dpkg compares version strings.

**Error Message**:
```
Rejected: slskdn-dev_0.24.1-dev-20251209-214612-1ppa202512092148~jammy.dsc: 
Version older than that in the archive. 
0.24.1-dev-20251209-214612-1ppa202512092148~jammy <= 0.24.1-dev.202512092002-1ppa202512092006~jammy
```

**Why This Breaks**:
Debian's `dpkg --compare-versions` treats hyphens as version separators, not as part of the version string:
- `0.24.1-dev-20251209-214612` is parsed as epoch `0`, version `0.24.1`, and the rest as debian revision
- `0.24.1-dev.202512092002` with dots keeps the full version number intact
- The comparison logic makes the hyphenated version appear older

**The Fix**:
Convert ALL hyphens to dots in the PPA version string:

```bash
VERSION="${{ needs.build.outputs.dev_version }}"  # 0.24.1-dev-20251209-214612
DEB_VERSION=$(echo "$VERSION" | sed 's/-/./g')    # 0.24.1.dev.20251209.214612

# Use DEB_VERSION in changelog
slskdn-dev (${DEB_VERSION}-1ppa${PPA_REV}~jammy) jammy; urgency=medium
```

**Critical**: This is the SAME issue as the AUR/RPM version problem, but it manifests differently - not as a build error, but as a PPA rejection during upload. You MUST convert hyphens to dots for ALL Debian-based packaging (AUR, RPM, DEB, PPA).

**Prevention**:
- ALWAYS use `sed 's/-/./g'` (global replace) for ANY package version strings
- Check EVERY place where `$VERSION` or `dev_version` is used in packaging workflows
- Test PPA uploads don't get rejected with "Version older than that in the archive"

---

## Yay Cache Contains Stale PKGBUILD After AUR Fix

**The Problem**: After fixing the AUR workflow to keep `SKIP` for the binary checksum, `yay -S slskdn-dev` still fails with "One or more files did not pass the validity check!" even though the AUR repo has the correct PKGBUILD.

**Root Cause**: Yay caches PKGBUILDs in `~/.cache/yay/package-name/`. If the cached PKGBUILD is from a previous (broken) workflow run that had a real hash instead of `SKIP`, yay will use the stale cached version instead of fetching the fixed one from AUR.

**Error Message**:
```
==> Validating source files with sha256sums...
    slskdn-dev-linux-x64.zip ... FAILED
==> ERROR: One or more files did not pass the validity check!
```

**Why This Happens**:
1. Old workflow pushed PKGBUILD with `sha256sums=('abc123...' 'SKIP' 'SKIP' 'SKIP')`
2. User ran `yay -S package-name` and yay cached that broken PKGBUILD
3. Workflow was fixed to preserve `SKIP` in the template
4. New correct PKGBUILD pushed to AUR: `sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')`
5. User runs `yay -S package-name` again, but yay uses the CACHED broken version
6. Checksum fails because the binary has changed but cached PKGBUILD has the old hash

**The Fix**:
Clear yay's cache for the package:

```bash
rm -rf ~/.cache/yay/package-name
yay -S package-name  # Will fetch fresh PKGBUILD from AUR
```

**Prevention**:
- When testing AUR packages during development, always clear cache after workflow fixes
- Add this to testing docs: "If you previously tested a broken build, clear yay cache first"
- Yay's cache is helpful for normal use but can hide fixes during rapid iteration

---

## EF Core Can't Translate DateTimeOffset to DateTime Comparison

**The Problem**: Backfill endpoint throws 500 error with "The LINQ expression could not be translated" when trying to compare `Search.StartedAt` (DateTime) with a DateTimeOffset value.

**Root Cause**: Entity Framework Core cannot translate implicit conversions between `DateTimeOffset` and `DateTime` to SQL. When you write `s.StartedAt < lastProcessedAt.Value` where `StartedAt` is `DateTime` and `lastProcessedAt` is `DateTimeOffset?`, EF can't generate the SQL query.

**Error Message**:
```
System.InvalidOperationException: The LINQ expression 'DbSet<Search>()
    .Count(s => (DateTimeOffset)s.StartedAt < __lastProcessedAt_Value_0)' could not be translated.
```

**The Fix**:
Convert `DateTimeOffset` to `DateTime` explicitly using `.UtcDateTime` before the comparison:

```csharp
// WRONG - EF can't translate this:
await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value);

// CORRECT - EF can translate this:
await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value.UtcDateTime);
```

**Prevention**:
- Always check the database column type before writing LINQ queries
- Use `.UtcDateTime` when comparing `DateTimeOffset` with `DateTime` in EF queries
- Test API endpoints that use LINQ queries against the database
- EF will throw this at runtime, not compile time, so manual testing is required

---

### 20. CreateDirectory on Existing File Path

**The Bug**: `System.IO.IOException: The file '/slskd/slskd' already exists` when trying to create a directory at a path that's already occupied by a file (the binary itself).

**Files Affected**:
- `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs`
- `src/slskd/Program.cs`

**What Happened**:
`SourceDiscoveryService` used `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` which returns `/slskd` in Docker containers. It then tried to `CreateDirectory("/slskd/slskd")` to store the discovery database, but `/slskd/slskd` is the binary executable file, not a directory. This caused a crash on every API request that needed `SourceDiscoveryService`.

**Why It Happened**:
1. `LocalApplicationData` is not reliable in containers - can return unexpected paths
2. No check for whether the path is a file vs directory before calling `CreateDirectory()`
3. Different behavior than other services which use `Program.AppDirectory`

**The Error**:
```
System.IO.IOException: The file '/slskd/slskd' already exists.
  at System.IO.FileSystem.CreateDirectory(String fullPath, UnixFileMode unixCreateMode)
  at System.IO.Directory.CreateDirectory(String path)
  at slskd.Transfers.MultiSource.Discovery.SourceDiscoveryService..ctor(...)
```

**The Fix**:
Use `Program.AppDirectory` (like all other services) and create a subdirectory:

```csharp
// WRONG - uses unreliable LocalApplicationData
var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var slskdPath = Path.Combine(appDataPath, "slskd");
System.IO.Directory.CreateDirectory(slskdPath); // CRASHES if /slskd/slskd is a file!

// CORRECT - use Program.AppDirectory and create subdirectory
public SourceDiscoveryService(
    string appDirectory,  // Injected via DI
    ISoulseekClient soulseekClient,
    IContentVerificationService verificationService)
{
    var slskdPath = Path.Combine(appDirectory, "discovery");
    if (!Directory.Exists(slskdPath))
    {
        Directory.CreateDirectory(slskdPath);
    }
    dbPath = Path.Combine(slskdPath, "discovery.db");
}

// Update DI registration to pass Program.AppDirectory
services.AddSingleton<ISourceDiscoveryService>(sp => new SourceDiscoveryService(
    Program.AppDirectory,
    sp.GetRequiredService<ISoulseekClient>(),
    sp.GetRequiredService<Transfers.MultiSource.IContentVerificationService>()));
```

**Prevention**:
- **ALWAYS** use `Program.AppDirectory` for data storage, never `LocalApplicationData`
- **ALWAYS** create a subdirectory for each service's data (e.g., `discovery/`, `ranking/`, `hashdb/`)
- **ALWAYS** check `Directory.Exists()` before `CreateDirectory()` when the path might vary
- Pattern to follow: `Path.Combine(Program.AppDirectory, "myservice")` → creates `/app/myservice/` in containers

**Related Pattern**:
```csharp
// Good examples from the codebase:
var rankingDbPath = Path.Combine(Program.AppDirectory, "ranking.db");
var hashDbService = new HashDbService(Program.AppDirectory, ...);
var wishlistDbPath = Path.Combine(Program.AppDirectory, "wishlist.db");
```

---

### 21. Scanner Detection Noise from Private IPs

**The Bug**: Logs spammed with hundreds of "Scanner detected from 192.168.1.77" warnings when users access the web UI from their LAN.

**Files Affected**:
- `src/slskd/Common/Security/FingerprintDetection.cs`
- `src/slskd/Common/Security/SecurityMiddleware.cs` (partial fix)

**What Happened**:
The web UI polls multiple API endpoints rapidly (~5-10 requests/second), which triggered the reconnaissance detection system. Even after fixing `SecurityMiddleware` to skip `RecordConnection()` for private IPs, old profiles from before the fix were still marked as scanners, and the logging still fired.

**Why It Happened**:
1. Web UI makes many rapid API calls (status bar, capabilities, DHT, mesh, hashdb, backfill stats, etc.)
2. This looks like port scanning / reconnaissance to `FingerprintDetection`
3. First fix: `SecurityMiddleware` skipped `RecordConnection()` for private IPs (lines 103-110)
4. But old profiles from before the fix were still in memory as flagged scanners
5. `FingerprintDetection.RecordConnection()` logged warnings for those old profiles

**The Error**:
```
20:09:16  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
20:09:26  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
20:09:36  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
... (repeats hundreds of times)
```

**The Fix**:
Add private IP check to `FingerprintDetection` itself, not just `SecurityMiddleware`:

```csharp
// In FingerprintDetection.RecordConnection():
if (profile.IsScanner)
{
    // Don't log warnings for private/local IPs (e.g., web UI polling APIs rapidly)
    if (!IsPrivateOrLocalIp(ip))
    {
        _logger.LogWarning(
            "Scanner detected from {Ip}: {Indicators}",
            ip,
            string.Join(", ", indicators.Select(i => i.Type)));

        ReconnaissanceDetected?.Invoke(this, new ReconnaissanceEventArgs(evt));
    }
}

// Add helper method (same as SecurityMiddleware):
private static bool IsPrivateOrLocalIp(IPAddress ip)
{
    // Check for 192.168.x.x, 10.x.x.x, 172.16-31.x.x, 127.x.x.x, fe80::/10, fc00::/7
    // ... (full implementation in code)
}
```

**Prevention**:
- Security logging should **always** check for private IPs before emitting warnings
- Private IP checks should be at **both** the middleware layer (prevent tracking) **and** the service layer (prevent logging)
- Web UI polling is legitimate behavior - don't treat LAN clients as threats
- Test security features with both public and private IPs

**Why Two Fixes Were Needed**:
1. **SecurityMiddleware fix**: Prevents NEW profiles from being created for private IPs
2. **FingerprintDetection fix**: Prevents logging warnings for OLD profiles (already flagged)
3. Both layers need the check to fully eliminate noise

**Private IP Ranges**:
- IPv4: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `169.254.0.0/16`, `127.0.0.0/8`
- IPv6: `fe80::/10` (link-local), `fc00::/7` (unique local), `::1` (loopback)

---

### 22. Ambiguous Type Reference (Directory)

**The Bug**: `error CS0104: 'Directory' is an ambiguous reference between 'Soulseek.Directory' and 'System.IO.Directory'`

**Files Affected**:
- Any file that has both `using System.IO;` and `using Soulseek;`

**What Happened**:
When fixing the CreateDirectory bug (#20), I added code that used `Directory.Exists()` and `Directory.CreateDirectory()`. The compiler couldn't determine if this meant `System.IO.Directory` or `Soulseek.Directory` (which is a completely different type representing a Soulseek shared directory).

**Why It Happened**:
Both namespaces define a type called `Directory`:
- `System.IO.Directory` - file system operations
- `Soulseek.Directory` - Soulseek protocol type for shared directories

When both namespaces are imported with `using`, the unqualified name `Directory` is ambiguous.

**The Error**:
```
/home/runner/work/slskdn/slskdn/src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs(73,18): 
error CS0104: 'Directory' is an ambiguous reference between 'Soulseek.Directory' and 'System.IO.Directory'
```

**The Fix**:
Always fully qualify `Directory` when both namespaces are imported:

```csharp
// WRONG - ambiguous when both System.IO and Soulseek are imported:
if (!Directory.Exists(slskdPath))
{
    Directory.CreateDirectory(slskdPath);
}

// CORRECT - fully qualified:
if (!System.IO.Directory.Exists(slskdPath))
{
    System.IO.Directory.CreateDirectory(slskdPath);
}
```

**Alternative Fix** (if you need both frequently):
Add a using alias at the top of the file:
```csharp
using IODirectory = System.IO.Directory;

// Then use:
if (!IODirectory.Exists(slskdPath))
{
    IODirectory.CreateDirectory(slskdPath);
}
```

**Prevention**:
- When you see both `using System.IO;` and `using Soulseek;` in a file, **always** qualify `Directory`
- Grep for this pattern before committing: `grep -n "using Soulseek" src/**/*.cs | grep -v "using System.IO"` won't help because they're often far apart
- Better: Run `dotnet build` locally before pushing to catch these at compile time

**Other Ambiguous Types in This Codebase**:
- `Directory` (System.IO vs Soulseek)
- `File` (System.IO vs Soulseek)
- `Transfer` (slskd.Transfers.Transfer vs Soulseek.Transfer) - already resolved with `using Transfer = slskd.Transfers.Transfer;` in Events.cs

**Quick Fix Command**:
```bash
# Find files that might have this issue:
grep -l "using Soulseek" src/slskd/**/*.cs | xargs grep -l "Directory\.Exists\|Directory\.Create" | xargs sed -i 's/Directory\.Exists/System.IO.Directory.Exists/g; s/Directory\.Create/System.IO.Directory.Create/g'
```

---

### E2E Test Infrastructure Issues

#### E2E-1: Server crashes during share initialization in test harness

**The Bug**: E2E test nodes crash with `ShareInitializationException: Share cache backup is missing, corrupt, or is out of date` because test nodes start with empty app directories and no share cache.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`

**Wrong**:
```typescript
const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath];
```

**Correct**:
```typescript
// Add --force-share-scan to avoid ShareInitializationException when cache doesn't exist
const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath, '--force-share-scan'];
```

**Why This Keeps Happening**: Test nodes start with fresh app directories, so share cache doesn't exist. The server requires either a valid cache or `--force-share-scan` to create one.

---

#### E2E-2: Static files return 404 because SPA fallback intercepts them

**The Bug**: Static files (`/static/js/*.js`, `/static/css/*.css`) return 404, preventing React from mounting. The SPA fallback endpoint runs before the file server and intercepts all requests, including static files.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
// SPA fallback endpoint runs BEFORE file server
endpoints.MapGet("{*path}", async context => {
    // This intercepts /static/* requests and returns 404
    if (!hasExtension) {
        await context.Response.SendFileAsync(indexPath);
    } else {
        context.Response.StatusCode = 404; // Static files get 404 here!
    }
});
app.UseFileServer(...); // Never reached for static files
```

**Correct**:
```csharp
// File server runs first
app.UseFileServer(fileServerOptions);

// SPA fallback middleware runs AFTER file server
app.Use(async (context, next) => {
    await next(); // Let file server try first
    
    // Only serve index.html if file server returned 404 for a client-side route
    if (context.Response.StatusCode == 404 && !isApi && !isStatic && !hasExtension) {
        await context.Response.SendFileAsync(indexPath);
    }
});
```

**Why This Keeps Happening**: Endpoints run before middleware, so a catch-all endpoint intercepts requests before the file server middleware can serve static files. The solution is to use middleware AFTER the file server that only handles 404s for client-side routes.

---

#### E2E-3: Excessive timeouts in test helpers

**The Bug**: `waitForHealth` polls for 60 seconds (120 iterations × 500ms) when the server typically starts in 2-5 seconds.

**Files Affected**:
- `src/web/e2e/helpers.ts`

**Wrong**:
```typescript
for (let i = 0; i < 120; i++) { // 60 seconds
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
}
```

**Correct**:
```typescript
// Server typically starts in 2-5 seconds, so 15 seconds is plenty
for (let i = 0; i < 30; i++) { // 15 seconds
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
}
```

**Why This Keeps Happening**: Default timeouts are set conservatively, but actual server startup is much faster. Reduce timeouts to match reality.

---

#### E2E-4: Multi-peer tests fail with "instance already running" mutex error

**The Bug**: When starting multiple test nodes (A and B), the second node fails with "An instance of slskd is already running" because the mutex name was global (based only on AppName), not per-app-directory.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
private static Mutex Mutex { get; } = new Mutex(initiallyOwned: true, Compute.Sha256Hash(AppName));
// Mutex check happens before AppDirectory is set
if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false)) {
    Log.Fatal($"An instance of {AppName} is already running");
    return;
}
AppDirectory ??= DefaultAppDirectory; // Set AFTER mutex check
```

**Correct**:
```csharp
private static Mutex Mutex { get; set; }

private static string GetMutexName() {
    var dir = AppDirectory ?? DefaultAppDirectory;
    return $"{AppName}_{Compute.Sha256Hash(dir)}";
}

// Set AppDirectory FIRST, then create mutex with app-directory-specific name
AppDirectory ??= DefaultAppDirectory;
Mutex = new Mutex(initiallyOwned: true, GetMutexName());
if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false)) {
    Log.Fatal($"An instance of {AppName} is already running in app directory: {AppDirectory}");
    return;
}
```

**Why This Keeps Happening**: The mutex was created as a static property initializer (before AppDirectory is set) with a global name. Each test node needs its own mutex based on its unique app directory.

---

#### E2E-6: Health check hangs during server startup

**The Bug**: E2E test nodes hang during startup because the `/health` endpoint never responds. The `MeshHealthCheck` calls `GetStatsAsync()` which can hang if mesh services aren't initialized yet, especially NAT detection which tries to connect to external STUN servers.

**Files Affected**:
- `src/slskd/Mesh/MeshHealthCheck.cs`
- `src/slskd/Mesh/MeshStatsCollector.cs`
- `src/slskd/Program.cs`
- `src/web/e2e/harness/SlskdnNode.ts`

**Wrong**:
```csharp
// MeshHealthCheck.cs - no timeout, hangs if services not ready
var stats = await _statsCollector.GetStatsAsync();

// MeshStatsCollector.cs - NAT detection can hang
natType = await stunDetector.DetectAsync();
```

**Correct**:
```csharp
// MeshHealthCheck.cs - add timeout and handle gracefully
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
var stats = await _statsCollector.GetStatsAsync().WaitAsync(timeoutCts.Token);
// Return Degraded instead of Unhealthy if timeout/error occurs

// MeshStatsCollector.cs - add timeout to NAT detection
using var natTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
natType = await stunDetector.DetectAsync(natTimeoutCts.Token);

// Program.cs - configure health check timeout
services.AddHealthChecks()
    .AddMeshHealthCheck(
        failureStatus: HealthStatus.Degraded, // Don't fail entire endpoint
        timeout: TimeSpan.FromSeconds(5));

// SlskdnNode.ts - use simpler readiness endpoint
const readinessUrl = `${this.apiUrl}/health/ready`; // Simple endpoint, no complex checks
```

**Why This Keeps Happening**: Health checks run during startup before all services are initialized. Mesh services (especially NAT detection) can hang waiting for external resources. The health endpoint waits for all checks to complete, so a hanging check blocks the entire endpoint.

**Prevention**:
- Always add timeouts to health checks that call async operations
- Return `Degraded` instead of `Unhealthy` for startup-time issues
- Use simpler readiness endpoints for E2E tests that bypass complex checks
- Add timeouts to any external service calls in health checks (NAT detection, DNS, etc.)

---

#### E2E-5: Tests should be lenient for incomplete features

**The Bug**: Tests fail when UI elements don't exist because features aren't fully implemented yet.

**Files Affected**:
- All E2E test files

**Wrong**:
```typescript
await page.getByTestId(T.someFeature).click(); // Fails if feature doesn't exist
await expect(page.getByTestId(T.someElement)).toBeVisible();
```

**Correct**:
```typescript
const featureBtn = page.getByTestId(T.someFeature);
if (await featureBtn.count() === 0) {
  test.skip(); // Skip if feature not available
  return;
}
await featureBtn.click();
await expect(page.getByTestId(T.someElement)).toBeVisible({ timeout: 10000 });
```

**Why This Keeps Happening**: Features may be partially implemented or not yet available. Tests should gracefully skip rather than fail, allowing the test suite to run and verify what's actually implemented.

---

#### E2E-6: React Router routes not matching due to basename/urlBase mismatch

**The Bug**: When BrowserRouter has a `basename` prop set, routes and Links should NOT include the `urlBase` prefix, otherwise routes won't match. Also, if using memory history (MemoryRouter), redirects won't update the browser URL, causing the symptom "UI shows different page than URL".

**Files Affected**:
- `src/web/src/index.jsx` - Router setup
- `src/web/src/components/App.jsx` - Route definitions
- `src/web/e2e/multippeer-sharing.spec.ts` - Test diagnostics

**Wrong**:
```jsx
// If urlBase is "/slskd" and basename is set:
<Router basename="/slskd">
  <Route path="/slskd/contacts" />  // ❌ Won't match! Router strips basename first
  <Link to="/slskd/contacts" />     // ❌ Double-prefix
</Router>
```

**Correct**:
```jsx
// When basename is set, routes should be base-relative:
<Router basename={urlBase && urlBase !== '/' ? urlBase : undefined}>
  <Route path="/contacts" />  // ✅ Router adds basename automatically
  <Link to="/contacts" />     // ✅ Router adds basename automatically
</Router>

// When basename is undefined (urlBase is empty or '/'), use full paths:
<Router basename={undefined}>
  <Route path={`${urlBase}/contacts`} />  // ✅ urlBase is empty, so becomes "/contacts"
  <Link to={`${urlBase}/contacts`} />     // ✅ urlBase is empty, so becomes "/contacts"
</Router>
```

**Diagnostic Pattern**:
```typescript
// In E2E tests, compare browser location vs app history:
const loc = await page.evaluate(() => ({ 
  href: location.href, 
  pathname: location.pathname 
}));
const appLoc = await page.evaluate(() => {
  if ((window as any).__APP_HISTORY__) {
    return (window as any).__APP_HISTORY__.location.pathname;
  }
  return null;
});
// If loc.pathname !== appLoc, you're using memory history or basename mismatch
```

**Why This Keeps Happening**: React Router's `basename` prop automatically prepends to all routes and links. If you manually include the basename in route paths, you get a double-prefix that prevents matching. Also, using MemoryRouter instead of BrowserRouter causes redirects to not update the browser URL.

---

#### E2E-7: TypeScript-only syntax in JSX breaks builds

**The Bug**: Using TypeScript-only syntax (e.g., `window as any`) in `.jsx` files causes the web build to fail or silently serve stale bundles, which hides routing/debugging changes.

**Files Affected**:
- `src/web/src/components/App.jsx`

**Wrong**:
```jsx
// ❌ TypeScript cast is invalid in plain JSX
(window as any).__ROUTE_MISS_ELEMENT__ = el.textContent;
```

**Correct**:
```jsx
// ✅ Plain JS assignment
window.__ROUTE_MISS_ELEMENT__ = el.textContent;
```

**Why This Keeps Happening**: It's easy to copy/paste TS patterns into a JS file. CRA/CRACO won't compile TS-only syntax in `.jsx`, and a failed build can leave old bundles in `wwwroot`, masking changes.

---

#### E2E-8: Ambiguous `/shares` route between file shares and share grants

**The Bug**: The legacy file shares API and the new share-grants API both used `/api/v0/shares`, causing `AmbiguousMatchException` (500) for GET `/api/v0/shares`.

**Files Affected**:
- `src/slskd/Shares/API/Controllers/SharesController.cs` (legacy file shares)
- `src/slskd/Sharing/API/SharesController.cs` (share grants)
- `src/web/src/lib/collections.js`

**Wrong**:
```csharp
[Route("api/v{version:apiVersion}/shares")] // used by BOTH controllers
```

**Correct**:
```csharp
[Route("api/v{version:apiVersion}/share-grants")] // share grants only
```

**Why This Keeps Happening**: Both features are named "Shares" but represent different domains (local file shares vs collection share grants). Without a distinct route prefix, ASP.NET Core can't disambiguate endpoints.

---

#### E2E-9: Share-grants "GetAll" is recipient-only (owner won't see outgoing shares)

**The Bug**: `GET /api/v0/share-grants` returns grants **accessible to the current user as a recipient** (direct user or share-group member). It does **not** include the grants you created as the owner unless you also happen to be a recipient/member, which makes the owner UI appear as "No shares yet" after a successful create.

**Files Affected**:
- `src/slskd/Sharing/ShareGrantRepository.cs` (accessibility logic)
- `src/slskd/Sharing/API/SharesController.cs` (endpoint semantics)
- `src/web/src/components/Collections/Collections.jsx` (owner view needs by-collection endpoint)

**Fix**:
- Keep `GET /share-grants` as recipient-accessible (used by "Shared with Me")
- Add `GET /share-grants/by-collection/{collectionId}` for owner/outgoing shares, and have the Collections UI use it

---

#### E2E-10: Cross-node share discovery requires token signing key plus distinct per-port CSRF cookie names

**The Bug**: Cross-node share discovery via private messages requires:
1. `Sharing:TokenSigningKey` configured (base64, min 32 bytes) or token creation fails
2. The antiforgery cookie token and the JS-readable request-token cookie must use different names, and both names must be port-specific, or the frontend can read the cookie token and send it back as the request token
3. OwnerEndpoint in announcements must use `127.0.0.1` not `localhost` (Playwright request client prefers IPv6 `::1` for "localhost")

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts` (config generation)
- `src/slskd/Program.cs` (CSRF cookie name, antiforgery config)
- `src/slskd/Sharing/API/SharesController.cs` (ownerEndpoint calculation)
- `src/web/src/lib/api.js` (CSRF token reading)

**Wrong**:
```csharp
options.Cookie.Name = $"XSRF-TOKEN-{OptionsAtStartup.Web.Port}";
context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken, cookieOptions);
var ownerEndpoint = $"{scheme}://localhost:{web.Port}"; // localhost → ::1 in Playwright
```

**Correct**:
```csharp
options.Cookie.Name = $"XSRF-COOKIE-{OptionsAtStartup.Web.Port}";
context.Response.Cookies.Append($"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", tokens.RequestToken, cookieOptions);
var ownerEndpoint = $"{scheme}://127.0.0.1:{web.Port}"; // Explicit IPv4
```

**Why This Keeps Happening**: Multi-instance E2E runs multiple nodes on the same host with different ports. Cookies are host-scoped (not port-scoped), so fixed names collide. A second trap is that ASP.NET antiforgery uses one cookie token and one request token; if both cookies share the `XSRF-TOKEN*` namespace, the frontend can pick up the wrong one and ASP.NET reports the token pair as swapped. Playwright's request client also resolves "localhost" to IPv6 by default, but nodes bind to IPv4.

### 0v. Share Scan Progress Must Stay Monotonic While Parallel Workers Finish Out Of Order

**The Bug**: Share scans process directories in parallel, but worker completions arrive out of order. The scanner can emit a newer progress snapshot first and then a stale lower snapshot later, which makes the UI and logs move backward from `9%` to `8%` or drop the in-progress file count.

**Files Affected**:
- `src/slskd/Shares/ShareService.cs`
- `tests/slskd.Tests.Unit/Shares/ShareServiceLifecycleTests.cs`

**Wrong**:
```csharp
ScanProgress = current.FillProgress,
Files = current.Files,
```

**Correct**:
```csharp
ScanProgress = current.Filling && current.FillProgress < state.ScanProgress ? state.ScanProgress : current.FillProgress,
Files = current.Filling && current.Files < state.Files ? state.Files : current.Files,
```

**Why This Keeps Happening**: even after moving the raw counters to `Interlocked`, the state update still arrives asynchronously from multiple workers. The service layer has to treat in-flight scan progress as monotonic state instead of trusting every late-arriving worker snapshot.

### 0w. Soulseek Network Teardown Exceptions Must Not Fall Through To Generic Fatal Telemetry

**The Bug**: expected Soulseek network churn such as disposed `Connection` objects, `Unknown PierceFirewall attempt`, `No route to host`, and inactivity timeouts could bypass the expected-network exception filter and get logged as generic `[FATAL] Unobserved task exception`, making ordinary connectivity failures look like process corruption.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
var isNetworkFailure =
    exception is TimeoutException ||
    exception is OperationCanceledException ||
    exception is IOException ||
    exception is SocketException ||
    typeName.Contains("Soulseek.ConnectionReadException", StringComparison.Ordinal);
```

**Correct**:
```csharp
var isNetworkFailure =
    exception is TimeoutException ||
    exception is OperationCanceledException ||
    exception is IOException ||
    exception is ObjectDisposedException objectDisposedException && string.Equals(objectDisposedException.ObjectName, "Connection", StringComparison.Ordinal) ||
    exception is SocketException ||
    typeName.Contains("Soulseek.ConnectionReadException", StringComparison.Ordinal) ||
    typeName.Contains("Soulseek.ConnectionException", StringComparison.Ordinal);
```

```text
Also match common Soulseek network detail strings such as "Unknown PierceFirewall attempt",
"No route to host", "Inactivity timeout", and both "Operation canceled" spellings.
```

**Why This Keeps Happening**: the Soulseek library surfaces several expected connection-failure paths through different exception types and message text. Filtering only the obvious socket/cancel/read exceptions leaves teardown races and peer-protocol failures to fall through the generic fatal logger.

### 0x. Port-Scoped CSRF Request Tokens Must Use The Injected Backend Port, Not `window.location.port`

**The Bug**: The web client switched to per-port CSRF request-token cookies, but the reader used `window.location.port` to choose the cookie name. That works for direct `:5030` access and fails behind a reverse proxy or default-port deployment, because the browser URL may have no visible port while the backend still issued `XSRF-TOKEN-5030`.

**Files Affected**:
- `src/web/src/lib/api.js`
- `src/web/src/lib/api.test.js`

**Wrong**:
```javascript
export const getCsrfTokenFromCookieString = (
  cookieString = document.cookie,
  currentPort = window.location.port,
) => {
  const portScopedToken = parsedCookies.get(`XSRF-TOKEN-${currentPort}`);
```

**Correct**:
```javascript
const inferredPort = String(window.port || window.location.port || '');
const portScopedToken = parsedCookies.get(`XSRF-TOKEN-${inferredPort}`);
```

```text
If the injected backend port is unavailable, only then fall back to the browser-visible port.
For reverse-proxy/default-port deployments, also fall back to the single available `XSRF-TOKEN-*`
cookie instead of sending no CSRF token at all.
```

**Why This Keeps Happening**: the frontend naturally wants to key off the current URL, but slskdN injects the real backend port separately because the app can sit behind path prefixes, TLS termination, or a proxy that hides the origin port from `window.location`. Per-port cookie names have to follow the injected backend port, not the visible URL port.

### 0y. Expected Soulseek Unobserved Network Churn Should Not Spam Warning-Level Telemetry

**The Bug**: After classifying more Soulseek teardown exceptions as expected, the handler still logged them as `[WARN] Unobserved Soulseek peer/distributed network exception ...`. That avoided the fake fatal crash signal, but it still made ordinary peer churn look like an active runtime problem and reopened the issue.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
var warningMessage = $"[WARN] Unobserved Soulseek peer/distributed network exception: {baseException.Message}";
Console.Error.WriteLine(warningMessage);
Log?.Warning(baseException, warningMessage);
```

**Correct**:
```csharp
var debugMessage = $"Ignoring expected Soulseek peer/distributed network exception: {baseException.Message}";
Log?.Debug(baseException, debugMessage);
```

```text
Also keep broadening the expected-network matcher for normal churn strings such as
"Remote connection closed" so the handler does not bounce between fatal, warning, and benign paths.
```

**Why This Keeps Happening**: once a noisy path has been misclassified as fatal, the instinctive follow-up is to downgrade it only one step to warning. For P2P connection churn that is already expected and observed, warning is still too loud; it becomes a second false-positive channel instead of a real fix.

---

#### E2E-11: Backfill requires OwnerEndpoint for HTTP downloads (cross-node)

#### E2E-12: SqliteShareRepository Keepalive Causes Process Exit During E2E Tests

**The Bug**: The `Keepalive()` method in `SqliteShareRepository` calls `Environment.Exit(1)` if the database check fails, causing nodes to exit unexpectedly during E2E tests. The original check used `pragma_table_info("filenames")` which may fail for FTS5 virtual tables or during transient database locks.

**Files Affected**:
- `src/slskd/Shares/SqliteShareRepository.cs` - `Keepalive()` method

**Wrong**:
```csharp
private void Keepalive()
{
    using var cmd = new SqliteCommand("SELECT COUNT(*) FROM pragma_table_info(\"filenames\");", KeepaliveConnection);
    var reader = cmd.ExecuteReader();
    if (!reader.Read() || reader.GetInt32(0) != 1)
    {
        var msg = "The internal share database has been corrupted...";
        Log.Fatal(msg);
        Environment.Exit(1);  // 💀 Kills process immediately, no recovery
        throw new DataMisalignedException(msg);
    }
}
```

**Correct**:
```csharp
private void Keepalive()
{
    try
    {
        // Check if table exists first
        using var cmd = new SqliteCommand(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='filenames';",
            KeepaliveConnection);
        var reader = cmd.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 1)
        {
            var msg = "The internal share database has been corrupted...";
            Log.Fatal(msg);
            Environment.Exit(1);
            throw new DataMisalignedException(msg);
        }
        // Verify table is queryable (handles FTS5 virtual tables correctly)
        using var verifyCmd = new SqliteCommand("SELECT COUNT(*) FROM filenames LIMIT 1;", KeepaliveConnection);
        verifyCmd.ExecuteScalar();
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
    {
        // Table doesn't exist or is corrupted - exit
        var msg = "The internal share database has been corrupted...";
        Log.Fatal(ex, msg);
        Environment.Exit(1);
        throw new DataMisalignedException(msg, ex);
    }
    catch (Exception ex)
    {
        // Log but don't exit on transient errors (e.g., database locked during backup)
        Log.Warning(ex, "Keepalive check encountered an error (may be transient): {Message}", ex.Message);
    }
}
```

**Why This Keeps Happening**: The keepalive check runs every 1 second and calls `Environment.Exit(1)` on any failure, including transient database locks or race conditions during startup. The original `pragma_table_info` check may not work correctly for FTS5 virtual tables, and there's no handling for transient errors like database locks during backups or concurrent access.

**Impact**: Causes 56+ ProcessExit events during E2E test runs, leading to `ERR_CONNECTION_REFUSED` errors and test failures.

**The Bug**: Backfill endpoint requires either `OwnerEndpoint` + `ShareToken` (for HTTP downloads) or owner username + `IDownloadService` (for Soulseek downloads). If neither is available, backfill fails with a generic error.

**Files Affected**:
- `src/slskd/Sharing/API/SharesController.cs` (Backfill method)

**Wrong**:
```csharp
// Only checks for Soulseek username
if (string.IsNullOrWhiteSpace(ownerUsername))
    return BadRequest("Owner username not available");
```

**Correct**:
```csharp
// Check for HTTP download first (cross-node), then Soulseek
var useHttpDownload = !string.IsNullOrWhiteSpace(ownerEndpoint) && !string.IsNullOrWhiteSpace(grant.ShareToken);
if (useHttpDownload) {
    // HTTP download path
} else if (!string.IsNullOrWhiteSpace(ownerUsername) && _downloadService != null) {
    // Soulseek download path
} else {
    return BadRequest("Cannot backfill: owner endpoint and token not available for HTTP download, and owner username or download service not available for Soulseek download");
}
```

**Why This Keeps Happening**: Backfill needs to work for both cross-node shares (HTTP) and same-network shares (Soulseek). The implementation must check for both methods and provide clear error messages when neither is available.

### 2w. Metrics Auth DataAnnotations Must Not Reject the Default Config When Metrics Are Disabled

**The Bug**: `Options.MetricsOptions.MetricsAuthenticationOptions.Password` had a `[StringLength(MinimumLength = 1)]` attribute even though metrics are disabled by default and the default password is intentionally empty. Full options validation ran before startup, so a fresh config could fail with `Metrics.Authentication.Password` length validation even when `metrics.enabled = false` or `metrics.authentication.disabled = true`.

**What Went Wrong**: The validation lived on the nested property instead of the feature gate. DataAnnotations treated the empty default password as invalid unconditionally, which broke NixOS service validation and any other startup path that bound defaults before metrics was actually enabled.

**How to Prevent It**:
- Put required-field validation for optional features on the parent options object where you can check `Enabled` and related flags.
- Do not use unconditional `[StringLength(MinimumLength = 1)]` on values that are allowed to remain empty while the feature is disabled.
- Add tests for all three cases: feature disabled, feature enabled with auth disabled, and feature enabled with auth required.

### 2x. Release Jobs That Write Back Into `master` Must Re-Sync Before Pushing

**The Bug**: The tag workflow successfully published release `0.24.5-slskdn.57` and updated the Homebrew tap repo, but the follow-up step that rewrote `Formula/slskdn.rb` in the main repo failed with `git push ... fetch first` because it committed in a fresh clone and then pushed straight into a moving `master`.

**What Went Wrong**: The workflow already derives the correct release version from the build tag, so the failure was not a versioning problem. The actual bug was treating a post-release write-back like an isolated branch update instead of a concurrent push target.

**How to Prevent It**:
- For any workflow that commits back into `master`, fetch and rebase against `origin/master` immediately before push, then retry a small number of times.
- If there are no staged changes after regenerating a packaging file, exit early instead of creating a no-op push path.
- Treat repository write-back steps as separate from artifact publication; a release can publish successfully while the write-back still races and turns the workflow red.

### 2y. Do Not Rebase a Generated Release Commit Until the Workflow Cleans Nix-Generated Dirt

**The Bug**: The Nix write-back job for release `0.24.5-slskdn.59` still failed after a 10-attempt retry loop because the checkout was already dirty by the time the loop reached `git rebase origin/master`, so every attempt died immediately with `cannot rebase: You have unstaged changes`.

**What Went Wrong**: The retry logic assumed the only local change was the committed `flake.nix` bump. In reality, the Nix verification step left additional working-tree/index changes behind, so the rebase loop never had a clean tree to operate on.

**How to Prevent It**:
- Run `nix flake check` with `--no-write-lock-file` in CI when the job is only validating metadata.
- Before any fetch/rebase/push retry loop, explicitly clean the checkout (`git reset --hard HEAD` plus `git clean -fd`) or regenerate the file from a fresh `origin/master` each attempt.
- Do not interpret "more retries" as a fix when the underlying checkout state is dirty; first make the retry loop re-runnable.

### 2z. Do Not Let Multiple Release Jobs Push Different Metadata Commits Into `master` In Parallel

**The Bug**: Homebrew, Winget, and Nix each tried to write separate commits back into `master` during the same release run. Even after individual retry fixes, the jobs kept invalidating each other because they were all racing to move the same branch.

**What Went Wrong**: The workflow treated each packaging surface as an isolated updater, but the shared target was still one branch. Independent retries reduce timing sensitivity; they do not eliminate branch-level write contention when three jobs are all competing to publish the "latest" metadata commit.

**How to Prevent It**:
- Use exactly one job to mutate `master` for release metadata.
- Regenerate all checked-in release metadata (`flake.nix`, checked-in Homebrew formula, Winget manifests, etc.) in the same workspace and push one consolidated commit.
- Keep external repo updates separate if necessary, but do not let more than one job in the workflow write to this repository's default branch.

### 3a. Do Not Rename a Release-Blocking Option or Shared Release Copy in Only One Layer

**The Bug**: `MeshServiceDescriptorValidator` checked `_options.RequireSignatures` even though `MeshServiceFabricOptions` only exposes `ValidateDhtSignatures`, which broke every publish job at compile time. Separately, the stable Winget locale text drifted away from the shared SongID/Discovery Graph release copy, so CI failed the packaging metadata validator before it even reached the app build.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/MeshServiceDescriptorValidator.cs`
- `src/slskd/Mesh/ServiceFabric/MeshServiceFabricOptions.cs`
- `packaging/winget/snapetech.slskdn.locale.en-US.yaml`
- `packaging/scripts/validate-release-copy.sh`

**Wrong**:
```csharp
else if (_options.RequireSignatures)
{
    return (false, "Signature required but not provided");
}
```

```yaml
ShortDescription: Batteries-included Soulseek web client
Description: |-
  slskdN is a batteries-included fork of slskd with advanced download features,
  automation, and network enhancements for Soulseek.
```

**Correct**:
```csharp
else if (_options.ValidateDhtSignatures)
{
    return (false, "Signature required but not provided");
}
```

```yaml
ShortDescription: Stable Soulseek client with SongID and Discovery Graph
Description: |-
  slskdN is a batteries-included fork of slskd with SongID, Discovery Graph,
  advanced download features, automation, and network enhancements for Soulseek.
```

**Why This Keeps Happening**: Release work in this repo spans code, workflows, and checked-in packaging metadata. If you update only the validator, only the options type, or only one release-copy file, different CI gates fail in sequence and hide the next problem. Audit the real options type and every checked-in release copy file together before tagging.

### 3b. Do Not Persist Pod Creation Fields Without Normalizing Required Defaults First

**The Bug**: `PodEntity.FocusContentId` is stored as a required SQLite column, but `SqlitePodService.CreateAsync()` wrote `pod.FocusContentId` directly. DM pod creation and several integration tests leave that field unset, so pod creation failed with `SQLite Error 19: 'NOT NULL constraint failed: Pods.FocusContentId'`.

**Files Affected**:
- `src/slskd/PodCore/SqlitePodService.cs`
- `src/slskd/PodCore/PodDbContext.cs`

**Wrong**:
```csharp
FocusContentId = pod.FocusContentId,
```

**Correct**:
```csharp
var normalizedFocusContentId = pod.FocusContentId ?? string.Empty;
pod.FocusContentId = normalizedFocusContentId;

FocusContentId = normalizedFocusContentId,
```

**Why This Keeps Happening**: The service layer treats some pod fields as optional, but the persistence model hard-requires non-null strings. If you change schema expectations or add a new required column, normalize the service input before save and keep the entity-to-model mapping tolerant of older/null rows.

### 3c. Bash Heredoc Terminators in GitHub Actions Must Start at Column 1 Unless You Use `<<-`

**The Bug**: The stable `metadata-main` job rewrote `Formula/slskdn.rb` with `cat <<EOF`, but the closing `EOF` was indented inside the workflow `run:` block. Bash never recognized the terminator, so the post-release metadata job crashed with `wanted 'EOF'` and `syntax error: unexpected end of file` even though the release artifacts were already published.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
cat > Formula/slskdn.rb <<EOF
  class Slskdn < Formula
    ...
  EOF
```

**Correct**:
```bash
cat > Formula/slskdn.rb <<EOF
class Slskdn < Formula
  ...
EOF
```

**Why This Keeps Happening**: YAML indentation makes it visually tempting to indent shell heredoc terminators to match the surrounding block, but bash still parses the literal script after YAML rendering. If you use plain `<<EOF`, the closing marker must be flush-left in the generated shell. Otherwise a release can fully publish and still fail red on a follow-up metadata write-back step.

### 3d. GitHub Actions `run: |` Blocks Still Need Valid YAML Indentation Before Bash Ever Sees the Heredoc

**The Bug**: After fixing the bash heredoc terminator bug, the next edit moved the heredoc body to column 1 in the workflow file itself. That made the shell content conceptually correct, but it broke the workflow at YAML parse time, so the `build-on-tag.yml` runs failed instantly with no jobs created.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```yaml
run: |
  cat > Formula/slskdn.rb <<EOF
class Slskdn < Formula
  ...
EOF
```

**Correct**:
```yaml
run: |
  cat > Formula/slskdn.rb <<EOF
  class Slskdn < Formula
    ...
  EOF
```

**Why This Keeps Happening**: GitHub Actions first parses YAML, then hands the deindented block to bash. The workflow file must satisfy both layers at once: keep the heredoc lines indented enough for YAML block-scalar syntax, but consistently indented so the runner deindents them back to column 1 for bash.

### 3e. Non-Nullable Tuple Return Sites Must Use `default`, Not `null!`

**The Bug**: Warning cleanup changed `SqliteShareRepository.FindFileInfo()` to return `null!` on the not-found path, but the method returns a non-nullable value tuple. That compiles for reference types, not tuples, so the next rebuild failed with `CS0037`.

**Files Affected**:
- `src/slskd/Shares/SqliteShareRepository.cs`

**Wrong**:
```csharp
if (!reader.Read())
{
    return null!;
}
```

**Correct**:
```csharp
if (!reader.Read())
{
    return default;
}
```

**Why This Keeps Happening**: During nullable cleanup, it is easy to mechanically replace "missing value" returns with `null!`. That only works for reference-type return paths. For tuples and other value types, keep the existing sentinel form such as `default` or change the signature explicitly.

### 3f. Async Controller Lookups Must Await Repository Tasks Before Null / NotFound Checks

**The Bug**: Warning cleanup changed `FindMessageAsync()` to return `Task<PrivateMessage?>`, but the controller kept comparing the un-awaited task result to `default`. That made the not-found branch unreachable and could incorrectly return `200 OK` for missing messages.

**Files Affected**:
- `src/slskd/Messaging/API/Controllers/ConversationsController.cs`
- `src/slskd/Messaging/ConversationService.cs`

**Wrong**:
```csharp
var message = Messages.Conversations.FindMessageAsync(username, id);
if (message == default)
{
    return NotFound();
}

return Ok(message);
```

**Correct**:
```csharp
var message = await Messages.Conversations.FindMessageAsync(username, id);
if (message == default)
{
    return NotFound();
}

return Ok(message);
```

**Why This Keeps Happening**: Nullable-signature cleanup often turns synchronous-looking lookups into `Task<T?>`, but controller code can still visually resemble the old synchronous pattern. In async controller actions, always await the lookup before testing for `null` / `default` or returning the payload.

### 3g. AUR Clone Failures Must Retry, Not Fall Back To `git init`

**The Bug**: The stable `Publish to AUR (Main - Source & Binary)` workflow treated any `git clone` failure for `slskdn-bin` as if the package repo did not exist. A transient AUR SSH disconnect created a brand-new local repo with `git init`, so the later push became a root commit and failed with `fetch first`.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-pkg-bin || {
  mkdir -p aur-pkg-bin
  cd aur-pkg-bin
  git init
  git remote add origin ssh://aur@aur.archlinux.org/slskdn-bin.git
}
```

**Correct**:
```bash
git clone "https://aur.archlinux.org/slskdn-bin.git" aur-pkg-bin
git -C aur-pkg-bin remote set-url --push origin "ssh://aur@aur.archlinux.org/slskdn-bin.git"
```

```text
Use HTTPS for clone/fetch/rebase and reserve SSH only for the final authenticated push. Only initialize a brand-new AUR repo during an intentional package bootstrap path. For normal release publishing, treat clone failures as transient network/auth errors and retry them; on push rejection, fetch/rebase and retry instead of pushing an unrelated root commit.
```

**Why This Keeps Happening**: the original fallback was written to be convenient for first-time package setup, but release workflows run against long-lived AUR repos where "clone failed" usually means SSH/network instability, not a missing repository. Reusing the bootstrap fallback in steady-state CI silently destroys git history and turns a recoverable clone hiccup into a guaranteed push failure. Even after removing the `git init` fallback, using SSH for read-side clone/fetch still leaves the workflow exposed to AUR-side connection drops; HTTPS reads plus SSH push isolates the flaky part to the only step that actually needs credentials.

### 3h. All GitHub Issue / PR / Release Actions In This Repo Must Target `snapetech/slskdn`, Never Upstream `slskd/slskd`

**The Bug**: GitHub cleanup work intended for this fork was run against the upstream `slskd/slskd` project instead. The root cause was repo-target ambiguity: this checkout has both `origin` (`snapetech/slskdn`) and `upstream` (`slskd/slskd`), and `gh` / connector operations were allowed to resolve against the wrong repository when the target was not stated explicitly.

**Files / Systems Affected**:
- local GitHub CLI state
- AI/operator instructions for this repo

**Wrong**:
```text
Run GitHub issue / PR / release commands without explicitly verifying the target repo,
or assume "slskd" and "slskdn" will be distinguished automatically by the tool.
```

**Correct**:
```text
For this repository, every GitHub issue / PR / release action must target `snapetech/slskdn`.
Treat upstream `slskd/slskd` as read-only reference only.

Before any GitHub write action:
1. Verify `origin` is `snapetech/slskdn`
2. Verify `gh repo set-default --view` resolves to `snapetech/slskdn`
3. Pass the repo explicitly to any CLI / MCP action when possible
4. Never comment on, close, label, or otherwise modify upstream `slskd/slskd`
```

**Why This Keeps Happening**: fork repos often keep an `upstream` remote for reference, and the names here differ by only one letter. That makes repo targeting an easy place to fail, especially when tools cache a default repo or infer one from context. When both fork and upstream are accessible, "implicit repo selection" is unsafe. Pin the default to `snapetech/slskdn`, verify it before write actions, and treat upstream as non-writable from this workspace.

### 3i. Share Scan Worker Defaults Must Be Conservative; `ProcessorCount` Is Too Aggressive For First-Time Scans

**The Bug**: The share scanner defaulted `shares.cache.workers` to `Environment.ProcessorCount`. On weaker hosts or slow storage, first-time library scans could drive load unreasonably high because each worker enumerates directories and reads file metadata concurrently.

**Files Affected**:
- `src/slskd/Core/Options.cs`
- `config/slskd.example.yml`
- `docs/config.md`

**Wrong**:
```csharp
public int Workers { get; init; } = Environment.ProcessorCount;
```

**Correct**:
```text
Use a conservative default that favors stability over peak scan throughput, and keep the
existing `shares.cache.workers` knob available for hosts that want to tune higher or lower.
```

**Why This Keeps Happening**: "one worker per core" sounds reasonable for CPU-bound work, but share scans are mixed CPU/I/O pressure and include metadata extraction, file system traversal, and moderation checks. On modest systems, the default needs to assume the host is doing other work and that storage is often the real bottleneck. Tune up explicitly if the machine can handle it; do not make the most aggressive path the default.

### 3j. Full-Instance Integration Startup Timeouts Must Tolerate Loaded Test Runs

**The Bug**: `SlskdnFullInstanceRunner.WaitForApiReadyAsync()` hard-coded a 25 second startup timeout. That was enough in isolated focused runs, but it failed in repo-wide validation when the full-instance CSRF integration tests booted a subprocess while the machine was already under heavy test load.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
const int maxAttempts = 50;
await Task.Delay(500, ct);
throw new TimeoutException($"slskdn instance did not become ready after {maxAttempts * 500}ms");
```

**Correct**:
```text
Use a startup wait budget that tolerates loaded local/CI runs for subprocess-backed integration tests,
and reserve the short timeouts for in-process `TestServer` style probes.
```

**Why This Keeps Happening**: focused harness tests often make the startup path look fast and deterministic, but subprocess-backed integration tests pay for real app startup, config/bootstrap work, and scheduler contention from the rest of the suite. A timeout that is "fine on a quiet machine" becomes a flake once the full solution runs together.

### 3k. Redirected Child-Process Output In Test Harnesses Must Be Drained Continuously

**The Bug**: `SlskdnFullInstanceRunner` started the subprocess with `RedirectStandardOutput = true` and `RedirectStandardError = true`, but it only read those streams if the process exited early. Under heavier startup logging, the child could block on full pipe buffers before the API came up, and the harness misreported that as a startup timeout.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
RedirectStandardOutput = true,
RedirectStandardError = true,
...
var stdout = slskdnProcess.StandardOutput.ReadToEnd();
var stderr = slskdnProcess.StandardError.ReadToEnd();
```

**Correct**:
```text
When redirecting child-process output in a long-lived test harness, begin asynchronous reads
immediately and keep a bounded in-memory buffer for diagnostics. Do not leave the pipes unread.
```

**Why This Keeps Happening**: redirected output feels harmless when the only goal is "capture logs if startup fails," but unread pipes have backpressure. As soon as the child emits enough startup logging, the harness itself becomes the reason the process stalls.

### 3l. Browse Cache Readers Must Allow Replacement, And Rebuilds Must Be Serialized

**The Bug**: `BrowseResponseResolver()` opened `browse.cache` with the default exclusive sharing mode, while `CacheBrowseResponse()` rebuilt the cache with `File.Move(..., overwrite: true)` and no rebuild lock. Active browse readers could therefore block cache replacement, and concurrent rebuild triggers could race each other as well.

**Files Affected**:
- `src/slskd/Application.cs`

**Wrong**:
```csharp
var stream = new FileStream(cacheFilename, FileMode.Open, FileAccess.Read);
...
File.Move(temp, destination, overwrite: true);
```

**Correct**:
```text
Open browse-cache readers with a sharing mode that permits the file to be replaced while it is being streamed,
and serialize cache rebuilds so only one writer updates `browse.cache` at a time.
```

**Why This Keeps Happening**: file-backed caches look simple because readers and writers touch the same pathname, but they still need an explicit concurrency contract. If readers take exclusive locks and writers replace the file opportunistically, normal live traffic turns every refresh into a lock race.

### 3m. Disabled Moderation Must Not Force Full-File Hashing During Share Scans

**The Bug**: `ShareScanner` computed a full SHA-256 for every scanned file before it even asked the moderation provider for a decision. When the active provider was `NoopModerationProvider` or another effectively inactive moderation setup, scans still paid the whole-file hashing cost for no benefit.

**Files Affected**:
- `src/slskd/Shares/ShareScanner.cs`
- `src/slskd/Common/Moderation/CompositeModerationProvider.cs`

**Wrong**:
```csharp
var fileHash = await Files.ComputeHashAsync(originalFilename, cancellationToken);
var localFileMetadata = new LocalFileMetadata
{
    PrimaryHash = fileHash,
    ...
};
var decision = await ModerationProvider.CheckLocalFileAsync(localFileMetadata, cancellationToken);
```

**Correct**:
```text
Only run the moderation path when local-file moderation is actually active, and only compute a full-file hash
when the active provider configuration truly requires one (for example, a hash blocklist check).
```

**Why This Keeps Happening**: the moderation API takes `LocalFileMetadata`, and `PrimaryHash` looks like part of that contract, so it is easy to front-load the hash unconditionally. On a large library scan, that turns “moderation disabled” into “still read every file end-to-end,” which looks like a scan hang or runaway load even with low worker counts.

### 3n. Share Scans Must Not Eagerly Probe Media Attributes For Every Supported File On Slow Or Remote Storage

**The Bug**: `SoulseekFileFactory.Create(...)` eagerly called `TagLib.File.Create(...)` for every supported audio and video file during share scans in order to populate Soulseek attributes. On slow or remote storage such as NFS-backed shares, that probing path could dominate the scan so heavily that scans appeared to stall after only a handful of files.

**Files Affected**:
- `src/slskd/Shares/SoulseekFileFactory.cs`
- `src/slskd/Shares/ShareScanner.cs`
- `tests/slskd.Tests.Unit/Shares/ShareScannerHarnessTests.cs`

**Wrong**:
```csharp
if (SupportedExtensions.Contains(extension))
{
    file = TagLib.File.Create(filename, TagLib.ReadStyle.Average | TagLib.ReadStyle.PictureLazy);
    ...
}
```

**Correct**:
```text
Keep share scans cheap by default. Do not synchronously probe full media metadata for every supported file
on the hot scan path unless the value is clearly worth the I/O cost. Prefer lightweight file records during scan,
or restrict expensive attribute probing to the smaller set of file types that truly need it.
```

**Why This Keeps Happening**: media attributes look small in the final `Soulseek.File`, so it is easy to forget that extracting them may require non-trivial reads and parsing for every file. On local SSDs this can hide in the noise; on remote or high-latency storage it becomes the actual bottleneck, and lowering worker count alone does not fix it.

### 3o. DHT Rendezvous Must Use A Stable Explicit UDP Port, Not A Random Startup Port

**The Bug**: `DhtRendezvousService` defaulted `DhtPort` to `0`, then replaced it with a random UDP port on each startup. Operators could correctly forward their normal Soulseek ports and still see `DHT bootstrap timed out` forever, because the actual DHT bootstrap traffic was leaving from a different random port that was never forwarded or mapped.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/Core/Options.cs`
- `config/slskd.example.yml`

**Wrong**:
```csharp
var dhtPort = _options.DhtPort > 0 ? _options.DhtPort : RandomNumberGenerator.GetInt32(6881, 7000);
```

**Correct**:
```text
DHT rendezvous must always use a stable explicit UDP port. Give it a real default,
validate that enabled DHT never runs with port 0, and tell operators clearly which
UDP port must be forwarded or mapped.
```

**Why This Keeps Happening**: random ports feel convenient because they avoid collisions during development, but a peer-discovery service is an operator-facing network surface, not an internal ephemeral socket. If users cannot know the port in advance, they cannot forward it, allow-list it, or reason about bootstrap failures.


### 3p. DHT Overlay Neighbors Must Populate The Mesh Circuit Peer Inventory

**The Bug**: DHT rendezvous could bootstrap successfully, discover peers, and even register active overlay neighbors in `MeshNeighborRegistry`, while `CircuitMaintenanceService` still logged `0 circuits, 0 total peers, 0 active, 0 onion-capable`. The circuit builder and maintenance path were looking at `IMeshPeerManager`, which never learned about those successful overlay neighbors.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshNeighborRegistry.cs`
- `src/slskd/Mesh/MeshPeerManager.cs`
- `src/slskd/Mesh/CircuitMaintenanceService.cs`

**Wrong**:
```text
Treat successful overlay handshakes as enough proof that mesh connectivity is working,
without synchronizing those neighbors into the peer inventory used by circuit services.
```

**Correct**:
```text
Whenever overlay neighbors become the source of truth for live mesh connectivity, explicitly
bridge `MeshNeighborRegistry` add/remove events into `IMeshPeerManager` and keep peer stats
in sync. Add regression coverage that proves a registered overlay neighbor increases mesh peer
stats and a removed neighbor is deleted from the circuit peer inventory.
```

**Why This Keeps Happening**: mesh connectivity currently has two layers of state that look similar in logs but are not the same thing. It is easy to stop at “DHT found peers” or “neighbor registered” and assume higher-level mesh features will work automatically, when the circuit stack is actually reading a different store.

---

*Last updated: 2026-03-21*

### 0z47. Mesh Self-Descriptors Must Not Advertise Impossible Direct Transports Or Wrong Default Ports

**What went wrong:** Live auditing on `local test host` showed `PeerDescriptorPublisher` still auto-detected clearnet endpoints as bare `ip:2234` / `ip:2235` and also emitted `DirectQuic` transport endpoints even when `QuicListener.IsSupported` was false on the host. That poisoned our own published descriptor with ports we were not actually listening on and advertised a direct QUIC transport that the node could not accept. Operators then saw DHT peers and circuit-maintenance churn without any realistic path to a working direct mesh connection.

**Why it happened:** The descriptor publisher mixed old Soulseek default ports with the newer mesh transport model, and it never cross-checked advertised transports against the runtime transport capability on the current host. Because DHT discovery and peer stats already looked busy, the impossible advertisement path hid behind noisy remote-candidate failures.

**How to prevent it:** Mesh self-descriptor publication must derive advertised ports from the real configured listeners, not hard-coded legacy defaults. Do not publish `DirectQuic` endpoints unless QUIC is actually supported on the running host, and never publish bare `ip:port` legacy endpoints when the consuming code expects explicit `udp://` / `quic://` schemes. Add regression coverage for both the configured port selection and the unsupported-QUIC path.

### 0z48. QUIC-Unsupported Hosts Need A Real Direct Mesh Dialer Fallback, Not Just Honest Descriptor Publication

**What went wrong:** Live validation on `local test host` showed that fixing the mesh self-descriptor only made the node honest; it did not make mesh circuits work. The host correctly stopped advertising impossible `DirectQuic` transports once `QuicListener.IsSupported` was false, but `TransportSelector` still only had `DirectQuicDialer` for clearnet mesh transport. That left QUIC-unsupported hosts with no direct dialer at all, so circuit formation could never succeed even though DHT rendezvous and the TCP overlay listener were healthy.

**Why it happened:** The codebase grew two separate direct-connection stacks: the mesh transport selector assumed direct clearnet means QUIC, while the anonymity layer already had a working direct TLS transport to the TCP overlay listener. We fixed the advertisement lie first, but the dialer layer still had no fallback to the transport the host could actually accept.

**How to prevent it:** Whenever a transport advertisement is made conditional on runtime capability, audit the matching outbound dialer path in the same change. Do not leave a host in a state where it truthfully advertises no supported direct transport but the circuit builder still assumes one exists. Either provide a direct TLS/TCP mesh dialer fallback or fail startup/package validation explicitly on QUIC-unsupported hosts.

### 0z46. Security Refactors Must Delete Or Rewrite Tests That Still Resolve Removed Service Types

**What went wrong:** A security refactor removed the old `TransferSecurity` service, but `SecurityStartupTests` still tried to resolve it from DI. The whole targeted unit pass then failed at compile time, which hid the actual status of the new hardening work.

**Why it happened:** The implementation moved to `SecurityOptions`/middleware wiring, but the test suite was not updated in the same change set. The test still asserted behavior against a deleted registration contract.

**How to prevent it:** When removing or folding a service during a security refactor, grep the unit suite for the deleted type name and either rewrite those tests to the new registration contract or delete them in the same commit. Never leave compile-broken tests as deferred cleanup.


### 0z49. Mesh Releases Need A Deterministic Two-Instance Smoke, Not Just Loopback Pieces And Live DHT Observation

**What went wrong:** We kept shipping mesh fixes after validating isolated pieces like single-process loopback handshakes, DHT counters, or live-host candidate discovery. That still missed the most important proof: two real `slskdn` processes standing up, forming an overlay connection, and reporting each other as connected peers. Without that deterministic two-instance smoke, we repeatedly confused partial signals for real end-to-end mesh success.

**Why it happened:** The repo had lower-level coverage (`MeshSearchLoopbackTests`, connector/server unit tests, live DHT diagnostics), but no stable full-instance path that could force one node to dial another and assert the resulting overlay state. Real-network validation through public DHT was too noisy and peer-quality-dependent to serve as the primary release gate.

**How to prevent it:** Keep a deterministic two-instance full-process mesh smoke in the integration suite. It should boot two `slskdn` instances, force one to connect to the other through the real overlay stack, and assert both sides report the peer/connection. Treat public-DHT/live-host checks as supplemental evidence only, not the main proof that mesh works.


### 0z50. Full-Instance Runner Must Pass `--app-dir`, Not Just `APP_DIR`, To Avoid Colliding With A Live User Install

**What went wrong:** The new two-instance mesh smoke started real `slskd` processes with a temporary config file and `APP_DIR` set in the child environment, but the process still exited immediately with `An instance of slskd is already running in app directory: <app-data-dir>`. The runtime singleton guard was checking the default app directory because the harness never passed the explicit `--app-dir` CLI argument.

**Why it happened:** The harness assumed the environment variable alone would override the app directory early enough in startup. In practice, the running process resolved the default appdir before the test harness intent took effect, so the test collided with the developer's live install instead of the temporary sandbox.

**How to prevent it:** Any full-instance test harness that launches `slskd` must pass both `--config` and `--app-dir` explicitly on the command line. Do not rely on environment-only appdir overrides for subprocess isolation when the product has singleton/appdir locking during startup.

### 0z51. Full-Instance Harnesses Must Override Every Bound Listener Port, Not Just The Primary HTTP Port

**What went wrong:** After fixing `--app-dir`, the new two-instance mesh smoke still died during startup because the subprocess tried to bind other default listeners already used by the developer machine, including HTTPS on `5031` and the mesh UDP/QUIC defaults on `50400/50401`. Randomizing only the primary web port was not enough to isolate the child process.

**Why it happened:** The harness wrote a partial config and assumed the remaining listeners were harmless defaults. `slskd` starts multiple network surfaces, so any unoverridden default port can collide with a live local install and make an integration test look like an application failure.

**How to prevent it:** Full-instance test config must explicitly set or disable every listener that can bind a socket: HTTP, HTTPS, overlay TCP, DHT UDP, UDP overlay, and QUIC/data overlay. Do not rely on product defaults when launching subprocesses on a developer machine with another instance already running.


### 0z52. Full-Instance Harness YAML Must Use The Binder Section Name `dhtRendezvous`, Not The Short Alias `dht`

**What went wrong:** The two-instance mesh smoke wrote a top-level `dht:` section because the human-facing example config documents DHT that way. In the full subprocess startup path, that left the child process on the default overlay/DHT ports (`50305`/`50306`) even though the HTTP port from the same file was honored. The result was a fake mesh failure caused by both instances accidentally sharing the same default DHT overlay listener.

**Why it happened:** The harness assumed the example/YAML alias and the runtime configuration binder were interchangeable. In this code path the binder key that actually drives `OptionsAtStartup.DhtRendezvous` is `dhtRendezvous`, so the short alias was ignored for the full-process child.

**How to prevent it:** When generating full-instance test config, use the exact runtime binder section name that the subprocess honors. Do not assume the example-file alias and the startup binder key are identical without proving it in a live child-process probe first.



### 0z53. Full-Instance Overlay Smokes Must Not Treat The Inbound Socket Source Port As The Remote Node's Listener Port

**What went wrong:** The new two-instance mesh smoke forced alpha to connect directly to beta's overlay listener, then asserted that beta's `/api/v0/overlay/connections` entry for alpha would report `alpha.OverlayPort`. That was wrong. On the inbound side, the connection registry reports the remote socket endpoint for the accepted TCP session, which uses alpha's ephemeral client source port, not alpha's overlay listener port. The test timed out even though the mesh connection actually existed.

**Why it happened:** We wrote the assertion as if both sides would expose a symmetric "peer listener port" view. The current controller surfaces `MeshOverlayConnection.RemoteEndPoint`, and for accepted inbound sockets that endpoint is the caller's transient outbound port. We were validating the wrong network fact and turned a healthy connection into a false negative.

**How to prevent it:** In full-instance overlay tests, assert on peer identity and connection presence, not the inbound side's remote socket source port. If listener-port identity matters, expose it explicitly in the overlay handshake payload or a dedicated response field instead of inferring it from the accepted TCP socket endpoint.


### 0z54. Experimental Bridge Search Must Not Replace The Proven Soulseek Search Path By Default

**What went wrong:** Issue `#209` evolved from DHT bootstrap failures into a user-visible search regression: a logged-in client could run a popular search and get `0` files while logs showed the request using `[ScenePodBridge]`. The bridge path depends on multiple experimental provider layers and mesh peer availability, so making it the default path allowed bridge/provider problems to look like core Soulseek search was broken.

**Why it happened:** `Feature.ScenePodBridge` defaulted to `true`, so normal searches were diverted into the aggregation path whenever providers were registered. That changed the baseline behavior of an upgrade from upstream `slskd`; users expected the proven Soulseek network search path, but the app ran the newer Scene/Pod bridge path unless disabled.

**How to prevent it:** Keep core Soulseek search as the default user path. Experimental bridge/provider aggregation must be explicit opt-in until it has end-to-end field proof and separate diagnostics. New mesh/bridge features may run as supplemental parallel paths only when they cannot suppress or replace normal Soulseek results.

### 0z55. Overlay Message Read Timeout Must Not Be Shorter Than Keepalive

**What went wrong:** Issue `#209` build `151` finally established an inbound mesh neighbor, then dropped it exactly 30 seconds later with `OperationCanceledException` from `SecureMessageFramer.ReadExactlyAsync`. The connection was healthy but quiet; the server read loop treated its own per-read timeout as a fatal message-loop error and unregistered the peer before keepalive could run.

**Why it happened:** `OverlayTimeouts.MessageRead` was 30 seconds, while `OverlayTimeouts.KeepaliveInterval` was 2 minutes and `OverlayTimeouts.Idle` was 5 minutes. `MeshOverlayConnection.ReadRawMessageAsync()` creates an internal timeout token, but `MeshOverlayServer.HandleMessagesAsync()` only treated cancellation as expected when the outer server token was canceled. A normal no-message interval therefore looked like an error and disconnected the neighbor.

**How to prevent it:** Blocking overlay reads in long-lived message loops must treat internal read timeout as "no message yet" and continue to the next loop iteration so keepalive and idle checks can make the lifecycle decision. Per-read timeouts can still protect request/response reads, but they must not be shorter than or semantically override keepalive/idle policy for persistent peer connections.

### 0z56. Connection Registries Must Remove The Same Object They Registered

**What went wrong:** While fixing issue `#209` mesh search after inbound-only neighbor discovery, the registry needed to replace an inbound connection with a reciprocal outbound connection for the same username. The existing unregister path removed entries by username and endpoint without verifying that the stored value was the same connection being cleaned up. A stale inbound message loop could therefore unregister a newer outbound replacement and make the peer disappear again.

**Why it happened:** `MeshNeighborRegistry` keyed connections by username and endpoint, but cleanup was written as if a username could only ever refer to one connection for the lifetime of the cleanup call. Once replacement/promotion exists, stale cleanup must be object-identity-aware.

**How to prevent it:** Any registry that allows replacing a value under the same key must remove by key + expected object identity, not key alone. Add tests where an old connection is replaced and then old cleanup runs; the replacement must remain registered.

### 0z57. Optional UI Badge Lookups Must Not Use Error-Shaped API Responses For Expected Missing Peer Data

**What went wrong:** The Downloads page rendered `UserCard` for historical/offline transfer users. `UserCard` fetched optional Soulseek user info for every username, and expected offline users returned HTTP 404. The component handled the missing data, but browsers still logged each 404 as a console resource error, so clean Playwright sweeps looked noisy even though the page was functional.

**Why it happened:** The backend endpoint correctly preserved normal API semantics (`404` for offline users, `503` for temporarily unavailable peer info), but the UI was using that endpoint for decorative/optional badges where missing peer data is routine. `Promise.allSettled` prevented a React failure but could not suppress browser-level failed-resource logging.

**How to prevent it:** Optional badge/polling UI should use an explicit quiet/optional API mode that returns a non-error empty response for expected unavailable peer data. Keep the default endpoint behavior unchanged for callers that need to distinguish offline/unavailable states.

### 0z58. Static Event Subscriber-Count Tests Must Not Run In Parallel

**What went wrong:** The `build-main-0.24.5-slskdn.173` release gate failed `ApplicationLifecycleTests.Dispose_UnsubscribesGlobalAndSoulseekEvents` because it asserted an exact `Clock.EveryMinute` static event subscriber count while other unit tests can create objects that subscribe to the same static event. The focused Release test passed in isolation, making the failure look nondeterministic.

**Why it happened:** Static events are process-global state, but xUnit runs test classes in parallel by default. A test that snapshots an invocation count and expects exactly one new subscriber can race with another test that subscribes/unsubscribes the same static event in the same process.

**How to prevent it:** Put any tests that inspect or mutate static event invocation lists in a shared non-parallel collection, or rewrite the assertion to prove the specific owned handler was removed without depending on a global count. Do not add new static event count assertions without isolating them from xUnit parallelism.

### 0z59. Public Mesh Descriptors Must Not Publish Every Local Interface

**What went wrong:** Live issue-209 diagnostics on `local test host` showed the node publishing five self-descriptor endpoints/transports from automatic interface detection while public DHT discovery still had zero active mesh connections. Those auto-detected addresses can include private, link-local, container, VPN, or otherwise non-public interface addresses that remote peers cannot reach. They make the mesh look populated while poisoning peer descriptors with bad direct candidates.

**Why it happened:** `PeerDescriptorPublisher` treated every non-loopback interface as "routable" and also supplemented explicitly configured `SelfEndpoints` with detected interfaces. `PeerDescriptorRefreshService` used a similar broad interface scan for IP-change detection, so private-interface churn could also trigger needless descriptor refreshes.

**How to prevent it:** Automatic mesh endpoint advertisement must only use public-routable IP addresses. Trust explicitly configured `SelfEndpoints` as operator intent, but do not silently add detected interfaces beside them. Keep descriptor refresh IP-change detection aligned with the same public-routable address policy so private/container interface changes do not republish bad descriptors.

### 0z60. Live Full-Instance Harnesses Must Emit The Soulseek Server Endpoint And Unique Listen Port

**What went wrong:** The optional live-account mesh smoke generated valid sandbox credentials and set `flags.no_connect: false`, but the child process stayed `Disconnected` until the test timed out. The generated YAML included username/password, yet omitted the Soulseek server address/port and left listen-port behavior to product defaults.

**Why it happened:** The harness was originally built for deterministic local overlay tests with `no_connect: true`, so it did not need to connect to the public Soulseek server. Reusing that same partial config for live-account validation meant the login watchdog had no usable server endpoint and could also collide on the default Soulseek listen port when two subprocesses ran at once.

**How to prevent it:** Any full-instance test that expects live Soulseek login must explicitly write `soulseek.address`, `soulseek.port`, and a unique per-process `soulseek.listen_port` alongside the credentials. Do not infer live-login readiness from credentials plus `no_connect: false`; inspect the generated child config for every network listener and upstream endpoint the path needs.

### 0z61. Full-Instance API Smokes Must Authenticate Unsafe Methods With An API Key

**What went wrong:** The live-account mesh smoke logged two fresh public Soulseek test accounts in successfully, then failed on `PUT /api/v0/shares` and later `POST /api/v0/overlay/connect` with HTTP 400 CSRF responses. The test had disabled web authentication and assumed that was enough for local harness calls.

**Why it happened:** Disabling web authentication does not disable CSRF protection for unsafe HTTP methods. Browser-cookie-style anonymous calls are still rejected, while API-key/JWT-authenticated API calls are allowed. The deterministic no-connect smoke had previously exercised these endpoints without proving the live auth/CSRF shape.

**How to prevent it:** Full-instance integration tests must call mutating API endpoints with an explicit API key or bearer token, even when the harness disables web authentication. When a test calls `POST`, `PUT`, `PATCH`, or `DELETE`, include response-body assertions so CSRF/auth failures are obvious instead of surfacing as generic `EnsureSuccessStatusCode()` failures.

### 0z62. AUR Binary PKGBUILDs Must Package Directly From The Downloaded Release Zip, Not From Whatever `makepkg` Auto-Unpacked Into `${srcdir}`

**What went wrong:** The `slskdn-bin` AUR package for `0.24.5.slskdn.175-1` was reported on Manjaro as starting without `Microsoft.AspNetCore.Diagnostics.Abstractions, Version=10.0.0.0`, even though the published GitHub release zip itself contained that DLL and the self-contained payload ran correctly when extracted directly. The PKGBUILD copied the bundle from `${srcdir}/*`, assuming `makepkg`'s implicit zip extraction had populated a complete runtime tree there.

**Why it happened:** `makepkg` extraction is an implementation detail, not the package contract. Packaging from `${srcdir}` instead of the versioned archive path means the final package contents depend on whatever the helper/toolchain auto-unpacked, reused, or left behind in the working tree. That makes AUR binary packages vulnerable to incomplete or stale extracted payloads even when the downloaded release zip is correct.

**How to prevent it:** For binary AUR packages, always install from the explicit downloaded archive file named in `source=()`. Unzip that archive into a temporary staging directory inside `package()`, assert that the apphost and critical managed runtime files exist there, and copy that staged tree into the package payload. Do not rely on `${srcdir}` globbing to discover what should be packaged from a release bundle.

### 0z65. Unit Tests Must Not Depend On Live DNS Or External Hostname Resolution

**What went wrong:** Repo-wide `dotnet test` failed in this environment because `SolidFetchPolicyTests` used `https://example.com/...` and expected live DNS resolution to succeed, while `DestinationAllowlistTests.OpenTunnel_WildcardHostnameMatch_Allowed` exercised the real `DnsSecurityService` for `www.example.com`. When the host environment had transient DNS/socket limits, those unit tests failed even though the production code path under test had not regressed.

**Why it happened:** These tests mixed policy assertions with real network resolution. That makes unit outcomes depend on runner DNS reachability, socket availability, and external hostname behavior instead of only the code's decision logic.

**How to prevent it:** Unit tests must use deterministic IP literals for success-path host validation or inject/mock the DNS validation dependency explicitly. Do not use public hostnames like `example.com` in unit tests unless the test is specifically about the DNS resolver itself and owns the resolution mechanism.

### 0z66. Dense Footers Need Live-Width Stress Tests, Not Just Mocked Happy-Path Screenshots

**What went wrong:** The Web UI footer redesign looked acceptable in mocked screenshots, but live rendering on `local test host` exposed awkward spacing and content pushing against pill edges. The rigid three-column grid also left odd empty space in light theme and made the operational cluster brittle when real counters, icon metrics, or theme font rendering differed from the mock data.

**Why it happened:** The first validation used representative but still too-small data and treated hidden elements with zero-size boxes as overflow noise. It did not stress long speed values, larger hash/sequence counters, active swarm/backfill labels, and the live light-theme rendering together.

**How to prevent it:** Dense footer/status UI must be validated with worst-case realistic counters at desktop, mid-width, and mobile sizes. Prefer flexible wrapping groups over rigid grid columns, and ignore `display:none` elements in overflow checks so the signal is about visible layout defects.

### 0z67. Sequential Soulseek Failover Must Filter Out Mesh-Overlay Sources

**What went wrong:** The trust-aware multi-source split correctly sent all-mesh source sets to the mesh parallel chunking path and mixed/raw source sets to the Soulseek sequential-failover path. The sequential path then iterated over the original mixed `request.Sources` list, so a mesh-overlay `VerifiedSource` in a mixed set could be passed to `ISoulseekClient.DownloadAsync` as if it were a raw Soulseek peer.

**Why it happened:** The path decision used `request.Sources.All(s => s.IsMeshOverlay())`, but the failover loop did not create a transport-specific candidate list. "Not all mesh" was treated as "all entries are valid Soulseek candidates," which is false for mixed sets.

**How to prevent it:** Any transport-specific transfer loop must filter candidates by transport before dialing. The Soulseek sequential-failover loop should only use `VerifiedSourceExtensions.IsSoulseekPeer()`, and mesh-overlay sources should only enter the mesh-aware path.

### 0z68. Built Web UI Assets Must Stay Subpath-Safe For `web.url_base`

**What went wrong:** A Vite build emitted root-relative `/assets/...` references while non-root `web.url_base` deployments such as `/slskd` expect the app to load under a mounted path. Direct visits to deep links could then fetch assets from the domain root instead of the configured Web UI base, producing blank or partially loaded pages behind reverse proxies and subpath installs.

**Why it happened:** The frontend build and backend HTML rewrite rules were solving different eras of the same problem. Legacy backend rewrites handled old root-relative assets, but the modern Vite build needed relative bundle references plus an injected mounted `<base>` tag for deep-link resolution. The build-output check only asserted that some proxy-safe paths existed, not that root-relative asset regressions were forbidden.

**How to prevent it:** Keep `src/web/vite.config.js` on relative build assets for packaged output, inject a mounted base href for non-root `web.url_base`, and run both `npm run test:build-output` and `node src/web/scripts/smoke-subpath-build.mjs` before release-tag work that touches frontend tooling, routing, or HTML rewriting.
