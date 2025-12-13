# Pods ↔ Soulseek Chat Bridge

## Backwards-Compatible Community Mapping

> File: docs/design/pods-soulseek-chat-bridge.md

## 1. Goals

Short-term adoption goal:

- Let users participate in **existing Soulseek chats** (rooms/PMs) from within the mesh network.
- Optionally map a **Pod** to a **Soulseek room** so "Fans of X" can be:
  - A native pod (Mesh-based, with PodCore semantics), and
  - A view onto the existing Soulseek room (backwards-compatible).

Key constraints:

- **No mass scraping / archiving** of Soulseek chat.
- **No auto-publishing** of Pod-private messages into Soulseek.
- Clear UX separation between:
  - Messages that go to Soulseek, and
  - Messages that are Pod-only on the mesh.

We prefer **layering Pods on top**, with optional **Soulseek bindings**, not trying to fully "clone" Soulseek's chat.

---

## 2. High-Level Approach

We introduce:

- A **Soulseek Chat Bridge** inside `SoulseekBridge/` that:
  - Connects to Soulseek servers.
  - Joins rooms and handles PMs through the normal Soulseek protocol.
- A way to mark a Pod as **bound** to a Soulseek room:
  - Pod metadata gets an `ExternalBinding` section.
  - Pod's UI shows a **combined view**:
    - A **bound channel** representing the Soulseek room.
    - Optional **native Pod channels** (mesh-only).

Conceptually:

- A "Fans of Daft Punk" pod can be:
  - A native pod (`PodId`, membership, chat, variant opinions).
  - Bound to `SoulseekRoom = "Daft Punk"` via the Soulseek bridge.
- Pod members can:
  - Talk in the room (Soulseek users see them as normal Slsk nicknames).
  - Talk in pod-native channels (mesh-only).
  - See pod-scoped stats / coverage / recommendations on top.

---

## 3. Data Model Extensions

### 3.1 Pod External Binding

Extend `PodMetadata` with an optional `ExternalBinding`:

```csharp
enum PodExternalBindingKind
{
    None,
    SoulseekRoom
    // (Future: DiscordChannel, MatrixRoom, etc.)
}

enum PodExternalBindingMode
{
    None,       // No link, pure pod
    ReadOnly,   // We mirror messages into the pod, but do not send out
    Mirror      // Two-way: messages from mesh can be posted to Soulseek
}

sealed class PodExternalBinding
{
    public PodExternalBindingKind Kind { get; }
    public PodExternalBindingMode Mode { get; }

    // For Soulseek rooms, Identifier = room name (e.g. "Daft Punk")
    public string Identifier { get; }

    // Optional: per-binding settings (rate limits, anonymization, etc.)
    public IReadOnlyDictionary<string, string> Settings { get; }
}
```

Update `PodMetadata`:

```csharp
sealed class PodMetadata
{
    public PodId Id { get; }

    public string DisplayName { get; }
    public string? Description { get; }

    public PodVisibility Visibility { get; }

    public PodFocusType FocusType { get; }
    public ContentId? FocusContentId { get; }

    public IReadOnlyList<string> Tags { get; }

    public PodExternalBinding? ExternalBinding { get; }

    public DateTimeOffset CreatedAt { get; }
    public string CreatedByPeerId { get; }
}
```

### 3.2 Bound Channels vs Native Channels

Extend channel concept to allow "bound" channels:

```csharp
enum PodChannelKind
{
    Native,     // mesh-native Pod channel
    Bound       // externally-bound channel (e.g. Soulseek room)
}

sealed class PodChannelId
{
    public PodId PodId { get; }
    public string ChannelKey { get; }    // "general", "offtopic", "soulseek-room", etc.
    public PodChannelKind Kind { get; }
}
```

For a Soulseek-bound pod, you might see:

* `ChannelKey = "soulseek-room"`, `Kind = Bound` → **Soulseek room mirror**.
* `ChannelKey = "general"`, `Kind = Native` → pod-only mesh chat.

