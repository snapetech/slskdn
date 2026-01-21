# T-SF04 Implementation Brief: Local HTTP/WebSocket Gateway for Mesh Services

**Repository**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Prerequisites**: T-SF01 (descriptors), T-SF02 (router/client), T-SF03 (service adapters) completed  
**Status**: 📋 Ready for Implementation  
**Created**: December 11, 2025

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## CRITICAL: SCOPE CONSTRAINTS

### WHAT YOU MUST DO IN T-SF04:

1. Implement HTTP endpoints in the existing web host that:
   - Take HTTP requests from localhost
   - Resolve a mesh service via `IMeshServiceDirectory`
   - Call it via `IMeshServiceClient`
   - Return the result as an HTTP response

2. Optionally add **WebSocket bridging** for long-lived streams (only if clean and supported)

3. Introduce configuration options to:
   - Enable/disable the gateway
   - Restrict addresses, ports, and allowed services
   - Configure size/time limits

### WHAT YOU MUST NOT DO IN T-SF04:

- ❌ Change the semantics of existing public HTTP APIs
- ❌ Expose the gateway to non-localhost interfaces by default
- ❌ Invent new mesh services; just call existing ones
- ❌ Touch DHT keyspaces or the core service fabric design
- ❌ Break existing functionality

**Your changes must compile, pass existing tests, and preserve current behavior.**

---

## 1. REQUIRED RECONNAISSANCE

**BEFORE touching anything**, you MUST locate and understand:

### 1.1. Web Host / API Project

Find:
- Which project exposes HTTP endpoints today
- How controllers/endpoints are defined (ASP.NET controllers, minimal APIs, etc.)
- How Kestrel (or equivalent) is configured: URLs, bindings, TLS, etc.

Questions to answer:
- What's the web project structure?
- How are routes defined?
- What's the existing API pattern?

### 1.2. Auth / Access Control

Find:
- Whether there is any existing authentication/authorization layer
- How configuration is loaded (appsettings, yml, env vars, etc.)

Questions to answer:
- Is there existing auth middleware?
- How do existing endpoints handle access control?
- What's the config pattern?

### 1.3. Service Fabric Wiring

Find:
- Where `IMeshServiceDirectory`, `IMeshServiceClient`, and `MeshServiceRouter` are registered
- How you can inject them into controllers

Questions to answer:
- Where are services registered in DI?
- How do I inject services into controllers?
- What's the service lifetime pattern?

**DO NOT proceed until you have clear answers to these questions.**

---

## 2. HIGH-LEVEL DESIGN

You are building a **local gateway** that acts as a "translator":
- HTTP request → `ServiceCall`
- `ServiceReply` → HTTP response

### Key Properties:

**Local by default**:
- Bind to `127.0.0.1` only unless explicitly configured otherwise

**Opt-in**:
- Gateway is disabled by default, must be turned on in config

**Restricted**:
- Only calls whitelisted services/methods
- Enforces size/time limits

**Privacy-aware**:
- Minimal logging
- No payload dumps by default

---

## 3. CONFIGURATION SURFACE

Introduce config options (naming to match existing style):

```jsonc
{
  "MeshGateway": {
    "Enabled": false,
    "BindAddress": "127.0.0.1",
    "Port": 0,                      // 0 = reuse existing port / no extra listener
    "AllowedServices": [
      "pods",
      "shadow-index",
      "mesh-introspect"
    ],
    "MaxRequestBodyBytes": 1048576,  // 1MB
    "RequestTimeoutSeconds": 30,
    "LogBodies": false,              // DO NOT enable in production
    "EnableRateLimiting": true,
    "MaxRequestsPerMinute": 100      // Per IP address
  }
}
```

### Requirements:

**Default config**:
- `Enabled = false`
- `BindAddress` must be loopback if you spin a separate listener
- If you share the main listener, enforce access control in code

**AllowedServices**:
- Only services listed here may be invoked via the gateway
- Empty list = no services allowed

