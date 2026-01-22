# App Host Design – VS Code Workbench as Ecosystem Shell

This document is an implementation brief for building a **VS Code-based client shell** for the mesh/slskdn ecosystem.

You (the code assistant) are expected to:

- Use the **VS Code workbench** (Code OSS / VS Code Web) as the **primary UI shell**.
- Implement an **App Host** extension that:
  - Manages context (realm/pod/user).
  - Loads small **app manifests**.
  - Renders app **surfaces** into the VS Code layout (tree views, panels, status items, commands).
  - Bridges all app traffic to pods via a centralized **Pod API client**.
- Keep the design:
  - **Web-compatible** (runs in desktop and web).
  - **Security-conscious** (no direct pod access from webviews; everything goes through the host).
  - **Extensible** (apps are described by manifests, not hard-coded view wiring).


## 1. Goals

1. **Universal shell:**
   - Use VS Code's workbench (activity bar, side bar, panels, editor grid, status bar) as a tiled UI framework.
   - Treat it as a **dashboard/window manager**, not only a text editor.

2. **Apps:**
   - Represent each ecosystem module as an **App**:
     - `mesh.chat`, `mesh.forum`, `mesh.media`, `mesh.mesh` (pods/realms), `mesh.gov`, etc.
   - Each app provides multiple **surfaces** inside the shell (trees, panels, status indicators, commands).

3. **Context-aware:**
   - Everything operates within a clear **context**:
     - `realm` (which universe),
     - `pod` (which pod within that realm),
     - `user` (current authenticated identity),
     - optional `org`.
   - Context is managed centrally and passed down to apps/views.

4. **Desktop + Web:**
   - Same App Host and app code must run in:
     - Code OSS (desktop, Electron-based).
     - VS Code Web (browser, via something like openvscode-server).
   - No reliance on Node-only APIs in the core App Host that break web execution.

5. **Security & control:**
   - Apps never talk directly to pods over the network.
   - All pod access is routed through a controlled **PodApiClient** in the App Host.
   - Enforce per-app permissions for which pod APIs they can touch.


## 2. Core Concepts

### 2.1 App

An **App** is a functional module in the ecosystem.

- Identified by a stable `app_id` (e.g., `mesh.chat`, `mesh.mesh`).
- Has:
  - `name`: human label.
  - One or more **surfaces**:
    - `tree` – explorer-style hierarchical views.
    - `panel` – main content areas (usually webviews).
    - `status` – status bar entries.
    - `command` – actions exposed in command palette/menus.

Initial target apps (stubbed, not fully functional):

- `mesh.mesh` – "Mesh Explorer":
  - Shows realms, pods, basic health.
- `mesh.chat` – "Chat":
  - Channels + basic chat panel.

These two will prove the model.

### 2.2 Context

Define a central **context** structure, something like:

```ts
interface MeshContext {
  realmId: string | null;
  podId: string | null;
  userId: string | null;
  // later: orgId, roles, etc.
}
```

Requirements:

- Context is:
  - Owned and updated by a single `ContextManager`.
  - Observable (e.g., event emitter or Rx-like subject) so surfaces can react.
- No surface should guess context on its own; always call `ContextManager.getContext()` or subscribe to updates.

### 2.3 Surfaces

Each app can declare multiple **surfaces**. Conceptual types:

- `tree`:
  - Renders into VS Code sidebar as a `TreeView`.
  - Typical uses:
    - Channel list,
    - Realms/pods,
    - Artists/books/shelves,
    - Governance objects.

- `panel`:
  - Renders into the editor area as a `WebviewPanel`.
  - Typical uses:
    - Chat UI,
    - Forum UI,
    - Media detail view,
    - Mesh health dashboard.

- `status`:
  - Renders as a `StatusBarItem`.
  - Typical uses:
    - Connection status,
    - Realm/pod indicator,
    - "You are F1000" badge.

- `command`:
  - VS Code commands, visible in the palette.
  - Typical uses:
    - "Open Chat".
    - "Connect to Pod".
    - "New Channel".

