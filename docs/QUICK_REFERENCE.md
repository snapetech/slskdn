# Quick Reference: New Features & Services

## 🔐 Ed25519 Cryptography
**What**: Production-grade cryptographic signatures for all mesh operations  
**Why**: Prevents impersonation, MITM attacks, and replay attacks  
**How**: NSec library, automatic key generation on first run  
**Files**: `LocalMeshIdentityService.cs`, `MeshPeerDescriptor.cs`

**Check Status**:
```bash
cat ~/.config/slskdn/mesh-identity.key
journalctl -u slskd | grep "Ed25519"
```

---

## 🌐 BitTorrent Rendezvous
**What**: Deterministic torrent for peer discovery (alternative to DHT-only)  
**Why**: Better discovery in restricted networks, DHT redundancy  
**How**: All instances create identical torrent, join same swarm  
**Files**: `BitTorrent/RendezvousTorrentService.cs`

**Enable**:
```yaml
bittorrent:
  enableRendezvousTorrent: true
```

**Check Status**:
```bash
ls ~/.config/slskdn/bittorrent/
journalctl -u slskd | grep "rendezvous"
```

---

## 🔄 Multi-Source Swarm (Mesh-First)
**What**: Download files from multiple sources simultaneously  
**Why**: Faster downloads, redundancy, rare file availability  
**How**: Prioritizes overlay > soulseek > other transports  
**Files**: `Swarm/SwarmDownloadOrchestrator.cs`

**Check Status**:
```bash
curl http://localhost:5030/api/v0/swarm/jobs
```

---

## 🌉 Soulseek Community Bridge
**What**: Feeds mesh/DHT/BT discoveries back to Soulseek network  
**Why**: Alternative discovery enhances the whole community  
**How**: Auto-registers discovered peers with Soulseek server  
**Files**: `SoulseekMeshBridgeService.cs`, `MeshSearchBridgeService.cs`

**Check Status**:
```bash
journalctl -u slskd | grep "Bridged user"
curl http://localhost:5030/api/v0/dht/mesh/peers
```

---

## 📊 Mesh Identity System
**What**: Cryptographic peer identity (Ed25519-based MeshPeerId)  
**Why**: Decouples mesh features from Soulseek presence  
**How**: MeshPeerId primary key, Soulseek username optional alias  
**Files**: `Mesh/Identity/*`

**Check Status**:
```bash
curl http://localhost:5030/api/v0/mesh/peers
```

---

## 🔍 Quick Diagnostics

### Check DHT Status
```bash
curl http://localhost:5030/api/v0/dht/status
```

### Check Mesh Peers
```bash
curl http://localhost:5030/api/v0/dht/mesh/peers | jq '.[] | {meshPeerId, username, endpoint}'
```

### Check BitTorrent Peers (if enabled)
```bash
journalctl -u slskd | grep -E "BitTorrent|BT peers"
```

### Check Bridge Activity
```bash
journalctl -u slskd | grep -E "Bridged|mesh-discovered" | tail -20
```

### Monitor Real-Time Activity
```bash
journalctl -u slskd -f | grep -E "mesh|bridge|crypto|BT"
```

---

## 📝 Configuration Templates

### Minimal (DHT Only)
```yaml
dht:
  enabled: true
  overlayPort: 50305
```

### Full Stack (DHT + BT + All Features)
```yaml
dht:
  enabled: true
  overlayPort: 50305
  beaconPort: 50306
  enableBeacon: true

bittorrent:
  enableRendezvousTorrent: true
  port: 6881
  maxRendezvousPeers: 50
  enableDht: true
  enablePex: true

soulseek:
  username: "YourUsername"
  password: "YourPassword"
```

---

## 🐛 Troubleshooting

### "0 peers discovered"
- Check firewall allows UDP on DHT port
- Verify DHT is enabled in config
- Check logs for self-discovery filtering
- Wait 2-5 minutes for DHT to populate

### "Signature verification failed"
- Old mesh-identity.key from before crypto implementation
- Delete `~/.config/slskdn/mesh-identity.key` and restart
- New Ed25519 key will be generated automatically

### "BitTorrent rendezvous not starting"
- Verify `enableRendezvousTorrent: true` in config
- Check logs for tracker connection errors
- Ensure port not blocked by firewall

### "Bridge not working"
- Verify logged into Soulseek
- Check DHT is enabled
- Bridge runs every 5 minutes (wait for next cycle)
- Check logs for "Bridged user" messages

---

## 📚 Documentation Index

| Document | Purpose |
|----------|---------|
| `DEVELOPMENT_PROGRESS_DEC_2025.md` | Complete feature list and progress |
| `ENHANCEMENTS_COMPLETE.md` | Technical details of 3 enhancements |
| `SOULSEEK_BRIDGE_STRATEGY.md` | Philosophy and community integration |
| `MESH_IDENTITY_COMPLETE.md` | Mesh identity refactoring summary |
| `LOCAL_TEST_GUIDE.md` | Local development testing |

---

## 🎯 Common Tasks

### Enable All Features
```bash
# Edit config
nano ~/.config/slskdn/slskdn.yml

# Add:
dht.enabled: true
bittorrent.enableRendezvousTorrent: true

# Restart
systemctl restart slskd
```

### Monitor Everything
```bash
watch -n 1 'curl -s http://localhost:5030/api/v0/dht/status | jq'
```

### Check Crypto Health
```bash
# Should see Ed25519 key loaded
journalctl -u slskd -b | grep "Ed25519"

# Should see handshake signatures
journalctl -u slskd | grep "signature" | tail -10
```

### Verify Bridge Working
```bash
# Should see bridged users
journalctl -u slskd | grep "✓ Bridged" | tail -10

# Check mesh peers with usernames
curl http://localhost:5030/api/v0/dht/mesh/peers | jq '.[] | select(.username != null)'
```

---

## 🚀 Performance Tips

1. **DHT works best with 50-200 nodes** - Give it 5 minutes to stabilize
2. **BitTorrent adds 10-50 peers** - Enable if DHT discovery is slow
3. **Bridge runs every 5 minutes** - Patience for user discovery
4. **Overlay connections are TCP** - Requires port forwarding for best results

---

## ⚠️ Important Notes

- **Ed25519 keys are generated once** - Backup `mesh-identity.key` if needed
- **BitTorrent is opt-in** - Disabled by default for privacy
- **Bridge is automatic** - No config needed if DHT enabled
- **All features are additive** - Existing Soulseek functionality unchanged

---

*Quick Reference - slskdn December 2025 Release*