**MaxRequestBodyBytes**:
- Used both at the HTTP layer (model binding/body reader)
- Enforced again before constructing `ServiceCall`

**LogBodies**:
- Default `false`
- If enabled, log with care and clearly mark as unsafe for production

Wire this neatly into whatever configuration system you already have; **no ad-hoc static reads**.

---

## 4. HTTP GATEWAY ENDPOINTS

### 4.1. Route Design

Add a controller or set of endpoints such as:

```
POST /mesh/http/{serviceName}/{**path}
GET  /mesh/http/{serviceName}/{**path}
```

Or use a prefix (`/api/mesh` or similar) to avoid collisions; follow existing API style.

### 4.2. Request → ServiceCall Mapping

For each incoming HTTP request:

#### Step 1: Gateway Guard

```csharp
// 1. Check if gateway enabled
if (!gatewayConfig.Enabled)
{
    return NotFound(); // Or Forbid(), document your choice
}

// 2. Check remote address
if (!IsLocalhost(HttpContext.Connection.RemoteIpAddress) && !gatewayConfig.AllowRemote)
{
    return Forbid();
}

// 3. Check service allowlist
if (!gatewayConfig.AllowedServices.Contains(serviceName))
{
    return Forbid();
}
```

#### Step 2: Body and Size

```csharp
// Enforce MaxRequestBodyBytes
Request.Body.MaxRequestBodySize = gatewayConfig.MaxRequestBodyBytes;

// Read body with size check
var body = await ReadBodyAsync(Request.Body, gatewayConfig.MaxRequestBodyBytes);
if (body.Length > gatewayConfig.MaxRequestBodyBytes)
{
    return StatusCode(413, new { error = "Request body too large" });
}
```

#### Step 3: Payload Schema

Define a small, generic HTTP call envelope to put into `ServiceCall.Payload`:

```csharp
public class HttpCallEnvelope
{
    public string HttpMethod { get; init; }           // GET, POST, etc.
    public string Path { get; init; }                 // Path portion after serviceName
    public Dictionary<string, string[]> Query { get; init; }
    public Dictionary<string, string[]> Headers { get; init; }
    public string Body { get; init; }                 // Base64 or raw depending on content type
}
```

**Constraints**:
- Keep this envelope minimal and consistent
- Do not shove full HTTP objects

**Example**:
```csharp
var envelope = new HttpCallEnvelope
{
    HttpMethod = Request.Method,
    Path = path,
    Query = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
    Headers = Request.Headers
        .Where(h => IsAllowedHeader(h.Key))
        .ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
    Body = Convert.ToBase64String(body)
};
```

#### Step 4: ServiceCall Construction

```csharp
var serviceCall = new ServiceCall
{
    ServiceId = serviceName,  // Or derive actual ID if that's your pattern
    Method = "HttpInvoke",    // Or more specific method names
    CorrelationId = Guid.NewGuid().ToString(),
    Payload = serializer.Serialize(envelope)
};
```

### 4.3. Service Resolution

```csharp
// 1. Find service descriptors
var descriptors = await serviceDirectory.FindByNameAsync(serviceName, cancellationToken);

if (descriptors == null || !descriptors.Any())
{
    return StatusCode(503, new { error = "Service not available" });
}

// 2. Filter descriptors (optional)
var descriptor = SelectDescriptor(descriptors);
// Options:
// - Prefer local services for debugging/performance
// - Prefer higher-reputation peers if reputation data exists
// - Round-robin or random selection

// 3. Call the service
try
{
    using var cts = new CancellationTokenSource(
        TimeSpan.FromSeconds(gatewayConfig.RequestTimeoutSeconds));
    
    var reply = await meshServiceClient.CallAsync(
        descriptor,
        serviceCall.Method,
        serviceCall.Payload,
        cts.Token);
    
    return MapReplyToHttpResponse(reply);
}
catch (TimeoutException)
{
    return StatusCode(504, new { error = "Gateway timeout" });
}
catch (Exception ex)
{
    logger.LogError(ex, "Mesh call failed: {ServiceName}", serviceName);
    return StatusCode(502, new { error = "Mesh call failed" });
}
```

