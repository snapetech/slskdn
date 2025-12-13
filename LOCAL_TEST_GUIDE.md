# Local Dev Build Test Guide

## Services Running

✅ **Backend (slskd)**: http://localhost:5030
- Process: PID 70699
- Logs: /tmp/slskd-server.log
- DHT: Active with 59-62 nodes
- Overlay Port: 50305

✅ **Frontend (React Dev Server)**: http://localhost:3000
- Process: PID 71881
- Logs: /tmp/slskdn-web-dev.log
- Hot reload: Enabled

## Testing the Mesh Identity Refactoring

### 1. Access the Web UI
Open: http://localhost:3000

### 2. Check DHT/Overlay Status
- Look at the footer - you should see DHT, NAT, and Overlay icons
- Hover over them to see connection counts
- DHT should show ~60 nodes
- Overlay should show connection status

### 3. Check Mesh Peer Registry
Once logged in, check the API:
```bash
curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:5030/api/v0/dht/overlay/connections | jq
```

### 4. Monitor Logs for Mesh Activity
Watch for mesh peer registration and hash sync:
```bash
tail -f /tmp/slskd-server.log | grep -E "Registered mesh peer|Auto-starting hash sync|MeshPeerId"
```

### 5. Test Mesh-Only Peer Connection
The system should automatically:
- Discover peers via DHT
- Register them with mesh peer IDs
- Initiate hash sync
- Work without requiring Soulseek usernames

### 6. Check Mesh Peer Registry Database
```bash
sqlite3 ~/.config/slskd/mesh_peers.db "SELECT * FROM mesh_peers;"
```

### 7. Check Username Mapping
```bash
sqlite3 ~/.config/slskd/mesh_identity_mapper.db "SELECT * FROM username_mesh_mapping;"
```

## What to Look For

### ✅ Success Indicators:
- DHT shows ~60 nodes in footer
- Overlay icon lights up when peers connect
- Logs show "Registered mesh peer" messages
- Hash sync initiates automatically
- No errors about missing usernames for mesh operations

### ❌ Issues to Watch For:
- "Cannot register connection without mesh peer ID" warnings
- Failed handshakes
- Signature verification errors (expected - crypto not implemented yet)
- Overlay connection failures

## Key Log Messages

**Peer Discovery**:
```
[INF] DHT found X peers for rendezvous hash
[INF] Registered mesh neighbor peer-abc123...
```

**Mesh Registration**:
```
[INF] Registered mesh peer peer-abc123... (username: <none>)
[DBG] Mapped Soulseek username X to mesh peer peer-abc123...
```

**Auto Hash Sync**:
```
[DBG] Auto-starting hash sync with peer-abc123...
[INF] Successfully initiated hash sync with peer-abc123...
```

## Stopping/Restarting

### Stop Everything:
```bash
pkill -f "dotnet run --project src/slskd"
pkill -f "npm start"
```

### Restart Backend:
```bash
cd /home/keith/Documents/Code/slskdn
dotnet run --project src/slskd > /tmp/slskd-server.log 2>&1 &
```

### Restart Frontend:
```bash
cd /home/keith/Documents/Code/slskdn/src/web
npm start > /tmp/slskdn-web-dev.log 2>&1 &
```

## Quick Commands

```bash
# Watch backend logs
tail -f /tmp/slskd-server.log

# Watch frontend logs
tail -f /tmp/slskdn-web-dev.log

# Check processes
ps aux | grep -E "slskd|npm start"

# Check ports
ss -tlnp | grep -E "3000|5030|50305"

# Rebuild backend
cd /home/keith/Documents/Code/slskdn && dotnet build src/slskd/slskd.csproj
```

## Testing Scenarios

1. **Mesh-Only Peer Test** (future - requires second instance):
   - Spin up another instance without Soulseek login
   - Watch it discover and connect via DHT
   - Verify hash sync works without username

2. **Username Mapping Test**:
   - Connect with Soulseek username
   - Check mapping database
   - Verify bidirectional lookups work

3. **Mixed Network Test**:
   - Some peers with usernames, some without
   - All should work seamlessly
   - Logs show correct handling of both types

**READY TO TEST!** 🚀















