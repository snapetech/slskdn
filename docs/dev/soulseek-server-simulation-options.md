# Soulseek Server Simulation Options for Testing

> **Purpose**: Enable protocol contract tests and integration tests that require a Soulseek server  
> **Status**: Options documented, implementation in progress

---

## Problem

The `ProtocolContractTests` (3 failing tests) require a Soulseek server to test protocol-level compliance:
- `Should_Login_And_Handshake` - Verify login and handshake protocol
- `Should_Send_Keepalive_Pings` - Verify keepalive mechanism
- `Should_Handle_Disconnect_And_Reconnect` - Verify reconnection logic

Currently, `SoulfindRunner` is a stub that doesn't actually start a server.

---

## Available Options

### Option 1: Soulfind Binary (Recommended for Local Dev)

**Soulfind** is an open-source Soulseek server implementation that can be used as a test harness.

#### Setup

1. **Install Soulfind**:
   ```bash
   # Option A: Build from source (if available)
   git clone <soulfind-repo>
   cd soulfind
   # Build instructions from Soulfind repo
   
   # Option B: Install from package manager (if available)
   # Check Soulfind documentation for installation
   ```

2. **Set Environment Variable**:
   ```bash
   export SOULFIND_PATH=/path/to/soulfind/binary
   ```

3. **SoulfindRunner will automatically discover and use it**

#### Implementation

`SoulfindRunner` already has `DiscoverSoulfindBinary()` method that:
- Checks common locations (`/usr/local/bin/soulfind`, `/usr/bin/soulfind`, etc.)
- Checks `SOULFIND_PATH` environment variable
- Uses `which`/`where` command

**Next Step**: Implement actual process startup in `SoulfindRunner.StartAsync()`.

---

### Option 2: Docker Container (Recommended for CI)

**Soulfind Docker Image** (if available) can be used in CI environments.

#### Setup

```bash
# Start Soulfind container
docker run -d --name soulfind-test -p 2242:2242 soulfind/soulfind:latest

# Run tests
dotnet test --filter Category=L1-Protocol

# Cleanup
docker rm -f soulfind-test
```

#### Implementation

`SoulfindRunner` can be enhanced to:
- Check for Docker availability
- Start/stop Docker containers
- Use Docker socket API or `docker` CLI

**Note**: Requires Docker to be available in test environment.

---

### Option 3: Mock ISoulseekClient (Recommended for Unit Tests)

**Mock the Soulseek client** instead of requiring a real server.

#### Current Usage

Many tests already use mocks:
```csharp
// From StubWebApplicationFactory.cs
.AddSingleton<ISoulseekClient>(_ => Mock.Of<ISoulseekClient>(x =>
    x.State == SoulseekClientStates.LoggedIn && x.Username == "test-user"));
```

#### Enhanced Mock for Protocol Tests

Create a more sophisticated mock that simulates protocol behavior:

```csharp
public class MockSoulseekClient : ISoulseekClient
{
    private SoulseekClientStates _state = SoulseekClientStates.Disconnected;
    private string _username = "test-user";
    
    public SoulseekClientStates State => _state;
    public string Username => _username;
    
    public async Task ConnectAsync(string address, int port, string username, string password, CancellationToken ct)
    {
        // Simulate connection delay
        await Task.Delay(100, ct);
        _state = SoulseekClientStates.Connected;
        _username = username;
    }
    
    // Implement other ISoulseekClient methods as needed
}
```

**Pros**:
- No external dependencies
- Fast test execution
- Full control over behavior
- Works in CI without Docker

**Cons**:
- Doesn't test actual protocol compliance
- May miss protocol edge cases

---

### Option 4: Skip Tests (Current Approach)

**Mark tests as skipped** if server simulator unavailable.

```csharp
[Fact(Skip = "Requires Soulseek server simulator - use Option 1, 2, or 3")]
public async Task Should_Login_And_Handshake()
{
    // ...
}
```

**Pros**:
- Tests don't fail
- Clear documentation of requirement

**Cons**:
- No protocol compliance verification
- Tests don't run

---

## Recommendation

### For Local Development
**Use Option 1 (Soulfind Binary)**:
- Install Soulfind locally
- Implement `SoulfindRunner` to start actual process
- Tests run against real server behavior

### For CI
**Use Option 3 (Mock)** or **Option 2 (Docker)**:
- **Option 3** if Docker unavailable or too complex
- **Option 2** if Docker available and Soulfind image exists
- Fallback to **Option 4** (skip) if neither available

### Hybrid Approach
1. **Try Option 1** (Soulfind binary) - if available, use it
2. **Try Option 2** (Docker) - if binary not available, try Docker
3. **Fallback to Option 3** (Mock) - if neither available, use enhanced mock
4. **Skip if all fail** - document requirement

---

## Implementation Plan

### Phase 1: Enhance SoulfindRunner (Option 1)

1. Implement actual process startup:
   ```csharp
   public async Task StartAsync(CancellationToken ct = default)
   {
       var binaryPath = DiscoverSoulfindBinary();
       if (string.IsNullOrEmpty(binaryPath))
       {
           logger.LogWarning("[TEST-SOULFIND] Soulfind binary not found - tests will use mocks");
           port = AllocateEphemeralPort();
           isRunning = false; // Indicates stub mode
           return;
       }
       
       port = AllocateEphemeralPort();
       var psi = new ProcessStartInfo
       {
           FileName = binaryPath,
           Arguments = $"--port {port} --data-dir {tempDataDir}",
           UseShellExecute = false,
           RedirectStandardOutput = true,
           RedirectStandardError = true
       };
       
       soulfindProcess = Process.Start(psi);
       await WaitForReadinessAsync(ct);
       isRunning = true;
   }
   ```

2. Add cleanup in `DisposeAsync()`:
   ```csharp
   if (soulfindProcess != null && !soulfindProcess.HasExited)
   {
       soulfindProcess.Kill();
       soulfindProcess.WaitForExit(5000);
   }
   ```

### Phase 2: Docker Support (Option 2)

1. Add Docker detection:
   ```csharp
   private async Task<bool> TryStartDockerContainerAsync(CancellationToken ct)
   {
       // Check if Docker is available
       // Start container: docker run -d -p {port}:2242 soulfind/soulfind:latest
       // Wait for readiness
   }
   ```

### Phase 3: Enhanced Mock (Option 3)

1. Create `MockSoulseekClient` class with protocol simulation
2. Use in `SlskdnTestClient` when `SoulfindRunner` unavailable
3. Simulate connection states, keepalive, etc.

---

## Current Status

- ✅ `SoulfindRunner` exists but is a stub
- ✅ `DiscoverSoulfindBinary()` implemented (not used)
- ✅ Mock `ISoulseekClient` used in other tests
- ⬜ Actual Soulfind process startup not implemented
- ⬜ Docker support not implemented
- ⬜ Enhanced mock for protocol tests not implemented

---

## References

- `docs/dev/soulfind-integration-notes.md` - Soulfind usage guidelines
- `tests/slskd.Tests.Integration/Harness/SoulfindRunner.cs` - Current implementation
- `tests/slskd.Tests.Integration/Protocol/ProtocolContractTests.cs` - Failing tests
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs` - Mock usage example
