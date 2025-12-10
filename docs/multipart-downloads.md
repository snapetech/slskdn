# Multi-Source Downloads: Network Impact Analysis

## Summary

**Multi-source downloads are not inherently damaging to the Soulseek network** — when implemented responsibly.

slskdn's design distributes load across peers instead of hammering a single user. The impact on upload queues and bandwidth is equivalent to multiple individual users downloading a file — which already happens organically.

---

## How It Works

slskdn's multi-source download system:

1. **Issues parallel FileRequest operations** with custom offset and length values
2. **Sources identical files from multiple users** using metadata matching (acoustic fingerprints, file hashes)
3. **Stitches chunks locally** to assemble the complete file

This mimics the organic behavior of multiple users downloading a file at once — just smarter and faster.

---

## Impact Comparison

| Factor | Single-Source Download | Multi-Source Download (slskdn) |
|--------|------------------------|-------------------------------|
| **Peer Load** | High on one user | Spread across N users |
| **Server Load** | Neutral (peer-to-peer) | Neutral (same message volume) |
| **Queuing Fairness** | One queue hit | N queues hit (same as N users) |
| **Slot Usage** | 1 active slot | 1 slot per chunk peer |
| **Bandwidth Peaks** | Sustained per peer | Mild spikes, shorter duration |
| **Abuse Vector** | Rare unless flooded | Possible if not throttled |

---

## Responsible Implementation

slskdn follows these principles to ensure network health:

### What We Do Right

- **Respect slot limits** — only pull from users who have available slots
- **No server load increase** — peer load is consistent with voluntary sharing
- **Each part treated normally** — chunks behave like standard downloads in queue
- **Concurrent request throttling** — limits on simultaneous chunk requests

### Built-in Safeguards

| Safeguard | Purpose |
|-----------|---------|
| Per-peer concurrency caps | Prevents flooding low-bandwidth users |
| Delayed retries for failed parts | Avoids fast-bombarding on failures |
| Minimum source threshold | Optionally avoid multi-part if fewer than X sources |
| Slot availability checking | Only requests from users with free upload slots |
| Backoff on queue position changes | Respects the natural queue flow |

---

## Potential Risks (If Unbounded)

Without proper throttling, multi-source downloads could:

- Accidentally flood low-bandwidth users with N chunk requests
- Clog queues by re-requesting failed chunks aggressively  
- Enable malicious clients to fragment downloads to overload peers

**slskdn mitigates all of these** through its throttling and fairness mechanisms.

---

## Defense Against Criticism

> "Isn't this abusing the network?"

No. This system:
- Mimics the organic behavior of multiple users downloading a file at once
- Respects slot limits and only pulls from users who already offer the file
- Creates no additional server load (Soulseek is peer-to-peer)
- Treats each part like a normal download in queue and behavior

> "Doesn't this hurt uploaders?"

Actually, it often helps them:
- Load is distributed, so no single uploader bears the full burden
- Downloads complete faster, freeing up slots sooner
- Users with partial availability can still contribute

---

## Configuration

Multi-source downloads can be tuned via configuration:

```yaml
transfers:
  multi_source:
    enabled: true
    min_sources: 2           # Minimum sources before enabling multi-part
    max_concurrent_chunks: 4 # Max simultaneous chunk downloads
    chunk_size_mb: 10        # Target chunk size
    retry_delay_ms: 5000     # Delay before retrying failed chunks
```

See [config.md](config.md) for full configuration options.

---

## Technical Details

For implementation details, see:
- [DHT Rendezvous Design](DHT_RENDEZVOUS_DESIGN.md) — peer discovery
- [Implementation Roadmap](IMPLEMENTATION_ROADMAP.md) — development status




