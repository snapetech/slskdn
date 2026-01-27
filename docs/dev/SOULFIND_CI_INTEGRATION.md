# Soulfind CI Integration

> **Status**: ✅ **Integrated into CI workflows**

---

## Overview

Soulfind (Soulseek server simulator) is now integrated into CI workflows to enable protocol contract tests.

**Repository**: `github.com/soulfind-dev/soulfind`  
**Docker Image**: `ghcr.io/soulfind-dev/soulfind:latest`

---

## CI Integration

### GitHub Actions Workflow

**Location**: `.github/workflows/ci.yml`

**Steps Added**:
1. **Setup Soulfind (Docker)** - Pulls Docker image before tests
2. **Run Tests** - Executes all tests including protocol contract tests

**Behavior**:
- ✅ Pulls `ghcr.io/soulfind-dev/soulfind:latest` image
- ✅ Tests automatically use Docker if available
- ✅ Tests gracefully skip if Docker/Soulfind unavailable
- ✅ No CI failures if Soulfind not available

---

## How It Works

### Test Execution Flow

1. **CI Setup**: Pulls Soulfind Docker image
2. **Test Start**: `SoulfindRunner.StartAsync()` tries:
   - Strategy 1: Soulfind binary (if `SOULFIND_PATH` set)
   - Strategy 2: Docker container (if Docker available)
   - Strategy 3: Stub mode (tests skip gracefully)
3. **Test Execution**:
   - If Soulfind available → Protocol contract tests run
   - If Soulfind unavailable → Tests skip (no failure)

### Protocol Contract Tests

**Category**: `L1-Protocol`

**Tests Enabled** (when Soulfind available):
- `Should_Login_And_Handshake` - Verify login protocol
- `Should_Send_Keepalive_Pings` - Verify keepalive mechanism
- `Should_Handle_Disconnect_And_Reconnect` - Verify reconnection logic

**Tests Always Run** (don't require Soulfind):
- `Should_Perform_Search` - Uses mocked client
- `Should_Join_And_Leave_Rooms` - Uses mocked client
- `Should_Browse_User_Files` - Uses mocked client

---

## Local Development

### Option 1: Use Docker (Recommended)

```bash
# Pull image
docker pull ghcr.io/soulfind-dev/soulfind:latest

# Run tests
dotnet test --filter Category=L1-Protocol
```

### Option 2: Install Binary

```bash
# Download from https://github.com/soulfind-dev/soulfind/releases
# Or build from source (requires D compiler)

# Set path
export SOULFIND_PATH=/path/to/soulfind

# Run tests
dotnet test --filter Category=L1-Protocol
```

### Option 3: Stub Mode (Default)

```bash
# No setup needed - tests skip gracefully
dotnet test --filter Category=L1-Protocol
```

---

## Docker Image Details

**Image**: `ghcr.io/soulfind-dev/soulfind:latest`

**Source**: https://github.com/soulfind-dev/soulfind

**Registry**: GitHub Container Registry (ghcr.io)

**Default Port**: 2242

**Usage**:
```bash
docker run -d --name soulfind-test -p 2242:2242 ghcr.io/soulfind-dev/soulfind:latest
```

---

## Troubleshooting

### Tests Skip in CI

**Cause**: Docker not available or image pull failed

**Solution**: 
- Check CI logs for Docker errors
- Verify Docker is available in CI environment
- Check if image exists: `docker pull ghcr.io/soulfind-dev/soulfind:latest`

**Impact**: Tests skip gracefully - no CI failure

### Tests Fail in CI

**Cause**: Soulfind started but connection failed

**Solution**:
- Check `SoulfindRunner` logs
- Verify port allocation (ephemeral ports)
- Check for port conflicts

---

## Future Enhancements

1. **Binary Caching**: Cache Soulfind binary in CI for faster startup
2. **Test Isolation**: Use separate Docker networks per test
3. **Protocol Verification**: Complete TODO items in protocol contract tests
4. **CI Matrix**: Run protocol tests on multiple platforms

---

## References

- `tests/slskd.Tests.Integration/Harness/SoulfindRunner.cs` - Implementation
- `tests/slskd.Tests.Integration/Protocol/ProtocolContractTests.cs` - Tests
- `docs/dev/soulseek-server-simulation-options.md` - Detailed options
- https://github.com/soulfind-dev/soulfind - Soulfind repository
