# Integration Test Configuration

## Test Categories

Tests are organized by levels (L0-L3) for CI/CD flexibility:

### L0: Unit Tests
- Fast, isolated, no external dependencies
- Run on every commit
- Example: Model validation, business logic

### L1: Protocol Contract Tests
- Test basic Soulseek protocol compliance
- Require Soulfind binary
- Run on PR builds
- Trait: `[Trait("Category", "L1-Protocol")]`

### L2: Multi-Client Integration Tests
- Test multi-node interactions (Alice/Bob/Carol topology)
- Require Soulfind + multiple slskdn instances
- Run on nightly builds
- Trait: `[Trait("Category", "L2-MultiClient")]`

### L3: Disaster Mode & Mesh Tests
- Test disaster scenarios, pure mesh operation
- Require full test infrastructure
- Run weekly or on-demand
- Traits: `[Trait("Category", "L3-DisasterMode")]`, `[Trait("Category", "L3-MeshOnly")]`

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Category
```bash
# L1 protocol tests only
dotnet test --filter "Category=L1-Protocol"

# L2 multi-client tests
dotnet test --filter "Category=L2-MultiClient"

# L3 disaster mode tests
dotnet test --filter "Category=L3-DisasterMode"

# L3 mesh-only tests
dotnet test --filter "Category=L3-MeshOnly"
```

### Run Without Soulfind
```bash
# Skip tests that require Soulfind
dotnet test --filter "Category!=L1-Protocol&Category!=L2-MultiClient&Category!=L3-DisasterMode"
```

## CI/CD Configuration

### GitHub Actions

```yaml
name: Integration Tests

on:
  pull_request:
  schedule:
    - cron: '0 2 * * *'  # Nightly at 2 AM

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - name: Run unit tests (L0)
        run: dotnet test --filter "Category!=L1-Protocol&Category!=L2-MultiClient&Category!=L3-DisasterMode&Category!=L3-MeshOnly"

  protocol-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Install Soulfind
        run: |
          wget https://github.com/slskd/soulfind/releases/latest/download/soulfind-linux-x64
          chmod +x soulfind-linux-x64
          sudo mv soulfind-linux-x64 /usr/local/bin/soulfind
      - name: Run protocol tests (L1)
        run: dotnet test --filter "Category=L1-Protocol"
        env:
          SOULFIND_PATH: /usr/local/bin/soulfind

  integration-tests:
    runs-on: ubuntu-latest
    if: github.event_name == 'schedule'  # Nightly only
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Install Soulfind
        run: |
          wget https://github.com/slskd/soulfind/releases/latest/download/soulfind-linux-x64
          chmod +x soulfind-linux-x64
          sudo mv soulfind-linux-x64 /usr/local/bin/soulfind
      - name: Run multi-client tests (L2)
        run: dotnet test --filter "Category=L2-MultiClient"
        env:
          SOULFIND_PATH: /usr/local/bin/soulfind
      - name: Run disaster mode tests (L3)
        run: dotnet test --filter "Category=L3-DisasterMode|Category=L3-MeshOnly"
```

## Environment Variables

- `SOULFIND_PATH`: Path to Soulfind binary (auto-discovered if not set)
- `SLSKDN_TEST_TIMEOUT`: Test timeout in seconds (default: 300)
- `SLSKDN_TEST_LOG_LEVEL`: Logging level (Debug, Information, Warning, Error)

## Test Data

Test fixtures are generated dynamically:
- Audio files: See `AudioFixtures.cs`
- MusicBrainz data: See `MusicBrainzFixtures.cs`
- No large binary files committed to repo

## Debugging Tests

### Enable Debug Logging
```bash
export SLSKDN_TEST_LOG_LEVEL=Debug
dotnet test --filter "Category=L1-Protocol" --logger "console;verbosity=detailed"
```

### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~Should_Login_And_Handshake"
```

### Keep Test Artifacts
Test harness creates temporary directories under `/tmp/slskdn-test/`.
To keep artifacts for debugging, set:
```bash
export SLSKDN_TEST_KEEP_ARTIFACTS=1
```

## Troubleshooting

### Soulfind Not Found
1. Check `SOULFIND_PATH` environment variable
2. Install from: https://github.com/slskd/soulfind
3. Ensure binary is executable: `chmod +x soulfind`

### Port Already in Use
Tests use ephemeral ports, but if conflicts occur:
```bash
# Kill any lingering Soulfind processes
pkill -9 soulfind
```

### Tests Timeout
Increase timeout:
```bash
export SLSKDN_TEST_TIMEOUT=600
```

### Flaky Tests
L2/L3 tests involve network timing and may be flaky. Retry:
```bash
dotnet test --filter "Category=L2-MultiClient" -- RunConfiguration.MaxCpuCount=1
```
