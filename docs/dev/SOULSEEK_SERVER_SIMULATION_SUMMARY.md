# Soulseek Server Simulation - Summary

> **Quick Reference**: Options for simulating Soulseek server in tests

---

## Current Implementation

**SoulfindRunner** now supports **3 strategies** (tried in order):

1. **Soulfind Binary** (Option 1) - If `SOULFIND_PATH` or binary found in PATH
2. **Docker Container** (Option 2) - If Docker available and `soulfind/soulfind:latest` image exists
3. **Stub Mode** (Fallback) - Tests skip gracefully, use mocked `ISoulseekClient`

---

## Quick Setup

### For Local Development

```bash
# Option A: Install Soulfind binary
export SOULFIND_PATH=/path/to/soulfind

# Option B: Use Docker
docker pull soulfind/soulfind:latest  # If image exists

# Run tests
dotnet test --filter Category=L1-Protocol
```

### For CI

Tests will automatically:
- Try Soulfind binary (if `SOULFIND_PATH` set)
- Try Docker (if available)
- Fall back to stub mode (tests skip gracefully)

**No action needed** - tests handle missing server gracefully.

---

## Test Behavior

### When Soulfind Available
- ✅ Protocol contract tests run against real server
- ✅ Tests actual protocol compliance
- ✅ Can test keepalive, reconnection, etc.

### When Soulfind Not Available (Stub Mode)
- ⚠️ Protocol contract tests skip gracefully
- ✅ Other integration tests still run
- ✅ No test failures

---

## Files

- `tests/slskd.Tests.Integration/Harness/SoulfindRunner.cs` - Multi-strategy implementation
- `tests/slskd.Tests.Integration/Protocol/ProtocolContractTests.cs` - Tests that use it
- `docs/dev/soulseek-server-simulation-options.md` - Detailed documentation

---

## Status

✅ **Implemented**: Multi-strategy SoulfindRunner  
✅ **Implemented**: Graceful fallback to stub mode  
✅ **Documented**: All options and setup instructions  

**Next Steps** (optional):
- Install Soulfind binary for local dev to enable protocol tests
- Create/publish Soulfind Docker image for CI
- Or continue using stub mode (tests skip, no failures)