---

## 5. ServiceReply → HTTP Response

When you receive a `ServiceReply`:

### Step 1: Map StatusCode

```csharp
private IActionResult MapReplyToHttpResponse(ServiceReply reply)
{
    // Map service status code to HTTP status code
    var httpStatusCode = reply.StatusCode switch
    {
        0 => 200,           // OK
        400 => 400,         // Bad Request
        401 => 401,         // Unauthorized
        403 => 403,         // Forbidden
        404 => 404,         // Not Found
        413 => 413,         // Payload Too Large
        429 => 429,         // Too Many Requests
        500 => 500,         // Internal Server Error
        504 => 504,         // Gateway Timeout
        _ => 502            // Bad Gateway (unknown status)
    };

    // Deserialize payload
    object responseBody;
    try
    {
        responseBody = serializer.Deserialize(reply.Payload);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to deserialize service reply payload");
        responseBody = new { error = "Invalid response from service" };
        httpStatusCode = 502;
    }

    return StatusCode(httpStatusCode, responseBody);
}
```

### Step 2: Payload

- Deserialize using the same schema used by the target service
- If the gateway is purely generic, you might just forward the payload as JSON

### Step 3: Headers

For T-SF04, keep it simple:
- Content-Type: application/json (or whatever the target service expects)
- Do NOT blindly pass through peer-provided headers without filtering

```csharp
Response.ContentType = "application/json";
// Only add safe, known headers
```

### Step 4: Error Handling

Wrap exceptions in a safe, generic error response:

```csharp
catch (Exception ex)
{
    // Log the internal exception with stack trace server-side
    logger.LogError(ex, "Gateway error: {ServiceName}.{Method}", 
        serviceName, method);
    
    // Return safe error to client (NO stack trace)
    return StatusCode(502, new 
    { 
        error = "mesh_call_failed",
        details = "An error occurred while calling the mesh service"
    });
}
```

---

## 6. SECURITY, PRIVACY, HARDENING

**This gateway is a potential footgun if not locked down.** You MUST:

### 6.1. Local-Only by Default

Ensure the gateway is only accessible from localhost unless:
- Config explicitly allows remote access, AND
- You verify this is the intended behavior with strong docs/comments

```csharp
private bool IsLocalhost(IPAddress? remoteAddress)
{
    if (remoteAddress == null) return false;
    
    return IPAddress.IsLoopback(remoteAddress) ||
           remoteAddress.Equals(IPAddress.Parse("127.0.0.1")) ||
           remoteAddress.Equals(IPAddress.IPv6Loopback);
}
```

### 6.2. Service Allowlist

Enforce `AllowedServices` strictly:

```csharp
if (!gatewayConfig.AllowedServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase))
{
    logger.LogWarning(
        "Gateway: rejected call to non-allowed service: {ServiceName} from {IP}",
        serviceName, HttpContext.Connection.RemoteIpAddress);
    return Forbid();
}
```

### 6.3. Rate Limiting

If there is an existing rate-limit or guard subsystem for HTTP APIs, plug into it:

```csharp
// Example using existing rate limiter
if (rateLimiter.IsRateLimited(remoteIp, "mesh-gateway"))
{
    return StatusCode(429, new { error = "Rate limit exceeded" });
}
```

If not, implement basic per-IP/per-service request counting:

```csharp
// Simple in-memory rate limiter (production should use distributed cache)
private readonly ConcurrentDictionary<string, RateLimitCounter> _rateLimits = new();

private bool CheckRateLimit(string key, int maxRequests, TimeSpan window)
{
    var counter = _rateLimits.GetOrAdd(key, _ => new RateLimitCounter());
    return counter.TryIncrement(maxRequests, window);
}
```

