# Phase 7: Testing Strategy with Soulfind & Mesh Simulator

> **Phase**: 7 (Testing Infrastructure)  
> **Dependencies**: All phases benefit from this  
> **Branch**: `experimental/brainz` (tests live alongside features)  
> **Estimated Duration**: 4-6 weeks  
> **Tasks**: T-900 through T-915

---

## 1. Purpose & Scope

This document defines how we use **Soulfind** and a **local mesh simulator** to test slskdn.

### Goals

1. Verify **Soulseek protocol correctness** (handshake, search, rooms, transfers) without hammering the real server.
2. Validate the **capture & normalization pipeline** that turns Soulseek traffic into MBID/AudioVariant metadata.
3. Exercise **disaster mode** and **Virtual Soulfind mesh** behaviour in controlled scenarios.
4. Provide a repeatable **CI-friendly** way to simulate "realistic enough" networks for regression tests.

### Scope includes

- Unit-style tests that depend on Soulfind as a local test server.
- Integration tests that spin up multiple slskdn instances plus Soulfind.
- Mesh-only tests using an in-process overlay/DHT simulator.
- How these tests are wired into the build/CI pipeline.

We explicitly **do not** use Soulfind in production; see `docs/dev/soulfind-integration-notes.md` for policy.

---

## 2. Testing Layers (Overview)

We use several layers of tests, with increasing complexity and runtime cost:

### L0 – Pure Unit Tests (No Network, No Soulfind)

- Business logic, MBID mapping, AudioVariant analyzers, job planning.
- Run on every build.

### L1 – Protocol Unit/Contract Tests (Soulfind-Assisted)

- Start a **local Soulfind** instance.
- One slskdn instance connects and exercises:
  - Handshake, login, keepalive.
  - Minimal search, room join/leave, and browse operations.
- Validates encoding/decoding and basic protocol behaviour.

### L2 – Soulseek Integration Tests (slskdn + Soulfind + multiple peers)

- Soulfind as a local server.
- Multiple slskdn instances configured as independent "clients" (Alice, Bob, Carol).
- Fake shared directories with deterministic audio test data.
- Drives:
  - Searches, transfers, room interactions.
  - Capture-to-MBID normalization.
- Used to test **compatibility** and **capture pipeline** end-to-end.

### L3 – Mesh & Disaster Simulation (Soulfind + Mesh + No Soulfind)

- Use Soulfind to simulate normal server operation initially.
- Trigger disaster scenarios by killing Soulfind mid-test.
- Verify:
  - Mode transition to **disaster mode** (mesh-only).
  - DHT + overlay-based discovery and transfer.
- Also includes **pure mesh-only** simulations with no Soulfind at all.

L0 runs always.  
L1/L2/L3 run as **integration test suites** guarded by flags/CI jobs, because they require Soulfind and are slower.

---

## 3. Test Harness Components

### 3.1. Soulfind Test Harness

A small module (e.g. `test_harness/soulfind_runner`) responsible for:

- Locating the Soulfind binary:
  - From an env var (e.g. `SOULFIND_BIN`), build artifact, or known path.
- Starting Soulfind with:
  - Ephemeral port (0 → OS chooses, then detect).
  - Minimal config (in-memory DB, reduced logging, deterministic seed).
- Waiting until Soulfind is ready to accept connections.
- Providing:
  - Connection info (host, port).
  - Shutdown hooks (graceful + forced).

#### Usage pattern in tests

```csharp
// C# example
public class SoulfindRunner : IDisposable
{
    public string Host { get; private set; }
    public int Port { get; private set; }
    
    public static SoulfindRunner Start(SoulfindConfig config = null)
    {
        // Find binary, start process, wait for ready
    }
    
    public void Shutdown()
    {
        // Graceful shutdown + forced kill if needed
    }
    
    public void Dispose() => Shutdown();
}

// In test
using var sf = SoulfindRunner.Start();
var slskdnConfig = new Options { Server = sf.Host, ServerPort = sf.Port };
// ... run test
```

### 3.2. slskdn Test Clients

A second harness component (e.g. `test_harness/slskdn_client_runner`) creates "mini instances" of slskdn for test purposes:

- Each instance has:
  - Its own config directory.
  - Its own Soulseek username/password (test credentials).
  - Its own overlay/DHT identity.
  - Optional test share directory with prepared audio files.

