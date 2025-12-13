# Signal: Swarm.RequestBtFallback

## End-to-End Example (Mesh + BT Extension)

> File: docs/design/signal-request-bt-fallback.md

## 1. Purpose

`Swarm.RequestBtFallback` is a control signal used when:

- Two slskdn peers are trying to transfer a `MediaVariant` via Mesh (or Soulseek bridge),  
- Connection attempts are repeatedly failing (NAT, CGNAT, firewall, etc.),  
- Both peers support **BitTorrentBackend** and the slskdn BT extension.

Goal:

- Safely escalate to a **private BitTorrent fallback** for this specific transfer/job,  
- Without blocking on a single channel:
  - Signal is sent over:
    - Mesh (primary), and
    - BT extension (secondary) if a BT session already exists.

This signal is our canonical example/pattern for all future signals.

---

## 2. Signal Type Definition

Logical type: `"Swarm.RequestBtFallback"`

### 2.1 Signal Shape

We assume the generic `Signal` model from `bittorrent-extension-signalling.md`:

```csharp
sealed class Signal
{
    public string SignalId { get; }          // ULID/UUID
    public string FromPeerId { get; }        // slskdn Mesh PeerId
    public string ToPeerId { get; }          // Target PeerId
    public DateTimeOffset SentAt { get; }

    public string Type { get; }              // e.g. "Swarm.RequestBtFallback"
    public IReadOnlyDictionary<string, object> Body { get; }

    public TimeSpan Ttl { get; }

    public IReadOnlyList<SignalChannel> PreferredChannels { get; }
}
```

For this signal:

```text
Type = "Swarm.RequestBtFallback"
Body fields:
  "jobId"          : string    // ID of the Swarm job for this transfer
  "variantId"      : string    // MediaVariant.VariantId
  "contentIdType"  : string    // e.g. "AudioRecording", "Movie"
  "contentIdValue" : string    // e.g. "mb:recording:…", "tmdb:movie:…"
  "reason"         : string    // Optional, short diagnostic; "mesh-failures", "soulseek-failures"
  "hints"          : object    // Optional dict: piece size suggestion, etc. (can be empty)
```

Example Body (JSON-ish):

```json
{
  "jobId": "swarm-job-1234",
  "variantId": "variant-abc",
  "contentIdType": "Movie",
  "contentIdValue": "tmdb:movie:12345",
  "reason": "mesh-failures",
  "hints": {
    "preferredPieceSizeBytes": 1048576
  }
}
```

### 2.2 Ack Type

For critical control we define a matching ack: `"Swarm.RequestBtFallbackAck"`.

Body fields:

```text
"jobId"          : string
"variantId"      : string
"accepted"       : bool
"reason"         : string   // If !accepted, why
"btFallbackId"   : string?  // Optional, e.g. a local ID for this BT session (if accepted)
```

---

## 3. When & How the Signal Is Sent

### 3.1 Trigger Conditions (Sender Side)

From SwarmCore / MeshBackend (simplified):

* We are the **leecher** (requesting side) or the **job orchestrator**.
* We have a `SwarmJob` for a `MediaVariant` with a target `PeerId`.
* We observe repeated failures over Mesh/Soulseek path:

  * Connection failures,
  * Timeouts,
  * Possibly SecurityCore says "mesh path degraded."

We then:

1. Check if both peers support **BitTorrentBackend**.
2. Check `SecurityCore`:

   * `EffectiveTrustScore(targetPeer) >= threshold`.
3. If conditions met → construct and send `Swarm.RequestBtFallback`.

### 3.2 Signal Construction Example (pseudo-code)

```csharp
public async Task RequestBtFallbackAsync(
    string jobId,
    MediaVariant variant,
    string targetPeerId,
    string reason,
    ISignalBus signalBus,
    CancellationToken ct)
{
    var signal = new Signal(
        signalId: Ulid.NewUlid().ToString(),
        fromPeerId: _meshIdentity.PeerId,
        toPeerId: targetPeerId,
        sentAt: DateTimeOffset.UtcNow,
        type: "Swarm.RequestBtFallback",
        body: new Dictionary<string, object>
        {
            ["jobId"] = jobId,
            ["variantId"] = variant.VariantId,
            ["contentIdType"] = variant.ContentId.Type.ToString(),
            ["contentIdValue"] = variant.ContentId.Value,
            ["reason"] = reason,
            ["hints"] = new Dictionary<string, object>
            {
                ["preferredPieceSizeBytes"] = 1_048_576
            }
        },
        ttl: TimeSpan.FromMinutes(5),
        preferredChannels: new[]
        {
            SignalChannel.Mesh,
            SignalChannel.BtExtension   // second preference
        });

    await signalBus.SendAsync(signal, ct);
}
```

