# Pod API Design – Base Shape for App Host & Ecosystem Apps

This document defines the **Pod API** shape that the App Host (VS Code-based shell) and ecosystem apps will call via the `PodApiClient` (`pods://` URIs).

This is **not** intended to be a fully stable public API yet. It is a **v0 internal contract** to:

- Unblock the App Host and core apps (Mesh Explorer, Chat, etc.).
- Provide a coherent set of endpoints and JSON shapes.
- Stay aligned with security, context, and realm/pod isolation principles.

## 1. Goals

- Provide a **consistent, simple HTTP/JSON API** for each pod.
- Allow the App Host to talk to a pod via a small set of domains:
  - `auth` – sessions/tokens (later).
  - `context` – who/where we are.
  - `apps` – which apps are available on this pod (optional, later).
  - `chat` – channels, messages, threads.
  - `mesh` – realm/pod metadata, health.
  - (later) `forum`, `media`, `gov`, etc.
- Map cleanly to the `pods://` URI scheme used by the App Host:
  - e.g. `pods://chat/listChannels` → `GET /api/chat/channels`.

For now, the implementation can be in-memory/mocked and later wired into real data stores and services.


## 2. Base Conventions

### 2.1 HTTP & Base Path

- All pod APIs are served under `/api`.
- Versioning:
  - For v0, endpoints are at `/api/...`.
  - When stable, we can adopt `/api/v1/...` while keeping v0 for internal use only.

### 2.2 Authentication

- For this first iteration, it is acceptable to:
  - Stub auth checks,
  - Or use a simple header such as `Authorization: Bearer <token>` and ignore validation.
- The API must be designed to **expect** auth in the future:
  - All handlers should be structured so that adding auth checks later does not break the shape.

### 2.3 Response Envelope

All endpoints should respond with a consistent JSON envelope:

```json
{
  "ok": true,
  "error": null,
  "data": { /* domain-specific payload */ }
}
```

On failure:

```json
{
  "ok": false,
  "error": {
    "code": "SOME_CODE",
    "message": "Human-readable error",
    "details": { /* optional */ }
  },
  "data": null
}
```

The App Host's `PodApiClient` expects this envelope.

### 2.4 Error Codes

Define a small initial set of error codes:

- `UNAUTHORIZED`
- `FORBIDDEN`
- `NOT_FOUND`
- `VALIDATION_FAILED`
- `INTERNAL_ERROR`
- `NOT_IMPLEMENTED`

This can expand later as needed.

## 3. Context & Identity Endpoints

These endpoints give the client basic information about **where** it is and who it is.

### 3.1 `GET /api/context`

**Purpose:**
Return the active realm/pod/user context from this pod's perspective.

**Mapped `pods://` URI:**
`pods://mesh/getContext`

**Request:**

- Method: `GET`
- Path: `/api/context`
- Auth: optional placeholder for now.

**Response `data` shape:**

```json
{
  "realm": {
    "id": "slskdn-main-v1",
    "name": "Main Realm"
  },
  "pod": {
    "id": "pod-1234",
    "name": "My Pod",
    "roles": ["owner", "admin"]  // roles relative to this pod
  },
  "user": {
    "id": "user-abcdef",
    "displayName": "Alice",
    "roles": ["member"],          // roles relative to this pod
    "isF1000": false              // if applicable in this realm
  }
}
```

This endpoint allows the App Host to:

- Initialize `MeshContext` (realmId/podId/userId).
- Render status bar indicators and basic identity info.

## 4. Apps Listing (Optional, Future)

Later, pods may expose app manifests or capabilities.

### 4.1 `GET /api/apps`

**Purpose:**
List apps/capabilities that this pod wants to expose to the client.

**Mapped `pods://` URI:**
`pods://mesh/listApps` (for future use).

**Request:**

- `GET /api/apps`

**Response `data` shape (initial idea):**

```json
{
  "apps": [
    {
      "appId": "mesh.chat",
      "enabled": true
    },
    {
      "appId": "mesh.mesh",
      "enabled": true
    }
  ]
}
```

For now, the App Host can rely on local manifests. This endpoint can be wired in later to allow pods to publish app availability and configuration.

