# Ecosystem Shell Architecture – High-Level Vision

This document describes the **conceptual architecture** for building a universal client shell for the mesh/slskd ecosystem, independent of specific tooling choices.

## 1. What We're Building

**Goal**: Create a universal, rearrangeable shell for the entire ecosystem that runs on both desktop and web.

**Not**: "An editor with chat bolted on"  
**Instead**: "A dashboard/window manager for pods, chat, media, governance, etc. (that can also edit code)"

---

## 2. The Shell: Tiled UI Framework

We use the **VS Code workbench** as our tiled layout engine:

```
┌─────────────────────────────────────────────────────────────┐
│  Activity Bar  │  Side Bar (Trees)  │  Editor Grid (Panels) │
│  ─────────────────────────────────────────────────────────── │
│  • Chat        │  Channels          │  ┌─────────┬─────────┐│
│  • Mesh        │  • #general        │  │ Chat    │ Health  ││
│  • Media       │  • #mesh-dev       │  │ Log     │ Dash    ││
│  • Gov         │  Pods              │  │         │         ││
│  • Code        │  • My Pod          │  └─────────┴─────────┘│
│                │  • Friend Pod      │                       │
│                └────────────────────┴───────────────────────│
│  Status Bar: [Realm: Main] [Pod: My Pod] [User: Alice]     │
└─────────────────────────────────────────────────────────────┘
```

**Key UI Components**:
- **Activity Bar**: App launcher (Chat, Mesh, Media, etc.)
- **Side Bar**: Tree views (channels, pods, artists, governance)
- **Editor Grid**: Main content panels (can split, tile, dock)
- **Status Bar**: Context indicators (realm/pod/user, connection status)
- **Command Palette**: Keyboard-driven entry to all actions

Users can rearrange, split, dock, maximize — just like they do in VS Code today.

---

## 3. The App Model

Instead of one monolithic UI, we define a **modular app system**.

### 3.1 What is an App?

An **App** is one functional module:

**Examples**:
- `mesh.chat` – Channels, messages, threads
- `mesh.forum` – Boards, topics, discussions
- `mesh.media` – Library, playback, metadata
- `mesh.mesh` – Pod/realm explorer, health dashboards
- `mesh.gov` – F1000, policies, moderation tools
- `mesh.code` – (Optional) File editing

Each app:
- Has a unique ID and name
- Declares **surfaces** it needs in the shell
- Talks to pods via a defined **Pod API**

Think of apps like **Slack apps**, but native to your network.

### 3.2 Context: Where We Are

Every app operates in a **context**:

```typescript
interface MeshContext {
  realm: {
    id: string;        // e.g., "slskdn-main-v1"
    name: string;      // e.g., "Main Realm"
  };
  pod: {
    id: string;        // e.g., "pod-1234"
    name: string;      // e.g., "My Pod"
    roles: string[];   // e.g., ["owner", "admin"]
  };
  user: {
    id: string;        // e.g., "user-abcdef"
    displayName: string;
    roles: string[];   // e.g., ["member"]
    isF1000: boolean;
  };
}
```

**Context is global state** that:
- The shell tracks centrally
- Apps read to know "where am I, who am I, what can I do"
- Updates trigger app refreshes

### 3.3 Surfaces: How Apps Appear in the Shell

Apps expose **surfaces** — the UI pieces they want in the shell:

#### Tree Surfaces
Explorer-style hierarchical views in the sidebar:
- **Chat app**: Channels tree (`#general`, `#mesh-dev`)
- **Mesh app**: Pods/realms tree with health indicators
- **Media app**: Artists, albums, playlists
- **Gov app**: F1000 members, policies, proposals

#### Panel Surfaces
Main content areas in the editor grid (usually webviews):
- **Chat app**: Message log with rich formatting
- **Forum app**: Thread view with nested replies
- **Media app**: Album detail, now playing
- **Mesh app**: Health dashboard with charts

#### Status Surfaces
Indicators in the status bar:
- **Connection status**: Online/offline/degraded
- **Realm/pod badge**: Current location
- **F1000 badge**: "You are F1000 here"
- **Sync status**: Replication progress

#### Command Surfaces
Actions in command palette / menus:
- `mesh.chat.newChannel` – Create new channel
- `mesh.mesh.connectToPod` – Switch pods
- `mesh.media.play` – Play selected track
- `mesh.gov.proposePolicy` – Submit governance proposal

**Key Point**: An app is just "a collection of surfaces + logic to talk to pods".

---

## 4. The App Host: Central Coordinator

To keep this architecture clean, we introduce a single **App Host** component.

### 4.1 Responsibilities

#### Context Management
```
┌──────────────────┐
│ ContextManager   │
├──────────────────┤
│ • getContext()   │
│ • setContext()   │
│ • onChange()     │
└──────────────────┘
```
- Tracks current realm/pod/user
- Persists last location
- Notifies apps on changes