- Exposes simple operations:
  - `ConnectToSoulseek()`
  - `JoinRoom(name)`
  - `SetShareDirectory(path)`
  - `Search(query)`
  - `RequestFile(peer, path)`
  - `WaitForCaptureFlush()` (ensure capture pipeline has flushed to DB).

#### Implementation options

- Separate test binaries for slskdn started by the harness, or
- In-process instantiation with a test config (if architecture allows).

```csharp
public class SlskdnTestClient : IDisposable
{
    public string Username { get; }
    public string ConfigDir { get; }
    
    public static SlskdnTestClient Create(string username, string shareDir = null)
    {
        // Create isolated config, start instance
    }
    
    public Task ConnectToSoulseekAsync(string host, int port)
    {
        // Configure + connect
    }
    
    public Task<SearchResults> SearchAsync(string query)
    {
        // Execute search via API
    }
    
    public void Dispose()
    {
        // Cleanup instance
    }
}
```

### 3.3. Mesh Simulator

For L3 tests involving DHT + overlay without Soulfind, we add a **mesh simulator**:

- Creates N in-process slskdn nodes with:
  - Connected DHT overlay (using loopback transports).
  - Pre-configured AudioVariant inventory (fake library).
- Provides utilities to:
  - Start jobs (MBID releases, discography, repair missions) on node A.
  - Declare which nodes have which MBIDs as "seeders".
  - Inspect metadata (e.g. observed shadow index values, overlay descriptors).
  - Intentionally introduce failures (disconnect nodes, drop messages) to verify robustness.

The simulator's goal is: **exercise the Virtual Soulfind mesh and multi-swarm logic** without needing Soulseek at all.

```csharp
public class MeshSimulator
{
    public List<SimulatedNode> Nodes { get; }
    
    public SimulatedNode CreateNode(string id, Dictionary<string, AudioVariant> inventory)
    {
        // Create in-memory node with fake library
    }
    
    public void ConnectNodes(params SimulatedNode[] nodes)
    {
        // Establish DHT/overlay connections
    }
    
    public void SimulateNetworkPartition(SimulatedNode node)
    {
        // Disconnect node from mesh
    }
}
```

---

## 4. L1 – Protocol Contract Tests with Soulfind

These are fast-ish tests that validate **client–server protocol correctness** using Soulfind.

### 4.1. Test Areas

#### 1. Handshake & Login

- slskdn connects to Soulfind and performs:
  - Login with valid credentials.
  - Optional status changes (away/online).
- Assertions:
  - No panics or unexpected disconnects.
  - Correct parsing of server responses.

#### 2. Keepalive & Idle Behaviour

- slskdn stays connected for a short period, sending keepalives or responding to pings.
- Assertions:
  - No timeouts in either direction.
  - slskdn does not flood the server.

#### 3. Search Requests

- slskdn issues search queries through Soulfind:
  - Basic filename search.
  - Room-scoped search (if supported).
- Soulfind can be configured (or pre-populated) to return controlled fake results.
- Assertions:
  - slskdn properly interprets the results.
  - No malformed search responses are dropped silently.

#### 4. Room Semantics

- slskdn joins, leaves, and re-joins a test room.
- Optionally sends/receives test messages via the room.
- Assertions:
  - Correct room IDs and membership mapping.
  - Capture pipeline (if enabled) captures enough context.

#### 5. User Lists / Browsing

- slskdn requests user information (browse shares / user info).
- Assertions:
  - Parser correctness for browse responses.
  - No state corruption when lists are large or truncated.

### 4.2. Capture & Normalize Integration

For these tests we also validate the **capture & normalization pipeline**:

- Capture observed search results, browses, and transfers (if any).
- Run a short pipeline to:
  - Guess MBIDs (using test fixtures and stubbed MB lookups).
  - Create `AudioVariant` records.

Assertions:

- For controlled test fixtures, we expect:
  - Known MB Release / Recording IDs mapped.
  - Known number of variants created.
- Confirms that the Soulseek-facing inputs are usable for MB-aware logic.

---

## 5. L2 – Soulseek Integration Tests (Multi-Client + Soulfind)

These tests simulate a **mini Soulseek network** with multiple slskdn instances attached to a **local Soulfind**.

### 5.1. Test topology

#### Soulfind

- Single instance on localhost.
- Minimal config; ephemeral port.

#### slskdn instances

At least 3 logical clients:

- **Alice**: has a set of "good" FLACs + metadata.
- **Bob**: has the same recordings as MP3 variants (some good, some obvious transcodes).
- **Carol**: minimal or no library; will request from Alice/Bob.

