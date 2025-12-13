# Soulbeet Integration Tests

This directory contains integration tests for Soulbeet compatibility with slskdn.

## Test Suites

### SoulbeetCompatibilityTests
Tests backwards-compatible slskd API endpoints:
- `GET /api/info` - Server info
- `POST /api/search` - File search
- `POST /api/downloads` - Create downloads
- `GET /api/downloads` - List downloads
- `GET /api/downloads/{id}` - Download details
- Full compat mode workflow

### SoulbeetAdvancedModeTests
Tests slskdn-native advanced features:
- `GET /api/slskdn/capabilities` - Feature detection
- `POST /api/jobs/mb-release` - MBID jobs
- `POST /api/jobs/discography` - Discography jobs
- `POST /api/jobs/label-crate` - Label crate jobs
- `GET /api/jobs` - Job listing
- `GET /api/jobs/{id}` - Job details
- `POST /api/slskdn/warm-cache/hints` - Cache hints
- `GET /api/slskdn/library/health` - Library health
- Full advanced mode workflow

## Running Tests

```bash
# Run all Soulbeet tests
dotnet test --filter FullyQualifiedName~slskd.Tests.Integration.Soulbeet

# Run compatibility tests only
dotnet test --filter FullyQualifiedName~SoulbeetCompatibilityTests

# Run advanced mode tests only
dotnet test --filter FullyQualifiedName~SoulbeetAdvancedModeTests

# Run specific test
dotnet test --filter "FullyQualifiedName=slskd.Tests.Integration.Soulbeet.SoulbeetCompatibilityTests.GetInfo_ShouldReturnSlskdnInfo"
```

## Test Scenarios

### Scenario 1: Vanilla slskd Compatibility
1. Soulbeet connects to slskdn
2. Tries `/api/slskdn/capabilities` → gets 200 OK (not 404)
3. Falls back to using standard slskd APIs
4. Search, download, status all work normally

### Scenario 2: slskdn Advanced Mode
1. Soulbeet connects to slskdn
2. Calls `/api/slskdn/capabilities` → detects slskdn
3. Uses MBID job API for album downloads
4. Polls job status instead of individual transfers
5. Submits warm cache hints for popular content
6. Checks library health

### Scenario 3: Error Handling
1. Invalid MBID → returns error
2. Job not found → returns 404
3. Warm cache disabled → returns error
4. Rate limiting → returns 429

## Mock Data

Tests use in-memory mocks for:
- Soulseek client responses
- MusicBrainz API responses
- File system operations
- Database operations

## E2E Testing

For full end-to-end testing with real Soulbeet:
1. Deploy slskdn in Docker
2. Deploy Soulbeet in Docker
3. Configure Soulbeet to use slskdn backend
4. Run real-world workflows
5. Verify Beets import works correctly

## CI/CD Integration

These tests run in CI on every commit to ensure:
- Backwards compatibility is maintained
- New features don't break existing clients
- API contracts are stable















