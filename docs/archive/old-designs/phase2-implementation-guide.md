# Phase 2 Implementation Guide: Index

> **Complete Planning Documentation for T-400 to T-411**  
> **Branch**: `experimental/brainz`  
> **Estimated Duration**: 6-8 weeks

---

## Overview

Phase 2 transforms slskdn from a download client into an intelligent library management system with:

- **Canonical Edition Scoring**: Identify the "best" version of each recording
- **Collection Doctor**: Automated library health scanning and issue detection
- **Intelligent Swarm Scheduling**: CDN-like performance optimization
- **Rescue Mode**: Automatic fallback to overlay when Soulseek transfers stall

All features maintain Soulseek as the primary network while adding mesh overlay intelligence.

---

## Documentation Structure

### Phase 2A: Canonical Edition Scoring (T-400 to T-402)
**Document**: `phase2-canonical-scoring-design.md`

**What it does**:
- Scores audio file quality (0.0 to 1.0) based on codec, bitrate, dynamic range
- Detects transcodes (lossy-to-lossless conversions)
- Aggregates quality statistics per (Recording ID, Codec Profile)
- Automatically prefers high-quality "canonical" variants in downloads

**Key Components**:
- `AudioVariant` data model with quality metrics
- `QualityScorer` algorithm (4 weighted factors)
- `TranscodeDetector` heuristics
- `CanonicalStatsService` for aggregation
- Integration with multi-source download selection

**Deliverables**:
- Database schema extensions for variants
- Quality scoring + transcode detection services
- Canonical stats aggregation service
- Download selection enhancement
- Unit + integration tests

---

### Phase 2B: Collection Doctor / Library Health (T-403 to T-405)
**Document**: `phase2-library-health-design.md`

**What it does**:
- Scans music libraries to detect quality issues
- Identifies transcodes, missing tracks, non-canonical variants
- Provides automated "Fix via Multi-Swarm" remediation
- Presents library health dashboard with actionable insights

**Key Components**:
- `LibraryHealthService` for scanning + issue detection
- 8 issue types (suspected transcodes, missing tracks, etc.)
- `LibraryHealthRemediationService` for automated fixes
- React dashboard UI + API endpoints
- One-click job creation to fix detected issues

**Deliverables**:
- Issue taxonomy and database schema
- Background library scanning service
- Health summary + issue filtering API
- React dashboard components
- Remediation job creation service
- Unit + integration + E2E tests

---

### Phase 2C: RTT + Throughput-Aware Swarm Scheduler (T-406 to T-408)
**Document**: `phase2-swarm-scheduling-design.md`

**What it does**:
- Tracks per-peer performance (RTT, throughput, error rates)
- Computes peer "cost" using configurable weighted function
- Assigns high-priority chunks to best (lowest-cost) peers
- Dynamically rebalances when peer performance degrades

**Key Components**:
- `PeerMetricsService` with exponential moving averages
- `PeerCostFunction` with 4 tunable weights (throughput, error rate, timeout rate, RTT)
- `SwarmScheduler` for cost-based chunk assignment
- Chunk priority system (10 levels)
- Automatic rebalancing background task

**Deliverables**:
- Peer performance metrics database schema
- Metrics collection service with EMA
- Cost function + peer ranking
- Enhanced swarm scheduler
- Rebalancing logic
- Unit + integration + performance tests

---

### Phase 2D: Rescue Mode (T-409 to T-411)
**Document**: `phase2-rescue-mode-design.md`

**What it does**:
- Detects underperforming Soulseek transfers (stuck in queue, <10 KB/s)
- Automatically supplements with overlay mesh sources
- Keeps original Soulseek transfer alive (deprioritized)
- Enforces guardrails to maintain Soulseek-primary behavior

**Key Components**:
- `UnderperformanceDetector` (3 detection rules: queued, slow, stalled)
- `RescueService` for activating overlay assistance
- `RescueGuardrailService` for policy enforcement
- Overlay/Soulseek byte ratio limits
- UI indicators for rescue mode status

**Deliverables**:
- Transfer performance state tracking
- Underperformance monitoring background service
- Rescue activation + overlay peer discovery
- Guardrail policy enforcement
- Rescue mode UI components
- Unit + integration tests

---

## Implementation Order