#### Shared libraries

Use **small, deterministic test fixtures**:

- 2–3 albums with:
  - Known MBIDs.
  - Hand-labeled expected quality tiers (lossless, good lossy, bad lossy).
- Include:
  - At least one obviously lossy-sourced FLAC.
  - At least one obviously transcoded MP3.

### 5.2. Scenarios

#### 1. Basic Search & Download via Soulseek

- Carol searches for tracks using filename-based queries.
- Soulfind returns Alice/Bob as sources.
- Carol requests files over **classic Soulseek transfers**.
- Capture pipeline maps everything to MBIDs and populates AudioVariants.

Assertions:

- Alice's FLACs are mapped to correct MBIDs with high `quality_score`.
- Bob's MP3s and bad FLACs get appropriate `quality_score` and `transcode_suspect`.

#### 2. Rescue Mode via Mesh

- Artificially slow or stall Soulseek transfer from Bob.
- Ensure:
  - Multi-swarm overlay kicks in (or at least scheduled).
  - Carol locates Alice through capture+overlay and rescues remaining chunks.

Assertions:

- Completed file matches Alice's FLAC variant identity.
- Transfer log shows Soulseek + overlay contributions.

#### 3. Room and Scene Capture

- Alice and Bob join a test room.
- Carol joins later, uses the room for search or metadata hints.
- Capture which peers appear in which rooms.

Assertions:

- Captured data sufficient to seed scene membership (for later Virtual Soulfind mesh logic).
- No protocol regressions around room join/leave.

#### 4. Reconnect & Presence Consistency

- Force-disconnect one slskdn instance (e.g., kill Alice).
- Ensure:
  - Server-side presence changes are handled without crashing.
  - Subsequent searches don't list disconnected peers as active sources.

Assertions:

- slskdn marks peers offline in its internal state.
- No stale connections are used for transfers.

These L2 tests are primarily about **"are we a good Soulseek citizen?"** and **"does our capture pipeline behave under realistic flows?"**.

---

## 6. L3 – Mesh & Disaster Simulation Tests

### 6.1. Soulfind-assisted disaster drills

These tests start with Soulfind online, then remove it mid-scenario.

#### Scenario: "Graceful Degradation to Mesh"

1. Start Soulfind and 3 slskdn instances: Alice, Bob, Carol.
2. Alice and Bob share test libraries (FLAC + MP3).
3. Carol starts:
   - A high-level MBID job (album).
   - Initially resolves sources via Soulseek (Carol → Soulfind → peers).
4. Mid-transfer:
   - The test harness **kills Soulfind** abruptly.
5. slskdn should:
   - Detect loss of server.
   - Transition to **disaster mode** (mesh-only).
   - Continue resolving MBID job using:
     - Shadow index (from previously captured data).
     - DHT + overlay descriptors.

Assertions:

- No deadlocks / infinite retries when server dies.
- MBID job eventually completes via overlay-only paths.
- Mode flag is set:
  - e.g., internal state `mode = Disaster`.

### 6.2. Mesh-only simulations (no Soulfind)

These tests rely solely on the mesh simulator.

#### Scenario: "Pure Mesh Discography Job"

1. Set up N in-process slskdn peers (no Soulseek at all).
2. Assign:
   - Alice: full discography for an artist (lossless).
   - Bob: partial discography (mixed formats).
   - Carol: empty library.
3. Pre-populate:
   - DHT shadow index entries for MBIDs (or have peers publish them).
   - Overlay descriptors for availability.

4. Carol starts a `discography` job via MB Release IDs (from MB metadata fixture).

Assertions:

- Carol discovers candidate peers purely via DHT/overlay.
- Multi-swarm scheduling uses `quality_score` to prefer Alice's FLACs over Bob's lossy/transcodes.
- No assumptions on Soulseek presence; tests confirm independence.

#### Scenario: "Repair Mission Across Mesh"

1. Carol has a known-bad FLAC (lossy-sourced) for some MB Release.
2. Alice has a canonical good FLAC variant.
3. DHT/mesh is populated with:
   - Canonical variant hints.
   - Some form of minimal pod/scene membership (optional).

4. Carol runs a "repair mission" job.

Assertions:

- Repair mission resolves to Alice via mesh-only discovery.
- Carol replaces bad FLAC with canonical one.
- AudioVariant records reflect updated quality & provenance.

---

