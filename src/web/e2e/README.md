# E2E Test Suite

This directory contains end-to-end tests for slskdn using Playwright.

## Test Structure

- `smoke-auth.spec.ts` - Authentication and basic health checks
- `core-pages.spec.ts` - Core UI pages (system, downloads, uploads, rooms, chat, users)
- `library.spec.ts` - Library indexing and browsing
- `search.spec.ts` - Search functionality
- `multippeer-sharing.spec.ts` - Multi-peer sharing workflows (invites, groups, collections, shares)
- `streaming.spec.ts` - Streaming functionality
- `policy.spec.ts` - Policy enforcement (stream/download restrictions)

## Intentionally Skipped Tests

Some tests are intentionally skipped because they are better tested at the API level or require specific timing/setup that is difficult to achieve reliably in E2E tests.

### `policy.spec.ts`

#### `expired_token_denied`
**Status**: Skipped  
**Reason**: This test requires precise timing (create share with 1-second expiry, wait 2 seconds, verify denial). This is better tested at the API level where timing can be controlled precisely. E2E tests are inherently flaky for sub-second timing requirements.

**Alternative**: API-level unit/integration tests can create shares with specific expiry times and verify token validation logic directly.

### `streaming.spec.ts`

#### `concurrency_limit_blocks_excess_streams`
**Status**: Skipped  
**Reason**: This test requires specific share setup with `MaxConcurrentStreams=1` and multiple streamable items. The test would need to:
1. Create a share with specific concurrency policy
2. Start first stream
3. Attempt second stream
4. Verify second stream is blocked

This is better tested at the API level where we can:
- Mock the stream session limiter
- Verify policy enforcement logic directly
- Test edge cases more reliably

**Alternative**: API-level tests can directly test `IStreamSessionLimiter` and policy enforcement without UI interaction.

### `search.spec.ts` / `library.spec.ts`

Some tests may skip if UI features are not available:
- Search page may not exist if feature is disabled
- Browse navigation may not be available if library browsing is not implemented

These are graceful skips that allow tests to pass when features are intentionally disabled or not yet implemented.

## Running Tests

### Local Development

```bash
# Start nodes manually or let harness start them
cd src/web
npm run test:e2e
```

### CI Environment

Tests run with `SLSKDN_TEST_NO_CONNECT=true` to disable Soulseek connections for deterministic testing.

```bash
npm run test:e2e:ci
```

## Test Harness

The `MultiPeerHarness` manages multiple slskdn instances for cross-node testing:
- Launches nodes on different ports
- Manages test fixtures (music, books, etc.)
- Handles cleanup on test completion

See `harness/MultiPeerHarness.ts` for details.

## Test Fixtures

E2E tests require real fixture files to be present before running. The harness will **fail fast** if fixtures are missing.

### Required Fixtures

Fixtures must be in `test-data/slskdn-test-fixtures/` with a valid `meta/manifest.json`:

- `book/treasure_island_pg120.txt` (text file)
- Additional audio/video files (see `test-data/slskdn-test-fixtures/meta/fetch_media.sh`)

### Generating Manifest

After downloading fixtures, generate the manifest:

```bash
cd test-data/slskdn-test-fixtures/meta
node generate-manifest.js
```

This creates `manifest.json` with sha256 checksums for validation.

### Node Configuration

Tests use 3 nodes:
- **Node A**: Shares `movie/` + `book/` directories
- **Node B**: Shares `music/` + `tv/` directories  
- **Node C**: Recipient-only (no shares)

### Fixture Validation

The harness validates fixtures on startup:
- Checks fixtures root directory exists
- Validates manifest.json exists and is valid
- Verifies all required files exist
- Optional checksum validation (set `SLSKDN_VALIDATE_FIXTURE_CHECKSUMS=1`)

If validation fails, tests abort immediately with clear error messages.
