# Ecosystem Shell Documentation Index

This directory contains comprehensive documentation for the **Ecosystem Shell** – a universal client for the mesh/slskd ecosystem.

## 📚 Document Trilogy

Read these documents in order to understand the full architecture:

### 1. [Ecosystem Shell Architecture](./ecosystem-shell-architecture.md) ← START HERE
**Purpose**: High-level conceptual overview  
**Audience**: Everyone (devs, designers, stakeholders)

**Covers**:
- Why we're building this (universal dashboard for ecosystem)
- What it is (VS Code as tiled UI framework)
- How apps work (manifest-driven, context-aware)
- How everything connects (App Host as coordinator)
- Security and boundaries
- Implementation phases

**Key Quote**: 
> "Not 'an editor with chat bolted on'  
> Instead: 'A dashboard/window manager for pods, chat, media, governance, etc. (that can also edit code)'"

**Read this first** to understand the vision and architecture.

---

### 2. [App Host Design](./app-host-design.md)
**Purpose**: Client-side implementation specification  
**Audience**: Client developers

**Covers**:
- VS Code extension structure
- Context management (`ContextManager`)
- App manifests and registry (`ManifestRegistry`)
- Surface hosting (trees, panels, status, commands)
- Pod API client (`PodApiClient`, `pods://` URIs)
- Webview bridge (secure message passing)
- Permissions model
- Desktop + web compatibility

**Implementation Tasks**: See Phase 8 in [TASK_STATUS_DASHBOARD.md](./docs/archive/status/TASK_STATUS_DASHBOARD.md)
- T-APPHOST-01 through T-APPHOST-08

**Read this** to implement the client shell.

---

### 3. [Pod API Design](./pod-api-design.md)
**Purpose**: Server-side API specification  
**Audience**: Server/backend developers

**Covers**:
- HTTP/JSON API structure
- Standard response envelope (`{ ok, error, data }`)
- Error codes (`UNAUTHORIZED`, `NOT_FOUND`, etc.)
- Domain endpoints:
  - Context: `GET /api/context`
  - Mesh: `GET /api/mesh/pods`, health endpoints
  - Chat: Channels and messages CRUD
- Mapping to `pods://` URIs
- Security considerations

**Implementation Tasks**: See Phase 9 in [TASK_STATUS_DASHBOARD.md](./docs/archive/status/TASK_STATUS_DASHBOARD.md)
- T-PODAPI-01 through T-PODAPI-06

**Read this** to implement the server-side API.

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│              VS Code Workbench (UI Shell)               │
│  ┌───────────────────────────────────────────────────┐  │
│  │           App Host Extension                      │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │ ContextManager                              │  │  │
│  │  │ - Tracks realm/pod/user                     │  │  │
│  │  │ - Notifies apps on changes                  │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │ ManifestRegistry                            │  │  │
│  │  │ - Catalogs available apps                   │  │  │
│  │  │ - Loads manifests                           │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │ Surface Hosts                               │  │  │
│  │  │ - TreeSurfaceHost    (sidebar trees)        │  │  │
│  │  │ - WebviewSurfaceHost (main panels)          │  │  │
│  │  │ - StatusSurfaceHost  (status bar)           │  │  │
│  │  │ - CommandHost        (commands/palette)     │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │ PodApiClient                                │  │  │
│  │  │ - Translates pods:// URIs → HTTP            │  │  │
│  │  │ - Injects auth                              │  │  │
│  │  │ - Enforces per-app permissions              │  │  │
│  │  └──────────────┬──────────────────────────────┘  │  │
│  └─────────────────┼─────────────────────────────────┘  │
└────────────────────┼────────────────────────────────────┘
                     │ HTTP/JSON
                     │ GET /api/context
                     │ GET /api/mesh/pods
                     │ GET /api/chat/channels
                     │ POST /api/chat/messages
                     ▼
          ┌──────────────────────┐
          │   Pod (Server)       │
          │  ┌────────────────┐  │
          │  │ Context API    │  │
          │  │ Mesh API       │  │
          │  │ Chat API       │  │
          │  │ (Future: Media,│  │
          │  │  Forum, Gov)   │  │
          │  └────────────────┘  │
          └──────────────────────┘