## 7. Build & CI Integration

### 7.1. Test categories

We categorize tests:

#### `unit` – L0 only (no network, no Soulfind)

- Run on every `build` and every PR.

#### `integration-soulseek` – L1 + L2 (requires Soulfind)

- Requires:
  - `SOULFIND_BIN` (or equivalent) to be available.
- Runs:
  - On main branch merges.
  - On nightly or scheduled CI jobs.

#### `integration-mesh` – L3 mesh-only tests

- No external binaries required beyond slskdn itself.
- Can run on PRs as a "slower but frequent" job if not too heavy.

### 7.2. CI configuration (example pattern)

Example (pseudo):

```bash
# make test
# Runs unit tests only

# make test-integration-soulseek
# Checks SOULFIND_BIN present
# Starts Soulfind and runs L1/L2 test suite

# make test-mesh
# Runs L3 mesh-only tests
```

#### CI jobs

- `unit-tests`:
  - `make test`

- `integration-mesh`:
  - `make test-mesh`

- `integration-soulseek`:
  - Only on main/nightly:
    - `SOULFIND_BIN` preinstalled (or built in previous step).
    - `make test-integration-soulseek`

### 7.3. Failure handling & skips

- If `SOULFIND_BIN` is missing:
  - `integration-soulseek` tests should **skip gracefully**, not fail.
  - CI config can treat "skipped due to no Soulfind" as expected in dev environments.

- Mesh-only tests should be deterministic and runnable on any developer machine without additional infra.

---

## 8. Test Fixtures & Data

### 8.1. Audio test fixtures

Located in `tests/fixtures/audio/`:

- **Small, deterministic files**:
  - `good-flac-44100-16bit.flac` (100 kB, known MBID)
  - `lossy-sourced-flac.flac` (obvious lowpass at 16 kHz)
  - `good-mp3-v0.mp3` (LAME V0, ~190 kbps)
  - `transcoded-mp3-320.mp3` (CBR 320 but suspect spectrum)
  - `good-opus-128.opus`
  - `good-aac-lc-256.m4a`

- **Metadata sidecar files**:
  - `fixtures-metadata.json`:
    ```json
    {
      "good-flac-44100-16bit.flac": {
        "mb_recording_id": "abc-123-def",
        "mb_release_id": "xyz-789",
        "expected_quality_score": 0.95,
        "transcode_suspect": false
      }
    }
    ```

### 8.2. MusicBrainz stubs

For tests, we use **stubbed MB API responses**:

- `tests/fixtures/musicbrainz/release-xyz-789.json`
- `tests/fixtures/musicbrainz/recording-abc-123-def.json`

Tests inject a mock `IMusicBrainzClient` that returns these fixtures.

### 8.3. Soulfind test data

When starting Soulfind for L1/L2 tests:

- Pre-seed with test users: `alice`, `bob`, `carol` (known passwords).
- Pre-populate Alice/Bob share lists from test fixtures directory.
- Use deterministic port assignment to avoid conflicts.

---

## 9. Task Breakdown

### T-900: Implement Soulfind test harness

**Deliverables:**

- `SoulfindRunner` class for starting/stopping local Soulfind
- Binary discovery logic (env var, build artifact, known paths)
- Ephemeral port allocation and readiness detection
- Graceful + forced shutdown

**Files:**

- `tests/slskd.Tests.Integration/Harness/SoulfindRunner.cs`

### T-901: Implement slskdn test client harness

**Deliverables:**

- `SlskdnTestClient` class for isolated test instances
- Config directory isolation
- Share directory configuration
- API wrappers for common operations (search, download, etc.)

**Files:**

- `tests/slskd.Tests.Integration/Harness/SlskdnTestClient.cs`

### T-902: Create audio test fixtures

**Deliverables:**

- Small deterministic audio files (FLAC, MP3, Opus, AAC)
- Known good and known bad (transcode) variants
- Metadata sidecar file with expected quality scores and MBIDs

**Files:**

- `tests/fixtures/audio/*` (files)
- `tests/fixtures/audio/fixtures-metadata.json`

### T-903: Create MusicBrainz stub responses

**Deliverables:**

- JSON fixtures for MB API responses (releases, recordings)
- Mock `IMusicBrainzClient` that returns fixtures
- Test helper to inject mock into DI container

**Files:**

- `tests/fixtures/musicbrainz/*.json`
- `tests/slskd.Tests.Integration/Mocks/MockMusicBrainzClient.cs`