---

## 4. Soulseek Chat Bridge (SoulseekBridge.Chat)

Inside `integrations/SoulseekBridge/`, define a chat bridge component:

```csharp
interface ISoulseekChatBridge
{
    Task JoinRoomAsync(string roomName, CancellationToken ct);
    Task LeaveRoomAsync(string roomName, CancellationToken ct);

    IAsyncEnumerable<SoulseekRoomEvent> SubscribeRoomEventsAsync(
        string roomName,
        CancellationToken ct);

    Task SendRoomMessageAsync(
        string roomName,
        string message,
        CancellationToken ct);

    IAsyncEnumerable<SoulseekPrivateMessage> SubscribePrivateMessagesAsync(
        CancellationToken ct);

    Task SendPrivateMessageAsync(
        string targetUsername,
        string message,
        CancellationToken ct);
}
```

Where:

* `SoulseekRoomEvent` includes:
  * Joins/leaves,
  * Room chat messages.
* `SoulseekPrivateMessage` maps 1:1 to Soulseek PM semantics.

This is essentially the chat subset of the Soulseek client, wrapped so PodCore can consume it.

---

## 5. Mapping Soulseek Rooms to Pods

### 5.1 Creation Modes

**Mode A – Pod created from an existing Soulseek room**

Flow:

1. User is connected to Soulseek.
2. User joins a room "Daft Punk".
3. UI offers: "Create a pod for this room":
   * Pod name = "Fans of Daft Punk" (pre-filled from room name).
   * `ExternalBinding = { Kind = SoulseekRoom, Identifier = "Daft Punk", Mode = Mirror or ReadOnly }`.
4. Pod is created with:
   * Focus content (optional): e.g. link to Daft Punk MB artist `ContentId`.
   * Default channels:
     * `soulseek-room` (Bound),
     * `general` (Native, mesh-only).

**Mode B – Attach an existing pod to a Soulseek room**

Flow:

1. A pod already exists (e.g. created for Daft Punk).
2. A Pod Owner decides to "bind to Soulseek room".
3. Owner sets `ExternalBinding = SoulseekRoom` with a room name.
4. PodCore instructs `ISoulseekChatBridge` to join that room when the user is online.

### 5.2 Ingestion and Mirroring

For pods with `ExternalBinding.Kind = SoulseekRoom`:

* PodCore subscribes to room events via `ISoulseekChatBridge`.

* For each `SoulseekRoomMessage`:

  ```text
  Soulseek: (roomName, username, text, timestamp)
    ↓
  PodCore: create PodMessage in bound channel "soulseek-room":
      SenderPeerId: synthetic "soulseek:username" or mapped alias
      Body: text
      Metadata: marks it as external-origin
  ```

* Messages show up in the pod's bound channel UI with a clear external origin indicator.

**Mode = ReadOnly**:

* We **mirror into pod** (Soulseek → Pod), but:
  * Messages sent in the mesh to the bound channel DO NOT go back to Soulseek.
  * Useful when you want a read-only window into a busy room.

**Mode = Mirror (two-way)**:

* Users can send messages from pod to Soulseek:

  ```text
  PodMessage in bound "soulseek-room" channel:
      SenderPeerId: local mesh user
      Body: "hello"
    ↓
  PodCore → SoulseekBridgeChat:
      SendRoomMessage("Daft Punk", "[mesh] <nick>: hello")
  ```

* Display style can be configured (e.g., prefix or suffix to indicate mesh origin).

---

## 6. Mapping Soulseek Identities

Soulseek identities are:

* Distinct from Mesh identities (`PeerId`).
* Centrally assigned via Soulseek servers (nicks).

We treat them as **external identities**:

* Represented as strings like:
  * `soulseek:username`.
* When a Soulseek user also runs the mesh client, we can optionally link:
  * `soulseek:username` → Mesh `PeerId` via an out-of-band verification (future).