## 5. Mesh Domain – Realm & Pod Metadata

These endpoints support the "Mesh Explorer" app.

### 5.1 `GET /api/mesh/pods`

**Purpose:**
Return a list of pods known to this pod in its realm (and optionally cross-realm, if applicable).

**Mapped `pods://` URI:**
`pods://mesh/listPods`

**Request:**

- `GET /api/mesh/pods`

**Response `data` shape:**

```json
{
  "realm": {
    "id": "slskdn-main-v1",
    "name": "Main Realm"
  },
  "pods": [
    {
      "id": "pod-1234",
      "name": "My Pod",
      "status": "online",     // "online" | "offline" | "degraded" | "unknown"
      "healthScore": 0.98,    // optional float 0.0–1.0
      "roles": ["self"]       // e.g. ["self"], ["peer"], ["bridge"]
    },
    {
      "id": "pod-5678",
      "name": "Friend Pod",
      "status": "online",
      "healthScore": 0.87,
      "roles": ["peer"]
    }
  ]
}
```

For now, return a mock list. Later, plug into the real mesh topology / HealthManager.

### 5.2 `GET /api/mesh/pods/:podId/health`

**Purpose:**
Detailed health info for a specific pod.

**Mapped `pods://` URI:**
`pods://mesh/getPodHealth?podId=pod-1234`

**Request:**

- `GET /api/mesh/pods/{podId}/health`

**Response `data` shape:**

```json
{
  "podId": "pod-1234",
  "status": "online",
  "healthScore": 0.98,
  "lastSeen": "2025-12-10T12:34:56Z",
  "metrics": {
    "latencyMs": 35,
    "failureRate": 0.01
  }
}
```

Initial implementation can be fully mocked.

## 6. Chat Domain – Channels & Messages

These endpoints back the Chat app surfaces.

### 6.1 Channel Data Model (v0)

Minimal shapes:

```ts
interface ChatChannel {
  id: string;
  name: string;
  isPrivate: boolean;
  topic?: string;
  createdAt: string;     // ISO timestamp
  createdBy: string;     // user id
}
```

```ts
interface ChatMessage {
  id: string;
  channelId: string;
  authorId: string;
  authorDisplayName: string;
  body: string;
  createdAt: string;     // ISO timestamp
  editedAt?: string;     // ISO timestamp
  threadRootId?: string; // if this message is part of a thread
}
```

### 6.2 `GET /api/chat/channels`

**Purpose:**
List chat channels available to the current user in this pod.

**Mapped `pods://` URI:**
`pods://chat/listChannels`

**Request:**

- `GET /api/chat/channels`

**Response `data` shape:**

```json
{
  "channels": [
    {
      "id": "chan-general",
      "name": "#general",
      "isPrivate": false,
      "topic": "General discussion",
      "createdAt": "2025-12-10T12:00:00Z",
      "createdBy": "user-abcdef"
    },
    {
      "id": "chan-mesh",
      "name": "#mesh-dev",
      "isPrivate": true,
      "topic": "Mesh development",
      "createdAt": "2025-12-10T12:05:00Z",
      "createdBy": "user-abcdef"
    }
  ]
}
```

Initial implementation can return a static list.

### 6.3 `GET /api/chat/channels/:channelId/messages`

**Purpose:**
Fetch messages in a given channel, with pagination.

**Mapped `pods://` URI:**
`pods://chat/getChannelMessages?channelId=...`

**Request:**

- `GET /api/chat/channels/{channelId}/messages`
- Query parameters:
  - `before` (optional): message ID or timestamp cursor.
  - `limit` (optional): integer, default 50.

**Response `data` shape:**

```json
{
  "channel": {
    "id": "chan-general",
    "name": "#general"
  },
  "messages": [
    {
      "id": "msg-1",
      "channelId": "chan-general",
      "authorId": "user-abcdef",
      "authorDisplayName": "Alice",
      "body": "Hello world",
      "createdAt": "2025-12-10T12:10:00Z"
    },
    {
      "id": "msg-2",
      "channelId": "chan-general",
      "authorId": "user-ghijkl",
      "authorDisplayName": "Bob",
      "body": "Hi Alice",
      "createdAt": "2025-12-10T12:11:00Z"
    }
  ],
  "paging": {
    "hasMore": false,
    "nextCursor": null
  }
}
```