### Week 1-2: Canonical Scoring Foundation
1. Implement `AudioVariant` model + database schema
2. Implement `QualityScorer` + `TranscodeDetector`
3. Add unit tests for scoring algorithms
4. Integrate with `HashDbService`

### Week 3-4: Canonical Stats + Library Health Setup
1. Implement `CanonicalStatsService`
2. Create library health database schema
3. Implement `LibraryHealthService` scanning logic
4. Add issue detection rules (transcodes, canonical upgrades)

### Week 5-6: Library Health UI + Swarm Metrics
1. Build library health API endpoints
2. Create React dashboard components
3. Implement `PeerMetricsService`
4. Integrate metrics collection into downloads

### Week 7-8: Swarm Scheduler + Rescue Mode
1. Implement `PeerCostFunction` + `SwarmScheduler`
2. Add rebalancing logic
3. Implement `UnderperformanceDetector`
4. Implement `RescueService` + guardrails
5. Add rescue mode UI indicators

---

## Configuration Summary

### Canonical Scoring
```yaml
audio:
  canonical_scoring:
    enabled: true
    prefer_canonical: true
    min_quality_improvement: 0.1
    local_quality_threshold: 0.85
    auto_replace_transcodes: false
```

### Library Health
```yaml
library_health:
  enabled: true
  auto_scan:
    enabled: false
    interval_hours: 168
  thresholds:
    quality_gap_for_upgrade: 0.2
    duration_tolerance_ms: 3000
  remediation:
    replace_existing: true
    backup_before_replace: true
```

### Swarm Scheduler
```yaml
multi_source:
  swarm_scheduler:
    cost_function:
      enabled: true
      throughput_weight: 1.0
      error_rate_weight: 0.5
      timeout_rate_weight: 0.3
      rtt_weight: 0.2
    rebalance_enabled: true
    rebalance_interval_seconds: 30
```

### Rescue Mode
```yaml
transfers:
  rescue_mode:
    enabled: true
    max_queue_time_seconds: 1800
    min_throughput_kbps: 10
    min_duration_seconds: 300
    stall_timeout_seconds: 120
    guardrails:
      require_soulseek_origin: true
      max_overlay_to_soulseek_ratio: 2.0
      max_concurrent_rescue_jobs: 5
```

---

## Testing Strategy

### Unit Tests (Required for Each Component)
- Quality scoring algorithm validation
- Transcode detection heuristics
- Cost function calculations
- Underperformance detection rules
- Guardrail policy enforcement

### Integration Tests (Required for Each Feature)
- Canonical stats aggregation with mock data
- Library health scanning with sample files
- Peer metrics collection + persistence
- Rescue mode activation flow

### E2E Tests (Recommended)
- Full library health scan → issue detection → remediation
- Multi-source download with cost-based scheduling
- Rescue mode activation → overlay discovery → completion

---

## Success Metrics

### Canonical Scoring
- % of downloads that select canonical variants
- Reduction in transcode downloads
- Quality score distribution across library

### Library Health
- Issues detected per 1000 tracks
- Remediation success rate
- Time to fix completion

### Swarm Scheduler
- Average download speed improvement vs. random assignment
- Peer cost variance over time
- Rebalancing frequency

### Rescue Mode
- % of stalled transfers rescued successfully
- Overlay/Soulseek byte ratio distribution
- Time from stall detection to rescue activation

---

## Dependencies

- **Phase 1 (T-300 to T-313)**: MusicBrainz/Chromaprint integration MUST be complete
- **Existing Multi-Source**: `MultiSourceDownloadService` must support chunk-level control
- **Mesh Overlay**: DHT rendezvous + TLS overlay connections must be operational
- **HashDb**: Extended schema from Phase 1 (fingerprints, MBIDs)

---

## Next Steps for Codex

1. Read this index + all 4 detailed design documents
2. Start with **T-400**: Implement `AudioVariant` model
3. Work sequentially through each task's checklist
4. Run tests after each component
5. Update `memory-bank/tasks.md` status as tasks complete
6. Move to next task when all checklist items done

---

**Total Tasks**: 12 (T-400 to T-411)  
**Total Documentation**: 4 detailed design docs (~15,000 lines of specifications)  
**Status**: Ready for implementation

All data models, algorithms, database schemas, API endpoints, UI components, and test strategies are fully specified. Codex has everything needed to implement Phase 2!


