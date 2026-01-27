# Bridge Proxy Wrapper Design

> **Alternative to Soulfind Fork**: Build a lightweight proxy server that implements the Soulseek protocol and forwards to slskdn bridge API.

---

## Problem

The bridge design (Phase 6X) requires a Soulseek-compatible server that legacy clients can connect to. The original design suggested forking Soulfind and adding proxy mode, but:

- **Maintenance burden**: Forking means maintaining a separate codebase
- **Complexity**: Soulfind may be written in a different language (D, Python, etc.)
- **Flexibility**: We need exactly what we need, not a full server

## Solution: Lightweight Protocol Proxy

Build a **minimal Soulseek protocol proxy** in C# that:

1. **Implements Soulseek protocol wire format** (handshake, login, search, download, rooms)
2. **Forwards operations to slskdn bridge API** (`/api/bridge/search`, `/api/bridge/download`, etc.)
3. **Translates responses** from bridge API format to Soulseek protocol format
4. **Handles connection management** (multiple legacy clients)

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Legacy Client (Nicotine+, SoulseekQt, Seeker)         │
│  Uses standard Soulseek protocol (TCP :2242)           │
└────────────────┬────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│  BridgeProxyServer (C# - new component)                 │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Soulseek Protocol Handler                       │   │
│  │  - Handshake, Login, Search, Download, Rooms     │   │
│  │  - Wire format parsing/serialization             │   │
│  └──────────────┬──────────────────────────────────┘   │
│                 │                                        │
│  ┌──────────────▼──────────────────────────────────┐   │
│  │  Bridge API Client                                │   │
│  │  - POST /api/bridge/search                        │   │
│  │  - POST /api/bridge/download                      │   │
│  │  - GET /api/bridge/rooms                           │   │
│  └──────────────┬──────────────────────────────────┘   │
└─────────────────┼───────────────────────────────────────┘
                  │ HTTP (localhost:5030)
                  ▼
┌─────────────────────────────────────────────────────────┐
│  slskdn Bridge API (existing)                            │
│  - BridgeApi (mesh integration)                         │
│  - BridgeController (HTTP endpoints)                     │
└─────────────────────────────────────────────────────────┘
```

---

## Implementation Plan

### Component 1: Soulseek Protocol Parser

**New**: `src/slskd/VirtualSoulfind/Bridge/Protocol/SoulseekProtocolParser.cs`

```csharp
namespace slskd.VirtualSoulfind.Bridge.Protocol
{
    /// <summary>
    /// Parses Soulseek protocol messages from legacy clients.
    /// </summary>
    public class SoulseekProtocolParser
    {
        // Handshake: LoginRequest, LoginResponse
        // Search: SearchRequest, SearchResponse
        // Download: DownloadRequest, DownloadResponse
        // Rooms: RoomListRequest, RoomListResponse, RoomJoinRequest, RoomJoinResponse
    }
}
```

**Reference**: Use Soulseek.NET library's protocol definitions as reference, or implement minimal subset.

### Component 2: Bridge Proxy Server

**New**: `src/slskd/VirtualSoulfind/Bridge/Proxy/BridgeProxyServer.cs`

```csharp
namespace slskd.VirtualSoulfind.Bridge.Proxy
{
    /// <summary>
    /// TCP server that accepts Soulseek protocol connections and proxies to bridge API.
    /// </summary>
    public class BridgeProxyServer : BackgroundService
    {
        private readonly TcpListener listener;
        private readonly IBridgeApi bridgeApi;
        private readonly IHttpClientFactory httpClient;
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            listener.Start();
            
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client, stoppingToken), stoppingToken);
            }
        }
        
        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            // 1. Handshake
            // 2. Login (validate with bridge API)
            // 3. Handle requests:
            //    - Search → POST /api/bridge/search → format as Soulseek response
            //    - Download → POST /api/bridge/download → proxy transfer
            //    - Rooms → GET /api/bridge/rooms → format as Soulseek response
        }
    }
}
```

### Component 3: Protocol Message Types

**New**: `src/slskd/VirtualSoulfind/Bridge/Protocol/SoulseekMessages.cs`

Minimal message types needed:
- `LoginRequest`, `LoginResponse`
- `SearchRequest`, `SearchResponse`
- `DownloadRequest`, `DownloadResponse`
- `RoomListRequest`, `RoomListResponse`

---

## Advantages Over Forking Soulfind

1. **No external dependency**: Pure C# implementation
2. **Minimal code**: Only implement what we need (search, download, rooms)
3. **Full control**: Can customize exactly for bridge use case
4. **Easy maintenance**: Same codebase, same language
5. **Better integration**: Direct access to slskdn services

---

## Implementation Tasks

**T-851-Wrapper**: Build bridge proxy server
- ✅ T-851.1: Soulseek protocol parser (handshake, login, search, download, rooms) - **COMPLETE**
- ✅ T-851.2: BridgeProxyServer TCP listener and client handler - **COMPLETE**
- ✅ T-851.3: Protocol message serialization/deserialization - **COMPLETE**
- ✅ T-851.4: Integration with BridgeApi (HTTP client) - **COMPLETE**
- ✅ T-851.5: Transfer proxying (download progress forwarding) - **COMPLETE**
- ✅ T-851.6: Connection management (multiple clients, cleanup) - **COMPLETE**
- ✅ T-851.7: Authentication (optional password check) - **COMPLETE**
- ✅ T-851.8: Error handling and graceful degradation - **COMPLETE**

## Implementation Status (2026-01-27)

**Core Implementation**: ✅ **COMPLETE**

- **Protocol Parser**: `SoulseekProtocolParser.cs` - Implements binary protocol with little-endian encoding
  - Message format: [4 bytes: length] [4 bytes: type] [N bytes: payload]
  - Supports: Login, Search, Download, RoomList messages
  - String encoding: UTF-8 with 4-byte length prefix

- **Proxy Server**: `BridgeProxyServer.cs` - TCP server with client session management
  - Accepts multiple concurrent clients (configurable max)
  - Handles handshake, login, and request routing
  - Integrated with BridgeApi for search, download, and room operations

- **Integration**: Fully integrated with existing BridgeApi
  - Search requests → BridgeApi.SearchAsync → formatted as Soulseek search response
  - Download requests → BridgeApi.DownloadAsync → download response
  - Room list requests → BridgeApi.GetRoomsAsync → formatted as Soulseek room list

**Note**: Protocol format may need refinement based on actual client testing. Current implementation follows reverse-engineered protocol specification.

## Completed Features (2026-01-27)

**All Tasks Complete**: ✅ **T-851.1 through T-851.8**

### T-851.5: Transfer Progress Proxying
- Background task `PushProgressUpdatesAsync` monitors transfer progress
- Sends progress updates to legacy clients via `FileTransfer` message type
- Updates sent every 5% to avoid spam
- Automatic cleanup when transfer completes or client disconnects

### T-851.6: Enhanced Connection Management
- Proper cleanup in `StopAsync` method
- Stops all progress proxies on shutdown
- Cleanup in `finally` blocks for client sessions
- Tracks active transfers and proxies per client

### T-851.7: Authentication Implementation
- Password validation against `BridgeOptions.Password`
- Configurable via `RequireAuth` flag
- Error responses for invalid credentials
- Logging of authentication attempts

### T-851.8: Error Handling & Graceful Degradation
- Comprehensive try-catch blocks around message handling
- Graceful handling of client disconnections (IOException)
- Error responses sent to clients for failures
- Continues processing other clients on individual client errors
- Proper resource cleanup in finally blocks

---

## Alternative: Use Soulseek.NET Client Library

**Option**: Soulseek.NET already implements the protocol. We could:

1. **Extract protocol code** from Soulseek.NET (if open source)
2. **Use as reference** to implement minimal server-side parser
3. **Reuse message types** if compatible license

**Check**: Is Soulseek.NET open source? Can we reuse protocol definitions?

---

## Recommendation

**Build the wrapper** rather than forking Soulfind:

- ✅ Faster to implement (we control the code)
- ✅ Better integration (direct C# → C#)
- ✅ Easier maintenance (one codebase)
- ✅ More flexible (can add features as needed)

**Timeline**: 1-2 weeks for basic implementation (search, download, rooms).

---

## Next Steps

1. Research Soulseek protocol specification (or use Soulseek.NET as reference)
2. Implement minimal protocol parser
3. Build BridgeProxyServer
4. Integrate with existing BridgeApi
5. Test with Nicotine+ client