For v0, you can ignore pagination and just return a fixed array with `hasMore: false`.

### 6.4 `POST /api/chat/messages`

**Purpose:**
Send a new message to a channel.

**Mapped `pods://` URI:**
`pods://chat/sendMessage`

**Request:**

- `POST /api/chat/messages`
- JSON body:

```json
{
  "channelId": "chan-general",
  "body": "Message text",
  "threadRootId": "msg-1"  // optional, if replying in a thread
}
```

**Response `data` shape:**

```json
{
  "message": {
    "id": "msg-3",
    "channelId": "chan-general",
    "authorId": "user-abcdef",
    "authorDisplayName": "Alice",
    "body": "Message text",
    "createdAt": "2025-12-10T12:12:00Z",
    "threadRootId": "msg-1"
  }
}
```

Initial implementation can:

- Accept any body,
- Echo back a message object with generated IDs and timestamps,
- Store in an in-memory list per pod process (no persistence needed in v0).

### 6.5 `POST /api/chat/channels`

**Purpose:**
Create a new channel.

**Mapped `pods://` URI:**
`pods://chat/createChannel`

**Request:**

- `POST /api/chat/channels`
- JSON body:

```json
{
  "name": "#new-channel",
  "isPrivate": false,
  "topic": "Optional topic"
}
```

**Response `data` shape:**

```json
{
  "channel": {
    "id": "chan-new-channel",
    "name": "#new-channel",
    "isPrivate": false,
    "topic": "Optional topic",
    "createdAt": "2025-12-10T12:20:00Z",
    "createdBy": "user-abcdef"
  }
}
```

For v0, you can:

- Validate that `name` is non-empty.
- Append to an in-memory channels list.

## 7. Mapping `pods://` URIs to HTTP Endpoints

The App Host's `PodApiClient` operates with `pods://` URIs. The mapping in v0 should be:

- `pods://mesh/getContext` → `GET /api/context`
- `pods://mesh/listPods` → `GET /api/mesh/pods`
- `pods://mesh/getPodHealth?podId=X` → `GET /api/mesh/pods/X/health`
- `pods://chat/listChannels` → `GET /api/chat/channels`
- `pods://chat/getChannelMessages?channelId=X` → `GET /api/chat/channels/X/messages`
- `pods://chat/sendMessage` → `POST /api/chat/messages`
- `pods://chat/createChannel` → `POST /api/chat/channels`

The exact query parameter encoding for `pods://` URIs is up to the App Host implementation; the pod-side only needs the HTTP routes.

## 8. Security & Hardening Considerations

Even in v0/stub form, adhere to these principles:

- **No cross-realm impersonation:**
  - A pod should only report data about its own realm/pod and known peers as configured.
- **Role awareness (future):**
  - The API shapes include `roles` fields so later we can enforce:
    - Who can create channels,
    - Who can see private channels,
    - etc.
- **Error handling:**
  - All errors must be mapped to the response envelope and not leak internal stack traces.

As the mesh governance and MCP layers are wired in, additional checks will be added on top of these endpoints to enforce content and behavior policies.

## 9. Initial Implementation Scope

For the first implementation pass, it is sufficient to:

- Implement all endpoints with **in-memory, process-local data**:
  - `GET /api/context` → hard-coded context or simple config.
  - `GET /api/mesh/pods` → fixed mock list.
  - `GET /api/mesh/pods/:podId/health` → mock health payloads.
  - `GET /api/chat/channels` → in-memory list of channels.
  - `POST /api/chat/channels` → append to in-memory channels.
  - `GET /api/chat/channels/:channelId/messages` → in-memory messages per channel.
  - `POST /api/chat/messages` → append to in-memory messages.

This is enough to:

- Unblock the App Host implementation.
- Demonstrate tree views and webviews with real HTTP calls.
- Evolve toward persistent storage and real mesh/health data later.
