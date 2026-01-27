# E2E Local Startup Hang Analysis

## Problem Summary

E2E tests are failing because the server startup hangs - the health endpoint (`/health`) never responds within the 60-second timeout, even though the process appears to be running.

## Previous Investigation Attempts (Likely Wrong Tree)

Based on the codebase and gotchas, previous attempts likely focused on:

1. **Timeout values** - Increased timeouts, but the server never responds
2. **Process crashes** - Checked for early exits, but process stays alive
3. **Share initialization** - Fixed with `--force-share-scan`, but issue persists
4. **Port conflicts** - Checked for port binding issues
5. **Mutex conflicts** - Fixed per-app-directory mutex, but issue persists

## Alternative Root Causes to Investigate

### 1. **Health Check Itself is Hanging** ⚠️ MOST LIKELY

The `/health` endpoint runs two health checks:
- `SecurityHealthCheck` - Synchronous, should be fast
- `MeshHealthCheck` - **Calls `_statsCollector.GetStatsAsync()` which could hang**

**Evidence**:
- `MeshHealthCheck.CheckHealthAsync()` calls:
  - `_statsCollector.GetStatsAsync()` - async operation
  - Accesses `_directory` and `_dhtClient` - might not be initialized yet

**Why this could hang**:
- Mesh services might not be initialized when health check runs
- `GetStatsAsync()` might be waiting on a lock or uninitialized service
- DHT client might be trying to connect to something that never responds

**How to verify**:
```bash
# Check if health endpoint responds without mesh health check
# Temporarily disable mesh health check in Program.cs line 2348
# Or check logs for mesh-related errors during startup
```

**Fix options**:
1. Make health checks timeout-aware (ASP.NET health checks have timeout support)
2. Make mesh health check defensive (catch exceptions, return degraded if services not ready)
3. Delay mesh health check registration until mesh services are initialized
4. Use a simpler health endpoint for E2E tests (bypass complex health checks)

### 2. **HTTP Server Starts But Middleware Pipeline Not Ready**

**Evidence**:
- `app.Run()` is called (line 725)
- Server might start listening before middleware pipeline is fully configured
- Health endpoint is mapped, but routing might not be active yet

**Why this could happen**:
- ASP.NET Core startup sequence: `app.Run()` starts the server, but middleware pipeline might not be ready immediately
- The health endpoint might be mapped, but routing middleware might not be active

**How to verify**:
- Check if ANY endpoint responds (try `/api/info` or root `/`)
- Check if server is actually listening: `netstat -tlnp | grep <port>`
- Check logs for "ApplicationStarted" event (line 679-681)

**Fix options**:
- Wait for `ApplicationStarted` event before considering server ready
- Use a simpler readiness check (e.g., check if port is listening)
- Add explicit startup completion signal

### 3. **Hosted Services Blocking Startup**

**Evidence**:
- `Application.StartAsync()` runs in background task (line 357)
- But `app.Run()` might wait for all hosted services to start
- If a hosted service hangs, the server might not fully start

**Why this could happen**:
- `Application.InitializeApplicationAsync()` does:
  - Share initialization (could hang if filesystem issues)
  - System clock start
  - Connection watchdog start
  - Mesh services registration (could hang)

**How to verify**:
- Check logs for which step completes last
- Check if `ApplicationStarted` event fires (indicates all hosted services started)
- Check for any blocking operations in hosted service `StartAsync()` methods

**Fix options**:
- Make hosted services non-blocking (already done for Application, but check others)
- Add timeout to hosted service startup
- Make health endpoint available before all hosted services start

### 4. **Kestrel Not Actually Listening**

**Evidence**:
- Kestrel is configured (line 560-593)
- But `app.Run()` might not actually start listening if there's an error

**Why this could happen**:
- Port binding might fail silently
- Kestrel might be waiting for something before binding
- Network interface might not be ready

**How to verify**:
```bash
# Check if port is actually listening
netstat -tlnp | grep <apiPort>
ss -tlnp | grep <apiPort>
lsof -i :<apiPort>

# Check if process is listening
ps aux | grep dotnet | grep slskd
```

**Fix options**:
- Check for port binding errors in logs
- Verify Kestrel actually starts listening
- Use explicit port binding verification

### 5. **Health Check Timeout Not Configured**

**Evidence**:
- Health checks are registered (line 2346-2348)
- But no timeout is configured
- If a health check hangs, the entire request hangs

**Why this could happen**:
- `MeshHealthCheck` calls async operations that might never complete
- No timeout means the request waits indefinitely
- The health endpoint waits for all health checks to complete

**How to verify**:
- Check ASP.NET health check timeout configuration
- See if health check requests are timing out at HTTP level vs health check level

**Fix options**:
```csharp
services.AddHealthChecks()
    .AddSecurityHealthCheck()
    .AddMeshHealthCheck()
    .AddCheckOptions(options => {
        options.Timeout = TimeSpan.FromSeconds(5); // Timeout individual checks
    });
```

### 6. **Fetch/HTTP Client Issue in Test Harness**

**Evidence**:
- Test harness uses `fetch()` to check health (line 402)
- Node.js `fetch()` might have different behavior than expected
- Network stack might not be ready

**Why this could happen**:
- `fetch()` in Node.js might have different timeout behavior
- DNS resolution might hang
- Localhost connection might be blocked

**How to verify**:
- Try using `curl` or `wget` manually to check health endpoint
- Check if `fetch()` is actually making the request
- Add more detailed logging in test harness

**Fix options**:
- Use `axios` or `node-fetch` with explicit timeout
- Add retry logic with exponential backoff
- Check network connectivity before making request

## Recommended Investigation Steps

1. **Check if server is actually listening**:
   ```bash
   # While test is running
   netstat -tlnp | grep <apiPort>
   curl -v http://127.0.0.1:<apiPort>/health
   ```

2. **Check logs for startup completion**:
   - Look for "ApplicationStarted" event
   - Look for "Host started and bound" message
   - Look for any errors in mesh initialization

3. **Temporarily disable mesh health check**:
   - Comment out `.AddMeshHealthCheck()` in Program.cs
   - See if health endpoint responds

4. **Add health check timeout**:
   - Configure health check timeout to 5 seconds
   - See if requests timeout faster

5. **Check for blocking operations**:
   - Look for any `GetAwaiter().GetResult()` calls in startup
   - Look for any synchronous I/O in health checks
   - Check for deadlocks in service initialization

6. **Verify test harness fetch behavior**:
   - Add detailed logging to fetch calls
   - Try alternative HTTP client
   - Check if fetch is actually being called

## Most Likely Root Cause

Based on the code analysis, **#1 (Health Check Hanging)** is most likely:

- `MeshHealthCheck` calls async operations that might not complete
- Mesh services might not be initialized when health check runs
- No timeout configured on health checks
- Health endpoint waits for all checks to complete

**Quick test**: Temporarily disable mesh health check and see if health endpoint responds.