The App Host is responsible for turning these abstract surface declarations into actual VS Code views and commands.

## 3. App Manifest Model

We want a small, structured way to describe what an app needs from the shell.

### 3.1 Manifest (conceptual)

We define an **App Manifest** YAML/JSON structure (this doc is conceptual; actual format can be JSON for easier parsing):

```yaml
app_id: "mesh.chat"
name: "Chat"
version: "0.1.0"

requires:
  - "context.realm"
  - "context.pod"

surfaces:
  - type: "tree"
    id: "chat.channels"
    title: "Channels"
    location: "sidebar"
    data_source: "pods://chat/listChannels"

  - type: "panel"
    id: "chat.main"
    title: "Chat"
    location: "editor"
    kind: "webview"
    entrypoint: "app://mesh.chat/main"

  - type: "status"
    id: "chat.connection"
    text_source: "pods://chat/status"
    click_command: "mesh.chat.openConnectionDetails"

commands:
  - id: "mesh.chat.newChannel"
    title: "New Channel"
    action: "pods://chat/createChannel"
```

Key points:

- `requires`:
  - Declare what this app expects from context; App Host can disable surfaces if requirements aren't met.
- `surfaces`:
  - Abstract description; App Host instantiates specific VS Code artifacts.
- `entrypoint`:
  - Logical reference to a webview frontend bundle.
  - App Host resolves `app://mesh.chat/main` to an actual resource URL.
- `data_source` / `action`:
  - Use a `pods://` URI scheme as an abstraction over the Pod API.

You **do not** need to fully implement a manifest loader up front; it's acceptable to start by hard-coding manifests in TypeScript as structured objects and then move to file-based manifests later.

## 4. App Host Responsibilities

Implement a single **App Host** extension module that does the following:

### 4.1 ContextManager

- API:

  ```ts
  class ContextManager {
    getContext(): MeshContext;
    setContext(next: Partial<MeshContext>): void;
    onDidChangeContext(listener: (ctx: MeshContext) => void): vscode.Disposable;
  }
  ```

- Responsibilities:
  - Store the current context in memory.
  - Optionally persist minimal bits (last pod/realm) in VS Code global state.
  - Provide events so surfaces can react to context changes.

- For now, you can hard-code a single `realmId`/`podId` or expose a simple command to set them.

### 4.2 ManifestRegistry

- Maintain a registry of app manifests:

  ```ts
  interface AppManifest { /* as per model above */ }

  class ManifestRegistry {
    registerManifest(manifest: AppManifest): void;
    getApps(): AppManifest[];
    getAppById(id: string): AppManifest | undefined;
  }
  ```

- Initial implementation:
  - Register two manifests in code:
    - `mesh.mesh` (Mesh Explorer stub).
    - `mesh.chat` (Chat stub).
  - Later: load from `json`/`yaml` files or from pods.

### 4.3 Surface Hosts

Implement host classes for each surface type:

- `TreeSurfaceHost`:
  - Reads all `type: "tree"` surfaces from manifests.
  - Creates a `TreeView` for each.
  - Uses a `TreeDataProvider` that:
    - Calls a resolver for `data_source` URIs to get items (mock data first).

- `WebviewSurfaceHost`:
  - Reads `type: "panel"` surfaces.
  - Creates `WebviewPanel`s when requested (via commands or activation).
  - Loads a bundled or external HTML/JS entrypoint.
  - Injects:
    - Initial context.
    - A message bridge to extension host.

- `StatusSurfaceHost`:
  - Reads `type: "status"` surfaces.
  - Creates `StatusBarItem`s and updates their text based on `text_source` URIs or context.

- `CommandHost`:
  - Registers VS Code commands from `commands` in manifests.
  - On execution, dispatches to:
    - `pods://` actions via PodApiClient, or
    - Surface creation/show logic.

### 4.4 PodApiClient & `pods://` URIs

Introduce a simple **PodApiClient** responsible for translating `pods://` URIs into actual Pod API calls.

