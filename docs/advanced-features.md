# Advanced Features Walkthrough

This guide covers advanced features and how to use them effectively.

## Table of Contents

- [Swarm Downloads](#swarm-downloads)
- [Scene ↔ Pod Bridging](#scene--pod-bridging)
- [Collections & Sharing](#collections--sharing)
- [Streaming](#streaming)
- [Integrated Web Player](#integrated-web-player)
- [Wishlist & Background Search](#wishlist--background-search)
- [Discography Concierge](#discography-concierge)
- [Auto-Replace Stuck Downloads](#auto-replace-stuck-downloads)
- [Smart Search Ranking](#smart-search-ranking)
- [Multiple Download Destinations](#multiple-download-destinations)
- [Job Management & Monitoring](#job-management--monitoring)

## Swarm Downloads

Swarm downloads use multiple sources simultaneously to download a file faster and more reliably.

### How It Works

1. **Multiple Sources**: slskdN finds multiple peers with the same file
2. **Content Verification**: Verifies all sources have identical content (hash matching)
3. **Chunk Assignment**: Divides file into chunks and assigns to different peers
4. **Parallel Download**: Downloads chunks simultaneously from multiple peers
5. **Automatic Assembly**: Assembles chunks into complete file

### Enabling Swarm Downloads

**Configuration:**
```yaml
features:
  swarmDownloads: true
```

**Automatic Activation:**
- Swarm downloads activate automatically when:
  - Multiple verified sources are available
  - File size is large enough to benefit from chunking
  - Sources support partial downloads

### Monitoring Swarm Downloads

1. **Downloads View**: See swarm status in Downloads list
2. **Jobs Dashboard**: System → Jobs → Active Swarm Downloads
3. **Detailed Visualization**: Click "View Details" for:
   - Peer contribution breakdown
   - Chunk assignment heatmap
   - Performance metrics
   - Real-time progress

### Optimizing Swarm Performance

**Chunk Size:**
- Automatically optimized based on:
  - File size
  - Number of peers
  - Network conditions
- Manual override available in API

**Peer Selection:**
- Automatically selects best peers based on:
  - Upload speed
  - Reliability (success rate)
  - Latency
  - Reputation

## Scene ↔ Pod Bridging

Unified search across Pod/Mesh and Soulseek Scene networks.

### Enabling the Feature

**Configuration:**
```yaml
features:
  scenePodBridge: true
```

**Web UI:**
- Settings → Features → Scene Pod Bridge

### Using Unified Search

1. **Select Sources**: Choose which networks to search:
   - ☑ Pod/Mesh (mesh network content)
   - ☑ Soulseek Scene (Soulseek network content)

2. **Search Results**: Results show source badges:
   - **POD**: Content from Pod/Mesh network
   - **SCENE**: Content from Soulseek Scene network
   - **POD+SCENE**: Available on both networks

3. **Actions**: 
   - **Download**: Works for both sources
   - **Stream**: Only available for Pod/Mesh content

### Privacy Considerations

- **Pod Identity Protection**: Pod peer IDs never exposed to Scene
- **No Auto-Advertising**: Pod content not automatically advertised to Scene
- **Opt-In Only**: All bridging features are opt-in

## Collections & Sharing

Create and share collections of content with other users.

### Creating Collections

1. **Navigate to Collections**: Sidebar → Collections
2. **Create Collection**: Click "Create Collection"
3. **Add Items**: 
   - Add files from your library
   - Add albums/releases
   - Add individual tracks
4. **Configure Metadata**: 
   - Title, description
   - Tags, categories
   - Cover art

### Sharing Collections

1. **Open Collection**: Click on your collection
2. **Share Options**:
   - **Public**: Anyone with link can access
   - **Private**: Only granted users can access
   - **Pod-Only**: Only visible within your pod
3. **Grant Access**: 
   - Add specific users
   - Set permissions (view, download)
4. **Share Link**: Copy and share collection link

### Downloading Shared Collections

1. **Receive Share Grant**: Via message or link
2. **View Collection**: Open shared collection
3. **Browse Items**: See all items in collection
4. **Backfill All**: Download entire collection with one click
   - Validates download permissions
   - Supports HTTP downloads (cross-node)
   - Falls back to Soulseek if needed

## Streaming

Stream shared, downloaded, and collection-backed audio through slskdN's integrated HTTP range endpoint.

### How Streaming Works

1. **Resolve Content**: slskdN resolves a `ContentId` from indexed content, a share grant, or configured local share/download roots.
2. **Serve Ranges**: The browser requests `GET /api/v0/streams/{contentId}` with standard HTTP range headers for playback and seeking.
3. **Enforce Boundaries**: Normal authentication, share tokens, stream caps, and path restrictions still apply.
4. **Play Locally**: The Web UI player owns browser audio output, so local mute and PWA controls stay device-local.

### Web Player

The persistent player sits above the footer and can collapse into a small drawer bar. It supports:

- Collection playback and a two-pane collection picker modal.
- Shared/downloaded local audio playback and a searchable file browser modal.
- Play/pause, stop, previous/next, rewind, fast-forward, browser-local mute, and Media Session controls.
- Optional MilkDrop visualizer, lightweight analyzer, equalizer, synced lyrics, crossfade, karaoke-style center-channel reduction, and ListenBrainz now-playing/scrobble submission.

See [Listening Party and Player](listening-party.md) for the full protocol and UI behavior.

### Enabling Streaming

**Configuration:**
```yaml
features:
  streaming: true
  streamingRelayFallback: true  # Use relay if direct unavailable
```

### Streaming Limitations

- **Configured Roots Only**: Local fallback resolution is limited to configured non-excluded shares and the configured downloads directory.
- **No New Rights**: Pod/listening-party metadata does not grant access to bytes.
- **Network Conditions**: Remote playback quality depends on the serving node and stream limits.

## Integrated Web Player

The Web UI includes a persistent player drawer for local shared/downloaded audio and collection items. Playback uses slskdN's integrated stream endpoint rather than an external media server.

### Starting Playback

1. Open **Collections** and play an item, or use the player empty state to browse collections and shared/downloaded local audio.
2. The player streams the selected `ContentId` through `/api/v0/streams/{contentId}` with HTTP range support.
3. Browser Media Session controls are populated with title, artist, and album when metadata is available.

### Player Controls

- **Transport**: play/pause, stop, previous, next, rewind 15 seconds, fast-forward 30 seconds
- **Drawer**: collapse/expand above the fixed footer
- **Local mute**: mutes only this browser's audio element without clearing now-playing state
- **Crossfade**: optional five-second fade between queue items

### Audio Tools

- **Equalizer**: 10-band Web Audio EQ with Flat, Classical, Dance, Metal, Rock, and Vocal presets. Settings persist in browser localStorage.
- **Spectrum / Oscilloscope**: lightweight canvas analyzer modes for visual feedback without loading MilkDrop.
- **MilkDrop**: butterchurn visualizer with inline, full-window, and native fullscreen modes.
- **Document Picture-in-Picture**: opens a tiny always-on-top spectrum window on browsers that support `documentPictureInPicture` (currently Chromium-family browsers).
- **Karaoke**: center-channel reduction using channel split/invert/merge. Results vary by mix and are intentionally a local playback effect only.

### Lyrics and Scrobbling

- **Synced lyrics**: the lyrics pane queries LRCLIB with the current artist/title and scrolls LRC lines from the audio clock.
- **ListenBrainz**: paste a ListenBrainz user token in the player to submit `playing_now` updates and completed listens. The token stays in browser localStorage and is not added to daemon configuration.

### Privacy and Network Impact

- EQ, analyzer, MilkDrop, crossfade, karaoke, local mute, and Picture-in-Picture are browser-local.
- Lyrics contact LRCLIB only when the lyrics pane is opened for a track with artist/title metadata.
- ListenBrainz is opt-in and only submits when a token is present.
- These player features do not browse remote Soulseek peers or add background network scanning.

## Wishlist & Background Search

Automatically search for content in the background.

### Creating Wishlist Items

1. **Navigate to Wishlist**: Sidebar → Wishlist
2. **Add Search**: Click "Add Search"
3. **Configure Search**:
   - **Query**: Search terms
   - **Filters**: Quality, format, etc.
   - **Max Results**: Limit results per run
4. **Set Options**:
   - **Auto-Download**: Automatically download matches
   - **Interval**: How often to run (seconds)
   - **Enabled**: Toggle on/off

### Managing Wishlist

- **Run Now**: Manually trigger search
- **View Matches**: See all matches found
- **View History**: See search execution history
- **Edit/Delete**: Modify or remove searches

### Best Practices

- **Specific Queries**: More specific = better results
- **Reasonable Intervals**: Don't run too frequently (60s minimum recommended)
- **Max Results**: Set limits to avoid overwhelming downloads
- **Use Filters**: Narrow results to what you actually want

## Discography Concierge

Map artist coverage from MusicBrainz into local action.

### Using Coverage Maps

1. **Navigate to Search**: Sidebar -> Search
2. **Open Discography Concierge**
3. **Enter Artist MBID**: Use the MusicBrainz artist identifier
4. **Choose Profile**: Core, Extended, or All Releases
5. **Map Coverage**: Build a release/track coverage view

### Track Statuses

- **Available**: HashDb has verified content evidence for the recording
- **Wishlist**: A matching Wishlist seed already exists
- **Ambiguous**: The release track is missing a reliable recording identity
- **Missing**: No local verified evidence or Wishlist seed is known

### Wishlist Promotion

Use **Add missing to Wishlist** to create conservative Wishlist searches for missing tracks. This does not run immediate searches, auto-download files, browse peers, mirror content, or treat slskdN as a backup tool.

## Auto-Replace Stuck Downloads

Automatically find alternatives when downloads get stuck.

### How It Works

1. **Detection**: Monitors download status
2. **Stuck Detection**: Identifies stuck downloads (timeout, error, rejected)
3. **Search Alternatives**: Searches for same file from other sources
4. **Ranking**: Ranks alternatives by:
   - Size match (within tolerance)
   - Upload speed
   - Queue length
   - Free slots
   - User history
5. **Auto-Replace**: Cancels stuck download, starts best alternative

### Enabling Auto-Replace

**Web UI:**
- Toggle "Auto-Replace" in Downloads header

**Configuration:**
```yaml
downloads:
  autoReplace:
    enabled: true
    maxSizeDiffPercent: 5.0  # 5% size difference tolerance
    interval: 60  # Check every 60 seconds
```

### Configuration Options

- **Max Size Difference**: How much size can differ (default 5%)
- **Check Interval**: How often to check for stuck downloads
- **File Extension Filter**: Only replace files with matching extensions

## Smart Search Ranking

Intelligent sorting of search results.

### Ranking Factors

1. **Upload Speed** (40 points): Faster uploads ranked higher
2. **Queue Length** (30 points): Shorter queues ranked higher
3. **Free Slots** (15 points): Users with free slots ranked higher
4. **Download History** (±15 points): 
   - +15 for users with successful downloads
   - -15 for users with many failures

### Using Smart Ranking

- **Default Sort**: "⭐ Smart Ranking (Best Overall)" is default
- **Score Display**: Purple badge shows smart score
- **Other Sorts**: Still available (Speed, Queue, File Count, etc.)

### Customizing Ranking

Ranking weights are configurable (advanced):
```yaml
search:
  ranking:
    speedWeight: 40
    queueWeight: 30
    freeSlotWeight: 15
    historyWeight: 15
```

## Multiple Download Destinations

Configure multiple download folders and choose where files go.

### Setting Up Destinations

**Configuration:**
```yaml
destinations:
  folders:
    - name: "Music"
      path: "/downloads/music"
      default: true
    - name: "Audiobooks"
      path: "/downloads/audiobooks"
    - name: "Other"
      path: "/downloads/other"
```

### Using Destinations

1. **Default Destination**: Files go to default destination
2. **Choose Destination**: Select destination when downloading
3. **Auto-Detection**: Some features auto-select based on content type

### Best Practices

- **Organize by Type**: Music, audiobooks, etc.
- **Set Default**: Mark most-used as default
- **Ensure Writable**: All paths must be writable
- **Sufficient Space**: Ensure all destinations have space

## Job Management & Monitoring

Monitor and manage all background jobs.

### Accessing Job Dashboard

**Navigation**: System → Jobs

### Job Types

1. **Discography Jobs**: Downloading entire discographies
2. **Label Crate Jobs**: Downloading label collections
3. **Swarm Downloads**: Multi-source downloads
4. **Other Background Jobs**: Various system jobs

### Job Dashboard Features

1. **Analytics Overview**:
   - Total jobs
   - Active jobs
   - Completed jobs
   - Jobs by type/status

2. **Active Swarm Downloads**:
   - Real-time progress
   - Chunks/second rate
   - Estimated time remaining
   - Active workers count

3. **Job List**:
   - Filterable by type and status
   - Sortable by date, status, ID
   - Paginated for large lists
   - Progress visualization

4. **Swarm Visualization**:
   - Click "View Details" on swarm job
   - Peer contribution breakdown
   - Chunk assignment heatmap
   - Performance metrics

### Monitoring Best Practices

- **Regular Checks**: Monitor jobs dashboard periodically
- **Swarm Visualization**: Use for troubleshooting slow downloads
- **Filter & Sort**: Use filters to find specific jobs
- **Check Logs**: Review logs for failed jobs

## Advanced Configuration

### Performance Tuning

**Concurrent Downloads:**
```yaml
downloads:
  maxConcurrent: 5  # Adjust based on network/CPU
```

**Search Limits:**
```yaml
search:
  maxResults: 1000  # Reduce for slower systems
  timeout: 30000  # Increase for slow networks
```

**Chunk Size Optimization:**
- Automatically optimized by default
- Manual override available in API
- See [Swarm Performance Tuning](../CHANGELOG.md#swarm-performance-tuning)

### Security Configuration

**Authentication:**
```yaml
web:
  authentication:
    method: "cookie"  # or "jwt"
    requireHttps: true  # For production
```

**CSRF Protection:**
- Enabled by default
- Configure allowed origins if needed

**Mesh Security:**
```yaml
mesh:
  syncSecurity:
    enforce: true  # Strict security checks
```

See [Security Guidelines](SECURITY-GUIDELINES.md) for detailed security configuration.

## Tips & Tricks

### Efficient Searching

1. **Use Filters**: Narrow results with quality/format filters
2. **Combine Sources**: Use both Pod and Scene for best coverage
3. **Save Searches**: Add to Wishlist for recurring searches
4. **Smart Ranking**: Let smart ranking find best sources

### Optimizing Downloads

1. **Enable Swarm**: Use multiple sources for faster downloads
2. **Enable Auto-Replace**: Automatically handle stuck downloads
3. **Monitor Jobs**: Use job dashboard to track progress
4. **Check Peer Performance**: Use swarm visualization to identify issues

### Managing Collections

1. **Organize Early**: Create collections as you discover content
2. **Use Tags**: Tag collections for easy organization
3. **Share Selectively**: Only share what you want to share
4. **Backfill Strategically**: Use backfill for entire collections

### Troubleshooting

1. **Check Logs**: Review logs for errors
2. **Use Visualization**: Swarm visualization shows detailed status
3. **Monitor Jobs**: Job dashboard shows all background activity
4. **Test Features**: Enable features one at a time to isolate issues

See [Troubleshooting Guide](troubleshooting.md) for detailed troubleshooting.

---

**Need more help?** Check the [Documentation Index](README.md) or join our [Discord](https://discord.gg/NRzj8xycQZ)!