### T-904: Implement L1 protocol contract tests

**Deliverables:**

- Test suite for basic Soulseek protocol operations:
  - Login/handshake
  - Keepalive
  - Search
  - Rooms
  - Browse

**Files:**

- `tests/slskd.Tests.Integration/L1_ProtocolContractTests.cs`

### T-905: Implement L2 multi-client integration tests

**Deliverables:**

- Test suite with Alice/Bob/Carol topology
- Scenarios: search, download, capture pipeline, room interactions
- Assertions on MBID mapping and quality scores

**Files:**

- `tests/slskd.Tests.Integration/L2_MultiClientTests.cs`

### T-906: Implement mesh simulator

**Deliverables:**

- `MeshSimulator` class for in-process DHT/overlay network
- `SimulatedNode` with fake library inventory
- Network partition simulation
- Message drop/delay simulation

**Files:**

- `tests/slskd.Tests.Integration/Simulator/MeshSimulator.cs`
- `tests/slskd.Tests.Integration/Simulator/SimulatedNode.cs`

### T-907: Implement L3 disaster mode tests

**Deliverables:**

- Test suite for Soulfind-assisted disaster drills
- Kill Soulfind mid-transfer, verify mesh takeover
- Assertions on disaster mode activation and completion

**Files:**

- `tests/slskd.Tests.Integration/L3_DisasterModeTests.cs`

### T-908: Implement L3 mesh-only tests

**Deliverables:**

- Pure mesh simulation tests (no Soulfind)
- Discography job across mesh
- Repair mission tests
- DHT/overlay-only discovery validation

**Files:**

- `tests/slskd.Tests.Integration/L3_MeshOnlyTests.cs`

### T-909: Add CI test categorization

**Deliverables:**

- Test traits/categories for L0/L1/L2/L3
- CI configuration for test categories
- Environment variable detection for Soulfind availability

**Files:**

- `.github/workflows/tests.yml` (or equivalent CI config)
- Test attribute annotations

### T-910: Add test documentation

**Deliverables:**

- README for running integration tests locally
- Instructions for setting up Soulfind binary
- Troubleshooting guide for test failures

**Files:**

- `tests/README.md`

### T-911: Implement test result visualization

**Deliverables:**

- Test report generation (HTML/Markdown)
- Coverage reports for integration tests
- Performance benchmarks for mesh operations

**Files:**

- CI configuration for test reporting
- Test result parsers

### T-912: Add rescue mode integration tests

**Deliverables:**

- Tests for underperforming transfer detection
- Overlay rescue activation validation
- Mixed Soulseek+overlay completion scenarios

**Files:**

- `tests/slskd.Tests.Integration/RescueModeTests.cs`

### T-913: Add canonical selection integration tests

**Deliverables:**

- Tests verifying canonical variant preference
- Quality score-based source selection
- Cross-codec deduplication tests with real files

**Files:**

- `tests/slskd.Tests.Integration/CanonicalSelectionTests.cs`

### T-914: Add library health integration tests

**Deliverables:**

- End-to-end tests for library scanning
- Issue detection validation (transcodes, non-canonical variants)
- Remediation job creation and execution

**Files:**

- `tests/slskd.Tests.Integration/LibraryHealthTests.cs`

### T-915: Performance benchmarking suite

**Deliverables:**

- Benchmark tests for:
  - DHT query latency
  - Overlay throughput
  - Canonical stats aggregation
  - Mesh simulation at scale (100+ nodes)

**Files:**

- `tests/slskd.Tests.Performance/MeshPerformanceTests.cs`
- `tests/slskd.Tests.Performance/CanonicalStatsPerformanceTests.cs`

---

## 10. Summary

- We use **Soulfind strictly as dev/test harness**, never as runtime infra.
- Unit and integration tests are layered:
  - **L0:** pure logic tests.
  - **L1:** protocol contract tests (slskdn ↔ Soulfind).
  - **L2:** multi-client Soulseek simulations with capture & normalization.
  - **L3:** mesh and disaster simulations, with and without Soulfind.

This gives us:

- Confidence that slskdn behaves correctly as a Soulseek client.
- Confidence that the capture pipeline produces good MB-aware metadata in realistic flows.
- Confidence that the **Virtual Soulfind mesh** and **disaster mode** work as intended and do *not* depend on any central server, Soulfind or otherwise.

---

*Phase 7 testing strategy specification complete. Ready for implementation.*