#### Manifest Registry
```
┌──────────────────┐
│ ManifestRegistry │
├──────────────────┤
│ • registerApp()  │
│ • getApps()      │
│ • getAppById()   │
└──────────────────┘
```
- Maintains catalog of available apps
- Reads app manifests (initially in code, later from files/pods)

#### Surface Hosting
```
┌─────────────────────┐
│ Surface Hosts       │
├─────────────────────┤
│ • TreeSurfaceHost   │
│ • PanelSurfaceHost  │
│ • StatusSurfaceHost │
│ • CommandHost       │
└─────────────────────┘
```
- Reads manifests
- Creates actual VS Code views/commands
- Wires up data flow

### 4.2 Pod API Bridge

Instead of every app hand-rolling HTTP clients, we define a **URI scheme**:

```
pods://chat/listChannels
pods://mesh/listPods
pods://mesh/getPodHealth?podId=pod-1234
pods://media/listAlbums
pods://gov/listF1000Members
```

The **PodApiClient** in App Host:
- Resolves these URIs to actual HTTP endpoints
- Adds authentication headers
- Uses current context (realm/pod/user)
- Enforces per-app permissions
- Provides consistent error handling

**Security Model**:
```
App "mesh.chat" → allowed: ["chat"]
App "mesh.media" → allowed: ["media"]
App "mesh.mesh" → allowed: ["mesh", "context"]
```

Before executing any `pods://` call, verify the calling app has permission for that namespace.

### 4.3 Webview Bridge

Panel surfaces are often rich web UIs (React/Vue/Svelte) in webviews.

**Message Flow**:
```
Webview (untrusted)
  ↓ postMessage({ type: 'podsRequest', uri: 'pods://chat/...' })
App Host (trusted)
  ↓ validate permissions
  ↓ call PodApiClient
  ↓ HTTP to Pod
Pod (server)
  ↑ HTTP response
App Host
  ↑ postMessage({ type: 'podsResponse', data: {...} })
Webview
```

**Security Benefits**:
- Webviews cannot make arbitrary HTTP calls to pods
- All traffic goes through controlled bridge
- Auth injection is centralized
- Permissions are enforced per-app

### 4.4 App Manifest Example

```yaml
app_id: "mesh.chat"
name: "Chat"
version: "0.1.0"

requires:
  - "context.realm"
  - "context.pod"

permissions:
  allowedNamespaces: ["chat"]

surfaces:
  - type: "tree"
    id: "chat.channels"
    title: "Channels"
    data_source: "pods://chat/listChannels"

  - type: "panel"
    id: "chat.main"
    title: "Chat"
    entrypoint: "app://mesh.chat/main"

  - type: "status"
    id: "chat.connection"
    text_source: "pods://chat/status"

commands:
  - id: "mesh.chat.newChannel"
    title: "New Channel"
    action: "pods://chat/createChannel"
```

The App Host reads this and:
1. Creates a tree view in sidebar
2. Registers a command to open chat panel
3. Creates a status bar item
4. Enforces that this app can only call `pods://chat/*`

---

## 5. The Pod Side: Clean HTTP/JSON API

For the App Host to work, pods expose a **consistent API**:

### 5.1 Response Envelope (All Endpoints)

**Success**:
```json
{
  "ok": true,
  "error": null,
  "data": { /* domain payload */ }
}
```

**Failure**:
```json
{
  "ok": false,
  "error": {
    "code": "NOT_FOUND",
    "message": "Channel does not exist",
    "details": { "channelId": "chan-123" }
  },
  "data": null
}
```

### 5.2 API Domains

#### Context
- `GET /api/context` → realm/pod/user info

#### Mesh
- `GET /api/mesh/pods` → list of known pods
- `GET /api/mesh/pods/:id/health` → health metrics

#### Chat
- `GET /api/chat/channels` → list channels
- `POST /api/chat/channels` → create channel
- `GET /api/chat/channels/:id/messages` → get messages
- `POST /api/chat/messages` → send message

#### Media (Future)
- `GET /api/media/artists`
- `GET /api/media/albums/:id`
- `POST /api/media/play`

#### Gov (Future)
- `GET /api/gov/f1000/members`
- `GET /api/gov/policies`
- `POST /api/gov/policies/:id/vote`

### 5.3 Mapping to pods:// URIs

```
pods://mesh/getContext        → GET /api/context
pods://mesh/listPods          → GET /api/mesh/pods
pods://mesh/getPodHealth?...  → GET /api/mesh/pods/:id/health
pods://chat/listChannels      → GET /api/chat/channels
pods://chat/createChannel     → POST /api/chat/channels
pods://chat/sendMessage       → POST /api/chat/messages
```

The App Host translates URIs → HTTP calls.

---

## 6. Desktop and Web: Two Runtimes, Same Apps

### Desktop Client
```
Code OSS (Electron)
  ├── App Host Extension
  ├── Core Apps (mesh.chat, mesh.mesh, etc.)
  └── User Settings
```
- Native desktop application
- Installable locally
- Connects to user's pods
- Full system integration

