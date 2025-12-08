# slskdn DEV Build

> *"Here be dragons. Rather friendly ones, actually."*

**Branch:** `experimental/multi-source-swarm`  
**Status:** 🧪 Experimental — working, but expect rough edges

---

## What's New

### 🔧 Fixes
- **AUR Package Checksums** — Proper hashes now; rebuilds behave themselves
- **DI Wiring Tidied** — Can't have loose ends, can we?
- **Security Component Headers** — Copyright attribution sorted

---

## 🚀 Experimental Features

Everything from the stable release, plus:

### ⚡ Multi-Source Downloads
Download files from multiple peers simultaneously. Dramatically improves speed and reliability for popular files.

| Feature | Status |
|---------|--------|
| **Parallel chunk downloads** | ✅ Working |
| **Automatic source discovery** | ✅ Working |
| **Intelligent stitching** | ✅ Working |
| **Failure resilience** | ✅ Working |
| **Content verification (SHA256)** | ✅ Working |

> **Is this damaging to the network?** No. Each chunk behaves like a normal download. We respect slot limits. The load is distributed rather than hammering a single user. It's equivalent to multiple users downloading the same file — which already happens organically.

### 🌐 DHT Peer Discovery
Find other slskdn users via BitTorrent DHT:

| Feature | Status |
|---------|--------|
| **DHT bootstrap** | ✅ 60+ nodes |
| **Mesh overlay network** | ✅ TLS-encrypted P2P |
| **Hash database sync** | ✅ Epidemic protocol |
| **Peer greeting service** | ✅ Auto-discovery |
| **NAT detection** | ✅ UPnP/NAT-PMP |

### 🛡️ Security Hardening
Zero-trust security framework with defence-in-depth:

| Feature | Status |
|---------|--------|
| **NetworkGuard** | ✅ Rate limiting, connection caps |
| **ViolationTracker** | ✅ Auto-escalating bans |
| **PathGuard** | ✅ Directory traversal prevention |
| **ContentSafety** | ✅ Magic byte verification |
| **PeerReputation** | ✅ Behavioural scoring |
| **CryptographicCommitment** | ✅ Pre-transfer hash commitment |
| **ProofOfStorage** | ✅ Random chunk challenges |
| **ByzantineConsensus** | ✅ 2/3+1 voting for multi-source |
| **EntropyMonitor** | ✅ RNG health checks |
| **FingerprintDetection** | ✅ Reconnaissance detection |
| **Honeypot & CanaryTraps** | ✅ Threat profiling |

### 🖥️ UI Additions
| Feature | Description |
|---------|-------------|
| **SlskdnStatusBar** | Live DHT/mesh/hash statistics in the header |
| **Network Tab** | Mesh overlay monitoring dashboard |
| **Security Tab** | Security feature dashboard |

---

## 📦 Installation

```bash
# Arch Linux (AUR)
yay -S slskdn-dev

# Docker
docker pull ghcr.io/snapetech/slskdn:dev
```

---

## ⚠️ Experimental Status

This is bleeding-edge code. Features are actively developed and may change. If you prefer stability, use the main `slskdn` package instead.

For the conservative upstream approach, see [slskd/slskd](https://github.com/slskd/slskd). They prefer scripts. We prefer batteries.

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [Multi-Source Downloads](docs/multipart-downloads.md) | Network impact analysis |
| [DHT Rendezvous Design](docs/DHT_RENDEZVOUS_DESIGN.md) | Peer discovery architecture |
| [Security Specs](docs/SECURITY_IMPLEMENTATION_SPECS.md) | Security feature details |

---

<p align="center"><em>"slop on top"</em> 🍦🤖✨</p>