- For now, implement **stubs**:
  - Recognize patterns such as:
    - `pods://chat/listChannels`
    - `pods://mesh/listPods`
  - Return mock data structures appropriate for tree/webview demos.

- Later, wire this to the real Pod HTTP/WebSocket APIs.

Responsibilities:

- Attach auth/session info from `ContextManager` when calling real APIs.
- Enforce per-app permissions (see below).
- Provide a single typed method, e.g.:

  ```ts
  interface PodsRequest {
    uri: string; // e.g. "pods://chat/listChannels"
    payload?: unknown;
  }

  interface PodsResponse {
    ok: boolean;
    error?: string;
    data?: unknown;
  }

  class PodApiClient {
    request(req: PodsRequest): Promise<PodsResponse>;
  }
  ```

All surfaces (tree/webview/commands) that need pod data should go through this client, not call fetch/axios directly.

### 4.5 Webview Bridge

For `panel` surfaces (webviews):

- Expose a minimal JS bridge:
  - Inside the webview, apps will call something like:

    ```js
    const vscode = acquireVsCodeApi();

    vscode.postMessage({
      type: 'podsRequest',
      uri: 'pods://chat/listChannels',
      payload: { /* ... */ }
    });
    ```

  - The App Host listens for messages, uses `PodApiClient`, then posts back:

    ```ts
    webview.postMessage({
      type: 'podsResponse',
      correlationId,
      ok,
      data,
      error
    });
    ```

- On creation, send initial:
  - `MeshContext` (realm/pod/user).
  - Any relevant app-specific config.

Ensure:

- Webviews cannot directly reach pods via `fetch` with pod URLs.
  They should be instructed to **only** talk through this bridge, so the App Host controls auth and permissions.

## 5. Security & Permissions (Initial Skeleton)

From the beginning, enforce some basic rules:

- Treat the App Host and core apps as **trusted**, but design for future third-party apps with restricted scopes.

- Implement an `AppPermissions` concept:

  ```ts
  interface AppPermissions {
    allowedNamespaces: string[]; // e.g. ["chat", "mesh"]
  }

  // Example mapping:
  // "mesh.chat" -> ["chat"]
  // "mesh.mesh" -> ["mesh"]
  ```

- Before executing `pods://chat/...` for a given app, verify that:
  - The app is allowed to access the `chat` namespace.
  - If not, reject and log the attempt.

This check lives in/around `PodApiClient.request()`.

## 6. Desktop and Web Considerations

### 6.1 Desktop

- Target Code OSS / VS Code desktop:
  - App Host extension must activate and run there.
  - Use VS Code SecretStorage for any future auth tokens (not needed for stubs).

### 6.2 Web

- Target VS Code Web (as served by something like openvscode-server):
  - App Host must avoid Node-only APIs directly in activation code.
  - Use `fetch` only through web-compatible APIs when real Pod API calls are added.
  - For now: mocks only, no real network.

App Host logic and app manifests must work identically in desktop and web.

## 7. Initial Implementation Scope

The initial implementation of the App Host should:

1. Define `MeshContext` and `ContextManager`.
2. Define an in-memory `ManifestRegistry`.
3. Register **two stub apps** in manifests:
   - `mesh.mesh` with:
     - A tree view ("Realms & Pods", with mock data).
     - A panel ("Mesh Diagnostics", simple HTML).
   - `mesh.chat` with:
     - A tree view ("Channels", mock data).
     - A panel ("Chat", simple echo UI).
4. Implement:
   - `TreeSurfaceHost` using mock `PodApiClient`.
   - `WebviewSurfaceHost` with a trivial webview and message bridge.
   - `CommandHost` for a couple of simple commands (e.g., "Open Mesh Explorer", "Open Chat").
5. Keep everything **self-contained and safe**:
   - No real network usage yet.
   - No direct filesystem writes beyond VS Code APIs.

Later phases can replace mock `PodApiClient` with real API calls and expand the app set.