### 6.4. Size and Timeout Limits

Enforce at multiple layers:

```csharp
// 1. ASP.NET level
[RequestSizeLimit(1048576)] // 1MB
public async Task<IActionResult> InvokeService(...)

// 2. Manual check
if (body.Length > gatewayConfig.MaxRequestBodyBytes)
{
    return StatusCode(413);
}

// 3. Timeout at client level
using var cts = new CancellationTokenSource(
    TimeSpan.FromSeconds(gatewayConfig.RequestTimeoutSeconds));
```

### 6.5. No Sensitive Logging by Default

```csharp
// DO log:
logger.LogInformation(
    "Gateway: {Method} {ServiceName}/{Path} -> {Status} ({Duration}ms)",
    httpMethod, serviceName, path, statusCode, elapsed.TotalMilliseconds);

// DO NOT log (unless LogBodies == true):
// - Full request/response bodies
// - Headers (except safe ones like Content-Type)
// - Query parameters (may contain tokens/secrets)

// If LogBodies enabled, log with warnings:
if (gatewayConfig.LogBodies)
{
    logger.LogWarning(
        "Gateway [VERBOSE]: Request body (UNSAFE): {Body}",
        Encoding.UTF8.GetString(body.Take(1000).ToArray())); // Truncate
}
```

### 6.6. Auth (Optional, Future-Extensible)

If the project already has API keys / tokens / auth, integrate:

```csharp
[Authorize] // Or custom attribute
public async Task<IActionResult> InvokeService(...)
```

If not, design the gateway such that adding auth later is straightforward:

```csharp
// Add auth check at the beginning
if (!await ValidateAuthAsync(Request))
{
    return Unauthorized();
}
```

---

## 7. OPTIONAL: WEBSOCKET GATEWAY

**Only attempt WebSocket support if**:
- The existing project uses ASP.NET Core with WS already configured, AND
- The mesh transport has a clean streaming primitive you can map to

### 7.1. WebSocket Route

If both are true, add:

```
GET /mesh/ws/{serviceName}/{channel}
```

### 7.2. Implementation

```csharp
[HttpGet("/mesh/ws/{serviceName}/{channel}")]
public async Task WebSocketHandler(string serviceName, string channel)
{
    // 1. Validate service allowlist
    if (!gatewayConfig.AllowedServices.Contains(serviceName))
    {
        HttpContext.Response.StatusCode = 403;
        return;
    }

    // 2. Accept WebSocket
    if (!HttpContext.WebSockets.IsWebSocketRequest)
    {
        HttpContext.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
    
    // 3. Resolve target service descriptor
    var descriptors = await serviceDirectory.FindByNameAsync(serviceName);
    if (!descriptors.Any())
    {
        await webSocket.CloseAsync(
            WebSocketCloseStatus.EndpointUnavailable,
            "Service not available",
            CancellationToken.None);
        return;
    }

    // 4. Open mesh service stream (from T-SF02)
    var descriptor = descriptors.First();
    var stream = await meshServiceClient.OpenStreamAsync(descriptor, channel);

    // 5. Bridge WebSocket ↔ mesh stream
    await BridgeWebSocketToStreamAsync(webSocket, stream, cancellationToken);
}

private async Task BridgeWebSocketToStreamAsync(
    WebSocket webSocket,
    MeshServiceStream stream,
    CancellationToken cancellationToken)
{
    // Frame size limits
    var buffer = new byte[gatewayConfig.MaxWebSocketFrameBytes];
    
    // Bidirectional forwarding
    var receiveTask = ReceiveFromWebSocketAsync(webSocket, stream, buffer, cancellationToken);
    var sendTask = SendToWebSocketAsync(webSocket, stream, cancellationToken);
    
    await Task.WhenAny(receiveTask, sendTask);
}
```

### 7.3. Hardening

