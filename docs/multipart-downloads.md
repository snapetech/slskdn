# Multi-Source Downloads: Scope, Mechanics, and Network Impact

## TL;DR

- **Default downloads use the standard single-source Soulseek path.** The slskdN multi-source code is not in the regular download flow.
- Multi-source is reached only from three opt-in entry points:
  1. `MultiSourceController` — explicit REST/UI request,
  2. `RescueService` — kicks in only after a normal download stalls or has missing byte ranges,
  3. `LibraryHealthRemediationService` — auto-fixes detected bad transcodes.
- **Trust is split by source kind.**
  - **Mesh-overlay peers** (other slskdN nodes, discovered via the mesh DHT) are protocol-aware and use **parallel chunked downloads**.
  - **Public Soulseek peers** are not protocol-aware. For these, slskdN uses **sequential failover** (one peer at a time, resume offset preserved on stall), so the receiving Soulseek client sees at most one cancelled transfer per failover, never one per chunk.
- Hash discipline is enforced before chunking. Multi-source is **declined** unless either (a) ≥2 sources share a verified content hash, or (b) all sources are mesh-overlay. Otherwise the request falls back to single-source.

## Why this design

The Soulseek protocol does not natively support range requests (`startoffset → EOF` only) and does not carry content hashes. Naïvely chunking across public peers therefore has two costs the protocol externalizes onto other users:

1. **Mid-stream cancellations show as failed transfers** in official clients (Nicotine+, SoulseekQt). At population scale this looks like a flaky-peer signature.
2. **Per-peer slot/queue overhead is the actual scarcity in 2026**, not bandwidth. Splitting one transfer across N peers consumes N slots and queue entries — the opposite of "less load."

slskdN addresses both by:

- Only chunking in parallel when the peers are mesh-overlay (i.e. other slskdN nodes that expect this pattern).
- Falling back to sequential failover with a clean resume offset for public Soulseek peers, so one failed transfer turns into at most one cancelled-mid-stream entry, not many.

## Where multi-source is wired in (and isn't)

| Component | Uses multi-source? | Default? |
|-----------|---------------------|----------|
| `DownloadService` (the normal Soulseek download path) | **No** | Default download path is unchanged |
| `MultiSourceController` (`/api/v0/multisource/*`, `/swarm`, `/swarm/async`) | Yes — explicit user/integration call | Opt-in |
| `RescueService` | Yes — but only after a normal download stalls / has missing ranges | Auto, behind guardrails |
| `LibraryHealthRemediationService` | Yes — to redownload library files flagged as bad transcodes | Behind library-health remediation |

## Verification before chunking

`ContentVerificationService.VerifySourcesAsync` issues a 32 KB read from each candidate, hashes it (SHA-256), and groups peers by matching hash. Two different transcodes of the same recording will land in different hash groups — preventing the "Frankenfile" risk that arises from splicing chunks across non-identical bitstreams.

Mitigations for the cost of those probes (each one is itself a mid-stream cancel, visible to the candidate as a failed transfer):

- **Per-peer-per-day probe budget** — each Soulseek peer can be probed at most `MaxProbesPerPeerPerDay` times per process lifetime per UTC day (currently 10). Over-budget candidates are skipped, not probed; this caps the visible noise we cause on any individual uploader.
- **Mesh-source skip** — when a request supplies `MeshOverlaySourceCount >= 2`, all Soulseek-side probes are skipped entirely. The mesh sources are trusted and probing public peers wouldn't change the outcome.
- **HashDb cache** — when `TryGetKnownHashAsync` finds a previously-verified hash for `(filename, fileSize)`, it's reused as the expected hash and propagated to the mesh.

## Hard floor for multi-source eligibility

`SelectCanonicalSourcesAsync` returns an empty list (caller falls back to single-source) unless one of the following holds:

- ≥2 candidates share the same verified content hash, **or**
- All candidates are tagged `VerificationMethod.MeshOverlay`.

This means a small or hash-divergent pool will never silently degrade into chunking across mismatched sources. The fallback is metered as `slskd_swarm_hard_floor_fallbacks_total`.

## Sequential failover (Soulseek-source path)

For sources that are not all mesh-overlay, `MultiSourceDownloadService.DownloadAsync` routes to `DownloadSequentialFailoverAsync`. The pattern:

1. Open the output `FileStream` once and stream from the first peer at `startOffset = 0`.
2. A speed monitor cancels the attempt if throughput drops below the floor for the stall window. The cancel is **clean** — bytes already received are kept, and the file position becomes the new resume offset.
3. The next peer is asked for `startOffset = bytesReceived → EOF`. The cycle repeats until either the file is complete or the source list is exhausted.
4. Each switch increments `slskd_swarm_sequential_failover_total{reason}`.

In the worst case (the file pulls bytes from N peers), this produces **at most N − 1 cancellation events** across the whole download — versus `(chunks × peers)` cancellations under naïve parallel chunking.

## Parallel chunking (mesh-overlay path)

When all sources are `VerificationMethod.MeshOverlay`, `DownloadAsync` keeps the original parallel-chunk worker pool: shared work queue, per-source workers, retries. Mesh-overlay peers expect this protocol; the cancellation noise concern doesn't apply.

## Observability

| Metric | What it tells you |
|--------|-------------------|
| `slskd_swarm_midstream_cancellations_total{peer_kind, reason}` | Mid-stream cancellation events. `peer_kind=soulseek` cancels are the ones that show on official-client UIs as failed transfers. Goal: keep low. |
| `slskd_swarm_verification_probes_total{peer_kind, outcome}` | Probe outcomes per peer kind. `outcome=skipped_budget` and `skipped_mesh` track when we declined to probe. |
| `slskd_swarm_hard_floor_fallbacks_total{reason}` | How often the hard floor declined multi-source and let the caller fall back to single-source. |
| `slskd_swarm_sequential_failover_total{reason}` | Switches between Soulseek peers in the sequential path (`stalled`, `errored`, `queue_too_deep`). |

These are scrape-able from the existing Prometheus surface. See **System → Metrics** in the web UI.

## Configuration

```yaml
transfers:
  multi_source:
    enabled: true
    chunk_size_kb: 512        # default chunk size for the mesh-overlay path
    retry_delay_ms: 5000
```

(The hard floor and per-peer probe budget are not user-configurable; they are baseline safety properties.)

## Defense against the common critique

> *"You're 10× the load on the network because you make 10× the slot allocations."*

Yes — if the implementation parallel-chunked every download across raw Soulseek peers, that critique would be correct. slskdN does not do that:

- The default download path is unchanged from upstream slskd.
- Multi-source is opt-in (or opt-in-after-failure) and the rescue path uses mesh-overlay peers, not public Soulseek peers.
- For the cases where multi-source does talk to public peers (explicit-API calls), the path is sequential failover, not parallel chunking — it consumes one slot at a time and produces one cancellation per failover, not one per chunk.
- Multi-source is declined entirely when the source pool can't pass the hard floor.

> *"You can't safely splice chunks because Soulseek has no content hashes."*

Correct. Two transcodes of the same recording are not byte-identical. slskdN handles this by hashing the first 32 KB of each candidate (SHA-256) and only chunking across sources that fall in the same hash group. The hash itself is gossiped over the mesh so the cost amortizes across slskdN nodes that have seen the file.

> *"Failed transfers stick around in the receiver's UI."*

Yes. The verification probe and the parallel-chunk path both produce mid-stream cancellations that an official client renders as `transfer failed`. slskdN limits this in three ways: (a) a per-peer-per-day probe budget, (b) skipping Soulseek probes entirely when mesh sources cover the request, (c) using sequential failover instead of parallel chunking when the source set isn't all-mesh, so a single download produces at most a handful of cancel events instead of dozens.

## Source: design vs. implementation

| Concern | Implementation |
|--------|----------------|
| Routing | `MultiSourceDownloadService.DownloadAsync` (`src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs`) |
| Hard floor | `MultiSourceDownloadService.ApplyHardFloor` |
| Sequential failover | `MultiSourceDownloadService.DownloadSequentialFailoverAsync` |
| Probe budget | `ContentVerificationService.TryConsumeProbeBudget` |
| Mesh-skip flag | `ContentVerificationRequest.MeshOverlaySourceCount` |
| Trust discriminator | `VerificationMethod.MeshOverlay` + `VerifiedSourceExtensions.IsMeshOverlay` |

## Related documents

- [DHT Rendezvous Design](DHT_RENDEZVOUS_DESIGN.md) — mesh peer discovery
- [Multi-Swarm Architecture](multi-swarm-architecture.md) — orchestration
- [Phase 2 Rescue Mode Design](phase2-rescue-mode-design.md)
- [Phase 2 Swarm Scheduling Design](phase2-swarm-scheduling-design.md)