```

---

## 🎯 Key Concepts

### Context
```typescript
interface MeshContext {
  realm: { id: string; name: string };
  pod: { id: string; name: string; roles: string[] };
  user: { id: string; displayName: string; roles: string[] };
}
```
Global state tracked by App Host, consumed by all apps.

### Apps
Modular components with manifests describing their surfaces:
- `mesh.chat` – Channels, messages
- `mesh.mesh` – Pod explorer, health dashboards
- `mesh.media` – Library, playback (future)
- `mesh.gov` – F1000, policies (future)

### Surfaces
UI pieces apps declare:
- **Tree**: Sidebar hierarchical views (channels, pods, artists)
- **Panel**: Main content (webviews with React/Svelte/etc.)
- **Status**: Status bar indicators (connection, context)
- **Command**: Actions in palette/menus

### pods:// URIs
Abstraction over Pod HTTP API:
```
pods://chat/listChannels     → GET /api/chat/channels
pods://chat/sendMessage      → POST /api/chat/messages
pods://mesh/listPods         → GET /api/mesh/pods
```

App Host translates these to HTTP calls with auth and permissions.

---

## 🚀 Getting Started

### For Client Developers
1. Read [ecosystem-shell-architecture.md](./ecosystem-shell-architecture.md)
2. Read [app-host-design.md](./app-host-design.md)
3. Implement Phase 8 tasks from [TASK_STATUS_DASHBOARD.md](./docs/archive/status/TASK_STATUS_DASHBOARD.md)
4. Start with T-APPHOST-01 (extension skeleton)

### For Server Developers
1. Read [ecosystem-shell-architecture.md](./ecosystem-shell-architecture.md)
2. Read [pod-api-design.md](./pod-api-design.md)
3. Implement Phase 9 tasks from [TASK_STATUS_DASHBOARD.md](./docs/archive/status/TASK_STATUS_DASHBOARD.md)
4. Start with T-PODAPI-01 (response envelope)

### For Both
Client and server can be developed **in parallel**:
- Client uses mock `PodApiClient` initially
- Server provides in-memory endpoints
- Connect them once both are working

---

## 📋 Implementation Tasks

See [TASK_STATUS_DASHBOARD.md](./docs/archive/status/TASK_STATUS_DASHBOARD.md) for complete task lists:

**Phase 8: App Host** (8 tasks)
- Extension skeleton, context, manifests, surfaces, permissions

**Phase 9: Pod API** (6 tasks)
- Response envelope, context endpoint, mesh endpoints, chat endpoints

---

## 🎨 Design Principles

### Security First
- All pod access via controlled PodApiClient
- Per-app permission namespaces
- No direct HTTP from webviews
- Centralized auth injection

### Context-Aware
- Everything knows realm/pod/user
- Apps react to context changes
- No data mixing across pods/realms

### Modular
- Apps are declarative manifests
- Surface hosts interpret manifests
- Easy to add/remove apps

### Cross-Platform
- Same code on desktop (Electron) and web (browser)
- Web-compatible APIs only
- No Node-specific dependencies in core

### Not Coupled to VS Code
- Manifest format is host-agnostic
- `pods://` URIs are portable
- Future: Other shells can use same apps

---

## 🔮 Future Enhancements

After initial implementation (mock data, basic surfaces):

1. **Rich App UIs**: React/Svelte webview frontends
2. **Real-Time Updates**: WebSocket for live chat, status changes
3. **More Apps**: Media, Forum, Governance
4. **Persistence**: SQLite/database for pod API
5. **Authentication**: Real session management
6. **Health-Aware Routing**: PodApiClient picks best pod
7. **Mobile Client**: iOS/Android shells using same manifests
8. **Third-Party Apps**: Plugin system with sandboxing

---

## 📖 Additional Documentation

Other relevant docs in this directory:
- [TASK_STATUS_DASHBOARD.md](./docs/archive/status/TASK_STATUS_DASHBOARD.md) – All tasks
- [security-hardening-guidelines.md](./security-hardening-guidelines.md) – Security requirements
- [CURSOR-META-INSTRUCTIONS.md](./CURSOR-META-INSTRUCTIONS.md) – Development rules

---

## 💬 Questions?

**Q: Why VS Code specifically?**  
A: Battle-tested tiled UI framework, cross-platform (desktop + web), extensible, familiar to developers.

**Q: Can we use a different shell later?**  
A: Yes! The manifest format, context model, and `pods://` URIs are designed to be shell-agnostic.

**Q: What about mobile?**  
A: Same architecture, different surface hosts. React Native or native apps can interpret the same manifests.

**Q: Is this just for developers?**  
A: No. VS Code is just the shell. Users interact with ecosystem apps (chat, media, etc.), not code by default.

**Q: What if I don't want chat/media/etc.?**  
A: Disable those apps. Only load what you need. It's modular.

---

**Last Updated**: December 11, 2025  
**Status**: Design complete, ready for implementation