Short-term:

* Messages from Soulseek users in a bound pod are shown as:
  * e.g. `Soulseek: username` in the UI.
* They are **not** automatically treated as pod members:
  * Pod membership remains explicit and mesh-based.
  * Users can choose to "follow" or "friend" a Soulseek nick, but that's PodCore UI logic.

---

## 7. Private Chats (Optional / Later)

We can optionally map Soulseek PMs to:

* Ephemeral per-user Pods or conversations.

Example pattern:

* `pod:dm:soulseek:<username>`:
  * UIs treat this more like a "conversation" than a full-featured pod.
* `ISoulseekChatBridge.SubscribePrivateMessagesAsync` feeds these:
  * When a PM arrives:
    * Create/update a DM conversation object.
    * Show messages in a "Conversations" panel.
* No need to integrate DMs deeply into PodCore at first:
  * Treat them as a separate "Soulseek DM" feature with simpler semantics.
  * We can unify with pod-based private groups later if needed.

For now, focusing on **room ↔ pod** mapping is enough to get adoption.

---

## 8. UI / UX Guidelines

To avoid confusion & accidental leakage:

1. **Strong visual distinction**
   * Bound channel messages:
     * Show a Soulseek badge/icon.
     * Use a distinct color or label.
   * Native Pod channels:
     * No external badges; clearly "mesh-only."

2. **Per-channel posting awareness**
   * The input box for a bound channel should clearly say:
     * "Send to Soulseek room 'Daft Punk'" (for Mirror mode).
   * For ReadOnly mode:
     * The input should be disabled or clearly marked as "Pod-only chat, does not go to Soulseek."

3. **Defaults**
   * When creating a pod from a Soulseek room:
     * Default to **ReadOnly** binding:
       * Safer; no accidental cross-posting.
   * Users can explicitly flip to Mirror mode if they want.

4. **Logging & history**
   * Should:
     * Respect user settings about storing Soulseek chat locally.
     * Not try to indefinitely archive entire room histories by default.

---

## 9. Safety & Legal Framing

This bridge is intentionally:

* **User-level**:
  * You are just another Soulseek client, talking in rooms via standard protocol.
* **Scoped**:
  * You only join rooms you choose.
  * You only mirror chats you're participating in anyway.
* **Non-scraping**:
  * No crawling all rooms.
  * No bulk harvesting of chat logs beyond what a normal client would see while present.

Pods remain:

* A **mesh construct** with:
  * Mesh-based membership,
  * Pod-only chat,
  * Variant opinions and stats.
* Soulseek binding is:
  * An optional overlay to ease adoption and let you stay plugged into existing communities while the mesh network grows.

---

## 10. Implementation Order

1. Implement `ISoulseekChatBridge` with:
   * Join/leave room.
   * Subscribe to room events.
   * Send room messages.

2. Add Pod `ExternalBinding` support:
   * Create pods bound to Soulseek rooms.
   * Create a single bound channel per such pod (`soulseek-room`).

3. ReadOnly mode:
   * Mirror Soulseek messages into bound channel.
   * No cross-posting from pod to Soulseek.

4. Mirror mode:
   * Allow two-way messaging:
     * Pod → Soulseek.
   * Add strong UI cues and settings.

5. Optional:
   * DM/conversation view for Soulseek PMs.
   * ID/linking between Soulseek usernames and mesh PeerIds.

---

## Big Picture

- **Pods stay mesh-native**, with their own semantics and membership.
- Soulseek chat is **a bridged external stream** that can be attached to a pod's "bound channel."
- Short term, this means the mesh can sit in existing Soulseek rooms and layer all the advanced features (pods, collection doctor, variant opinions) on top, without forcing anyone else to move off Soulseek yet.
- This provides a smooth migration path: users can gradually adopt mesh features while still participating in their existing Soulseek communities.
















