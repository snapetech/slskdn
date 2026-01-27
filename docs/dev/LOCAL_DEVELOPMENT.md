# Local Development Guide

> **Quick reference for local development workflows**

---

## Build and Test

### Standard Build (with tests)

```bash
./bin/build
```

This will:
- Build web frontend (with tests)
- Build .NET backend (with tests)
- **Automatically check for Soulfind Docker image** (if Docker available)
- Run all tests (unit, integration, protocol contract)

### Build Only (skip tests)

```bash
./bin/build --skip-tests
```

### Web Only

```bash
./bin/build --web-only
```

### .NET Only

```bash
./bin/build --dotnet-only
```

---

## Protocol Contract Tests

### What They Are

Protocol contract tests verify compliance with the Soulseek protocol:
- Login and handshake
- Keepalive pings
- Disconnect and reconnection

**Category**: `L1-Protocol`

### Running Protocol Tests

```bash
# Run all integration tests (includes protocol tests)
dotnet test tests/slskd.Tests.Integration

# Run only protocol contract tests
dotnet test --filter Category=L1-Protocol
```

### Enabling Protocol Tests

Protocol tests require a Soulseek server simulator (Soulfind). The build script automatically tries to set this up:

**Option 1: Docker (Automatic)**
```bash
# Build script will automatically pull image if Docker is available
./bin/build
```

**Option 2: Docker (Manual)**
```bash
# Pull image manually
docker pull ghcr.io/soulfind-dev/soulfind:latest

# Run tests
dotnet test --filter Category=L1-Protocol
```

**Option 3: Binary**
```bash
# Download from https://github.com/soulfind-dev/soulfind/releases
# Or build from source

# Set path
export SOULFIND_PATH=/path/to/soulfind

# Run tests
dotnet test --filter Category=L1-Protocol
```

**Option 4: Skip (Default)**
```bash
# No setup needed - tests skip gracefully
dotnet test --filter Category=L1-Protocol
```

### Test Behavior

- ✅ **If Soulfind available**: Protocol tests run against real server
- ⚠️ **If Soulfind unavailable**: Tests skip gracefully (no failure)

---

## Watch Mode (Development)

```bash
./bin/watch
```

Starts the application in watch mode for hot-reload development.

---

## Linting

```bash
# Lint code
./bin/lint

# Lint documentation
./bin/lint-docs
```

---

## Coverage

```bash
./bin/cover
```

Generates code coverage reports.

---

## Running the Application

### Development Mode

```bash
# Start with watch mode
./bin/watch

# Or run directly
cd src/slskd
dotnet run
```

### Production Build

```bash
# Build release
./bin/build

# Run
cd src/slskd
dotnet run --configuration Release
```

---

## Troubleshooting

### Protocol Tests Skip

**Cause**: Soulfind not available

**Solutions**:
1. Install Docker and pull image: `docker pull ghcr.io/soulfind-dev/soulfind:latest`
2. Install Soulfind binary and set `SOULFIND_PATH`
3. Continue without setup (tests skip, no failure)

**Impact**: Tests skip gracefully - no build failure

### Docker Not Available

**Impact**: Build script will skip Docker pull, tests use stub mode

**Solution**: Install Docker or use binary option

---

## References

- `docs/dev/SOULFIND_CI_INTEGRATION.md` - Soulfind setup details
- `docs/dev/soulseek-server-simulation-options.md` - All simulation options
- `bin/build` - Build script source
- `tests/slskd.Tests.Integration/README.md` - Integration test details