- Size limits for frames
- Max concurrent WS connections
- Respect cancellation when client disconnects

```csharp
private int _activeWebSocketConnections = 0;

// Before accepting WS
if (_activeWebSocketConnections >= gatewayConfig.MaxConcurrentWebSockets)
{
    HttpContext.Response.StatusCode = 503;
    return;
}

Interlocked.Increment(ref _activeWebSocketConnections);
try
{
    // ... handle websocket ...
}
finally
{
    Interlocked.Decrement(ref _activeWebSocketConnections);
}
```

**If any of this is messy or unclear**, DO NOT force it into T-SF04. Leave WS for a later task with a clear TODO.

---

## 8. TESTING

You must add or extend tests to cover:

### 8.1. Config Gating

```csharp
[Test]
public async Task Gateway_WhenDisabled_Returns404()
{
    // Arrange
    var config = new MeshGatewayConfig { Enabled = false };
    var controller = CreateController(config);

    // Act
    var result = await controller.InvokeService("pods", "test");

    // Assert
    Assert.IsInstanceOf<NotFoundResult>(result);
}

[Test]
public async Task Gateway_WhenEnabled_IsReachable()
{
    // Arrange
    var config = new MeshGatewayConfig { Enabled = true, AllowedServices = ["pods"] };
    var controller = CreateController(config);

    // Act & Assert
    // Should not return 404
}
```

### 8.2. Service Allowlist

```csharp
[Test]
public async Task Gateway_RejectsNonAllowedService()
{
    // Arrange
    var config = new MeshGatewayConfig 
    { 
        Enabled = true, 
        AllowedServices = ["pods"] 
    };
    var controller = CreateController(config);

    // Act
    var result = await controller.InvokeService("unauthorized-service", "test");

    // Assert
    Assert.IsInstanceOf<ForbidResult>(result);
}
```

### 8.3. Happy Path

```csharp
[Test]
public async Task Gateway_HappyPath_ReturnsSuccess()
{
    // Arrange
    var mockDirectory = new Mock<IMeshServiceDirectory>();
    mockDirectory.Setup(d => d.FindByNameAsync("pods", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { CreateTestDescriptor("pods") });
    
    var mockClient = new Mock<IMeshServiceClient>();
    mockClient.Setup(c => c.CallAsync(It.IsAny<MeshServiceDescriptor>(), 
                                       It.IsAny<string>(), 
                                       It.IsAny<ReadOnlyMemory<byte>>(), 
                                       It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ServiceReply 
              { 
                  StatusCode = 0, 
                  Payload = Serialize(new { result = "success" }) 
              });

    var controller = CreateController(mockDirectory.Object, mockClient.Object);

    // Act
    var result = await controller.InvokeService("pods", "test");

    // Assert
    var okResult = Assert.IsInstanceOf<OkObjectResult>(result);
    Assert.AreEqual(200, okResult.StatusCode);
}
```

### 8.4. Limits and Errors

```csharp
[Test]
public async Task Gateway_OversizedBody_Returns413()
{
    // Test that oversized request body is rejected
}

[Test]
public async Task Gateway_Timeout_Returns504()
{
    // Mock client that times out
    var mockClient = new Mock<IMeshServiceClient>();
    mockClient.Setup(c => c.CallAsync(...))
              .ThrowsAsync(new TimeoutException());

    var controller = CreateController(mockClient: mockClient.Object);

    // Act
    var result = await controller.InvokeService("pods", "test");

    // Assert
    var statusResult = Assert.IsInstanceOf<ObjectResult>(result);
    Assert.AreEqual(504, statusResult.StatusCode);
}

[Test]
public async Task Gateway_ClientError_Returns502WithSafeError()
{
    // Mock client that throws exception
    var mockClient = new Mock<IMeshServiceClient>();
    mockClient.Setup(c => c.CallAsync(...))
              .ThrowsAsync(new Exception("Internal error"));

    var controller = CreateController(mockClient: mockClient.Object);

    // Act
    var result = await controller.InvokeService("pods", "test");

    // Assert
    var statusResult = Assert.IsInstanceOf<ObjectResult>(result);
    Assert.AreEqual(502, statusResult.StatusCode);
    
    var body = statusResult.Value as dynamic;
    Assert.IsNotNull(body);
    Assert.DoesNotContain("Internal error", body.ToString()); // Should not leak internal details
}
```