### Web Client
```
VS Code Web (Browser)
  ├── App Host Extension (same code)
  ├── Core Apps (same code)
  └── User Settings
```
- Runs in browser
- Served by openvscode-server or similar
- Same apps, same manifests
- No installation required

**Key Design Principle**: Write apps in a web-compatible way (no Node-only APIs in core).

### Why This Works

The **App Host** and **app manifests** are designed to be **runtime-agnostic**:
- VS Code is just the first **host** that interprets manifests
- Future hosts (pure web SPA, mobile app) could render the same apps
- Apps talk to pods via standard HTTP/JSON, not desktop-specific APIs

---

## 7. Architectural Boundaries

### 7.1 Not Over-Coupled to VS Code

**What's Portable**:
- App manifest format
- Context model (`MeshContext`)
- `pods://` URI scheme
- Pod API shapes

**What's VS Code-Specific**:
- Surface hosts (create TreeView, WebviewPanel, etc.)
- Extension activation model

If we later want a **pure web dashboard** or **mobile app**:
- Reuse the manifests
- Reuse the Pod API
- Write new surface hosts for that platform

### 7.2 Trust and Permissions

**First-Party Apps** (trusted):
- `mesh.chat`, `mesh.mesh`, `mesh.media`, etc.
- Bundled with App Host
- Full access to their designated namespaces

**Third-Party Apps** (future):
- Explicitly installed by user
- Declare required permissions in manifest
- App Host enforces permission scopes
- Cannot access other apps' namespaces

### 7.3 Network Discipline

**Strict Rule**: All pod communication goes through PodApiClient.

**Why**:
- Centralized auth injection
- Consistent error handling
- Permission enforcement
- Health-aware routing (future)
- Content/abuse policy enforcement

**Prohibited**: Direct `fetch()` from webviews to pods.

### 7.4 Multi-Pod / Multi-Realm Hygiene

**Context Binding**:
- Every surface is bound to specific context
- Context includes realm + pod + user
- No mixing data from different pods unless explicitly labeled

**Cross-Scope Views** (when needed):
- Org-level dashboards
- Federated social feeds
- Cross-realm bridges

These are **explicitly designed** with clear visual indicators showing data sources.

---

## 8. Implementation Phases

### Phase 1: Foundation (Mock Data)
**Goal**: Prove the architecture

- App Host skeleton
- ContextManager
- ManifestRegistry
- Two stub apps: `mesh.mesh`, `mesh.chat`
- Mock PodApiClient (in-memory data)
- Tree views + basic webview panels

**Outcome**: Working shell with mock data, no real networking.

### Phase 2: Real Pod API
**Goal**: Connect to actual pods

- Implement Pod API endpoints (context, mesh, chat)
- Wire PodApiClient to real HTTP calls
- Add authentication/sessions
- Basic error handling

**Outcome**: Shell talks to real pods, data persists.

### Phase 3: Rich Apps
**Goal**: Production-quality UIs

- Build proper webview frontends (React/Svelte)
- Add real-time updates (WebSocket)
- Implement all chat features
- Add media app
- Add forum app

**Outcome**: Usable client for daily ecosystem use.

### Phase 4: Distribution
**Goal**: Ship to users

- Package desktop app (Electron)
- Deploy web app (openvscode-server)
- Add auto-update
- Documentation and onboarding

**Outcome**: Users can install and use the shell.

---

## 9. Benefits of This Architecture

### Unified Client Experience
- Same UI on desktop and web
- Apps work identically in both
- Single codebase for client-side logic

### Modular and Extensible
- Add new apps without rewriting shell
- Apps are declarative (manifests)
- Easy to disable/enable features

### Security by Design
- Centralized permission enforcement
- No direct pod access from untrusted code
- Clear trust boundaries

### Multi-Tenancy Ready
- Context model supports realm/pod switching
- No data leakage between pods
- Can manage multiple identities

### Not Reinventing Wheels
- VS Code handles layout/tiling
- No custom windowing system
- Battle-tested UI framework

### Future-Proof
- Manifest format is host-agnostic
- Can support other shells later
- Pod API is independent of client tech

---

## 10. What Success Looks Like

**For Users**:
- One app for everything (chat, pods, media, governance, code)
- Rearrangeable dashboard that fits their workflow
- Works on desktop and web with same experience

**For Developers**:
- Clear contracts (manifests, Pod API, `pods://` URIs)
- Easy to add new apps
- Testable components (mock PodApiClient)
- Cross-platform by default

**For the Ecosystem**:
- Professional, polished client experience
- Security-first architecture
- Easy onboarding for new users
- Foundation for mobile/other platforms later

---

## Summary

We're building a **universal shell** for the mesh/slskd ecosystem:
- Uses VS Code workbench as tiled layout engine
- Apps are modular, manifest-driven components
- App Host coordinates everything (context, surfaces, permissions)
- Apps talk to pods via controlled `pods://` URIs
- Same architecture runs on desktop and web

This gives us a professional, extensible client without reinventing UI frameworks, and positions us for future expansion to other platforms.