### 3.3 Preferred Channels

For `Swarm.RequestBtFallback`:

```text
PreferredChannels = [ Mesh, BtExtension ]
```

* Mesh:

  * Primary/most reliable control plane.
* BT extension:

  * Secondary (only if a private BT session already exists with this peer).

If Mesh fails but a BT session is open, the BT extension can carry the signal as a fallback.

---

## 4. How the Signal Is Delivered Over Each Channel

### 4.1 Mesh Channel (MeshSignalChannelHandler)

**Outbound:**

* `MeshSignalChannelHandler.SendAsync(signal)`:

  * Wraps `Signal` into a Mesh message envelope, e.g.:

    ```json
    {
      "messageType": "slskdnSignal",
      "signal": { ... Signal JSON/CBOR ... }
    }
    ```

  * Uses MeshCore overlay to route to `ToPeerId`.

**Inbound:**

* MeshCore receives a `SlskdnSignal` message:

  * Handler decodes the payload into a `Signal` object.
  * Sends into `SignalBus.ReceiveAsync`.

### 4.2 BT Extension Channel (BtExtensionSignalChannelHandler)

**Outbound:**

* `BtExtensionSignalChannelHandler.SendAsync(signal)`:

  1. Serialize the `Signal` to CBOR/JSON.

  2. Wrap into `slskdnExtensionMessage`:

     ```csharp
     var msg = new SlskdnExtensionMessage
     {
         Kind = SlskdnSignalKind.SignalEnvelope,
         Payload = SerializeSignalToCbor(signal)
     };
     ```

  3. Send via BT extension message ID assigned to `"slskdn"`.

**Inbound:**

* We subscribe to BT extension messages where:

  * Extension name = `"slskdn"`,
  * `Kind = SlskdnSignalKind.SignalEnvelope`.

* For each incoming extension payload:

  ```csharp
  var signal = DeserializeSignalFromCbor(payload);
  // Push into SignalBus inbound stream
  ```

---

## 5. Deduplication & Fallback

Because a `Signal` may be sent over Mesh and BT extension:

* `SignalBus` keeps an LRU cache of seen `SignalId`s.
* On first arrival:

  * It passes the `Signal` to subscribers (SwarmCore, PodCore, etc.).
* On subsequent arrivals (via other channels):

  * It drops them as duplicates.

If Mesh is broken but BT still works:

* Only the BT extension version will arrive.
* The system doesn't care which channel succeeded; it sees the `Signal` once.

If BT is not available:

* `CanSendTo` on the BT handler returns false.
* SignalBus only sends via Mesh.

---

## 6. Receiver Behaviour (Target Peer)

### 6.1 High-Level Flow

On the **receiver** side (target peer):

1. `SignalBus.SubscribeAsync` emits a `Signal` with:

   * `Type = "Swarm.RequestBtFallback"`,
   * `ToPeerId = local PeerId`.

2. SwarmCore's signal handler inspects the signal and:

   * Validates:

     * `jobId` corresponds to a known job.
     * `variantId` belongs to that job.
     * The requester is the job orchestrator or expected leecher.

   * Consults SecurityCore:

     * Is BT fallback allowed with `FromPeerId`?
     * Are we configured to use BTBackend?

   * If not accepted:

     * Sends `Swarm.RequestBtFallbackAck` with `accepted = false`.
     * Optionally logs a reason.

   * If accepted:

     * Creates (or prepares) a private slskdn torrent for this `MediaVariant`.
     * Starts seeding via BitTorrentBackend.
     * Sends `Swarm.RequestBtFallbackAck` with:

       * `accepted = true`,
       * Possibly a local `btFallbackId`.

### 6.2 Receiver Handler Pseudo-Code

```csharp
public async Task HandleSignalAsync(Signal signal, CancellationToken ct)
{
    if (signal.Type != "Swarm.RequestBtFallback")
        return;

    var jobId          = (string)signal.Body["jobId"];
    var variantId      = (string)signal.Body["variantId"];
    var contentIdType  = (string)signal.Body["contentIdType"];
    var contentIdValue = (string)signal.Body["contentIdValue"];
    var reason         = (string)signal.Body["reason"];

    var job = await _swarmJobStore.TryGetJobAsync(jobId, ct);
    if (job is null || !job.HasVariant(variantId))
    {
        await SendBtFallbackAckAsync(signal, accepted: false,
            reason: "unknown-job-or-variant", ct);
        return;
    }

    // Evaluate security / trust
    var decision = await _securityPolicyEngine.EvaluateAsync(
        SecurityContext.ForBtFallback(
            fromPeerId: signal.FromPeerId,
            jobId: jobId,
            variantId: variantId),
        ct);

    if (!decision.Allowed)
    {
        await SendBtFallbackAckAsync(signal, accepted: false,
            reason: "security-denied", ct);
        return;
    }

    if (!_bitTorrentBackend.IsSupported())
    {
        await SendBtFallbackAckAsync(signal, accepted: false,
            reason: "bt-backend-disabled", ct);
        return;
    }

    // At this point, we accept the fallback
    var btFallbackId = await _bitTorrentBackend.PreparePrivateTorrentAsync(
        job, variantId, ct);

    // Optionally start seeding now or on demand

    await SendBtFallbackAckAsync(signal, accepted: true,
        reason: "ok", btFallbackId: btFallbackId, ct);
}
```

