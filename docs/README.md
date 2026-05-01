# Documentation Index

Complete guide to all slskdn documentation.

## 🚀 Quick Start

- **[Getting Started](getting-started.md)** ← **Start here!** Complete guide for new users
- **[Troubleshooting](troubleshooting.md)** - Common issues and solutions
- **[Advanced Features](advanced-features.md)** - Detailed walkthrough of advanced features
- [System Requirements](system_requirements.md) - Hardware and software requirements
- [Configuration](config.md) - Complete configuration reference
- [Building from Source](build.md) - Build instructions
- [Docker Deployment](docker.md) - Container setup
- [Reverse Proxy Setup](reverse_proxy.md) - Running behind a proxy
- [Known Issues](known_issues.md) - Current known problems

## 📘 User Guides

- [SongID and Discovery](songid-discovery.md) - Native identification and Discovery Graph workflows
- [System Admin Surfaces](system-surfaces.md) - Guided System UI for policies, integrations, diagnostics, source providers, and local preferences
- [Pods, Rooms, and Messages](pods-and-rooms.md) - Gold Star, pod rooms, unified messages, and listen-along boundaries
- [Soulseek Type-1 Obfuscation](soulseek-type1-obfuscation.md) - Default-on compatibility posture, mode semantics, runtime status, and safety caveats
- [Pod Private Service Gateway](pod-vpn/vpn-user-guide.md) - Pod-scoped private service tunnels over mesh; not host VPN routing or internet egress
- [Lidarr Integration](lidarr-integration.md) - Configure Lidarr wanted sync, auto-download, path mapping, and import behavior
- [Listening Party and Player](listening-party.md) - Integrated player, streaming, visualizers, and listen-along behavior
- [Virtual Soulfind User Guide](VIRTUAL_SOULFIND_USER_GUIDE.md) - Using Virtual Soulfind and Shadow Index
- [Solid Integration User Guide](SOLID_USER_GUIDE.md) - Using Solid WebID and Solid-OIDC integration

## 📖 Design Documents

### Core Features
- [Multi-Source Downloads](multipart-downloads.md) - Network impact analysis and architecture
- [DHT Rendezvous Design](DHT_RENDEZVOUS_DESIGN.md) - Peer discovery and mesh overlay architecture
- [Music Discovery Federation Plan](design/music-discovery-federation-plan.md) - Planned mesh/social discovery features without backup or mirroring scope

### Security
- [Security Implementation Specs](SECURITY_IMPLEMENTATION_SPECS.md) - Detailed security feature specifications
- [CSRF Testing Guide](security/CSRF_TESTING_GUIDE.md) - CSRF protection testing and validation
- [Security Comparison Analysis](security/SECURITY_COMPARISON_ANALYSIS.md) - Comparison with upstream slskd
- [Documentation Audit - Security Claims](archive/audits/DOCUMENTATION_AUDIT_SECURITY_CLAIMS.md) - Security claims review

## 🔧 Implementation Guides

- [How It Works](HOW-IT-WORKS.md) - Technical architecture overview
- [Features Overview](FEATURES.md) - Complete feature list and details
- [Lidarr Integration](lidarr-integration.md) - First-class plugin-free Lidarr wanted sync, download handoff, and safe post-download import
- [Soulseek Type-1 Obfuscation](soulseek-type1-obfuscation.md) - Peer-message obfuscation options and runtime activation plan
- [VPN Agent](../src/slskdN.VpnAgent/README.md) - Host-side fail-closed VPN routing and forwarded-port integration
- [System Admin Surfaces](system-surfaces.md) - Guided System UI and operator panels
- [Implementation Roadmap](IMPLEMENTATION_ROADMAP.md) - Development status and planned features

## 📚 Development Documentation

- [Development History](archive/DEVELOPMENT_HISTORY.md) - Feature completion timeline and releases
- [Fork Vision](archive/FORK_VISION.md) - Project philosophy and roadmap
- [Contributing](../CONTRIBUTING.md) - How to contribute to the project
- [API Documentation](api-documentation.md) - Complete API reference
- [Local Development](dev/LOCAL_DEVELOPMENT.md) - Development environment setup, including git hook installation

## 🔍 Additional Resources

- [Relay Mode](relay.md) - Relay server configuration
- [Migrations](migrations.md) - Database migration guide
- [Upstream Bug Testing](upstream-bug-testing.md) - Testing upstream issues

---

**Note**: Historical implementation notes live under `docs/archive/` and may not
match current defaults. Prefer this index, [Getting Started](getting-started.md),
[Configuration](config.md), and feature-specific user guides for current
behavior.
