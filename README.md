# slskdn

**An experimental fork of [slskd](https://github.com/slskd/slskd)** exploring advanced download features, protocol extensions, and network enhancements for Soulseek.

---

## 🚀 Experimental Features

### Multi-Source Downloads

Download files from multiple peers simultaneously, dramatically improving speed and reliability.

| Feature | Benefit |
|---------|---------|
| **Parallel chunk downloads** | Faster completion times |
| **Automatic source discovery** | Finds all peers with matching files |
| **Intelligent stitching** | Seamlessly assembles chunks |
| **Failure resilience** | Continues from other sources if one fails |

#### Is This Damaging to the Network?

**No.** Multi-source downloads distribute load across peers instead of hammering a single user. The impact is equivalent to multiple individual users downloading a file — which already happens organically.

- ✅ Respects slot limits
- ✅ No additional server load (peer-to-peer)
- ✅ Each chunk behaves like a normal download
- ✅ Built-in throttling and fairness mechanisms

📖 **[Full analysis: docs/multipart-downloads.md](docs/multipart-downloads.md)**

---

### DHT Peer Discovery

Discover other slskdn users via BitTorrent DHT for enhanced features:

- **Mesh overlay network** — secure, TLS-encrypted peer-to-peer communication
- **Hash database sync** — share file fingerprints for better matching
- **Source ranking** — prioritize reliable, fast peers

📖 **[Design document: docs/DHT_RENDEZVOUS_DESIGN.md](docs/DHT_RENDEZVOUS_DESIGN.md)**

---

### Protocol Extensions

Experimental enhancements to the Soulseek protocol:

- Chunked/multi-part transfers
- Content verification (SHA256)
- Peer reputation tracking
- Acoustic fingerprint matching

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [Multi-Source Downloads](docs/multipart-downloads.md) | Network impact analysis |
| [DHT Rendezvous Design](docs/DHT_RENDEZVOUS_DESIGN.md) | Peer discovery architecture |
| [Implementation Roadmap](docs/IMPLEMENTATION_ROADMAP.md) | Development status |
| [Configuration](docs/config.md) | All configuration options |
| [Building](docs/build.md) | Build instructions |
| [Docker](docs/docker.md) | Container deployment |

---

## ⚠️ Experimental Status

This is an **experimental fork**. Features are in active development and may change. Use at your own risk.

For the stable upstream client, see [slskd/slskd](https://github.com/slskd/slskd).

---

## 🔒 Security

slskdn includes defense-in-depth security features:

- **Input validation** — all peer data is untrusted and validated
- **Rate limiting** — prevents abuse and DoS attacks
- **Path sanitization** — prevents directory traversal
- **Content verification** — detects file type mismatches
- **Violation tracking** — auto-escalating bans for bad actors

---

## License

AGPL-3.0 — See [LICENSE](LICENSE) for details.