### 6.3 Ack Sender Helper

```csharp
private async Task SendBtFallbackAckAsync(
    Signal requestSignal,
    bool accepted,
    string reason,
    string? btFallbackId = null,
    CancellationToken ct = default)
{
    var ack = new Signal(
        signalId: Ulid.NewUlid().ToString(),
        fromPeerId: _meshIdentity.PeerId,
        toPeerId: requestSignal.FromPeerId,
        sentAt: DateTimeOffset.UtcNow,
        type: "Swarm.RequestBtFallbackAck",
        body: new Dictionary<string, object>
        {
            ["jobId"] = (string)requestSignal.Body["jobId"],
            ["variantId"] = (string)requestSignal.Body["variantId"],
            ["accepted"] = accepted,
            ["reason"] = reason,
            ["btFallbackId"] = btFallbackId ?? ""
        },
        ttl: TimeSpan.FromMinutes(5),
        preferredChannels: new[]
        {
            SignalChannel.Mesh,        // primary for acks
            SignalChannel.BtExtension  // secondary if session exists
        });

    await _signalBus.SendAsync(ack, ct);
}
```

---

## 7. Sender Handling of Ack

On the **sender** side (the original requester):

1. When sending `Swarm.RequestBtFallback`, we:

   * Optionally register a pending request in a local dictionary:

     * `pendingFallbacks[jobId] = new PendingRequest { SignalId, VariantId, Timeout }`.

2. We subscribe to `SignalBus` and handle `Swarm.RequestBtFallbackAck` signals:

   ```csharp
   public async Task HandleSignalAsync(Signal signal, CancellationToken ct)
   {
       if (signal.Type != "Swarm.RequestBtFallbackAck")
           return;

       var jobId     = (string)signal.Body["jobId"];
       var variantId = (string)signal.Body["variantId"];
       var accepted  = (bool)signal.Body["accepted"];
       var reason    = (string)signal.Body["reason"];
       var btFallbackId = (string)signal.Body["btFallbackId"];

       if (!_pendingFallbacks.TryGetValue(jobId, out var pending))
           return; // unknown/late ack

       if (!accepted)
       {
           // Log, mark job as "no-fallback-available" or try another peer
           _pendingFallbacks.Remove(jobId);
           return;
       }

       // Accepted: instruct SwarmCore to attach BitTorrentBackend to this job
       await _swarmCore.EnableBtFallbackForJobAsync(
           jobId, variantId, signal.FromPeerId, btFallbackId, ct);

       _pendingFallbacks.Remove(jobId);
   }
   ```

3. If no ack arrives within some timeout (e.g., `ttl / 2`):

   * We mark fallback as failed.
   * Optionally:

     * Try a different peer,
     * Or downgrade to "mesh-only; no BT fallback available."

---

## 8. Template for Future Signals

This example establishes a repeatable pattern:

1. **Define `Type`:** `"Domain.Action"` string.

2. **Define `Body` schema:**

   * Minimal, typed fields.
   * Enough to correlate with local jobs/entities.

3. **Set `PreferredChannels`:**

   * For critical control: `[Mesh, BtExtension]`.
   * For social stuff: `[Mesh]` only.

4. **Implement sender:**

   * Construct `Signal` with `SignalId`, `FromPeerId`, `ToPeerId`, `Type`, `Body`, `Ttl`.
   * Call `SignalBus.SendAsync`.

5. **Implement receiver:**

   * Subscribe via `SignalBus.SubscribeAsync`.
   * Filter on `Type`.
   * Validate body + security.
   * Perform action.
   * Optionally send ack via another `Signal`.

6. **Let `SignalBus` and channel handlers:**

   * Handle Mesh vs BT extension differences,
   * Deduplicate via `SignalId`,
   * Fallback between channels.

Copy this structure for any future signals (e.g. `Swarm.JobCancel`, `Pod.MembershipUpdate`, `Pod.VariantOpinionUpdate`), keeping the same multi-channel, dedup, and optional ack pattern.















