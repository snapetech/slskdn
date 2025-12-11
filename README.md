# slskdn

**A feature-rich distribution of [slskd](https://github.com/slskd/slskd)** with batteries-included UX, advanced download features, protocol extensions, and network enhancements for Soulseek.

> **Note**: slskdn is not just a fork—it's a complete distribution with bundled opinions, advanced features, and experimental subsystems. While based on slskd's excellent foundation, slskdn diverges significantly in scope and philosophy.

---

## ✨ Features (v0.24.1-slskdn.27)

### Multi-Source Downloads ✅
Download files from multiple peers simultaneously, dramatically improving speed and reliability.

| Feature | Status |
|---------|--------|
| **Parallel chunk downloads** | ✅ Working |
| **Automatic source discovery** | ✅ Working |
| **Intelligent stitching** | ✅ Working |
| **Failure resilience** | ✅ Working |
| **Content verification (SHA256)** | ✅ Working |

### DHT Peer Discovery ✅
Discover other slskdn users via BitTorrent DHT:

| Feature | Status |
|---------|--------|
| **DHT bootstrap** | ✅ 60+ nodes |
| **Mesh overlay network** | ✅ TLS-encrypted P2P |
| **Hash database sync** | ✅ Epidemic sync |
| **Peer greeting service** | ✅ Auto-discovery |
| **NAT detection** | ✅ UPnP/NAT-PMP |

### Security Hardening ✅
Zero-trust security framework with defense-in-depth:

| Feature | Status |
|---------|--------|
| **NetworkGuard** | ✅ Rate limiting, connection caps |
| **ViolationTracker** | ✅ Auto-escalating bans |
| **PathGuard** | ✅ Directory traversal prevention |
| **ContentSafety** | ✅ Magic byte verification |
| **PeerReputation** | ✅ Behavioral scoring |
| **CryptographicCommitment** | ✅ Pre-transfer hash commitment |
| **ProofOfStorage** | ✅ Random chunk challenges |
| **ByzantineConsensus** | ✅ 2/3+1 voting for multi-source |
| **EntropyMonitor** | ✅ RNG health checks |
| **FingerprintDetection** | ✅ Reconnaissance detection |
| **Honeypot & CanaryTraps** | ✅ Threat profiling |

### UI Enhancements ✅

| Feature | Status |
|---------|--------|
| **SlskdnStatusBar** | ✅ Live DHT/mesh/hash stats |
| **Network tab** | ✅ Mesh overlay monitoring |
| **Security tab** | ✅ Security dashboard |
| **Footer bar** | ✅ GitHub, Discord links |

---

## 📖 Is Multi-Source Damaging to the Network?

**No.** Multi-source downloads distribute load across peers instead of hammering a single user. The impact is equivalent to multiple individual users downloading a file — which already happens organically.

- ✅ Respects slot limits
- ✅ No additional server load (peer-to-peer)
- ✅ Each chunk behaves like a normal download
- ✅ Built-in throttling and fairness mechanisms

📖 **[Full analysis: docs/multipart-downloads.md](docs/multipart-downloads.md)**

---

## 📦 Installation

### Latest Stable Release

**[Download v0.24.1-slskdn.27 →](https://github.com/snapetech/slskdn/releases/tag/v0.24.1-slskdn.27)**

#### Linux Packages

```bash
# Arch Linux (AUR)
yay -S slskdn

# Fedora/RHEL (COPR)
sudo dnf copr enable slskdn/slskdn
sudo dnf install slskdn

# Ubuntu/Debian (PPA)
sudo add-apt-repository ppa:keefshape/slskdn
sudo apt update
sudo apt install slskdn

# openSUSE (OBS)
# Visit: https://software.opensuse.org/package/slskdn
```

#### Docker

```bash
docker pull ghcr.io/snapetech/slskdn:latest
```

---

### 🧪 Latest Development Build

**⚠️ Unstable builds from experimental branches**

<!-- BEGIN_DEV_BUILD -->
**[Development Build dev-20251209-232357 →](https://github.com/snapetech/slskdn/releases/tag/dev-20251209-232357)** 

Version: `0.24.1-dev-20251209-232416` | Branch: `experimental/multi-source-swarm` 

```bash
# Arch Linux (AUR)
yay -S slskdn-dev

# Fedora/RHEL (COPR)
sudo dnf copr enable slskdn/slskdn-dev
sudo dnf install slskdn-dev

# Ubuntu/Debian (PPA)
sudo add-apt-repository ppa:keefshape/slskdn
sudo apt update
sudo apt install slskdn-dev

# Docker
docker pull ghcr.io/snapetech/slskdn:dev
```
<!-- END_DEV_BUILD -->

---

## 🚀 Quick Start (Build from Source)

```bash
# Clone
git clone https://github.com/snapetech/slskdn.git
cd slskdn

# Build
dotnet build src/slskd/slskd.csproj

# Run
dotnet run --project src/slskd/slskd.csproj
```

Open http://localhost:5030 (default credentials: slskd/slskd)

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [Multi-Source Downloads](docs/multipart-downloads.md) | Network impact analysis |
| [DHT Rendezvous Design](docs/DHT_RENDEZVOUS_DESIGN.md) | Peer discovery architecture |
| [Security Specs](docs/SECURITY_IMPLEMENTATION_SPECS.md) | Security feature details |
| [Implementation Roadmap](docs/IMPLEMENTATION_ROADMAP.md) | Development status |
| [Merge Branch Status](docs/MERGE_BRANCH_STATUS.md) | Latest smoke test results |
| [Configuration](docs/config.md) | All configuration options |
| [Building](docs/build.md) | Build instructions |
| [Docker](docs/docker.md) | Container deployment |

---

## 🔧 Configuration

### Enable Security Features

Add to your `slskd.yml`:

```yaml
Security:
  Enabled: true
  Profile: Standard  # Minimal, Standard, Maximum, or Custom
```

### Security Profiles

| Profile | Features |
|---------|----------|
| **Minimal** | NetworkGuard, ViolationTracker only |
| **Standard** | + PeerReputation, Consensus, Fingerprinting |
| **Maximum** | All features including Honeypots |

---

## ⚠️ Experimental Status

This is an **experimental distribution** of slskd with advanced features. Many features are in active development and may change. Use at your own risk.

### Feature Status

| Feature Category | Status | Notes |
|------------------|--------|-------|
| **Multi-Source Downloads** | ✅ Stable | Production-ready with concurrency limits |
| **DHT Peer Discovery** | ✅ Stable | Fully functional mesh overlay |
| **Security Hardening** | ✅ Stable | Comprehensive security framework |
| **UI Enhancements** | ✅ Stable | Status bars, network monitoring |
| **PodCore** | 🟡 Experimental | Subject to change, API may evolve |
| **Rescue Mode** | 🟡 Experimental | Advanced features may change |
| **Backfill Pipeline** | 🟡 Experimental | Conservative scheduling, may be refined |

**Note**: Features marked as "✅ Stable" are production-ready. Features marked as "🟡 Experimental" are functional but may have API changes or refinements in future releases.

For the stable upstream client, see [slskd/slskd](https://github.com/slskd/slskd).

---

## 🤝 Contributing

PRs welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## 📜 License

AGPL-3.0 — See [LICENSE](LICENSE) for details.

---

<p align="center">
  <em>"slop on top"</em> 🍦🤖✨
</p>