Tests should follow existing patterns (xUnit/NUnit/etc.) and live alongside other web API tests.

---

## 9. ANTI-SLOP CHECKLIST FOR T-SF04

Before you consider T-SF04 done, check:

### 9.1. Scope Control

- [ ] Only HTTP gateway (and optional WS) was touched
- [ ] Core service fabric, DHT, and mesh routing weren't redesigned
- [ ] No changes to existing HTTP APIs

### 9.2. Safety

- [ ] Gateway is disabled by default (`Enabled = false`)
- [ ] Binding is localhost only by default
- [ ] Service allowlist is enforced
- [ ] Body size limits exist and are wired to config
- [ ] Timeout limits exist and are wired to config
- [ ] Rate limiting is implemented (or integrated with existing system)

### 9.3. Code Quality

- [ ] No `.Result` / `.Wait()` on async calls
- [ ] DI and logging use existing patterns
- [ ] No giant controllers full of business logic
- [ ] Mapping logic is small and tidy
- [ ] Error handling is robust

### 9.4. Privacy

- [ ] No full body logging by default
- [ ] No injection of PII into responses or logs
- [ ] Safe error messages (no stack traces to clients)
- [ ] `LogBodies` option clearly marked as unsafe

### 9.5. Documentation

- [ ] XML docs or summary comments for new controller endpoints
- [ ] Config keys documented
- [ ] Security considerations documented in code comments
- [ ] README or docs updated with gateway usage examples

---

## 10. IMPLEMENTATION SEQUENCE

**Recommended order of implementation**:

1. **Reconnaissance** (Section 1)
   - Document findings about web host, auth, and service wiring

2. **Configuration** (Section 3)
   - Add `MeshGatewayConfig` class
   - Wire up configuration in existing pattern
   - Add validation

3. **HTTP endpoints** (Section 4)
   - Create controller or minimal API endpoints
   - Implement gateway guards
   - Implement request → ServiceCall mapping
   - Implement service resolution
   - Implement ServiceReply → HTTP response mapping

4. **Security hardening** (Section 6)
   - Add localhost check
   - Add allowlist enforcement
   - Add rate limiting
   - Add size/timeout limits
   - Implement safe logging

5. **WebSocket support** (Section 7) - OPTIONAL
   - Only if clean and supported
   - Otherwise, skip with TODO marker

6. **Testing** (Section 8)
   - Add config gating tests
   - Add allowlist tests
   - Add happy path tests
   - Add error/limit tests
   - Verify no regressions

7. **Documentation**
   - XML docs for public APIs
   - Code comments for security logic
   - Update README with usage examples

---

## 11. SUCCESS CRITERIA

T-SF04 is complete when:

- [ ] Reconnaissance complete: web host, auth, and service fabric wiring documented
- [ ] `MeshGatewayConfig` implemented with all required options
- [ ] HTTP endpoints implemented: `POST/GET /mesh/http/{serviceName}/{**path}`
- [ ] Gateway guards implemented: enabled check, localhost check, allowlist check
- [ ] Request → ServiceCall mapping implemented with safe serialization
- [ ] Service resolution implemented via `IMeshServiceDirectory`
- [ ] `IMeshServiceClient` integration working
- [ ] ServiceReply → HTTP response mapping implemented
- [ ] Security hardening complete: localhost-only, allowlist, rate limits, size limits
- [ ] Safe error handling with no internal details leaked
- [ ] Privacy-conscious logging (no bodies by default)
- [ ] WebSocket support implemented (if feasible) OR TODO marker added
- [ ] Unit tests pass for all gateway functionality
- [ ] Integration tests pass (end-to-end HTTP → mesh service)
- [ ] All existing tests still pass (no regressions)
- [ ] XML docs present for public APIs
- [ ] Anti-slop checklist satisfied
- [ ] README/docs updated with usage examples

