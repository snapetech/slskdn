# T-SF02 Implementation Brief: Mesh Service Routing & RPC/Streams (Hardened)

**Repository**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Prerequisites**: T-SF01 completed (MeshServiceDescriptor + IMeshServiceDirectory)  
**Status**: 📋 Ready for Implementation  
**Created**: December 11, 2025

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## CRITICAL: SCOPE CONSTRAINTS

### WHAT YOU MUST DO IN T-SF02:

1. Define service invocation contracts:
   - `IMeshService` interface
   - `ServiceCall` and `ServiceReply` DTOs
   - Supporting types: `MeshServiceContext`, `MeshServiceStream` (if needed)

2. Implement service routing:
   - **Service router** that accepts overlay messages and dispatches to `IMeshService` implementations
   - **Service client** abstraction that sends `ServiceCall`s and receives `ServiceReply`s

3. Integrate with existing systems:
   - Mesh overlay / control message pipeline
   - Security / guard / reputation components

### WHAT YOU MUST NOT DO IN T-SF02:

- ❌ Implement or modify HTTP or WebSocket gateways (that's T-SF04)
- ❌ Rewrite pods, VirtualSoulfind, or other features to use services (that's T-SF03)
- ❌ Change DHT keyspaces or `IMeshServiceDirectory` behavior (that's T-SF01)
- ❌ Introduce new external dependencies
- ❌ Break existing functionality

**Your changes must compile and preserve existing behavior.**

---

## 1. REQUIRED RECONNAISSANCE

**BEFORE writing any code**, you MUST locate and understand:

### 1.1. Overlay / Transport Layer

Find:
- QUIC/UDP transport implementation
- Control message / envelope types (e.g., `ControlEnvelope` or similar)
- Where incoming overlay messages are dispatched today
- How encryption/signing is handled

Questions to answer:
- Where do overlay messages get parsed?
- How do I add a new message type?
- How do I send a message to a specific peer?

### 1.2. Security / Guard / Reputation

Find:
- Guard / violation / reputation systems
- How they are invoked for misbehaving peers
- How banned/quarantined peers are represented

Questions to answer:
- How do I check if a peer is banned?
- How do I register a violation?
- How do I get a peer's reputation score?

### 1.3. Dependency Injection / Service Registration

Find:
- How cross-cutting services are registered (DI container, composition root)
- Where you can wire up the new router and client

Questions to answer:
- Where do I register the router and client?
- How do services get injected?
- What's the existing DI pattern?

**DO NOT proceed until you have clear answers to these questions.**

---

## 2. SERVICE CONTRACTS

### 2.1. IMeshService Interface

Define a service interface for mesh-exposed services.

**Example** (adapt naming/style to the repo):

```csharp
public interface IMeshService
{
    /// <summary>
    /// Logical service name, must be stable and match the ServiceName in MeshServiceDescriptor.
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Handles a single request/response style call.
    /// </summary>
    Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional streaming support for long-lived bidirectional flows.
    /// If the project's transport does not support this cleanly, keep it as a stub or optional.
    /// </summary>
    Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default);
}
```

**Requirements**:
- If streaming is not clean to implement right now, keep `HandleStreamAsync` minimal or support only what the existing overlay allows
- `MeshServiceContext` should encapsulate:
  - Remote peer identity
  - Security/reputation handles
  - Logging/tracing helpers

### 2.2. DTOs: ServiceCall and ServiceReply

Introduce DTOs for service RPC:

```csharp
public sealed class ServiceCall
{
    public string ServiceId { get; init; }         // or ServiceName if more consistent
    public string Method { get; init; }           // short method name, e.g. "Join", "SendMessage"
    public string CorrelationId { get; init; }    // unique per in-flight call
    public byte[] Payload { get; init; }          // serialized request payload
}

public sealed class ServiceReply
{
    public string CorrelationId { get; init; }
    public int StatusCode { get; init; }          // 0 = OK; nonzero = app/error codes
    public byte[] Payload { get; init; }          // serialized response payload or error details
}
```

**Requirements**:
- Use the project's existing serialization infrastructure for payloads (JSON, protobuf, custom, etc.)
- Follow the project's naming and style conventions
- Keep fields minimal; no PII or unnecessary metadata

### 2.3. Supporting Types

Add minimal supporting types if needed:

**MeshServiceContext**:
- Should carry:
  - Remote peer ID / descriptor
  - Reference to security/guard components
  - Cancellation / logger, etc.

**MeshServiceStream**:
- Only if the overlay supports multiplexed streams cleanly now
- Should be a high-level abstraction over stream send/receive, not a raw transport object

**DO NOT over-design these.** Keep them small, focused, and consistent with existing code.

---

## 3. SERVICE ROUTER – SERVER SIDE

Implement a **router** that:
- Knows which `IMeshService`s are available locally
- Receives appropriately-tagged overlay messages
- Dispatches them to the right service method

### 3.1. Registration

Create a router class, e.g. `MeshServiceRouter`:

**Responsibilities**:
- Maintain a map: `ServiceName` → `IMeshService` instance
- Register services via:
  - DI (preferred) or
  - Explicit registration method (e.g. `Register(IMeshService service)`)

**Design constraints**:
- It must be thread-safe
- It must not introduce global static state if avoidable; fit into existing DI pattern

### 3.2. Integration with Overlay

You must:
- Identify how overlay control messages are currently parsed and dispatched
- Add a new control message type (or reuse an existing generic payload container) to carry `ServiceCall` and `ServiceReply`

**Typical flow**:

1. Incoming overlay message is identified as a "service" message:
   - E.g., via a `MessageKind` enum or a field in the envelope

2. The router:
   - Validates the message (size, structure)
   - Performs security checks (see Section 4)
   - Locates the target `IMeshService` by `ServiceId` or `ServiceName`
   - Calls `HandleCallAsync` and awaits the reply

3. The router sends back a `ServiceReply` over the overlay to the originating peer

**Requirements**:
- Integrate with existing logging, metrics, and tracing
- Do not break any existing message types/flows; this is additive

### 3.3. Basic Error Handling

The router must:
- Translate exceptions from service handlers into `ServiceReply` with nonzero `StatusCode` and a safe error payload
- Handle unknown services or methods by returning an explicit error code
- Never crash the process due to malformed remote input

---

## 4. SERVICE CLIENT – CLIENT SIDE

Implement an abstraction for initiating service calls over the mesh.

### 4.1. IMeshServiceClient Interface

Define an interface:

```csharp
public interface IMeshServiceClient
{
    Task<ServiceReply> CallAsync(
        MeshServiceDescriptor targetService,
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);
}
```

**Responsibilities**:
- Open a connection/stream over the existing overlay transport to the peer that owns `targetService`
- Serialize `ServiceCall` into the appropriate control/overlay message
- Send it and wait for the corresponding `ServiceReply`
- Enforce timeouts and respect cancellation

**Implementation constraints**:
- Reuse existing connection/session management; do not reinvent connection pools
- Use CorrelationId correctly to match replies to requests
- Make sure concurrent calls from/to the same peer are handled safely

---

## 5. SECURITY, PRIVACY, AND HARDENING

**This is critical.** You must integrate with the existing security and reputation subsystems.

### 5.1. On Incoming Calls (Router Side)

Before handing a `ServiceCall` to `IMeshService`:

1. **Identify the remote peer**:
   - Use existing peer identity from overlay session / envelope

2. **Apply security policies**:
   - If peer is banned/quarantined → reject immediately
   - Enforce **per-peer** and **per-service** rate limits if those facilities exist or introduce simple counters:
     - Max requests per time window
     - Max concurrency per peer
   - Enforce payload size limits:
     - If `Payload.Length` exceeds a configurable threshold, drop and log

3. **Track violations**:
   - On abuse (too many calls, invalid messages, etc.), inform existing violation/reputation logic if present

**Never call `HandleCallAsync` if basic checks fail.**

### 5.2. On Outgoing Calls (Client Side)

- Respect cancellation and timeouts:
  - Do not leave hanging calls
- Use sensible defaults for timeouts (configurable if the project already has such patterns)
- Do not leak internal exception details into the network; log locally, send only a controlled error code/message if needed

### 5.3. Privacy & Logging

- Do not log full payloads of generic `ServiceCall`/`ServiceReply` by default
- Log:
  - Service name
  - Method
  - Peer ID
  - Status (success/failure)
  - Error categories (e.g., timeout, validation failed, rate-limited)
- Avoid logging any sensitive fields that might appear in payloads

---

## 6. TESTING FOR T-SF02

You must add or adapt tests where possible.

### Minimum Expectations:

#### 6.1. Unit Tests

**Router dispatch**:
- Given a mock `IMeshService` registered under a name, verify that an incoming call is routed correctly
- Verify unknown service/method results in appropriate error

**Security checks**:
- Verify oversized payloads are rejected
- Verify banned/quarantined peers are rejected

**Client–router loopback**:
- In a simplified test harness, simulate client → router → service → reply and assert the round-trip

#### 6.2. No Regression of Existing Tests

- All existing tests must still pass

**If the repo's current test coverage is thin**, still:
- Put new logic into testable units
- Provide focused tests for the router and client logic using mocks/fakes where possible

---

## 7. ANTI-SLOP CHECKLIST SPECIFIC TO T-SF02

Before finalizing your changes, verify:

### 7.1. Scope Control

- [ ] You did not modify HTTP controllers, pods, VirtualSoulfind, or `IMeshServiceDirectory`
- [ ] You only added/modified code related to service contracts, router, and client, plus minimal supporting plumbing

### 7.2. Async Correctness

- [ ] No `.Result` / `.Wait()` on async tasks in the new code
- [ ] `CancellationToken` is accepted and forwarded in all async APIs

### 7.3. Performance

- [ ] No heavy LINQ chains or large allocations in per-message paths
- [ ] Reasonable limits enforced on payload sizes and in-flight calls

### 7.4. Security

- [ ] Router enforces basic per-peer/per-service limits and size checks
- [ ] Security/reputation components are invoked where appropriate
- [ ] No sensitive data is logged

### 7.5. Consistency

- [ ] Naming, DI patterns, and logging follow the repo's conventions
- [ ] Serialization/integration with overlay messages uses existing helpers

### 7.6. Documentation

- [ ] `IMeshService`, `IMeshServiceClient`, `ServiceCall`, `ServiceReply`, and router have brief XML docs
- [ ] Non-obvious security behavior is commented in code (short, clear remarks)

---

## 8. IMPLEMENTATION SEQUENCE

**Recommended order of implementation**:

1. **Reconnaissance** (Section 1)
   - Document findings in code comments or a separate reconnaissance doc

2. **Define contracts** (Section 2)
   - `IMeshService`, `ServiceCall`, `ServiceReply`
   - `MeshServiceContext`, `MeshServiceStream` (if needed)

3. **Implement router** (Section 3)
   - Registration mechanism
   - Message dispatching
   - Error handling

4. **Implement client** (Section 4)
   - Call initiation
   - Reply handling
   - Timeout enforcement

5. **Security integration** (Section 5)
   - Router-side checks
   - Client-side error handling
   - Privacy-conscious logging

6. **Testing** (Section 6)
   - Unit tests for router
   - Unit tests for client
   - Integration test for round-trip
   - Verify no regressions

7. **Documentation**
   - XML docs for public APIs
   - Code comments for security logic

---

## 9. SUCCESS CRITERIA

T-SF02 is complete when:

- [ ] `IMeshService` interface is defined
- [ ] `ServiceCall` and `ServiceReply` DTOs are defined
- [ ] `MeshServiceRouter` is implemented and can dispatch calls to registered services
- [ ] `IMeshServiceClient` is implemented and can make calls over the overlay
- [ ] Security checks are integrated (ban list, rate limits, size limits)
- [ ] Unit tests pass for router and client
- [ ] Integration test passes for client → router → service → reply round-trip
- [ ] All existing tests still pass (no regressions)
- [ ] XML docs are present for public types
- [ ] Anti-slop checklist is satisfied

---

## 10. INTEGRATION WITH T-SF01

T-SF01 should have provided:
- `MeshServiceDescriptor` and `MeshServiceEndpoint` types
- `IMeshServiceDirectory` interface with `FindByNameAsync` and `FindByIdAsync` methods

In T-SF02, you will:
- **Consume** `MeshServiceDescriptor` when making client calls (to know which peer to contact)
- **Not modify** the directory implementation
- **Not publish** service descriptors yet (that's later)

Your client (`IMeshServiceClient`) will:
```csharp
// Given a MeshServiceDescriptor from directory:
var descriptor = await directory.FindByNameAsync("some-service");
var reply = await client.CallAsync(descriptor.First(), "SomeMethod", payload);
```

---

## 11. CONFIGURATION AND DEFAULTS

Add configuration options for:

**Router**:
- `MaxPayloadSize` (default: 1MB)
- `MaxCallsPerPeerPerMinute` (default: 100)
- `MaxConcurrentCallsPerPeer` (default: 100)

**Client**:
- `DefaultTimeout` (default: 30s)
- `MaxRetries` (default: 0, no retries for now)

Use the project's existing configuration pattern (appsettings.json, IOptions, etc.).

---

## 12. ERROR CODES

Define a standard set of error codes for `ServiceReply.StatusCode`:

```csharp
public static class ServiceStatusCodes
{
    public const int Ok = 0;
    public const int BadRequest = 400;           // Malformed request
    public const int Unauthorized = 401;         // Banned/quarantined peer
    public const int NotFound = 404;             // Unknown service/method
    public const int TooManyRequests = 429;      // Rate limited
    public const int PayloadTooLarge = 413;      // Oversized payload
    public const int InternalError = 500;        // Unhandled exception
    public const int Timeout = 504;              // Client-side timeout
}
```

Use these consistently in router and client implementations.

---

## 13. LOGGING EXAMPLES

**Router side**:
```csharp
logger.LogInformation(
    "Service call: {ServiceName}.{Method} from {PeerId} -> {StatusCode}",
    call.ServiceId, call.Method, context.PeerId, reply.StatusCode);

logger.LogWarning(
    "Rate limit exceeded: {PeerId} -> {ServiceName}",
    context.PeerId, call.ServiceId);
```

**Client side**:
```csharp
logger.LogInformation(
    "Calling service: {ServiceName}.{Method} on {PeerId}",
    targetService.ServiceName, method, targetService.OwnerPeerId);

logger.LogError(
    "Service call failed: {ServiceName}.{Method} -> {Error}",
    targetService.ServiceName, method, ex.Message);
```

**DO NOT log**:
- Full payloads
- Internal exception stack traces (log locally, not over network)

---

## 14. EXAMPLE USAGE (FOR TESTING)

Here's how a simple test service might look:

```csharp
public class EchoService : IMeshService
{
    public string ServiceName => "echo";

    public Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        if (call.Method == "Echo")
        {
            // Just echo back the payload
            return Task.FromResult(new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Ok,
                Payload = call.Payload
            });
        }

        return Task.FromResult(new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.NotFound,
            Payload = Encoding.UTF8.GetBytes("Unknown method")
        });
    }

    public Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Streaming not supported yet");
    }
}
```

Use this `EchoService` for integration testing.

---

## 15. COMMIT MESSAGE

When T-SF02 is complete, commit with:

```
feat(mesh): add service routing and RPC layer (T-SF02)

Implements:
- IMeshService interface for mesh-exposed services
- MeshServiceRouter for dispatching incoming calls
- IMeshServiceClient for making outbound calls
- Security integration: rate limits, payload size checks, ban list
- Basic unit and integration tests

Part of service fabric initiative (T-SF02).
Depends on T-SF01 (service descriptors and directory).
```

---

## 16. WHAT COMES NEXT

After T-SF02 is merged:

**T-SF03** will:
- Wrap existing features (pods, VirtualSoulfind, stats) as `IMeshService` implementations
- Test that existing functionality works through the new service layer

**T-SF04** will:
- Add HTTP/WebSocket gateway for external apps

DO NOT implement T-SF03 or T-SF04 in this task.

---

## 17. FINAL REMINDERS

- **Read the existing code first** – understand overlay transport, security, and DI patterns
- **Small, focused changes** – do not touch unrelated files
- **Security first** – enforce limits, validate inputs, track violations
- **Test thoroughly** – unit tests, integration tests, regression tests
- **Document clearly** – XML docs, code comments, logging
- **Review the anti-slop checklist** – before committing

---

**Implement T-SF02 following these constraints. Keep the diff small, focused, and easy to review. Once it compiles and tests pass, the next task (T-SF03) can start wrapping specific subsystems (pods, VirtualSoulfind, etc.) on top of this new service fabric.**

---

*Last Updated: December 11, 2025*  
*Branch: experimental/multi-source-swarm*  
*Status: 📋 Ready for Implementation*