---

## 12. USAGE EXAMPLES (FOR DOCUMENTATION)

### Example 1: Enable Gateway (appsettings.json)

```jsonc
{
  "MeshGateway": {
    "Enabled": true,
    "AllowedServices": ["pods", "shadow-index"]
  }
}
```

### Example 2: Call Pods Service

```bash
# Join a pod
curl -X POST http://localhost:5030/mesh/http/pods/join \
  -H "Content-Type: application/json" \
  -d '{"podId": "general"}'

# Post a message
curl -X POST http://localhost:5030/mesh/http/pods/post \
  -H "Content-Type: application/json" \
  -d '{"podId": "general", "content": "Hello world"}'

# Fetch messages
curl -X GET http://localhost:5030/mesh/http/pods/messages?podId=general&limit=50
```

### Example 3: Call Shadow Index Service

```bash
# Look up by MBID
curl -X GET "http://localhost:5030/mesh/http/shadow-index/lookup?mbid=<mbid>"

# Register track location
curl -X POST http://localhost:5030/mesh/http/shadow-index/register \
  -H "Content-Type: application/json" \
  -d '{"mbid": "<mbid>", "trackId": "<track-id>"}'
```

### Example 4: Get Mesh Stats

```bash
curl -X GET http://localhost:5030/mesh/http/mesh-introspect/status
```

---

## 13. COMMIT MESSAGE

When T-SF04 is complete, commit with:

```
feat(gateway): add local HTTP/WebSocket gateway for mesh services (T-SF04)

Implements:
- HTTP endpoints: POST/GET /mesh/http/{serviceName}/{**path}
- Request → ServiceCall → HTTP response mapping
- Service resolution via IMeshServiceDirectory
- IMeshServiceClient integration for outbound calls
- Security: localhost-only, service allowlist, rate limiting
- Privacy: safe logging, no body dumps by default
- Configuration: MeshGatewayConfig with enable/disable and limits
- Optional WebSocket support: /mesh/ws/{serviceName}/{channel}
- Comprehensive unit and integration tests

Gateway is disabled by default and localhost-only for security.
External apps can now access mesh services via HTTP.

Part of service fabric initiative (T-SF04).
Depends on T-SF01 (descriptors), T-SF02 (router/client), T-SF03 (adapters).
```

---

## 14. WHAT COMES NEXT

After T-SF04 is merged:

**T-SF05** will:
- Security & abuse review: tighten limits, reputation hooks, logging
- Comprehensive security audit of all service/gateway paths

**T-SF06** will:
- Developer docs & examples: "how to build a mesh service"
- Sample external scripts that call the gateway

**T-SF07** will:
- Observability: metrics counters for service call rates, failures, latency
- Dashboard/monitoring for fabric health

**DO NOT implement T-SF05/06/07 in this task.**

---

## 15. FINAL REMINDERS

- **Read the existing code first** – understand web host structure and patterns
- **Localhost-only by default** – this is critical for security
- **Service allowlist** – enforce strictly, no exceptions
- **Safe error messages** – never leak internal details to clients
- **Test thoroughly** – unit tests, integration tests, security tests
- **Document clearly** – XML docs, code comments, usage examples
- **Review the anti-slop checklist** – before committing

---

**Implement T-SF04 following these constraints. Create a secure, privacy-conscious local gateway that enables external apps to access mesh services without compromising the system. Once complete and tested, the service fabric will be fully usable from external applications.**

---

*Last Updated: December 11, 2025*  
*Branch: experimental/multi-source-swarm*  
*Status: 📋 Ready for Implementation*
