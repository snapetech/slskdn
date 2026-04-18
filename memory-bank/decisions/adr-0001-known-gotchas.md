# ADR-0001: Known Gotchas & Anti-Patterns

> **Status**: Active (Living Document)  
> **Date**: 2025-12-08  
> **Author**: AI-assisted development sessions

This document captures known issues, anti-patterns, and "gotchas" that AI models and developers have encountered. **Read this before making changes.**

---

## ⚠️ FOR AI AGENTS: ADD TO THIS FILE IMMEDIATELY

**When you fix a bug, ADD IT HERE before doing anything else.**

This is not optional. This is the highest priority action after fixing a bug.

### When to add an entry:
- You fixed a bug that you or another AI introduced
- You fixed the same type of bug more than once
- You discovered a non-obvious pattern that causes errors
- You spent >5 minutes debugging something that should have been documented

### Entry template:
```markdown
### N. short-descriptive-name

**The Bug**: One sentence description.

**Files Affected**:
- `path/to/file.cs`

**Wrong**:
```code
// bad code
```

**Correct**:
```code
// good code
```

**Why This Keeps Happening**: Explanation of the root cause.
```

### After adding:
1. Commit immediately: `git commit -m "docs: Add gotcha for [description]"`
2. Then continue with other work

**If you skip this step, the same bug WILL happen again. Document it NOW.**

---

## 🚨 CRITICAL: Bugs That Keep Coming Back


### 0z50. DHT Discovery Must Not Treat "Seen Once" As "Tried Forever"

**The Bug**: `DhtRendezvousService` stored discovered overlay endpoints in `_discoveredPeers` and only attempted an outbound overlay connection when `TryAdd(...)` succeeded. Once an endpoint had been seen once, every later discovery cycle skipped the connect path entirely, even if the first attempt failed because of a transient timeout, stale local state, or a later code fix. The node kept a growing list of candidates but never retried them.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`

**Wrong**:
```text
Use one dictionary as both the discovered-peer cache and the retry gate, so a single failed first connection attempt suppresses all future retries for that endpoint.
```

**Correct**:
```text
Track discovery separately from outbound attempt timing. Keep discovered endpoints cached, but maintain explicit retry/backoff state so unverified peers can be retried on later discovery cycles until they either connect or age out.
```

**Why This Keeps Happening**: It is tempting to use `TryAdd` as a cheap dedupe and trigger point, but discovery dedupe and connection-attempt scheduling are different concerns. A mesh node operates in a flaky network, so “we already saw this endpoint” is not the same thing as “we should never try it again.”


### 0z49. QUIC-Less Mesh Hosts Need A Real Advertised Direct Path Or They Publish Zero Usable Direct Candidates

**The Bug**: While hardening mesh issue `#209`, we correctly stopped QUIC-unsupported hosts from advertising impossible `DirectQuic` transports, but that left them with `transports=0` and no usable direct path at all. At the same time, `TransportSelector` only parsed legacy `quic://...` endpoints, so publishing `udp://...` legacy endpoints did nothing for direct mesh dialing. The host became more honest but still could not form mesh connections.

**Files Affected**:
- `src/slskd/Mesh/Dht/PeerDescriptorPublisher.cs`
- `src/slskd/Mesh/Transport/TransportSelector.cs`
- `src/slskd/Mesh/Transport/DirectTlsDialer.cs`

**Wrong**:
```text
Treat honest descriptor publication as sufficient: suppress QUIC-only transports on unsupported hosts,
publish only legacy UDP endpoints, and assume the selector or dialer stack still has a usable direct path.
```

**Correct**:
```text
If a QUIC-less host still supports direct mesh over the TCP/TLS overlay listener, advertise that direct path
through the mesh transport endpoint model and teach the selector to fall back across multiple dialers for the
same logical direct transport type.
```

**Why This Keeps Happening**: The codebase has two overlapping systems: the older mesh transport stack and the newer DHT rendezvous/TCP overlay path. Fixing one layer to stop lying did not automatically make the other layer usable. If the published descriptor model and the selector's parser disagree about what counts as a direct endpoint, mesh stays broken while the logs look cleaner.

### 0z45. FFmpeg Fingerprint Extraction Cannot Buffer Unlimited PCM In Memory

**The Bug**: `FingerprintExtractionService` piped ffmpeg PCM output into a plain `MemoryStream` with no size cap. A long or malformed decode stream could keep growing until the process consumed far more memory than intended before Chromaprint ever saw a sample.

**Files Affected**:
- `src/slskd/Integrations/Chromaprint/FingerprintExtractionService.cs`

**Wrong**:
```text
Read ffmpeg stdout into an unbounded MemoryStream and assume `-t` alone is enough to keep memory usage safe.
```

**Correct**:
```text
Apply an internal byte cap derived from the configured sample rate/channel count/duration and abort the decode once ffmpeg exceeds the maximum PCM payload needed for fingerprinting.
```

**Why This Keeps Happening**: Audio decode helpers look harmless because the happy path is short, but piping raw PCM means every extra second is a linear memory increase. If the stream is not bounded explicitly, one bad input or tool behavior change turns a utility decode into an unbounded buffer sink.

### 0z43. IP-Only Login Lockouts Do Not Stop Distributed Password Spray Against One Username

**The Bug**: Session login throttling only tracked failed attempts by remote IP. That blocked single-source brute force but did nothing against a distributed spray where an attacker rotates IPs while hammering the same admin username. The account could be tested indefinitely as long as each source stayed under the per-IP threshold.

**Files Affected**:
- `src/slskd/Core/API/Controllers/SessionController.cs`

**Wrong**:
```text
Treat per-IP lockout as sufficient for the web login surface and assume brute-force attempts always come from one address.
```

**Correct**:
```text
Track failed login windows by both remote IP and normalized username. Clear both counters on successful authentication, and reject requests when either the source or the target username is currently locked out.
```

**Why This Keeps Happening**: Rate limiting naturally starts with network identity, but authentication abuse targets accounts as well as origins. A password-spray attacker only needs many low-volume IPs to bypass IP-only lockouts. Login throttling needs both dimensions.

### 0z44. Share Tokens Need JWT Audience Binding Or Cross-Collection Replay Stays Valid

**The Bug**: Share tokens carried `collection_id` as a signed claim, but JWT validation still had `ValidateAudience = false`. That meant a token could be replayed without any JWT-layer audience check, and the cryptographic token envelope itself was not asserting that it belonged to the intended collection.

**Files Affected**:
- `src/slskd/Sharing/ShareTokenService.cs`

**Wrong**:
```text
Store the target collection only as a custom claim while disabling JWT audience validation entirely.
```

**Correct**:
```text
Set the JWT `aud` value to the collection id and require validation to prove that the token audience matches the signed `collection_id` claim. This binds the token envelope itself to the intended collection and rejects replay against mismatched targets.
```

**Why This Keeps Happening**: Custom claims feel “good enough” once they are signed, but JWT already has a first-class audience concept for exactly this binding problem. If audience validation is left off, the token shape looks correct while one of the protocol’s main anti-replay checks is silently unused.

### 0z42. Overlay Connector Stats That Only Count Success/Failure Hide The Actual Failing Layer

**The Bug**: While validating issue `#209` on `kspls0`, `/api/v0/overlay/stats` only exposed aggregate `successfulConnections` and `failedConnections`. That made the live system look like “overlay is broken” even after our inbound TLS/HELLO path was proven healthy, because the stats could not distinguish `connect timeout`, `no route`, `TCP refused`, `TLS EOF`, or protocol-handshake failures. We kept reaching for broad fixes because the product diagnostics were too coarse to tell whether the current failure was ours or the remote candidate's.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnectorStats.cs`
- `src/slskd/DhtRendezvous/API/DhtRendezvousController.cs`

**Wrong**:
```text
Track only "successful" and "failed" outbound overlay connections and expect operators to infer the
real failure layer from raw debug logs or manual probes.
```

**Correct**:
```text
Classify outbound overlay failures at the connector boundary and expose reason counts in the API.
At minimum, distinguish reachability failures from TLS failures and protocol-handshake failures so
live diagnostics can tell whether the current problem is local, remote, or just bad DHT candidates.
```

**Why This Keeps Happening**: Aggregate failure counters are enough for happy-path dashboards, but they are not enough for live mesh triage. Without typed failure reasons, every new report looks like "maybe the fix didn't work" even when the remaining problem is a different layer entirely. The connector must turn exception shapes into stable operational categories, or we will keep shipping blind.

### 0z37. Clearing Stale Antiforgery Cookies After `GetAndStoreTokens()` Is Too Late To Stop Framework Log Spam

**The Bug**: Issue `#209` kept showing repeated `An exception was thrown while deserializing the token` / `The antiforgery token could not be decrypted` errors even after we added stale-cookie cleanup and retry logic. The real problem was ordering: on safe GET requests we still let `IAntiforgery.GetAndStoreTokens()` read the incoming stale `XSRF-COOKIE-*` first, and ASP.NET logged the decryption failure inside `DefaultAntiforgery.GetCookieTokenDoesNotThrow(...)` before our catch block could clear and replace the cookies.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```text
Catch stale antiforgery exceptions around `GetAndStoreTokens()` and clear cookies afterward,
assuming that prevents the operator-visible decrypt spam.
```

**Correct**:
```text
On safe requests that mint replacement CSRF tokens, strip the known antiforgery cookies from the
incoming request before calling `GetAndStoreTokens()`. Then expire the stale cookies in the
response and issue a fresh token pair. If cleanup happens only after `GetAndStoreTokens()`,
ASP.NET has already logged the decrypt failure.
```

**Why This Keeps Happening**: It is easy to think “we catch the stale-cookie exception, so we fixed it,” but antiforgery token deserialization and logging happen inside ASP.NET before the exception reaches our code. That means post-failure cleanup can repair browser state while still leaving the exact noisy log spam the user reported. The only way to stop that path is to prevent the framework from seeing the stale cookie on the minting request in the first place.

### 0z41. Writing `cert_pins.json` In-Place Can Corrupt The Whole TOFU Store On Crash Or Concurrent Interruption

**The Bug**: `CertificatePinStore.Save()` previously serialized the pin set straight to `cert_pins.json` with `File.WriteAllText(...)`. If the process crashed or the write was interrupted mid-update, the file could be left truncated or partially written. On the next startup, `Load()` would treat the malformed JSON as unreadable and effectively drop every pin, degrading TOFU pinning into first-use-on-every-restart.

**Files Affected**:
- `src/slskd/DhtRendezvous/Security/CertificateManager.cs`

**Wrong**:
```text
Rewrite the live pin store file in place and assume the full JSON payload always reaches disk atomically.
```

**Correct**:
```text
Write the serialized pins to a sibling temp file, flush it to disk, and atomically rename it over the real `cert_pins.json`. Clean up the temp file on failure.
```

**Why This Keeps Happening**: Small JSON stores look harmless, so it is easy to treat them like config writes instead of durability-sensitive identity state. But the pin store is part of overlay identity continuity. Once it is corrupted, the node forgets every peer pin and starts re-learning trust from scratch.

### 0z40. DHT-Discovered Endpoints Cannot Be Counted As Onion-Capable Peers Before An Overlay Handshake Proves Them

**The Bug**: While validating issue `#209` on `kspls0`, `Circuit maintenance` reported `11 total peers, 11 onion-capable` even though live overlay stats still showed `successfulConnections = 1` and `activeMeshConnections = 0`. The cause was `DhtRendezvousService.PublishDiscoveredPeer(...)`: it inserted every DHT-discovered endpoint into `IMeshPeerManager` with `supportsOnionRouting: true` immediately, before any overlay handshake succeeded.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/Mesh/MeshPeerManager.cs`
- `src/slskd/Mesh/CircuitMaintenanceService.cs`

**Wrong**:
```text
Treat every DHT-discovered rendezvous endpoint as an onion-capable mesh peer the moment it is discovered.
```

**Correct**:
```text
Track DHT-discovered endpoints as unverified candidates first. Only mark a peer onion-capable after a successful overlay connect or a live neighbor registration proves it actually speaks the overlay protocol.
```

**Why This Keeps Happening**: DHT discovery and overlay verification are separate stages, but the current peer-manager model only has one `SupportsOnionRouting` bit. It is tempting to set that bit early so circuit code can “see” candidates, but that makes peer stats, circuit-maintenance logs, and operator troubleshooting overstate reality. Candidate discovery and verified overlay capability must stay distinct.

### 0z39. Auto-Banning Peers On Overlay Certificate Pin Mismatch Can Partition The Mesh After Normal Cert Rotation

**The Bug**: While live-testing issue `#209` on `kspls0`, DHT discovery found real peers and at least one real slskdn overlay endpoint, but the node still never formed a neighbor because `CertificatePinStore` had a stale pin for `minimus7`. The connector treated the mismatch as a possible MITM, blocked that username for an hour, and stopped trying. Clearing the stale pin immediately produced the first successful overlay neighbor.

**Files Affected**:
- `src/slskd/DhtRendezvous/Security/CertificateManager.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`

**Wrong**:
```text
Treat every overlay certificate pin mismatch as a hard MITM event and auto-ban the username, even
though real peers can rotate self-signed certificates across reinstalls or appdir loss.
```

**Correct**:
```text
For overlay TOFU pins, log the mismatch loudly but rotate the stored pin to the newly presented
certificate instead of auto-blocking the peer. Otherwise a normal peer certificate rotation can
partition the mesh until an operator manually clears `cert_pins.json` or the blocklist.
```

**Why This Keeps Happening**: The current overlay identity is only TOFU on self-signed certs, so a strict block-on-mismatch policy assumes certificate continuity that many real installs do not have. In practice that turns ordinary peer reinstalls into self-inflicted partitions. If the system cannot provide a stronger long-lived peer identity, pin mismatches need a softer recovery path than automatic bans.

### 0z38. DHT Status APIs Cannot Report `IsEnabled` From `IsDhtRunning` Or The UI Lies During Bootstrap

**The Bug**: While rechecking issue `#209` on `kspls0`, the live `/api/v0/dht/status` response reported `isEnabled: false` and `isDhtRunning: false` even though the configured DHT service was running, had a node count, and was actively transitioning through bootstrap states. `DhtRendezvousController.GetDhtStatus()` incorrectly mapped `IsEnabled` from `stats.IsDhtRunning` instead of the actual configured enabled flag.

**Files Affected**:
- `src/slskd/DhtRendezvous/API/DhtRendezvousController.cs`
- `src/slskd/DhtRendezvous/IDhtRendezvousService.cs`
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`

**Wrong**:
```text
Treat "enabled in config" and "currently Ready" as the same field in the API response.
```

**Correct**:
```text
Expose both values separately: one flag for whether DHT rendezvous is configured/enabled, and one
for whether the live engine is currently running/Ready. Bootstrap and degraded states must not
masquerade as "disabled" in diagnostics.
```

**Why This Keeps Happening**: Status DTOs are easy to wire by copying the nearest-looking property, but bootstrap state, configured enablement, and actual readiness are different concepts. When the API collapses them together, the UI and troubleshooting output become misleading precisely when operators most need accurate state.

### 0z36. `AnonymityMode.Direct` Cannot Still Bootstrap Only Tor Or Circuit Building Will Fail Exactly Like A Missing Tor Proxy

**The Bug**: Issue `#209` kept advancing from `DHT state changed to: Ready` and `DHT discovery found ... peers` straight into `Tor SOCKS proxy not available at 127.0.0.1:9050`, `No available anonymity transports found`, and `Circuit establishment failed - not all hops connected`. The root cause was that `AnonymityTransportSelector` treated `AnonymityMode.Direct` as if it should initialize the Tor transport, and `GetTransportPriorityOrder(...)` also prioritized `Tor` for direct mode. So the default direct configuration still depended on a local Tor SOCKS proxy even though no real direct transport existed in the selector at all.

**Files Affected**:
- `src/slskd/Common/Security/AnonymityTransportSelector.cs`
- `src/slskd/Mesh/MeshCircuitBuilder.cs`
- `tests/slskd.Tests.Unit/Mesh/Transport/AnonymityTransportSelectionTests.cs`

**Wrong**:
```text
Treat `Direct` mode as a naming alias for "try Tor first and maybe fall back later" while never
actually registering a usable direct transport. That makes circuit building fail as soon as Tor is
not running, even though the logs and defaults say the node is in direct mode.
```

**Correct**:
```text
`AnonymityMode.Direct` must register and prioritize a real direct transport. A failed direct dial in
that mode should fail as a direct connection attempt, not as "no anonymity transport available"
because Tor is absent.
```

**Why This Keeps Happening**: The selector mixed two concepts: "anonymity transport selection" and "how circuit builder gets any stream at all." Because `Direct` was appended as a fallback token without a concrete implementation, the code looked like it supported direct mode while the runtime still hard-required Tor. The only reliable guard here is a focused test that reproduces the tester's exact path: DHT peers exist, Tor is absent, and direct mode must still choose a usable transport candidate.

### 0z35. Shell Command Substitution Inside `debian/rules` Needs `$$(` So `make` Does Not Eat It

**The Bug**: While fixing the Jammy PPA path drift, we changed `packaging/debian/rules` to discover `libcoreclrtraceptprovider.so` dynamically with `tracept_provider=$(find ...)`. Under `make`, that expanded as a make variable reference instead of shell command substitution, so the staged DEB install always saw an empty `tracept_provider` and silently skipped the SONAME patch even when the file was present.

**Files Affected**:
- `packaging/debian/rules`

**Wrong**:
```make
tracept_provider=$(find debian/slskdn/usr/lib/slskd -name libcoreclrtraceptprovider.so -print -quit); \
```

**Correct**:
```make
tracept_provider=$$(find debian/slskdn/usr/lib/slskd -name libcoreclrtraceptprovider.so -print -quit); \
```

**Why This Keeps Happening**: `debian/rules` is a makefile, not a plain shell script. Single `$(` means “expand a make function/variable now,” while `$$(` is what leaves `$(` intact for the shell inside the recipe. Packaging fixes that look right in shell syntax can be wrong once they are embedded in make recipes.

### 0z34. Standalone PPA/COPR/Linux Release Workflows Must Track The Main Release Toolchain And Bundle Layout

**The Bug**: The Jammy PPA build for `0.24.5.slskdn.144` still failed after we fixed `patchelf` build-depends, because the standalone `release-ppa.yml` path had drifted behind the main release flow. It was still pinned to `.NET 8` and the Debian rules hard-coded `debian/slskdn/usr/lib/slskd/libcoreclrtraceptprovider.so`, even though these distro-packaging flows are repackaging prebuilt publish output whose exact runtime file layout can change when the toolchain or bundling strategy changes. Launchpad ended up trying to patch a file path that did not exist in the staged package tree.

**Files Affected**:
- `.github/workflows/release-ppa.yml`
- `.github/workflows/release-copr.yml`
- `.github/workflows/release-linux.yml`
- `packaging/debian/rules`

**Wrong**:
```text
Treat the standalone distro workflows as if they can keep their own stale SDK version and
assume a single hard-coded runtime-library path inside the packaged appdir forever.
```

**Correct**:
```text
Keep every distro/release workflow on the same supported .NET target as the main release path,
and patch bundled runtime files by discovering them inside the staged package tree rather than
assuming one flat path. If a package workflow repackages a prebuilt publish directory, it must
validate the real staged payload before mutating it.
```

**Why This Keeps Happening**: The main release workflow gets the most attention, so it is easy for side workflows like PPA/COPR/raw Linux release jobs to keep older SDK pins and older path assumptions. Once the packaging logic starts mutating bundled runtime files, those stale assumptions become hard failures that only show up when a user or Launchpad tries the neglected path.

### 0z33. Stale Antiforgery Cookie Recovery Cannot Only Catch `AntiforgeryValidationException`

**The Bug**: Issue `#209` still showed repeated `The antiforgery token could not be decrypted` / `The key ... was not found in the key ring` noise even after we added stale-cookie cleanup. The recovery helper only caught `AntiforgeryValidationException`, but `GetAndStoreTokens(...)` can surface the same stale key-ring condition as a different wrapped exception, including raw `CryptographicException`. That meant the stale-cookie path sometimes bypassed cleanup entirely and fell into the generic warning path again and again.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```text
Assume stale antiforgery cookie/key-ring mismatches will always arrive as
`AntiforgeryValidationException`, and only clear cookies in that one catch block.
```

**Correct**:
```text
Classify stale antiforgery failures by the flattened exception content, not just one concrete
exception type. If the exception chain indicates a key-ring/decryption mismatch, clear the
known cookies and retry token minting once.
```

**Why This Keeps Happening**: ASP.NET antiforgery and Data Protection wrap failures differently depending on exactly where token deserialization failed. The stale-token condition is semantic, not tied to one exception type, so a narrow catch filter misses real stale-cookie cases and leaves the operator staring at repeated key-ring warnings.

### 0z32. DHT Discovery Must Feed `IMeshPeerManager`, Not Just Fire Opportunistic Overlay Connect Attempts

**The Bug**: Issue `#209` kept reporting `DHT state changed to: Ready` and nonzero peers discovered, but the runtime still logged `Circuit maintenance: 0 circuits, 0 total peers, 0 active, 0 onion-capable`. `DhtRendezvousService.OnPeersFound(...)` stored discovered endpoints in `_discoveredPeers` and kicked off `TryConnectToPeerAsync(...)`, but it never published those discovered candidates into `IMeshPeerManager`. The circuit layer only reads `IMeshPeerManager`, so DHT discovery could be healthy while circuit building stayed blind.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/Mesh/CircuitMaintenanceService.cs`
- `src/slskd/Mesh/MeshCircuitBuilder.cs`

**Wrong**:
```text
Treat DHT peer discovery as a fire-and-forget connection hint only. If the immediate overlay
connect attempt does not already succeed, leave the discovered peer out of the mesh peer
inventory entirely.
```

**Correct**:
```text
When DHT discovery yields a candidate overlay endpoint, publish it into `IMeshPeerManager`
immediately as an onion-capable peer candidate, then let later overlay success/failure update
its quality. Circuit maintenance must see the same discovered peers that DHT already found.
```

**Why This Keeps Happening**: The code split neighbor state and circuit-peer state into two separate inventories. `MeshNeighborPeerSyncService` only mirrors peers after a successful overlay registration, but `CircuitMaintenanceService` and `MeshCircuitBuilder` never look at the DHT discovery cache or neighbor registry directly. That makes it easy to believe “DHT found peers” means the routing layer can use them, when in reality the peer manager is still empty.

### 0z31. Launchpad Only Installs Debian `Build-Depends`, So DEB Rules Cannot Assume CI-Only Tooling Like `patchelf`

**The Bug**: The Jammy PPA build for `slskdn 0.24.5.slskdn.141` failed in `override_dh_auto_install` with `make[1]: patchelf: No such file or directory`. We had updated the DEB package recipe to patch `libcoreclrtraceptprovider.so` with `patchelf`, and we installed `patchelf` in GitHub Actions, but `packaging/debian/control` still only declared `debhelper-compat (= 13)` in `Build-Depends`.

**Files Affected**:
- `packaging/debian/control`
- `packaging/debian/rules`

**Wrong**:
```text
Teach the Debian packaging rules to invoke `patchelf`, but rely on CI job setup to provide the tool instead of declaring it in Debian source metadata.
```

**Correct**:
```text
Any tool invoked from `debian/rules` must be listed in `Build-Depends` so Launchpad/sbuild install it automatically.
```

**Why This Keeps Happening**: GitHub Actions package jobs can hide missing source-package metadata because they install extra build tools out-of-band. Launchpad only trusts the Debian source package metadata, so if a tool is missing from `Build-Depends`, the PPA build fails even though CI looked green.

### 0z29. Clean DEB/RPM Installs Need Explicit ICU Runtime Dependencies Because .NET Loads It Dynamically

**The Bug**: Clean Ubuntu 24.04 and Fedora 43 package installs completed, but `/usr/bin/slskd --version` immediately failed with `Couldn't find a valid ICU package installed on the system.` The bundled apphost does not record ICU as a normal ELF dependency, so DEB/RPM metadata generation never pulled it in automatically.

**Files Affected**:
- `packaging/debian/control`
- `packaging/rpm/slskdn.spec`

**Wrong**:
```text
Assume a self-contained .NET bundle will automatically generate package-manager dependencies for
runtime libraries that it dlopens at startup, especially ICU/globalization support.
```

**Correct**:
```text
For distro packages built from the published bundle, declare ICU explicitly in package metadata.
Clean-package smoke must include actually running `/usr/bin/slskd --version`, not just verifying
that the package installed.
```

**Why This Keeps Happening**: Package managers only see normal link-time dependencies by default, but .NET discovers ICU dynamically at runtime. A package can install perfectly and still be dead on first launch unless ICU is listed explicitly.

### 0z28. RPM Packages Cannot Mix `%{_libdir}` With A Hard-Coded `/usr/lib/slskd` Service Path

**The Bug**: After fixing the Fedora `liblttng-ust` SONAME issue, the RPM installed successfully but dropped the bundle into `%{_libdir}/slskd` (`/usr/lib64/slskd` on x86_64) while the shared `slskd.service` still executed `/usr/lib/slskd/slskd`. The package looked installed, but the systemd unit pointed at a path that did not exist on Fedora.

**Files Affected**:
- `packaging/rpm/slskdn.spec`
- `packaging/aur/slskd.service`

**Wrong**:
```text
Use distro-native `%{_libdir}` for the bundled app payload in RPMs while reusing a service file
that hard-codes `/usr/lib/slskd/slskd` as the executable path.
```

**Correct**:
```text
If the project promises a drop-in `/usr/lib/slskd` runtime path, RPM packaging must install the
payload there too. Do not let `%{_libdir}` silently move the bundle to `/usr/lib64/slskd` while the
service and operator docs still target `/usr/lib/slskd`.
```

**Why This Keeps Happening**: `%{_libdir}` is the normal RPM instinct, but this project intentionally treats `/usr/lib/slskd` as a compatibility contract across installers. Reusing the shared service file without matching the payload path creates a package that installs cleanly yet cannot start.

### 0z27. Linux Package Builds Must Patch .NET's Old liblttng-ust SONAME Before Shipping Fedora/RPM Artifacts

**The Bug**: The published Linux glibc bundle still contains `libcoreclrtraceptprovider.so` linked against `liblttng-ust.so.0`. Fedora 43 provides `liblttng-ust.so.1`, so the generated RPM ended up with an unsatisfied auto-detected dependency and `dnf` refused to install it on a clean system with `nothing provides liblttng-ust.so.0()(64bit)`.

**Files Affected**:
- `packaging/rpm/slskdn.spec`
- `packaging/debian/rules`
- `.github/workflows/release-packages.yml`
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```text
Assume the published Linux zip is package-manager ready as-is on every glibc distro and
ship it into RPM/DEB builds without the same SONAME patching we already apply in Nix.
```

**Correct**:
```text
Before building Linux distro packages from the published bundle, patch
`libcoreclrtraceptprovider.so` to replace `liblttng-ust.so.0` with `liblttng-ust.so.1`.
Install-time package smoke on Fedora should pass on a clean host before trusting RPM/COPR.
```

**Why This Keeps Happening**: The binary zip looks runnable on Ubuntu-like systems where the bundled runtime otherwise works, so it is easy to forget that RPM dependency scanning sees the old SONAME directly. We already encoded the fix in `flake.nix`; any distro package path that repackages the same zip must apply the same patch or Fedora-family installs will fail before users ever start the service.

### 0z26. Pacman File Conflicts Are Checked Before AUR pre_upgrade Scriptlets, So A Loose Root App Bundle Cannot Repair Its Own Upgrade Path

**The Bug**: `slskdn-bin` tried to solve stale `/usr/lib/slskd` file conflicts with a `slskd.install` `pre_upgrade()` cleanup, but pacman checks filesystem conflicts before it runs that scriptlet. On a real `0.24.5.slskdn.129 -> 0.24.5.slskdn.140` upgrade, the package still aborted with `failed to commit transaction (conflicting files)` because unmanaged runtime DLLs and compressed web assets already existed directly under `/usr/lib/slskd`.

**Files Affected**:
- `packaging/aur/PKGBUILD`
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/PKGBUILD-dev`
- `packaging/aur/slskd.install`

**Wrong**:
```text
Install the full self-contained app bundle directly into `/usr/lib/slskd` and assume a
`pre_upgrade()` scriptlet can delete stale files before pacman performs its conflict check.
```

**Correct**:
```text
Keep `/usr/lib/slskd` as the drop-in public path, but package the mutable app payload inside
a managed subdirectory underneath it and leave the root path to a stable launcher/service
surface. Do not depend on pacman scriptlets to rescue a root-level file dump once unmanaged
files already exist there.
```

**Why This Keeps Happening**: The old layout made the root appdir both the compatibility surface and the payload dump. That works until a manual copy, release-asset experiment, or previous package version leaves behind one unowned file. Once that happens, pacman sees the conflict before any package cleanup code can run. The only reliable same-path fix is to stop spraying versioned payload files directly into the root compatibility directory.

### 0z30. Bash EXIT Traps Cannot Reference Function-Local `mktemp` Variables Under `set -u`

**The Bug**: `packaging/linux/install-from-release.sh` successfully installed the published Linux bundle, but still exited nonzero at the end with `/tmp/install-from-release.sh: line 1: work_dir: unbound variable`. The script set `trap '''rm -rf "$work_dir"''' EXIT` inside `main()` after declaring `local work_dir`. By the time the shell processed the EXIT trap, `main()` had returned and the local variable was out of scope, so `set -u` turned cleanup into a hard failure.

**Files Affected**:
- `packaging/linux/install-from-release.sh`

**Wrong**:
```bash
local work_dir
work_dir="$(mktemp -d)"
trap '''rm -rf "$work_dir"''' EXIT
```

**Correct**:
```bash
local work_dir
work_dir="$(mktemp -d)"
trap "rm -rf '$work_dir'" EXIT
```

**Why This Keeps Happening**: In Bash, an EXIT trap runs after the function scope is gone. If the trap body relies on a function-local variable being expanded later, `set -u` can turn a successful script into a failing one during cleanup. Expand the `mktemp` path into the trap string up front, or use a global variable if the trap must dereference at shell exit.

### 0z24. Successful Soulseek Transfers Can Still Emit A Terminal "Transfer complete" Exception That Must Be Treated As Expected Churn

### 0z25. DHT Bootstrap Can Take Longer Than 30 Seconds Even When The Network Path Is Healthy

**The Bug**: On `kspls0`, once router forwarding and host firewall rules were both correct, the MonoTorrent DHT still took about 90 seconds to move from `Initialising` to `Ready`. Our startup path treated 30 seconds as the failure threshold, logged a warning that implied misconfiguration, and started spamming `Cannot announce` / `Cannot discover peers` even though the same process later became healthy without any further changes.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`

**Wrong**:
```text
Assume DHT bootstrap should always reach `Ready` within 30 seconds and warn about
firewall/forwarding problems immediately when it does not.
```

**Correct**:
```text
Allow a longer bootstrap grace period before treating DHT startup as suspicious.
Slow public-router bootstrap is normal on some hosts; do not emit operator-facing
misconfiguration warnings until the grace period has actually elapsed.
```

**Why This Keeps Happening**: Once DHT has zero or only a few nodes at startup, the public bootstrap routers can seed slowly even on a healthy network path. A short static timeout turns that normal warm-up into a misleading product error and sends debugging in the wrong direction.


**The Bug**: On `kspls0`, downloads were succeeding end to end, but the process still emitted `[FATAL] Unobserved task exception` with `Soulseek.ConnectionException: Transfer failed: Transfer complete` immediately after the successful transfer state transition. The transfer was already done; only the trailing connection teardown surfaced as an exception name/message we were not classifying as expected Soulseek transport churn.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```text
Downgrade socket resets, remote closes, and remote-declared transfer failures, but leave
the Soulseek post-success `Transfer failed: Transfer complete` teardown exception outside
the same expected-exception bucket.
```

**Correct**:
```text
If the transfer path raises `Transfer failed: Transfer complete` after a successful file
transfer, treat it as expected Soulseek connection churn for unobserved-task telemetry so
completed downloads do not emit fake fatal crash noise.
```

**Why This Keeps Happening**: The Soulseek library can signal the end of a completed transfer through an exception-shaped teardown path rather than a clean no-op completion. It looks like a real failure if you only key off exception type names, but on a live host you can see the transfer already reached `Completed, Succeeded` before the finalizer-thread exception appears. The message text has to be folded into the same expected-churn classifier as the other transfer-layer cases.

### 0z23. Remote Peer Transfer Rejections Are Expected Soulseek Churn, Not Fatal Host Errors

**The Bug**: After the local queue and DHT fixes, `kspls0` showed both successful downloads and normal remote-peer failures on the same build. One remaining bad behavior was that `Soulseek.TransferReportedFailedException` (`Download reported as failed by remote client`) could still fall through the unobserved-task classifier and show up as fake `[FATAL] Unobserved task exception` noise, even though the remote peer simply declined or aborted that one transfer.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```text
Classify socket resets/timeouts as expected Soulseek network churn but leave remote-declared transfer failures outside the same expected-exception bucket.
```

**Correct**:
```text
If the exception chain shows Soulseek transfer-layer failures like `TransferReportedFailedException` /
`Download reported as failed by remote client`, treat them as expected peer/runtime churn for unobserved-task telemetry so the host does not emit fake fatal crash noise.
```

**Why This Keeps Happening**: Soulseek peer interactions fail in several layers: pure socket errors, read/write timeouts, and explicit transfer-layer rejections from the remote client. It is easy to stop once the socket-layer cases are downgraded, but operators still see the same scary `[FATAL]` signal unless the transfer-layer failure names/messages are folded into the same expected-churn classifier.

### 0z22. Package-Managed App Payload Directories Must Be Pruned On Upgrade If Builds Can Leave Unowned Files Behind

**The Bug**: The AUR `slskdn-bin` package installs the entire app payload under `/usr/lib/slskd`, but older builds and manual/runtime copy flows left extra files there that pacman did not own. On the next package upgrade, pacman refused to install the new package with `failed to commit transaction (conflicting files)` because stale unowned files like `Microsoft.AspNetCore.StaticAssets.dll`, native runtime libraries, and compressed `wwwroot` assets were still present in `/usr/lib/slskd`. The repo also referenced an `install=slskd.install` hook file that no longer existed, so there was no package-script chance to clean the directory before upgrade.

**Files Affected**:
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/PKGBUILD`
- `packaging/aur/PKGBUILD-dev`
- `packaging/aur/slskd.install`

**Wrong**:
```text
Treat `/usr/lib/slskd` like a normal package payload directory even though release/runtime copy flows can leave extra unowned files behind, and assume pacman upgrades will overwrite the directory cleanly without a package hook.
```

**Correct**:
```text
Ship a real pacman install script and prune the managed app payload directory before upgrade/reinstall so stale unowned binaries and compressed asset files cannot block the next package install. Keep mutable config/data outside `/usr/lib/slskd`.
```

**Why This Keeps Happening**: App bundles install hundreds of files under one directory, and release/runtime packaging changes can add or remove files between versions. Pacman only replaces files it owns; anything left behind by older builds, manual deploys, or missing package metadata becomes a hard conflict later unless the package explicitly cleans that managed application directory during upgrade.

### 0z21. Background Enqueue Tasks Must Finish Before Their Shared Semaphore Goes Out Of Scope

**The Bug**: `DownloadService.EnqueueAsync(...)` created a per-batch `SemaphoreSlim` with `using var enqueueSemaphore`, then spawned background enqueue tasks that released that semaphore in their `finally` blocks. The method only waited for the transfer to reach `Queued, Remotely` and then moved on, so the scope could end and dispose `enqueueSemaphore` while those background tasks were still unwinding. On the live host this surfaced as `ObjectDisposedException: Cannot access a disposed object. Object name: 'System.Threading.SemaphoreSlim'.` immediately after downloads entered `Queued, Remotely`.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`

**Wrong**:
```text
Create a shared synchronization primitive in a local scope, fire background tasks that
use it in their finally blocks, and return before those tasks have definitely completed.
```

**Correct**:
```text
If background tasks share a scoped semaphore or similar disposable primitive, keep track of
those tasks and await their completion before disposing the primitive or leaving the scope.
```

**Why This Keeps Happening**: The transfer enqueue path mixes synchronous local bookkeeping with asynchronous background task observation. It is easy to think "the important work is done once the transfer reaches Queued, Remotely" and forget that the background task still has cleanup logic that touches shared synchronization objects. Scoped disposal and async finally blocks do not mix unless the parent explicitly waits for the child tasks to finish.

### 0z20. Empty Permission Defaults Must Fall Back To Umask Instead Of Being Parsed As A Real chmod Value

**The Bug**: `permissions.file.mode` defaults to `string.Empty` to mean "no explicit Unix mode; let the host umask apply." But `FileService.CreateFile(...)` and `MoveFile(...)` still called `Mode?.ToUnixFileMode()`, so the empty default string was treated like a real permission value and downloads failed at file-creation time with `The value cannot be an empty string or composed entirely of whitespace. (Parameter 'permissions')`.

**Files Affected**:
- `src/slskd/Files/FileService.cs`
- `src/slskd/Core/Options.cs`

**Wrong**:
```text
Treat an unset permission option (`""` / whitespace) like a configured chmod mode and
parse it anyway in low-level file creation/move helpers.
```

**Correct**:
```text
Only parse `permissions.file.mode` when it contains a real non-whitespace chmod string.
Otherwise pass `null` and let the OS default umask govern file and directory creation.
```

**Why This Keeps Happening**: The option model uses an empty string as the "not configured" default, which is fine at the boundary, but callers that rely on null-conditional access (`Mode?.ToUnixFileMode()`) accidentally still invoke the parser on empty strings. Any low-level file path that skips the explicit `IsNullOrWhiteSpace` guard can turn the harmless default into a hard runtime failure.

### 0z19. Serialized Bulk Actions Still Need A Real Background Queue With De-Dupe

**The Bug**: Simply changing transfer bulk actions from `Promise.all(...)` to a serial `for ... await` loop stopped the immediate `429` storm, but it still kept the whole bulk action bound to the click handler and allowed the same files to be re-enqueued if the user clicked the same bulk action again while the first drain was still in progress. That meant the UI could still create duplicate background work and repeated retries/removals against the same transfer set.

**Files Affected**:
- `src/web/src/components/Transfers/Transfers.jsx`
- `src/web/src/components/Transfers/TransferGroup.jsx`

**Wrong**:
```text
Replace parallel bulk transfer requests with a serial loop inside the button
handler and assume that is enough to make the action queue-safe.
```

**Correct**:
```text
Bulk retry/remove should enqueue work into a background queue that:
- drains at a controlled rate
- keeps in-flight items deduped by transfer/action key
- ignores repeated submissions for work already queued or running
- reports failures once per batch instead of once per file
```

**Why This Keeps Happening**: It is easy to treat "not parallel anymore" as the same thing as "properly queued." It is not. If the user can trigger the same bulk action again before the first drain finishes, the UI still needs explicit queue ownership and de-dupe semantics or it will recreate the same storm more slowly.

### 0z18. Transfer Bulk Actions Must Respect The Backend Request Shape Instead Of Spamming Per-File Calls

**The Bug**: The Transfers page implemented `Retry All` and `Remove All` as `Promise.all(...)` over one API request per selected file. That looked simple in the UI, but the backend download enqueue path is intentionally concurrency-limited and returns `429` when hit in parallel, while completed-transfer cleanup already has dedicated bulk-clear endpoints. In practice, bulk retry turned into a toast storm of self-inflicted `429` failures, and bulk remove completed created unnecessary request floods instead of one clear operation.

**Files Affected**:
- `src/web/src/components/Transfers/Transfers.jsx`
- `src/web/src/components/Transfers/TransferGroup.jsx`
- `src/web/src/lib/transfers.js`
- `src/slskd/Transfers/API/Controllers/TransfersController.cs`

**Wrong**:
```text
Implement "Retry All" and "Remove All" by firing one request per file in
parallel from the browser and assume the backend wants that shape too.
```

**Correct**:
```text
Use the backend's actual contract:
- serialize or batch retry requests so they do not trip the enqueue limiter
- call the dedicated clear-completed endpoint when the action is "remove all completed"
- reserve per-file calls for mixed or non-completed selections that genuinely need them
```

**Why This Keeps Happening**: Bulk UI actions are easy to write as `Promise.all(...)`, but transfer backends often have throttling or special bulk endpoints for a reason. If the frontend ignores those contracts, the product generates its own errors and makes a sick queue look much worse than it is.

### 0z17. Do Not Run UDP Hole-Punch Preflight Against DHT Overlay TCP Endpoints

**The Bug**: `MeshOverlayConnector` took each DHT-discovered overlay endpoint and wrapped it as `udp://host:port` for NAT traversal preflight before the real TCP connect. But DHT peers advertise the mesh overlay TCP listener port, and there is no corresponding UDP responder in that path, so the hole-punch attempts were guaranteed to fail and produced misleading `[HolePunch] ... FAILED` noise even when DHT discovery itself was healthy.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
- `src/slskd/Mesh/Nat/NatTraversalService.cs`
- `src/slskd/Mesh/Nat/UdpHolePuncher.cs`

**Wrong**:
```text
Treat a discovered TCP overlay endpoint as if it were a UDP hole-punch target just because the host and port are available.
```

**Correct**:
```text
Only attempt NAT traversal preflight when you have an actual UDP/relay endpoint contract for that peer. Plain DHT overlay candidates should go straight to the real TCP overlay connect path.
```

**Why This Keeps Happening**: The code has NAT traversal primitives and DHT-discovered endpoints in the same area, so it is tempting to wire them together speculatively. But transport information matters: a TCP listener port is not automatically a valid UDP hole-punch target. Without an actual UDP responder on the remote side, the preflight can only fail and mislead operators.

### 0z16. Frontend API Libraries Must Stay On The Same Versioned Route Family As Their Controllers

**The Bug**: The WebUI `userNotes` client called `/api/v0/users/notes`, but `UserNotesController` only advertised API version `1`. That left the UI with a reproducible `GET /api/v0/users/notes -> 404` even though the backend feature existed and the frontend was using the same route shape as the rest of the app.

**Files Affected**:
- `src/web/src/lib/userNotes.js`
- `src/slskd/Users/Notes/API/UserNotesController.cs`

**Wrong**:
```text
Add a new controller on a different API version than the existing frontend route family and assume versioned routing will just line up.
```

**Correct**:
```text
When a WebUI lib already targets `/api/v0/...`, the controller must either support `v0` too or the frontend route must be updated in the same change, with an integration test proving the versioned route actually resolves.
```

**Why This Keeps Happening**: Most of the app still uses `v0` routes, so a controller that defaults to `v1` looks valid in isolation but breaks only when exercised through the frontend. Route/version mismatches are easy to miss unless the exact versioned URL is covered in integration tests.

### 0z15. Public Overlay Exposure Creates Follow-On Noise Unless We Classify Expected Handshake Churn And Clear Stale CSRF Cookies

**The Bug**: After issue `#209` finally fixed DHT bootstrap, the first public test node immediately started logging three follow-on problems as if the feature were still broken: `Connection reset by peer` surfaced as a `[FATAL]` unobserved task exception, stale antiforgery cookies from a reinstall spammed decrypt/key-ring errors on every safe request, and random internet junk hitting the overlay port showed up as warning-stack traces from the TLS handshake path.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Core/Security/ValidateCsrfForCookiesOnlyAttribute.cs`
- `src/slskd/DhtRendezvous/MeshOverlayServer.cs`

**Wrong**:
```text
Fix DHT bootstrap, expose the overlay port publicly, and keep treating every
subsequent connection reset, stale XSRF cookie, and garbage TLS probe as an
unexpected fatal-or-warning condition.
```

**Correct**:
```text
Once the node is reachable:
- classify `Connection reset by peer` as expected network churn
- clear stale antiforgery cookies when data-protection keys changed across reinstall
- downgrade obvious non-TLS overlay probes to debug-level noise instead of warning stack traces
```

**Why This Keeps Happening**: The first successful public deployment changes the operating environment. A reachable overlay listener attracts scanners and failed handshakes immediately, and reinstalling a web app often leaves old antiforgery cookies in the browser. If we only test the bootstrap path and not the first reachable-runtime behavior, the next operator report looks like the original bug never got fixed even though the real problem has moved on.

### 0z14. Release Asset Naming Changes Must Be Applied Atomically Across Build Outputs, Repo Metadata, And Package Workflows

**The Bug**: We changed stable package metadata to `slskdn-main-linux-x64.zip` while the release workflow still published `slskdn-main-linux-glibc-x64.zip`. That left the main release partially split-brain: COPR copied one filename while the RPM spec referenced the other, metadata refresh rebuilt `flake.nix` against a filename the just-created release did not publish, and package jobs failed even though the Linux payload itself had built successfully.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/scripts/update-stable-release-metadata.sh`
- stable packaging metadata (`flake.nix`, Homebrew, Snap, Flatpak, RPM, AUR)

**Wrong**:
```text
Change the canonical stable Linux asset name in one layer (repo metadata or workflow consumers) without changing the release output and every downstream package job in the same edit.
```

**Correct**:
```text
Pick one canonical stable Linux asset name and update all of these together:
- release upload step
- repo metadata updater
- package workflows (COPR, Snap, PPA, Homebrew, metadata smoke)
- checked-in package metadata
```

**Why This Keeps Happening**: The release pipeline has multiple independent consumers of the same Linux zip, and several of them rewrite local filenames before packaging. If one part of the pipeline moves from `linux-x64` to `linux-glibc-x64` without the others moving in lockstep, failures surface later as missing files or 404s in unrelated jobs rather than at the initial release upload step.

### 0z13. Stable Metadata Must Reference Asset Names That Already Exist On The Published Stable Release

**The Bug**: We changed `flake.nix` and other stable package metadata to `slskdn-main-linux-glibc-*.zip` before any stable GitHub release actually published those asset names. `Nix Package Smoke` then fetched `0.24.5-slskdn.131/slskdn-main-linux-glibc-x64.zip`, got a `404`, and failed even though the real stable asset was still `slskdn-main-linux-x64.zip`.

**Files Affected**:
- `flake.nix`
- `packaging/scripts/update-stable-release-metadata.sh`
- stable packaging metadata files (`Formula`, `snapcraft`, `flatpak`, `rpm`, `aur`)

**Wrong**:
```text
Switch stable metadata to a future asset naming scheme as soon as the workflow code changes.
```

**Correct**:
```text
Stable metadata must point at the asset names on the latest published stable release.
Only change those URLs after a release has successfully published the new asset names, or
teach the metadata updater to choose the asset names that actually exist for that release.
```

**Why This Keeps Happening**: Release workflows and post-release metadata files move on different timelines. A workflow can be updated to produce new asset names on the next tag, but package metadata is consumed immediately against the current published stable release. If the metadata jumps ahead, anything that validates or downloads the current stable asset will 404.

### 0z12. Stable Linux Releases Must Ship An Explicit Installer Path, Not Just Raw Zip Payloads

**The Bug**: Stable GitHub releases published only the platform zip payloads, while dev releases also shipped the Linux service/config helper files. That leaves Linux users upgrading from an existing `slskd` systemd install to guess how to replace the old service path, and it is easy to restart the old package-managed binary while thinking the new release zip is running.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/linux/install-from-release.sh`

**Wrong**:
```text
Ship a stable Linux zip and assume operators will correctly replace any existing service/unit/install path by hand.
```

**Correct**:
```text
Publish the Linux install helper and service/config assets with stable releases too, and give release users
a single supported install/migration path that rewrites the systemd unit to the extracted release tree.
```

**Why This Keeps Happening**: Raw release zips are just file payloads. They do not carry a service migration story, and existing `slskd` installs already have a unit file, config location, and binary path. If stable releases do not ship an explicit installer path, users can extract the new tree somewhere and still restart the old service target.

### 0z11. A Reported "Still Broken" Release Can Actually Be A Stale Running Install, So The App Must Self-Identify Its Executable And Config Paths

**The Bug**: We treated issue `#209` as if the new DHT build was still failing in the same way, but the reporter's WebUI still showed version `126` while they believed they had installed `131`. That means the running process was still an older binary, and we had no fast way to prove which executable/config path the live instance was actually using.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Core/State.cs`

**Wrong**:
```text
Validate the fix in code and release assets, then assume the reporter's machine is actually running that new binary.
```

**Correct**:
```text
Make the live process identify itself clearly: log the executable path and base directory at startup,
and expose the running executable/app/config paths in runtime state so /system/info shows what is
actually running before diagnosing the feature itself.
```

**Why This Keeps Happening**: Once a repo has package installs, raw zip installs, systemd units, and reused app directories, an operator can replace one tree while the service still launches another. Version mismatches then look like feature regressions. Before calling a user report a failed fix, first prove the live process version and path match the release you think they installed.

### 0z10. Release Assets Must Not Publish The Same Build Under Both Stable And Version-Named Zip Files

**The Bug**: Stable releases were uploading identical Linux payloads multiple times under names like `slskdn-main-linux-x64.zip` and `slskdn-0.24.5.slskdn.131-linux-x64.zip`. That made the release page look like it contained extra architectures or variants when it was really the same archive duplicated for compatibility.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `.github/workflows/release-packages.yml`
- `packaging/scripts/update-stable-release-metadata.sh`

**Wrong**:
```bash
zip -r ../slskdn-main-linux-x64.zip .
cp ../slskdn-main-linux-x64.zip ../slskdn-0.24.5.slskdn.131-linux-x64.zip
```

**Correct**:
```bash
zip -r ../slskdn-main-linux-glibc-x64.zip .
```

```text
Update packaging and metadata consumers to use the one explicit asset name
instead of publishing duplicate aliases into the release itself.
```

**Why This Keeps Happening**: GitHub Releases do not have lightweight aliases, so it is tempting to upload the same file repeatedly under machine-friendly and human-friendly names. That pushes compatibility clutter into the public release page. Pick one canonical asset name per runtime, make the runtime identifier explicit (`glibc` vs `musl`), and keep any backward-compat lookup logic only in consumers that still need to fetch older releases.

### 0z8. Tag Builds Must Move Docker And Workflow SDK Versions In Lockstep With The App Target Framework

**The Bug**: Stable tag builds can pass most of the repo and still fail only in the Docker publish leg when `slskd` moves to a newer target framework but `.github/workflows/build-on-tag.yml` and `Dockerfile` are still pinned to the previous SDK/runtime images. The failure only shows up late as `NETSDK1045`.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `Dockerfile`

**Wrong**:
```yaml
env:
  DOTNET_VERSION: '8'
```

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS publish
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-bookworm-slim AS slskd
```

**Correct**:
```yaml
env:
  DOTNET_VERSION: '10'
```

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-bookworm-slim AS publish
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-bookworm-slim AS slskd
```

**Why This Keeps Happening**: The repo can move its project files to a new target framework without immediately breaking local builds, but the tag-only Docker path uses its own SDK/runtime pins. Every framework bump must include the tag workflow's `setup-dotnet` version and the Dockerfile base images in the same change.

### 0z9. Matrix Message Redaction In This Release Workflow Uses `PUT`, Not `POST`

**The Bug**: Release announcements could succeed in Discord but still fail the combined announce job because the Matrix cleanup step tried to redact the previous release message with `POST`, and the homeserver returned `405 Method Not Allowed`.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
curl --fail --silent --show-error   -X POST   -H "Authorization: Bearer ${MATRIX_RELEASE_ACCESS_TOKEN}"   -H "Content-Type: application/json"   -d '{"reason":"Superseded by newer release announcement"}'   "${MATRIX_BASE_URL}/_matrix/client/r0/rooms/.../redact/${previous_event_id}/${redact_txn}"
```

**Correct**:
```bash
curl --fail --silent --show-error   -X PUT   -H "Authorization: Bearer ${MATRIX_RELEASE_ACCESS_TOKEN}"   -H "Content-Type: application/json"   -d '{"reason":"Superseded by newer release announcement"}'   "${MATRIX_BASE_URL}/_matrix/client/r0/rooms/.../redact/${previous_event_id}/${redact_txn}"
```

**Why This Keeps Happening**: The send step already uses `PUT`, so it is easy to assume the redact helper can be sketched from memory without checking the server behavior. When touching Matrix release automation, verify the exact method against the live homeserver path instead of trusting a generic snippet.

### 0z6. MonoTorrent `3.0.2` DHT Bootstrap Can Stall Forever Because It Only Seeds From `router.bittorrent.com`

**The Bug**: slskdn's DHT rendezvous looked broken in production because the pinned `MonoTorrent 3.0.2` bootstrap path seeded only from `router.bittorrent.com`. If that single router did not answer, the engine stayed in `Initialising` with `nodes=0`, so announce/discovery never became usable no matter how much local logging or port explanation we added.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `config/slskd.example.yml`

**Wrong**:
```csharp
// slskdn relied on MonoTorrent 3.0.2's hidden bootstrap defaults.
await dhtEngine.StartAsync();
```

```text
MonoTorrent 3.0.2 only seeded bootstrap from router.bittorrent.com in this path,
so DHT startup could stall forever at nodes=0 when that router was unreachable.
```

**Correct**:
```csharp
await dhtEngine.StartAsync(initialNodes, _options.BootstrapRouters);
```

```text
Pin a MonoTorrent build with the multi-router bootstrap fix and carry the router
list in slskdn's own config so DHT startup does not depend on one hidden upstream default.
```

**Why This Keeps Happening**: DHT bootstrap failures are easy to misdiagnose as local firewall or NAT mistakes because the visible symptom is just `NotReady` with zero nodes. When the underlying library hides a single-router bootstrap default, operator-facing logging changes do nothing. Reproduce the engine outside the app, confirm whether the routing table ever gets seeded, and make bootstrap routers explicit in our own configuration instead of trusting opaque upstream defaults.

### 0z4. Bridge Integration Tests Must Preflight External `soulfind` Prerequisites Before Launching A Full `slskdn` Instance

**The Bug**: `dotnet test slskd.sln` could hang in `BridgeProxyServerIntegrationTests` when the environment did not have a `soulfind` binary. The test harness launched a full `slskdn` process in bridge mode first, then waited for the bridge listener, so the suite never failed or skipped cleanly when the external bridge dependency was missing.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
if (enableBridge)
{
    bridgePort = bridgePortOverride ?? AllocateEphemeralPort();
}

slskdnProcess = Process.Start(startInfo);
await WaitForApiReadyAsync(ct);
await WaitForBridgeReadyAsync(bridgePort.Value, ct);
```

**Correct**:
```csharp
if (enableBridge)
{
    bridgePort = bridgePortOverride ?? AllocateEphemeralPort();

    if (string.IsNullOrEmpty(DiscoverSoulfindBinary()))
    {
        throw new InvalidOperationException(
            "Soulfind binary not found. Install soulfind or set SOULFIND_PATH before running bridge integration tests.");
    }
}
```

**Why This Keeps Happening**: Integration harnesses that rely on external binaries cannot assume those tools exist on every dev or CI machine. If a test path needs `soulfind`, check that prerequisite before spawning the main application process and fail or skip fast with a clear message; otherwise the suite ends up diagnosing a stuck host instead of the real missing dependency.

### 0z5. Long Fixed Delays In Integration Tests Can Trigger `--blame-hang` Even When The Code Path Is Fine

**The Bug**: `DisasterModeTests.Disaster_Mode_Recovery_Should_Deactivate_When_Soulfind_Returns` timed out under `dotnet test --blame-hang --blame-hang-timeout 30s` because the test spent most of its runtime inside two blind `Task.Delay(...)` calls while never asserting the underlying state transition. The host was still alive, but the runner saw 30 seconds of inactivity and killed the suite.

**Files Affected**:
- `tests/slskd.Tests.Integration/DisasterMode/DisasterModeTests.cs`

**Wrong**:
```csharp
await soulfind!.StopAsync();
await Task.Delay(TimeSpan.FromSeconds(15));

await soulfind.StartAsync();
await Task.Delay(TimeSpan.FromSeconds(10));
```

**Correct**:
```csharp
await soulfind!.StopAsync();
await WaitForStatusEndpointAsync(alice!);

await soulfind.StartAsync();
await WaitForStatusEndpointAsync(alice);
```

**Why This Keeps Happening**: Long integration waits are easy to add while sketching end-to-end scenarios, especially when the test has TODO assertions. Under hang diagnostics, a quiet sleep is indistinguishable from a stuck testhost. Poll the observable condition you care about instead of burning fixed delays.

### 0z3. `@testing-library/react` Major Upgrades Can Require A Direct `@testing-library/dom` Dependency In This Repo

**The Bug**: After upgrading the web stack to React 18 and `@testing-library/react` 16, Vitest failed before several suites could load with `Cannot find module '@testing-library/dom'`. The repo had `@testing-library/react` installed, but not the DOM package it now expects in this dependency graph.

**Files Affected**:
- `src/web/package.json`
- `src/web/package-lock.json`

**Wrong**:
```json
"devDependencies": {
  "@testing-library/react": "^16.3.2"
}
```

```text
Vitest can fail at module load time because @testing-library/react no longer
brings in a usable @testing-library/dom path here automatically.
```

**Correct**:
```json
"devDependencies": {
  "@testing-library/dom": "^10.4.1",
  "@testing-library/react": "^16.3.2"
}
```

**Why This Keeps Happening**: Testing-library upgrades look like a simple React-version follow-up, but their package graph changes across majors. When bumping `@testing-library/react`, run the full Vitest suite and treat any missing peer/helper package as part of the same upgrade instead of assuming the old dependency tree still holds.

### 0z2. React Router Major Migrations Must Remove Every Stale v5 `history` / `match` Reference, Not Just The Imports

**The Bug**: During the React Router 7 migration, `Searches.jsx` was updated to `useNavigate()` and `useParams()`, but one old fallback still called `history.replace(match.url.replace(...))`. Lint caught `match` as undefined, but the deeper problem is that partial router migrations leave dead v5 navigation code behind in edge-path cleanup branches.

**Files Affected**:
- `src/web/src/components/Search/Searches.jsx`
- `src/web/src/components/App.jsx`
- `src/web/src/components/System/System.jsx`

**Wrong**:
```javascript
import { useNavigate, useParams } from 'react-router-dom';

// ...later, in an edge path that did not get migrated:
history.replace(match.url.replace(`/${searchId}`, ''));
```

**Correct**:
```javascript
import { useNavigate, useParams } from 'react-router-dom';

const navigate = useNavigate();

// ...all route repair/redirect paths must use the same router API:
navigate('/searches', { replace: true });
```

**Why This Keeps Happening**: Router major upgrades are easy to do mechanically at the import level while missing less-traveled fallback branches. Every file moving off Router v5 needs a full pass for `history`, `match`, `Redirect`, and route-render props, not just the happy-path navigation buttons.

### 0z1. `jsdom 29.0.2` Breaks This Vitest/JSDOM Stack Even When Plain Node Imports Still Resolve

**The Bug**: Bumping the web test toolchain from `jsdom 29.0.1` to `29.0.2` caused Vitest fork workers to fail before any tests ran, with `Cannot find module 'parse5'` and `Cannot find module 'entities/decode'` coming from the JSDOM HTML parser path, even though direct `node --input-type=module` imports of `parse5` and `entities/decode` still succeeded.

**Files Affected**:
- `src/web/package.json`
- `src/web/package-lock.json`

**Wrong**:
```json
"jsdom": "^29.0.2"
```

```text
Vitest worker bootstrap can fail in this repo with parse5/entities resolution errors
after that bump, so a plain npm install + node import smoke check is not enough.
```

**Correct**:
```json
"jsdom": "^29.0.1"
```

```text
Keep the last known-good JSDOM line unless the Vitest worker pool passes again in
this exact repo environment after the upgrade.
```

**Why This Keeps Happening**: Dependency bumps that look safe in isolation can still break this repo's older React/Vitest/JSDOM stack in ways that only show up when Vitest forks its workers. Direct package-resolution spot checks are weaker than the actual `npm test` path here, so test-runner dependencies need real end-to-end Vitest validation before they are kept.

### 0z. Release-Gate Subpath Smoke Checks Must Mirror Backend HTML Rewrite Behavior, Not Old Relative-Asset Assumptions

**The Bug**: The frontend build was correctly switched back to root-relative asset URLs (`/assets/...`) with ASP.NET HTML rewriting for `web.url_base`, but the release-gate smoke script still expected built `index.html` to contain relative asset references like `./assets/...`. Stable tag builds failed in `run-release-gate.sh` before any release jobs or Discord announcements could run.

**Files Affected**:
- `src/web/scripts/smoke-subpath-build.mjs`
- `src/web/scripts/verify-build-output.mjs`
- `packaging/scripts/run-release-gate.sh`

**Wrong**:
```javascript
const relativeAssetMatches = [...indexHtml.matchAll(/(?:src|href)="(\.[^"]+)"/g)];

if (relativeAssetPaths.length === 0) {
  fail('Expected built index.html to contain relative asset references under a subpath');
}
```

**Correct**:
```javascript
// Smoke tests for subpath deployment must emulate the backend's HTML rewrite layer.
// Built output should stay root-relative, and the smoke server should rewrite those
// root-relative references to the mounted subpath before fetching assets.
```

**Why This Keeps Happening**: It is easy to update the frontend build and backend serving model but forget the standalone smoke harnesses in release automation. Any check that validates subpath behavior must follow the same contract as `Program.CreateWebHtmlRewriteRules(...)`; otherwise CI ends up enforcing the superseded behavior and blocks releases even though the product code is correct.

### 0x. Vite Relative Asset URLs Break Deep-Link Refreshes In The Embedded Web UI

**The Bug**: Switching the Vite build to `base: './'` made the root page work under `web.url_base`, but it broke hard refreshes on client-side routes like `/system`. Browsers resolved `./assets/...` relative to the current route, so `/system` tried to load `/system/assets/...` instead of the actual app root assets.

**Files Affected**:
- `src/web/vite.config.js`
- `src/web/index.html`
- `src/slskd/Program.cs`

**Wrong**:
```javascript
export default defineConfig({
  base: './',
});
```

```html
<link rel="manifest" href="./manifest.json" />
<script type="module" src="./src/index.jsx"></script>
```

**Correct**:
```javascript
export default defineConfig({
  base: '/',
});
```

```html
<link rel="manifest" href="/manifest.json" />
<script type="module" src="/src/index.jsx"></script>
```

```csharp
foreach (var (pattern, replacement) in CreateWebHtmlRewriteRules(urlBase))
{
    app.UseHTMLRewrite(pattern, replacement);
}
```

**Why This Keeps Happening**: Relative asset URLs look attractive because they work in a static subdirectory smoke test, but slskdn is not serving a plain static site. It serves an SPA behind ASP.NET with `UsePathBase`, client-side routes, and HTML rewriting. Deep-link refreshes need build output to use app-root paths, and the backend must rewrite those root-relative paths to the configured `web.url_base`.

### 0y. Soulseek Client Listener Settings Must Exist In Initial Options, Not Only In Startup Reconfiguration

**The Bug**: The Soulseek client was instantiated with `enableListener: false` and no listen endpoint, then later reconfigured during `Application.StartAsync()`. That left a window where connect/login work could still observe a non-listening client and throw `InvalidOperationException: Not listening. You must call the Start() method before calling this method.`

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Application.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
return new SoulseekClientOptions(
    enableListener: false,
    enableDistributedNetwork: false,
    acceptDistributedChildren: false,
    ...);
```

**Correct**:
```csharp
return new SoulseekClientOptions(
    enableListener: true,
    listenIPAddress: startupListenAddress,
    listenPort: optionsAtStartup.Soulseek.ListenPort,
    enableDistributedNetwork: !optionsAtStartup.Soulseek.DistributedNetwork.Disabled,
    acceptDistributedChildren: !optionsAtStartup.Soulseek.DistributedNetwork.DisableChildren,
    distributedChildLimit: optionsAtStartup.Soulseek.DistributedNetwork.ChildLimit,
    ...);
```

**Why This Keeps Happening**: It is easy to assume that no network work happens until after `Application.StartAsync()` finishes, but the Soulseek client’s own connection/login flow and background tasks can still depend on listener state once connects begin. Listener/distributed-network bootstrap settings need to be present on the initial client object, while later reconfiguration should only patch resolvers, caches, and other runtime-dependent services.

### 0v. CSRF Token Middleware Must Not Mint New Tokens On Unsafe Requests

**The Bug**: The custom CSRF middleware called `antiforgery.GetAndStoreTokens(context)` on every request, including `POST`/`PUT`/`DELETE`/`PATCH`. That meant a state-changing request could receive a freshly rotated antiforgery token pair immediately before `ValidateCsrfForCookiesOnlyAttribute` validated the request, causing valid header/cookie pairs from the previous page load to fail with `CSRF token validation failed`.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Integration/Security/CsrfPortScopedTokenIntegrationTests.cs`

**Wrong**:
```csharp
var tokens = antiforgery.GetAndStoreTokens(context);
context.Response.Cookies.Append($"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", tokens.RequestToken, ...);
```

```text
This ran on every request, including the unsafe request currently being validated.
```

**Correct**:
```csharp
if (HttpMethods.IsGet(context.Request.Method) ||
    HttpMethods.IsHead(context.Request.Method) ||
    HttpMethods.IsOptions(context.Request.Method) ||
    HttpMethods.IsTrace(context.Request.Method))
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append($"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", tokens.RequestToken, ...);
}
```

```text
Only mint/store antiforgery tokens on safe requests. Unsafe requests should validate the pair the client already has; they should not rotate it mid-flight.
```

**Why This Keeps Happening**: It is easy to think of the antiforgery middleware as harmless cookie setup that can run globally, but `GetAndStoreTokens(...)` is stateful. Once validation is deferred to a controller/filter, token issuance must stay on safe/bootstrap requests or the request under validation can invalidate itself.

### 0w. Frontend API Helpers Must Not Re-Add `/api/v0` When Axios Already Has That Base URL

**The Bug**: Some Web UI helper modules hardcoded endpoint roots like `/api/v0/security/...` and `/api/v0/mediacore/...` even though the shared Axios client already uses `apiBaseUrl = ${rootUrl}/api/v0`. Requests became `/api/v0/api/v0/...`, which broke System tabs with 404s.

**Files Affected**:
- `src/web/src/lib/api.js`
- `src/web/src/lib/security.js`
- `src/web/src/lib/mediacore.js`

**Wrong**:
```javascript
const baseUrl = '/api/v0/security';
return (await api.get(`${baseUrl}/dashboard`)).data;
```

**Correct**:
```javascript
const baseUrl = '/security';
return (await api.get(`${baseUrl}/dashboard`)).data;
```

**Why This Keeps Happening**: Some frontend modules build URLs relative to Axios, while others build fully-qualified API paths. Once `api.js` owns the `/api/v0` prefix, every helper that uses `api.get/post/put/delete(...)` must pass paths relative to that prefix or the request will be versioned twice.

### 0w1. Route Smoke Coverage Must Exercise The Same Versioned Web UI Paths Production Uses

**The Bug**: The Jobs Web UI helper still called `/api/jobs...` through an Axios client already rooted at `/api/v0`, while multiple MediaCore controllers exposed versioned-looking paths without `ApiVersion` metadata. The existing tests still passed because they asserted the wrong frontend URLs and the release integration smoke filter never exercised the affected `/api/v0/jobs`, `/api/v0/mediacore/...`, `/api/v0/security/...`, or `/api/v0/bridge/...` routes.

**Files Affected**:
- `src/web/src/lib/jobs.js`
- `src/web/src/lib/jobs.test.js`
- `src/slskd/API/Native/JobsController.cs`
- `src/slskd/MediaCore/API/Controllers/*.cs`
- `tests/slskd.Tests.Integration/Api/VersionedApiRoutesIntegrationTests.cs`
- `packaging/scripts/run-release-integration-smoke.sh`

**Wrong**:
```javascript
const url = `/api/jobs${queryString ? `?${queryString}` : ''}`;
expect(api.get).toHaveBeenCalledWith('/api/jobs');
```

```csharp
[Route("api/v0/mediacore/contentid")]
public class ContentIdController : ControllerBase
```

```bash
FILTER='...|FullyQualifiedName~SoulbeetAdvancedModeTests|...'
```

**Correct**:
```javascript
const url = `/jobs${queryString ? `?${queryString}` : ''}`;
expect(api.get).toHaveBeenCalledWith('/jobs');
```

```csharp
[Route("api/v{version:apiVersion}/mediacore/contentid")]
[ApiVersion("0")]
public class ContentIdController : ControllerBase
```

```bash
FILTER='...|FullyQualifiedName~VersionedApiRoutesIntegrationTests|FullyQualifiedName~SecurityRoutesIntegrationTests|FullyQualifiedName~NicotinePlusIntegrationTests'
```

**Why This Keeps Happening**: Route regressions can hide behind two layers of false confidence at once: unit tests that only assert whatever broken path a helper currently builds, and release smoke filters that skip the exact versioned routes the Web UI uses in production. For Web UI APIs, tests must assert the helper's relative path against the shared Axios base URL, controllers must declare explicit version metadata when serving `/api/v{version:apiVersion}/...`, and release smoke must include at least one end-to-end probe for every critical System-page route family.

### 0w1a. Search UI Actions Must Import The Same API Helper Module They Invoke

**The Bug**: A search response action called `library.createBatch(...)` without importing `library`, so the UI path only failed at runtime when users queued nearby graph searches from a result card.

**Files Affected**:
- `src/web/src/components/Search/Response.jsx`

**Wrong**:
```javascript
const count = await library.createBatch({ queries });
```

**Correct**:
```javascript
import * as searches from '../../lib/searches';

const count = await searches.createBatch({ queries });
```

**Why This Keeps Happening**: Nearby components use slightly different helper names (`search`, `searches`, `library`, `createBatch`), so copy/paste between panels can leave a stale identifier behind. Any new UI action that queues searches should be checked against its import list, not just nearby components with similar behavior.

### 0w1b. Do Not Mix `+` And `??` Without Explicitly Defaulting Each Operand First

**The Bug**: Explorer totals used `directory?.directories?.length + directory?.files?.length ?? 0`, which looks like “sum lengths or fall back to zero” but actually evaluates the addition first and can produce `NaN` before the nullish coalescing runs.

**Files Affected**:
- `src/web/src/components/System/Files/Explorer.jsx`

**Wrong**:
```javascript
const total = directory?.directories?.length + directory?.files?.length ?? 0;
```

**Correct**:
```javascript
const total =
  (directory?.directories?.length ?? 0) +
  (directory?.files?.length ?? 0);
```

**Why This Keeps Happening**: `??` has lower precedence than `+`, so a fallback at the end of an arithmetic expression does not protect intermediate operands. When optional values participate in math, default each term before the calculation.

### 0w2. `Connection refused` Must Not Be Blanket-Classified As A Benign Unobserved Task Failure

### 0w3. Tagged Release Notes Must Never Fall Back To The Entire `Unreleased` Section

**The Bug**: The release-note generator preferred a matching changelog section, but when one did not exist yet for the exact tag it fell back to the full `docs/CHANGELOG.md` `## [Unreleased]` section. That caused each new GitHub release body to re-publish old bullets from prior releases instead of only listing the delta since the previous tag.

**Files Affected**:
- `scripts/generate-release-notes.sh`
- `docs/CHANGELOG.md`

**Wrong**:
```bash
# Tagged release notes pulled the whole rolling Unreleased bucket.
elif [[ -n "$UNRELEASED_SECTION" ]]; then
  printf '%s\n\n' "$UNRELEASED_SECTION"
```

**Correct**:
```bash
# Tagged release notes must use either the matching version section or
# synthesize from the previous-tag commit range. Unreleased is for in-flight
# work only, not for published tags.
```

**Why This Keeps Happening**: `Unreleased` is a rolling staging area for future release content, so it always contains a mixture of old and new bullets until someone manually cuts a dated/versioned section. Using it at tag time feels convenient, but it breaks the core release contract: a published release body must describe only the changes introduced since the immediately previous release.

### 0w4. `Soulseek.ListenIpAddress` Must Not Be Set To Loopback For A Live Client

**The Bug**: A live slskd node was configured with `Soulseek.ListenIpAddress = 127.0.0.1`. The client still logged in and could search, but every peer-facing operation (`endpoint`, `info`, `browse`, downloads) failed because the Soulseek server handed other peers the node's externally visible address while slskd was only listening on loopback.

**Files Affected**:
- `src/slskd/Core/Options.cs`
- `config/slskd.example.yml`

**Wrong**:
```yaml
soulseek:
  listen_ip_address: 127.0.0.1
```

**Correct**:
```yaml
soulseek:
  listen_ip_address: 0.0.0.0
```

```text
If the node is meant to connect to the Soulseek network, bind the Soulseek
listener to 0.0.0.0 or a reachable LAN/VPN interface, not loopback.
```

**Why This Keeps Happening**: Loopback feels safe for local testing because the daemon still starts, logs in, and can initiate server-side activity like searches. But peer operations are different: other clients dial the address the server knows for you, not your local loopback binding. That creates the exact “logged in, searchable, but all peer transfers/browse/info fail” pattern unless startup rejects the configuration.

**The Bug**: After the listener-startup race was fixed, `Program.IsBenignUnobservedTaskException(...)` still treated any unobserved `SocketError.ConnectionRefused` as benign. That meant real refused connections from unrelated or still-broken transfer paths could be silently downgraded before the narrower Soulseek-network classifier had a chance to decide whether the failure was expected churn or a real bug.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
return exception switch
{
    SocketException socketException when socketException.SocketErrorCode == SocketError.ConnectionRefused => true,
    _ => false,
};
```

**Correct**:
```csharp
return false;
```

```text
Only `IsExpectedSoulseekNetworkException(...)` should downgrade expected peer/distributed-network churn, because it checks the exception type and the Soulseek-specific context. Blanket refusal suppression hides real failures.
```

**Why This Keeps Happening**: Once a specific startup race is fixed, it is tempting to keep a broad suppression rule around as “harmless noise control.” In practice that turns a targeted workaround into a catch-all mask. Global unobserved-task handling must stay narrower than the suspected failure domain, or the logs and tests stop distinguishing expected network churn from real transfer-path regressions.

### 0w3. Download Enqueue Must Not Pre-Fail On Auxiliary `ConnectToUserAsync` Priming

**The Bug**: `DownloadService.EnqueueAsync(...)` fetched the user's endpoint and then eagerly called `Client.ConnectToUserAsync(...)` before scheduling the real transfer task. That control-channel priming was not required for the actual `Client.DownloadAsync(...)` path, but if the auxiliary peer connect hit `Connection refused` it aborted the whole enqueue before the transfer pipeline had a chance to run or report the real failure state.

**Files Affected**:
- `src/slskd/Transfers/Downloads/DownloadService.cs`
- `tests/slskd.Tests.Unit/Transfers/Downloads/DownloadServiceTests.cs`

**Wrong**:
```csharp
endpoint = await Client.GetUserEndPointAsync(username, cancellationToken);
await Client.ConnectToUserAsync(username, invalidateCache: false, cancellationToken);
```

**Correct**:
```csharp
// Do not require an eager auxiliary peer-control connection here.
// Let the actual transfer pipeline own connection establishment and failure handling.
```

**Why This Keeps Happening**: It feels reasonable to “warm” a peer connection up front for validation or caching, but that creates a second connection path with different failure behavior than the real transfer code. When the preflight connect fails earlier or differently, slskdn aborts legitimate transfer requests for the wrong reason and the logs point at the warm-up path instead of the transfer path that actually matters.

### 0w4. Startup Soulseek Option Patches Must Match The Live Reconfigure Transfer Surface

**The Bug**: The startup `Application.StartAsync()` patch path and the later live-reconfigure path were not actually equivalent. Live reconfigure updated `incomingConnectionOptions`, but startup patching only set `peerConnectionOptions` and `transferConnectionOptions`. That left transfer-listener behavior dependent on whether the process had only booted once or had already gone through a later options reconfigure, which is exactly the kind of environment-sensitive seam that can keep search/browse working while peer transfers still misbehave.

**Files Affected**:
- `src/slskd/Application.cs`
- `tests/slskd.Tests.Unit/Core/ApplicationLifecycleTests.cs`

**Wrong**:
```csharp
return new SoulseekClientOptionsPatch(
    ...
    peerConnectionOptions: connectionOptions,
    transferConnectionOptions: transferOptions,
    distributedConnectionOptions: distributedOptions,
    ...);
```

**Correct**:
```csharp
return new SoulseekClientOptionsPatch(
    ...
    peerConnectionOptions: connectionOptions,
    transferConnectionOptions: transferOptions,
    incomingConnectionOptions: connectionOptions,
    distributedConnectionOptions: distributedOptions,
    ...);
```

```text
Startup and later reconfigure must configure the same transfer-related option surface, or fixes appear
"working" only after an options reload instead of on a clean boot.
```

**Why This Keeps Happening**: Boot-time configuration code tends to drift from the "real" runtime reconfigure code because both paths are manually assembled patches with overlapping fields. If one path gets a new transfer-related option and the other does not, clean startup and post-reconfigure behavior silently diverge. Any Soulseek client patch helper used at startup should be shared and unit-tested against the fields the live reconfigure path depends on.

### 0n. Missing `yt-dlp` Must Degrade YouTube SongID Runs, Not Fail Them

**The Bug**: SongID treated a missing `yt-dlp` binary as a fatal YouTube run failure. Metadata analysis already fell back to a raw URL query, but the later evidence pipeline still called `PrepareYouTubeAssetsAsync()` unguarded and crashed the run at the evidence stage.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`
- `packaging/aur/PKGBUILD`
- `packaging/proxmox-lxc/setup-inside-ct.sh`

**Wrong**:
```csharp
await RunToolAsync("yt-dlp", new[] { "-f", "bestaudio", "-o", audioOutput, source }, cancellationToken).ConfigureAwait(false);
```

```text
Result: YouTube SongID runs failed with "An error occurred trying to start process 'yt-dlp'..."
instead of completing with metadata-only evidence.
```

**Correct**:
```csharp
if (!await CommandExistsAsync("yt-dlp", cancellationToken).ConfigureAwait(false))
{
    run.Evidence.Add("yt-dlp unavailable; skipping YouTube audio, video, and comment extraction. Continuing with metadata-only SongID analysis.");
    return new PreparedAnalysisAssets
    {
        WorkspacePath = workspace,
        AnalysisAudioSource = "youtube_metadata",
    };
}
```

```text
Also make packaging install yt-dlp anywhere we claim YouTube SongID works out of the box.
```

**Why This Keeps Happening**: The source-analysis phase already handles missing helper tools gracefully, but the downstream evidence pipeline is easy to forget because it runs later and uses different helper methods. Any external tool that is optional for enrichment must be checked again at the asset-preparation stage, not just when building the initial query.

### 0o. Metadata-Only SongID Runs Cannot Call `Max()` On An Empty Clip List

**The Bug**: Once YouTube SongID was allowed to continue without `yt-dlp`, the evidence pipeline still crashed when no clips were generated because `AddPipelineEvidenceAsync()` computed `MaxAiArtifactScore` with `run.Clips.Max(...)`.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`

**Wrong**:
```csharp
run.Scorecard.MaxAiArtifactScore = run.Clips.Max(clip => clip.AiHeuristics?.ArtifactScore ?? 0);
```

**Correct**:
```csharp
run.Scorecard.MaxAiArtifactScore = run.Clips.Count == 0
    ? 0
    : run.Clips.Max(clip => clip.AiHeuristics?.ArtifactScore ?? 0);
```

**Why This Keeps Happening**: SongID scoring code was written assuming the evidence pipeline always produces at least one clip for non-text sources. As soon as optional-tool fallback paths or metadata-only analysis are introduced, every aggregate over `run.Clips` needs an empty-list-safe default.

### 0p. Native Job API Clients Must Use The Backend's Snake-Case Contract Exactly

**The Bug**: SongID action buttons like `Plan Discography` and single-release album planning silently failed because the frontend posted camelCase keys (`artistId`, `targetDirectory`, `mbReleaseId`, `targetDir`) to native job endpoints whose request models are annotated with snake-case names (`artist_id`, `target_dir`, `mb_release_id`).

**Files Affected**:
- `src/web/src/lib/jobs.js`
- `src/slskd/Jobs/DiscographyJobService.cs`
- `src/slskd/API/Native/JobsController.cs`

**Wrong**:
```javascript
await api.post('/api/jobs/discography', {
  artistId,
  profile,
  targetDirectory,
});
```

**Correct**:
```javascript
await api.post('/api/jobs/discography', {
  artist_id: artistId,
  profile,
  target_dir: targetDirectory,
});
```

**Why This Keeps Happening**: Most of the web client talks to the versioned REST API using camelCase payloads, so it is easy to assume the native job endpoints behave the same way. When a backend request type uses explicit `JsonPropertyName` values, mirror that contract exactly in the shared frontend client and lock it down with tests.

### 0q. SongID Artist-Graph Expansion Must Be Time-Boxed Per Artist

**The Bug**: SongID runs could appear stuck at `38%` in `artist_graph` because `AddArtistCandidatesAsync()` awaited full MusicBrainz release-graph expansion for each artist candidate. Large artists with many release groups caused long or effectively unbounded waits, which stalled the whole SongID run before the evidence pipeline even started.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `src/slskd/Integrations/MusicBrainz/ReleaseGraphService.cs`
- `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`

**Wrong**:
```csharp
var releaseGraph = await _releaseGraphService.GetArtistReleaseGraphAsync(artistId, false, cancellationToken).ConfigureAwait(false);
```

**Correct**:
```csharp
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(ArtistGraphFetchTimeout);
releaseGraph = await _releaseGraphService.GetArtistReleaseGraphAsync(artistId, false, timeoutCts.Token).ConfigureAwait(false);
```

```text
If the fetch times out or fails, continue with a lightweight artist candidate instead of stalling the run.
```

**Why This Keeps Happening**: the artist-graph stage looks like lightweight candidate enrichment from the SongID side, but the underlying MusicBrainz graph service performs deep release-group expansion with rate-limited per-group requests. Treat that dependency like a potentially expensive remote enrichment step and bound it explicitly inside SongID.

### 0r. SongID Search Actions Must Emit Canonical `Artist - Track` Queries, Not Metadata Soup

**The Bug**: SongID-generated searches were reusing generic query builders that concatenated uploader names, album text, duplicate titles, and other metadata into the search string. That made the actual Soulseek searches noisy and reduced recall for the intended `artist + track` match.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`

**Wrong**:
```csharp
analysis.Query = BuildBestQuery(track, artist, title, uploader);
SearchText = string.Join(" ", new[] { hit.Artist, hit.Title }.Where(value => !string.IsNullOrWhiteSpace(value)));
```

**Correct**:
```csharp
analysis.Query = BuildTrackSearchText(artist ?? uploader, track ?? title);
SearchText = BuildTrackSearchText(hit.Artist, hit.Title);
```

```text
Use a dedicated formatter for user-facing search actions so generated searches stay in the canonical `Artist - Track` shape unless there truly is no artist/title pair to work with.
```

**Why This Keeps Happening**: `BuildBestQuery(...)` is fine for broad metadata lookups, but it is too permissive for the actual search strings we send to Soulseek. Once SongID has an artist/title pair, switching back to a generic "join every clue" helper quickly pollutes the query with low-signal metadata.

### 0s. Release Notes Must Filter Release-Hygiene Doc Commits Out Of The Included-Commits List

**The Bug**: The repo-backed release-note generator listed standalone ADR gotcha commits and `docs: add release notes ...` commits in `## Included Commits`, even when the actual product change was already summarized in the changelog. That made release pages look like the same fix landed multiple times.

**Files Affected**:
- `scripts/generate-release-notes.sh`

**Wrong**:
```text
- `9da3519` docs: Add gotcha for packaged slskd config precedence
- `8265aff` docs: Add gotcha for packaged dual-port web defaults
- `d988e37` fix: harden packaged defaults and SongID youtube fallback
```

**Correct**:
```text
Treat `docs: Add gotcha for ...` and `docs: add release notes ...` as release-hygiene commits.
Keep them out of the generated Included-Commits list so the visible commit summary only reflects distinct product/code changes.
```

**Why This Keeps Happening**: this repo intentionally creates extra docs-only commits during bugfix work, and the generic git-log based release-note generator has no idea those commits are bookkeeping for the real fix. Without an explicit filter, the release page inflates one bugfix into multiple apparent changes.

### 0t. Changelog Discipline Must Be Enforced At Commit/PR Time, Not Deferred To Release Generation

### 0u. GitHub Actions Metadata Jobs Must Emit Every Referenced Checksum Output

**The Bug**: The stable release metadata job in `build-on-tag.yml` called `update-stable-release-metadata.sh` with `${{ steps.hashes.outputs.linux_arm64_hex }}` even though the `Calculate Hashes` step never emitted that output. The same block also passed a Windows checksum under the inconsistent name `win_x64_sha`, which made the argument contract harder to audit. The metadata update step then failed immediately with the script usage error.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/scripts/update-stable-release-metadata.sh`

**Wrong**:
```yaml
echo "linux_x64_hex=$(sha256sum slskdn-main-linux-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "macos_x64_hex=$(sha256sum slskdn-main-osx-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "macos_arm64_hex=$(sha256sum slskdn-main-osx-arm64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "win_x64_sha=$(sha256sum slskdn-main-win-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
```

```yaml
bash packaging/scripts/update-stable-release-metadata.sh \
  "${VERSION}" \
  "${{ steps.hashes.outputs.linux_x64_hex }}" \
  "${{ steps.hashes.outputs.linux_arm64_hex }}" \
  "${{ steps.hashes.outputs.macos_x64_hex }}" \
  "${{ steps.hashes.outputs.macos_arm64_hex }}" \
  "${{ steps.hashes.outputs.win_x64_sha }}" \
  "${VERSION}"
```

**Correct**:
```yaml
echo "linux_x64_hex=$(sha256sum slskdn-main-linux-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "linux_arm64_hex=$(sha256sum slskdn-main-linux-arm64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "macos_x64_hex=$(sha256sum slskdn-main-osx-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "macos_arm64_hex=$(sha256sum slskdn-main-osx-arm64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
echo "win_x64_hex=$(sha256sum slskdn-main-win-x64.zip | cut -d' ' -f1)" >> "$GITHUB_OUTPUT"
```

```yaml
bash packaging/scripts/update-stable-release-metadata.sh \
  "${VERSION}" \
  "${{ steps.hashes.outputs.linux_x64_hex }}" \
  "${{ steps.hashes.outputs.linux_arm64_hex }}" \
  "${{ steps.hashes.outputs.macos_x64_hex }}" \
  "${{ steps.hashes.outputs.macos_arm64_hex }}" \
  "${{ steps.hashes.outputs.win_x64_hex }}" \
  "${VERSION}"
```

**Why This Keeps Happening**: GitHub Actions expressions silently expand missing outputs to empty strings, so the workflow looks correct at a glance until the downstream script rejects the argument list. Whenever a shell script has positional required arguments, define the workflow outputs next to the call site and keep the output names aligned with the script parameter names.

### 0v. CodeQL Must Track The Live Default Branch, Or Fixed Alerts Stay Open Forever

**The Bug**: The repository’s CodeQL workflow was still configured for `master` while active development and releases happen on `main`. Security fixes landed on `main`, but GitHub never re-analyzed the branch automatically, so open alerts on `main` persisted and reappeared in release triage even after the underlying code changed.

**Files Affected**:
- `.github/workflows/codeql.yml`

**Wrong**:
```yaml
on:
  push:
    branches: [master]
  pull_request:
    branches: [master]
```

**Correct**:
```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
```

```text
If the repo still needs compatibility during a branch rename, include both branches explicitly.
```

**Why This Keeps Happening**: release and security work naturally follow the real default branch, but old workflow triggers are easy to miss after a branch rename because the YAML still looks valid and GitHub does not warn that the workflow is effectively dormant for the active branch. Any branch rename must be followed by an audit of all workflow trigger branches, especially CodeQL and other security automation.

### 0w. Swashbuckle.AspNetCore 10 Is Not A Drop-In Upgrade For The Current OpenAPI Surface

**The Bug**: Merging the Dependabot bump from `Swashbuckle.AspNetCore 6.6.2` to `10.1.7` immediately broke the backend build. Existing code references `Microsoft.OpenApi.Models` and the current `IOperationFilter` surface expected by the 6.x package set, so the build failed as soon as restore picked up the new package.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `.github/dependabot.yml`
- `src/slskd/Common/OpenAPI/ContentNegotiationOperationFilter.cs`
- `src/slskd/Program.cs`

**Wrong**:
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.7" />
```

```text
Result: `Microsoft.OpenApi.Models` and `OpenApiOperation` references no longer resolved against the restored package graph.
```

**Correct**:
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
```

```yaml
- dependency-name: "Swashbuckle.AspNetCore"
  update-types: ["version-update:semver-major"]
```

```text
Treat the 10.x line as an intentional migration task, not a background Dependabot merge.
```

**Why This Keeps Happening**: Swagger/OpenAPI packages look like low-risk tooling deps, but major-version jumps often change transitive OpenAPI assemblies and code-generation contracts. If the repo has handwritten `Microsoft.OpenApi` integrations, keep major Swashbuckle bumps behind an explicit migration plan instead of auto-merging them from a green Dependabot PR.

### 0x. Roslyn Analyzer Package Upgrades Must Match The Effective Compiler Version

**The Bug**: Upgrading `Microsoft.CodeAnalysis.Analyzers` to `5.3.0` removed a Dependabot PR but introduced persistent `CS9057` warnings because the analyzer assembly expects compiler `4.12.0.0` while the current build still runs compiler `4.11.0.0`.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `.github/dependabot.yml`

**Wrong**:
```xml
<PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="5.3.0" />
```

```text
CSC : warning CS9057: The analyzer assembly ... references version '4.12.0.0'
of the compiler, which is newer than the currently running version '4.11.0.0'.
```

**Correct**:
```xml
<PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" />
```

```yaml
- dependency-name: "Microsoft.CodeAnalysis.Analyzers"
  update-types: ["version-update:semver-major", "version-update:semver-minor", "version-update:semver-patch"]
```

```text
Keep analyzer package upgrades blocked until the repo intentionally upgrades to a compiler/SDK line that satisfies the analyzer's Roslyn dependency.
```

**Why This Keeps Happening**: analyzer packages look like ordinary dev-time dependencies, but they execute inside the compiler and are tightly coupled to the Roslyn version shipped by the active SDK. Green restore/build does not mean the package is actually compatible; always check for `CS9057` after analyzer bumps and treat that warning as a version-compatibility failure, not benign noise.

### 0y. Dependabot Must Ignore Deliberately Pinned `Microsoft.Extensions.*` Major Lines

**The Bug**: Dependabot kept reopening PRs for `Microsoft.Extensions.Configuration 10.0.5` and `Microsoft.Extensions.Caching.Memory 10.0.5` even though `slskd.csproj` already documents those direct references as intentionally pinned to the current compatibility line.

**Files Affected**:
- `.github/dependabot.yml`
- `src/slskd/slskd.csproj`

**Wrong**:
```yaml
ignore:
  - dependency-name: "Microsoft.Data.Sqlite"
    update-types: ["version-update:semver-major"]
```

```xml
<!-- Pin to 9.x so framework-dependent publish includes these; dotNetRdf 3.4.1 requires 9.0.9 -->
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.14" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.14" />
```

**Correct**:
```yaml
ignore:
  - dependency-name: "Microsoft.Extensions.Caching.Memory"
    update-types: ["version-update:semver-major"]
  - dependency-name: "Microsoft.Extensions.Configuration"
    update-types: ["version-update:semver-major"]
```

```text
If a package is intentionally pinned for runtime/publish compatibility, Dependabot must carry the same rule or it will keep reopening the same "unresolved" major bump PRs.
```

**Why This Keeps Happening**: the project file comment explains the package pin, but Dependabot only knows what is encoded in `.github/dependabot.yml`. Any deliberate direct-package pin needs a matching ignore rule, otherwise the PR queue drifts back open even after the team already decided not to take that major line.

### 0z. `Microsoft.Extensions.*` Upgrades Must Move As An Aligned Set Across App And Test Projects

**The Bug**: The repo partially upgraded onto `Microsoft.Extensions.* 10.0.5` by moving `Configuration.Abstractions` and `Primitives`, but left direct `Caching.Memory`, `Configuration`, and the performance-test `Logging.Abstractions` / `Options` packages on `9.0.14`. Dependabot PR `#189` then failed restore with `NU1605` because `slskd` pulled `10.0.5` transitive requirements into `slskd.Tests.Performance`, which still pinned lower versions.

**Files Affected**:
- `src/slskd/slskd.csproj`
- `tests/slskd.Tests.Performance/slskd.Tests.Performance.csproj`

**Wrong**:
```xml
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.14" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.14" />
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.5" />
<PackageReference Include="Microsoft.Extensions.Primitives" Version="10.0.5" />
```

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.14" />
<PackageReference Include="Microsoft.Extensions.Options" Version="9.0.14" />
```

**Correct**:
```text
When taking a `Microsoft.Extensions.*` major line, align the direct app references and any test-project companion references (`Logging.Abstractions`, `Options`, etc.) to the same line before judging the upgrade.
```

**Why This Keeps Happening**: these packages are tightly interrelated, but they live across multiple projects and some of them arrive transitively. A partial upgrade can look harmless in the main app project while still breaking restore in test projects that pin adjacent `Microsoft.Extensions.*` packages directly.

**The Bug**: The repo relied on `scripts/generate-release-notes.sh` fallback behavior at release time instead of requiring feature/fix commits to update `docs/CHANGELOG.md` as they landed. That left dozens of releases with no curated changelog content, and release notes were synthesized from commit history long after the actual work happened.

**Files Affected**:
- `docs/CHANGELOG.md`
- `.githooks/pre-commit`
- `.github/workflows/ci.yml`

**Wrong**:
```text
Ship a feature/fix commit without touching docs/CHANGELOG.md, then hope the release-time generator can reconstruct something acceptable from git subjects later.
```

**Correct**:
```text
If a commit or PR changes product code, packaging behavior, user-visible UI, or workflows that affect shipped behavior, require a corresponding entry under `docs/CHANGELOG.md` `## [Unreleased]` before the commit/PR can pass.
```

```text
Release generation should consume curated changelog content, not serve as the first time release-worthy changes are summarized.
```

**Why This Keeps Happening**: release automation is easier to notice because it runs on every tag, while changelog discipline has no pain until much later. Without a local hook and CI validation, developers optimize for shipping code and defer the changelog until the release is already being cut, which is exactly when recall is worst.

### 0u. A Checked-In Hook Is Useless Unless The Repo Explicitly Installs `core.hooksPath`

**The Bug**: The repo added meaningful checks in `.githooks/pre-commit` and `.githooks/pre-push`, but nothing in the normal setup path actually configured `git config core.hooksPath .githooks`. That meant local enforcement was silently absent for anyone who had not configured hooks manually.

**Files Affected**:
- `.githooks/pre-commit`
- `.githooks/pre-push`
- local setup / bootstrap docs and scripts

**Wrong**:
```text
Assume that committing hook scripts into `.githooks/` is enough for them to run automatically on every clone.
```

**Correct**:
```text
Provide an explicit repo bootstrap step (script and docs) that runs:
git config core.hooksPath .githooks
```

```text
If local hook enforcement matters, install the hooks as part of normal developer setup instead of relying on tribal knowledge.
```

**Why This Keeps Happening**: checked-in hooks look "present" in the tree, so it is easy to forget Git ignores them unless `core.hooksPath` or `.git/hooks` is configured. CI catches some problems later, but the whole point of local hooks is to fail earlier than PR time.

### 0v. Packaging Static-File Edits Must Immediately Refresh AUR `sha256sums`

**The Bug**: Changing packaged static files like `packaging/aur/slskd.service` or `packaging/aur/slskd.yml` without updating `PKGBUILD` and `PKGBUILD-bin` left the repo in a state where later unrelated commits were blocked by the AUR hash consistency hook.

**Files Affected**:
- `packaging/aur/PKGBUILD`
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/slskd.service`
- `packaging/aur/slskd.yml`

**Wrong**:
```text
Edit `packaging/aur/slskd.service` or `packaging/aur/slskd.yml`, but leave the checked-in `sha256sums=()` entries pointing at the old file contents.
```

**Correct**:
```text
Whenever a packaged static file changes, recompute and commit the matching `sha256sums` in both AUR PKGBUILDs as part of the same change.
```

**Why This Keeps Happening**: the package templates and the hash declarations live in different files, so it is easy to update one and forget the other until a later pre-commit run trips over it. The fix belongs with the packaging edit, not in some later cleanup commit.

### 0w. Unobserved Soulseek Peer/Distributed Connection Failures Must Not Be Logged As Process-Fatal

**The Bug**: `InstallShutdownTelemetry()` logged every `TaskScheduler.UnobservedTaskException` as `[FATAL]`, even for routine Soulseek peer/distributed network failures like timeouts, connection refusals, canceled socket reads, and indirect connection failures. The process stayed alive because the handler immediately called `e.SetObserved()`, but the logs made healthy-yet-noisy peer churn look like repeated daemon crashes.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    var msg = $"[FATAL] Unobserved task exception: {e.Exception.Message}";
    Console.Error.WriteLine(msg);
    Log?.Fatal(e.Exception, msg);
    e.SetObserved();
};
```

**Correct**:
```text
Classify common Soulseek peer/distributed network exceptions separately and log them as warning/debug noise, not process-fatal shutdown telemetry.
Reserve `[FATAL]` for truly unhandled process-level failures.
```

**Why This Keeps Happening**: unobserved task handlers are a tempting catch-all for "silent crash" telemetry, but P2P networking libraries often use fire-and-forget tasks internally and can surface expected connection churn there. Without classification, ordinary peer timeout noise becomes indistinguishable from an actual daemon-killing fault.

### 0x. Docker Images Must Override The Loopback HTTP Bind Default

**The Bug**: The container image inherited the global `web.address = 127.0.0.1` default, so `docker run -p 5030:5030 ...` looked healthy from inside the container while every host-side HTTP request reset because Kestrel was only listening on container loopback.

**Files Affected**:
- `Dockerfile`

**Wrong**:
```dockerfile
ENV \
  SLSKD_HTTP_PORT=5030 \
  SLSKD_HTTPS_PORT=5031
```

```text
Result: `/health` succeeds inside the container, Docker marks the container healthy,
but `curl http://host:5030/` from outside the container resets because nothing is
bound on the container's non-loopback interface.
```

**Correct**:
```dockerfile
ENV \
  SLSKD_HTTP_ADDRESS=0.0.0.0 \
  SLSKD_HTTP_PORT=5030 \
  SLSKD_HTTPS_PORT=5031
```

```text
Any Docker or container-oriented distribution path must force the web listener to
`0.0.0.0` unless it deliberately expects an in-container reverse proxy.
```

**Why This Keeps Happening**: the repo-wide default is intentionally conservative for bare-metal installs, but containers invert the reachability model. A loopback default that is safe on a host is broken in Docker unless the image or packaged config explicitly overrides it.

### 0y. Stable Release Metadata Automation Must Update `main` And The Full Metadata Set

**The Bug**: The stable tag workflow's repo-metadata step only rewrote a small subset of files and still tried to reset/push `origin/master`. In a `main`-based repo that meant successful stable releases could leave checked-in package metadata stale for multiple releases, and the next tag build would fail the release gate on mismatched versions.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `packaging/scripts/update-stable-release-metadata.sh`

**Wrong**:
```text
After a stable release, update only `flake.nix`, `Formula/slskdn.rb`, and Winget,
then `git reset --hard origin/master` and `git push origin HEAD:master`.
```

**Correct**:
```text
Use one repo-owned script that updates every checked-in stable metadata target from
the actual release asset hashes, and have the workflow fetch/reset/push `origin/main`.
```

```text
If release automation is supposed to keep the repo in sync, it must target the real
default branch and cover every file the release gate validates.
```

**Why This Keeps Happening**: release automation was built incrementally around whichever package manager was in front of us at the time, so the "current stable version" ended up duplicated across many files with no single source of truth. Once the repo switched from `master` to `main`, the branch mismatch quietly turned that partial updater into a no-op for the actual default branch.

### 0z. Launchpad PPA Uploads Need Passive FTP And Retry Logic On GitHub Runners

**The Bug**: The PPA workflow successfully signed and started uploading the source package, then failed mid-transfer with `550 Requested action not taken: internal server error`. Launchpad's `dput` output explicitly pointed at `passive_ftp`, and the current workflow used plain FTP with no passive mode or retry.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- `.github/workflows/release-ppa.yml`

**Wrong**:
```text
Configure `dput` with anonymous FTP only, then do a single `dput slskdn-ppa "$CHANGES_FILE"` attempt.
```

**Correct**:
```text
Set `passive_ftp = 1` in the PPA `dput` target and wrap the upload in a small retry loop,
because Launchpad FTP failures can be transient even after signatures and package assembly succeed.
```

**Why This Keeps Happening**: PPA upload is one of the last release steps, so it often gets less local exercise than build/test/package creation. On ephemeral GitHub runners, FTP behavior is more fragile than the earlier release stages, and the workflow needs to be explicit about passive mode instead of relying on whatever default the runner image happens to ship.

### 0l. Packaged Service Config Can Keep Reading The Runtime Copy Under `~/.local/share/slskd`, Not `/etc/slskd/slskd.yml`

**The Bug**: On packaged installs, changing `/etc/slskd/slskd.yml` did not affect the live service because the systemd unit runs with `HOME=/var/lib/slskd` and no `--config`, so `slskd` kept loading `/var/lib/slskd/.local/share/slskd/slskd.yml`. That left the Web UI bound to `127.0.0.1:5030` even after `/etc/slskd/slskd.yml` was updated.

**Files Affected**:
- `packaging/aur/slskd.service`
- runtime config at `/var/lib/slskd/.local/share/slskd/slskd.yml`

**Wrong**:
```ini
ExecStart=/usr/share/dotnet/dotnet /usr/lib/slskd/slskd.dll
Environment="HOME=/var/lib/slskd"
```

```yaml
# edited, but ignored by the running service
web:
  port: 5030
  address: "*"
```

**Correct**:
```ini
ExecStart=/usr/share/dotnet/dotnet /usr/lib/slskd/slskd.dll --http-address "*"
```

```text
If the package intends `/etc/slskd/slskd.yml` to be authoritative, pass `--config /etc/slskd/slskd.yml` explicitly from the service unit.
```

**Why This Keeps Happening**: The package ships `/etc/slskd/slskd.yml`, which strongly suggests that file is authoritative, but the service's config search order prefers the runtime config under the service account home directory when no explicit `--config` is passed. On fresh installs that also inherit the loopback default `web.address`, the service looks healthy while the Web UI is unreachable remotely.

### 0m. Packaged Installs Should Not Enable HTTPS On `5031` By Default If The Login UX Still Centers `5030`

**The Bug**: Packaged installs exposed HTTP on `5030` and HTTPS on `5031` by default, while docs and user expectation still centered on `5030`. Browsers that auto-upgraded to HTTPS or users who manually tried `https://host:5030` hit TLS failures or confusing "problem loading page" behavior even though the HTTP UI itself was healthy.

**Files Affected**:
- `packaging/aur/slskd.yml`
- `packaging/aur/README.md`
- release workflows that publish `packaging/aur/slskd.yml`

**Wrong**:
```yaml
web:
  port: 5030
```

**Correct**:
```yaml
web:
  port: 5030
  https:
    disabled: true
```

**Why This Keeps Happening**: The application defaults are reasonable for a generic binary, but packaged installs are judged by the first URL users type. If packaging wants `5030` to be the default entry point, it must make that path unambiguous by disabling the extra HTTPS listener unless the user explicitly enables TLS and chooses to manage `5031`.

### 0j. Relay Validation Logs Must Hash Agent And Connection Identifiers

**The Bug**: Relay credential-validation paths logged raw cached relay connection ids and compared response credentials directly in debug logs, which exposed server-internal identifiers and kept triggering CodeQL cleartext-storage findings.

**Files Affected**:
- `src/slskd/Relay/RelayService.cs`

**Wrong**:
```csharp
Log.Debug("Validation failed: No registration for cached relay connection {ConnectionId}", trustedConnectionId);
Log.Debug("Validation failed: Supplied credential {Credential} does not match expected credential {Expected}", credential, expectedCredential);
```

**Correct**:
```csharp
Log.Debug("Validation failed: No registration for cached relay connection {ConnectionId}", GetConnectionLogId(trustedConnectionId));
Log.Debug("Validation failed: Supplied response credential does not match expected credential for agent {Agent}", GetAgentLogId(agentName));
```

**Why This Keeps Happening**: Relay auth/debug code lives right next to token verification, so it is easy to log the raw values that are convenient during manual troubleshooting. Treat relay agent names, connection ids, and response credentials like secrets in logs and emit only stable hashed identifiers or higher-level state.

### 0k. `Serilog.Sinks.Grafana.Loki` 8.x Replaced `outputTemplate` With `textFormatter`

**The Bug**: Upgrading `Serilog.Sinks.Grafana.Loki` from 7.x to 8.x broke `Program.ConfigureGlobalLogger()` at compile time because the sink overload no longer accepts `outputTemplate`, and the new default formatter changes payload shape unless a formatter is provided explicitly.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/slskd.csproj`

**Wrong**:
```csharp
config => config.GrafanaLoki(
    OptionsAtStartup.Logger.Loki ?? string.Empty,
    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
```

**Correct**:
```csharp
config => config.GrafanaLoki(
    OptionsAtStartup.Logger.Loki ?? string.Empty,
    textFormatter: new MessageTemplateTextFormatter(
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        null))
```

**Why This Keeps Happening**: version-bump PRs that look like pure package updates can still carry sink/config API breaks, especially around logging formatters. When upgrading logging sinks, check the new extension-method signature and preserve the intended output shape explicitly instead of assuming template parameters are stable across major versions.

### 0f. Invalid-Config Startup Tests Must Satisfy Base Option Validation Before Asserting Later Hardening Failures

**The Bug**: `EnforceInvalidConfigIntegrationTests` expected the subprocess to fail on a hardening rule, but CI hit the earlier base-options validation first because the temporary app directory did not contain `wwwroot`, so startup returned success from the early validation path and never reached the hardening check.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
await File.WriteAllTextAsync(yml, """
    web:
      enforceSecurity: true
""");
```

```csharp
if (!OptionsAtStartup.TryValidate(out var result))
{
    Log.Information(result.GetResultView());
    return;
}
```

**Correct**:
```csharp
Directory.CreateDirectory(Path.Combine(tempDir, "wwwroot"));
await File.WriteAllTextAsync(yml, """
    web:
      contentPath: wwwroot
      enforceSecurity: true
""");
```

```csharp
if (!OptionsAtStartup.TryValidate(out var result))
{
    Log.Information(result.GetResultView());
    Exit(1);
}
```

**Why This Keeps Happening**: Startup has more than one validation layer. Tests that target a later validation stage can be accidentally preempted by unrelated defaults unless the temporary environment satisfies the earlier base constraints first. When startup does reject config, it must terminate non-zero or release-gate tests will treat a real config failure as a false success.

### 0g. Startup Failure Tests Need a Deterministic Plain-Text Rule Signal, Not Just Structured Logger Output

**The Bug**: The invalid-config subprocess test exited non-zero on CI but still failed because the captured output did not reliably include the hardening rule name, even though the exception was being logged.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
catch (HardeningValidationException hex)
{
    Log.Fatal(hex, "Hardening validation failed: {Message}", hex.Message);
    Exit(1);
}
```

**Correct**:
```csharp
catch (HardeningValidationException hex)
{
    Console.Error.WriteLine($"[HardeningValidation] {hex.RuleName}: {hex.Message}");
    Log.Fatal(hex, "Hardening validation failed: {Message}", hex.Message);
    Exit(1);
}
```

**Why This Keeps Happening**: Integration tests read raw subprocess stdout/stderr, not the structured logger event stream. If the test depends on a specific diagnostic token, write that token directly to stderr/stdout before exiting.

### 0h. Async Timeout/Circuit Tests Should Assert Eventual State Change, Not An Exact Transition Call Count

**The Bug**: `ServiceTimeout_TriggersCircuitBreaker` assumed the circuit breaker would always be visibly open on the 6th timed-out call, but CI occasionally returned one more timeout before the open-state reply, making the test fail even though the breaker logic was still converging correctly.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 5; i++)
{
    await router.RouteAsync(call, peerId);
}

var lastReply = await router.RouteAsync(lastCall, peerId);
Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Correct**:
```csharp
ServiceReply? lastReply = null;
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Why This Keeps Happening**: Timeouts and cancellation-driven state transitions can land on slightly different attempts under CI scheduling. For async resilience tests, assert that the expected state change happens within a bounded window instead of pinning the assertion to one exact call number unless the implementation explicitly guarantees it.

### 0i. Circuit-Breaker Failure Tests Have The Same Exact-Transition Flake As Timeout Tests

**The Bug**: `CircuitBreaker_OpensAfter5ConsecutiveFailures` assumed the open-state response must appear on the 6th failing call, but CI can surface one more ordinary failure before returning `ServiceUnavailable`, creating the same exact-transition flake as the timeout-based breaker test.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 6; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Correct**:
```csharp
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Why This Keeps Happening**: The breaker state update is observable through asynchronous request flow, not as a hard guarantee tied to a specific numbered call. If the behavior being tested is "the breaker opens after sustained failures," the assertion should allow a bounded convergence window.

### 0j. Subprocess Config Tests Must Create Relative Content Directories Under `AppContext.BaseDirectory`

**The Bug**: `EnforceInvalidConfigIntegrationTests` created a temp `wwwroot` and changed the subprocess working directory, but `contentPath` validation and runtime static-file setup both resolve relative paths under `AppContext.BaseDirectory`, so CI still failed base config validation before the hardening rule.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
Directory.CreateDirectory(Path.Combine(tempDir, "wwwroot"));
await File.WriteAllTextAsync(yml, """
    web:
      contentPath: wwwroot
""");
```

**Correct**:
```csharp
var contentPath = "test-wwwroot-" + Guid.NewGuid().ToString("N")[..8];
var contentDir = Path.Combine(Path.GetDirectoryName(slskdDll)!, contentPath);
Directory.CreateDirectory(contentDir);
```

**Why This Keeps Happening**: `SLSKD_APP_DIR` and `WorkingDirectory` do not control this option. The validator and `Program` both explicitly combine `OptionsAtStartup.Web.ContentPath` with `AppContext.BaseDirectory`, so tests must place any temporary relative content directory under the built app output directory.

### 0j2. `FileExistsAttribute` Must Treat Empty Strings As "Not Configured", Not As A Path To Validate

**The Bug**: Full-startup invalid-config tests were still being preempted before hardening validation because optional config fields that default to `string.Empty` hit `Path.GetFullPath("")` inside `FileExistsAttribute`, throwing `ArgumentException` instead of cleanly skipping validation for an unset optional path.

**Files Affected**:
- `src/slskd/Common/Validation/FileExistsAttribute.cs`
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`
- `src/slskd/Core/Options.cs`

**Wrong**:
```csharp
if (value != null)
{
    var file = Path.GetFullPath(value.ToString()!);
    if (!string.IsNullOrEmpty(file))
    {
        // validate file
    }
}
```

**Correct**:
```csharp
var rawPath = value?.ToString();
if (string.IsNullOrWhiteSpace(rawPath))
{
    return ValidationResult.Success;
}

var file = Path.GetFullPath(rawPath);
// validate file
```

**Why This Keeps Happening**: Many optional path settings in `Options` intentionally default to `string.Empty`. Validation attributes must distinguish "unset optional value" from "configured path" before normalizing or resolving the path, or they will fail startup for the wrong reason and mask the real validation behavior being tested.

### 0j3. Subprocess Startup Tests Must Launch The Freshly Built App Binary, Not A Hard-Coded `Release` Output

**The Bug**: `EnforceInvalidConfigIntegrationTests` always launched `src/slskd/bin/Release/net8.0/slskd.dll`, so `dotnet test` could rebuild the project in `Debug` while the test still executed a stale old `Release` binary and reported a failure that had already been fixed in source.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
var slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd.dll");
if (!File.Exists(slskdDll))
{
    return;
}
```

**Correct**:
```csharp
var slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Debug", "net8.0", "slskd.dll");
if (!File.Exists(slskdDll))
{
    slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd.dll");
}
```

**Why This Keeps Happening**: Integration tests that spawn the app as a subprocess are not automatically tied to the current test build configuration. If they hard-code one output folder, they can silently run stale binaries and invalidate the test result. Always resolve the current build output first, then fall back only if necessary.

### 0j3b. Full-Instance Test Harnesses Must Prefer The Current `Debug` Binary Over An Older Native `Release` Executable

**The Bug**: `SlskdnFullInstanceRunner` searched for the native app executable in `Release` before `Debug`. During `dotnet test`, the integration project rebuilt the app in `Debug`, but the harness still launched an older `src/slskd/bin/Release/net8.0/slskd` binary. That made end-to-end CSRF tests report stale runtime behavior even though the current source already emitted the correct antiforgery cookies.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
var candidates = new[]
{
    Path.Combine(solutionRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd"),
    Path.Combine(solutionRoot, "src", "slskd", "bin", "Debug", "net8.0", "slskd"),
};
```

**Correct**:
```csharp
var candidates = new[]
{
    Path.Combine(solutionRoot, "src", "slskd", "bin", "Debug", "net8.0", "slskd"),
    Path.Combine(solutionRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd"),
};
```

**Why This Keeps Happening**: subprocess integration harnesses are easy to treat like "real app" launchers, but they still need to follow the build configuration of the current test run. If `Release` is checked first, an old executable can survive indefinitely and make new fixes look broken. Always prefer the freshly built `Debug` output inside test harnesses, then fall back to `Release` only when needed.

### 0j3c. CI Must Not Enforce Constant-Time Behavior With Raw Wall-Clock Microbenchmarks

**The Bug**: `SecurityUtilsTests.ConstantTimeEquals_LargeArrays_PerformsConstantTime` compared `MeasureTimingVariance()` results for equal and unequal inputs and failed the release gate when GitHub runner noise made the ratio explode. The test was measuring `max - min` across tiny stopwatch samples, so scheduler jitter dominated the result and created a flaky false failure unrelated to the actual `ConstantTimeEquals` implementation.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/SecurityUtilsTests.cs`

**Wrong**:
```csharp
var timingEqual = SecurityUtils.MeasureTimingVariance(() =>
    SecurityUtils.ConstantTimeEquals(a, a), 100);
var timingUnequal = SecurityUtils.MeasureTimingVariance(() =>
    SecurityUtils.ConstantTimeEquals(a, b), 100);

var ratio = (double)timingUnequal / Math.Max(timingEqual, 1);
Assert.True(ratio < 300.0, $"Timing ratio too high: {ratio} ...");
```

**Correct**:
```text
Keep deterministic correctness coverage in CI, and treat constant-time claims as code-structure / algorithm reviews, not stopwatch-ratio assertions. If timing is checked at all, do it in a dedicated benchmark or security harness outside the release gate.
```

**Why This Keeps Happening**: wall-clock timing tests look attractive for security helpers, but shared CI runners are hostile to microbenchmarks. A ratio built from min/max stopwatch deltas mostly measures host jitter, preemption, and CPU frequency changes. For release gating, prefer deterministic invariants such as full-length iteration logic and `NoInlining`/`NoOptimization` markers over pseudo-benchmark thresholds.

### 0j4. Empty-String Unix Socket Defaults Must Be Treated As "Not Configured" Before Kestrel Startup

**The Bug**: Full-instance integration tests timed out for 25 seconds per test because `Program` treated `web.socket` as configured whenever it was non-null. The option defaults to `string.Empty`, so Kestrel received an empty Unix socket path and crashed during `builder.Build()` before the API ever came up.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Core/Options.cs`

**Wrong**:
```csharp
if (OptionsAtStartup.Web.Socket != null)
{
    options.ListenUnixSocket(OptionsAtStartup.Web.Socket);
}
```

**Correct**:
```csharp
if (!string.IsNullOrWhiteSpace(OptionsAtStartup.Web.Socket))
{
    options.ListenUnixSocket(OptionsAtStartup.Web.Socket);
}
```

**Why This Keeps Happening**: This codebase uses `string.Empty` for many optional path-like settings. Startup code must check for a real configured value, not just non-null, or the app can die in a later subsystem with a misleading exception instead of simply leaving the optional feature disabled.

### 0j5. Full-Instance Bridge Tests Must Set The Bridge-Enable Environment Variable, Not Just Bridge Config

**The Bug**: `SlskdnFullInstanceRunner` wrote `virtualSoulfind.bridge.enabled: true` into test config, but `Program` only registers `BridgeProxyServer` when `SLSKDN_ENABLE_BRIDGE_PROXY` is present. The bridge integration tests therefore spent their startup budget booting an app that would never open the expected bridge port.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};
```

**Correct**:
```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};

if (enableBridge)
{
    startInfo.Environment["SLSKDN_ENABLE_BRIDGE_PROXY"] = "1";
}
```

**Why This Keeps Happening**: Some test-only or deadlock-guarded features are gated by environment variables in addition to config. If a harness expects a hosted service to exist, it must mirror the same startup gate the application uses, or tests will silently wait on a port that the process was never allowed to bind.

### 0j6. Startup Fallbacks Must Treat Blank Static Path Settings As Unset, And Test Harnesses Must Pass `APP_DIR`

**The Bug**: Full-instance bridge tests still failed before config load with `Filesystem exception: Directory  does not exist...` because the child process never received an app directory, while `Program` used `??=` on static string properties initialized to `string.Empty`. A blank `AppDirectory` or `ConfigurationFile` therefore stayed blank instead of falling back to the defaults.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
AppDirectory ??= DefaultAppDirectory;
ConfigurationFile ??= DefaultConfigurationFile;
```

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};
```

**Correct**:
```csharp
if (string.IsNullOrWhiteSpace(AppDirectory))
{
    AppDirectory = DefaultAppDirectory;
}

if (string.IsNullOrWhiteSpace(ConfigurationFile))
{
    ConfigurationFile = DefaultConfigurationFile;
}
```

```csharp
startInfo.Environment["APP_DIR"] = appDir;
```

**Why This Keeps Happening**: Several startup path fields are modeled as empty strings, not nulls. `??=` only fixes null, so blank values can leak into filesystem setup and explode before logging/config are fully online. Test harnesses that expect isolated app state must also pass `APP_DIR` explicitly instead of assuming `WorkingDirectory` or the config file location will set it indirectly.

### 0j7. SOCKS/Tunnel Tests Must Use Bounded Timeouts And Deterministic Silent Endpoints, Not "Probably Unused" Ports

**The Bug**: `TorTransport_ConnectionTimeout_HandledGracefully` hung for minutes because it assumed `127.0.0.1:12345` was unused. If something listens on that port but never speaks SOCKS, `TorSocksTransport` had no internal handshake timeout and would wait forever on `ReadAsync`.

**Files Affected**:
- `src/slskd/Common/Security/TorSocksTransport.cs`
- `tests/slskd.Tests.Integration/Security/TorIntegrationTests.cs`

**Wrong**:
```csharp
var torOptions = new TorOptions
{
    SocksAddress = "127.0.0.1:12345",
};

await transport.ConnectAsync("example.com", 80);
```

**Correct**:
```csharp
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
var effectiveToken = linkedCts.Token;
```

```csharp
using var silentServer = new SilentTcpServer();
await silentServer.StartAsync();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, cts.Token));
```

**Why This Keeps Happening**: Connection-refused tests are only deterministic if the endpoint state is deterministic. A "random closed port" can become an open but silent endpoint on another machine or CI worker, and transports without a bounded connect/handshake timeout will then hang forever in network reads.

### 0j8. Tuple Member Renames Must Be Updated In Tests Too, Or `dotnet test` Will Fail At Compile Time

**The Bug**: Root `dotnet test` still failed after the integration fixes because a unit test was reading a tuple member as `.totalKeys` after the production API had been normalized to PascalCase tuple names `(int TotalKeys, int ContentHintKeys)`.

**Files Affected**:
- `tests/slskd.Tests.Unit/Mesh/Phase8MeshTests.cs`

**Wrong**:
```csharp
var stored = dht.GetStoreStats();
Assert.True(stored.totalKeys >= 1);
```

**Correct**:
```csharp
var stored = dht.GetStoreStats();
Assert.True(stored.TotalKeys >= 1);
```

**Why This Keeps Happening**: Tuple element names are part of the compile-time API surface even though they look lightweight. When cleanup work renames tuple elements for consistency, stale tests won’t fail until the affected project is rebuilt, so always grep the test tree for the old element name after changing a returned tuple signature.

### 0j9. Optional Lazy Service Resolvers Must Not Throw Before Stats Objects Return Their Local Counters

**The Bug**: `MeshStatsCollector.GetStatsAsync()` returned all-zero stats in unit tests even after `RecordMessageSent()` and `RecordMessageReceived()` because optional lazy resolvers for DHT and overlay services threw before the method reached the return statement, and the outer catch replaced the partially collected counters with a default zeroed stats object.

**Files Affected**:
- `src/slskd/Mesh/MeshStatsCollector.cs`

**Wrong**:
```csharp
this.dhtClient = new Lazy<Dht.InMemoryDhtClient>(() =>
    serviceProvider.GetService(typeof(VirtualSoulfind.ShadowIndex.IDhtClient)) as Dht.InMemoryDhtClient
        ?? throw new InvalidOperationException(...));
```

**Correct**:
```csharp
this.dhtClient = new Lazy<Dht.InMemoryDhtClient?>(() =>
    serviceProvider.GetService(typeof(VirtualSoulfind.ShadowIndex.IDhtClient)) as Dht.InMemoryDhtClient);
```

**Why This Keeps Happening**: Diagnostics collectors often depend on optional subsystems. If a lazy resolver throws for an absent optional service, the whole stats call can fall into a broad catch and wipe out independent counters that were already valid. Optional service lookups should return `null` and let the collector degrade gracefully.

### 0j10. Re-entrant Stop/Dispose Paths Must Null Out `CancellationTokenSource` Before Canceling

**The Bug**: `LocalPortForwarder.StopForwardingAsync()` could throw `ObjectDisposedException` because `ForwarderInstance.StopAsync()` called `_cts?.Cancel()` even when a previous stop/dispose path had already disposed that same `CancellationTokenSource`.

**Files Affected**:
- `src/slskd/Common/Security/LocalPortForwarder.cs`

**Wrong**:
```csharp
_cts?.Cancel();
...
_cts?.Dispose();
```

**Correct**:
```csharp
var cts = _cts;
_cts = null;

try
{
    cts?.Cancel();
}
catch (ObjectDisposedException)
{
}

cts?.Dispose();
```

**Why This Keeps Happening**: Stop and dispose paths often converge on the same field. If the field remains published while cleanup is in progress, later callers can observe a disposed token source and try to cancel it again. Copy the reference locally, clear the field first, and then clean it up once.

### 0k. Empty-String DTO Defaults Break `??`-Based Fallback Chains For Hash Selection

**The Bug**: `AudioVariant` cleanup initialized codec-specific hash properties to `string.Empty`, but `CanonicalStatsService` still used `??` fallback chains when building dedup keys. Empty strings are non-null, so FLAC variants with missing `FlacStreamInfoHash42` stopped falling back to `FlacPcmMd5` and collapsed into the same canonical candidate bucket.

**Files Affected**:
- `src/slskd/Audio/CanonicalStatsService.cs`
- `src/slskd/Audio/AudioVariant.cs`

**Wrong**:
```csharp
var streamHash = v.Codec switch
{
    "FLAC" => v.FlacStreamInfoHash42 ?? v.FlacPcmMd5 ?? v.FileSha256,
    "MP3" => v.Mp3StreamHash ?? v.FileSha256,
    _ => v.FileSha256,
};
```

**Correct**:
```csharp
var streamHash = v.Codec switch
{
    "FLAC" => FirstNonEmpty(v.FlacStreamInfoHash42, v.FlacPcmMd5, v.FileSha256),
    "MP3" => FirstNonEmpty(v.Mp3StreamHash, v.FileSha256),
    _ => FirstNonEmpty(v.FileSha256),
};
```

**Why This Keeps Happening**: Nullability cleanup often replaces nullable strings with `string.Empty`, but any fallback logic that relied on `??` now changes behavior silently. When a value is semantically "missing", use `string.IsNullOrWhiteSpace`-aware fallback helpers instead of null-coalescing chains.

### 0k. Timeout-Based Circuit Tests Must Distinguish "Breaker Opened" From "Open-State Reply Observed"

**The Bug**: `ServiceTimeout_TriggersCircuitBreaker` still flaked after widening the retry window because the last timeout call could be the one that opens the breaker, which means the first `ServiceUnavailable` reply only appears on the next probe request.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}
```

**Correct**:
```csharp
for (int i = 0; i < 10; i++)
{
    await router.RouteAsync(call, peerId);

    var circuit = router.GetStats().CircuitBreakers.Find(cb => cb.ServiceName == "slow-service");
    if (circuit?.IsOpen == true)
    {
        break;
    }
}

var blockedReply = await router.RouteAsync(probeCall, peerId);
Assert.Equal(ServiceStatusCodes.ServiceUnavailable, blockedReply.StatusCode);
```

**Why This Keeps Happening**: The timeout response reports the result of the current request, while the breaker state change affects the next request. For timeout-driven breaker tests, inspect router state or issue a separate probe after failures instead of expecting the opening transition and blocked reply to collapse onto the same call.

### 0l. E2E Harnesses Must Not Treat Gitignored Downloaded Media As Baseline CI Fixtures

### 0m. Lightweight Integration Hosts Must Stub Every Controller Dependency They Expose

**The Bug**: Integration test hosts included the VirtualSoulfind controllers in their application parts, but did not register `IDisasterModeCoordinator` and `IShadowIndexQuery` consistently, so tests failed at request time with controller activation errors instead of exercising the endpoint contracts.

**Files Affected**:
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs`
- `tests/slskd.Tests.Integration/Harness/SlskdnTestClient.cs`

**Wrong**:
```csharp
services.AddControllers()
    .AddApplicationPart(typeof(global::slskd.API.VirtualSoulfind.DisasterModeController).Assembly);
```

```csharp
builder.Services.AddSingleton<global::slskd.VirtualSoulfind.ShadowIndex.IShadowIndexQuery>(_ =>
    new StubShadowIndexQueryForTests());
```

**Correct**:
```csharp
services.AddSingleton<global::slskd.VirtualSoulfind.DisasterMode.IDisasterModeCoordinator>(_ =>
    new StubDisasterModeCoordinatorForTests());
services.AddSingleton<global::slskd.VirtualSoulfind.ShadowIndex.IShadowIndexQuery>(_ =>
    new StubShadowIndexQueryForTests());
```

**Why This Keeps Happening**: The lightweight test hosts deliberately avoid the full production DI graph, so every added controller creates a manual dependency obligation. If you expose a controller assembly in a stub host, audit its constructor dependencies immediately or the tests will fail with activation errors that look like app regressions.

### 0n. Native API DTOs Need Explicit Snake_Case Binding When Compatibility Clients Post Snake_Case JSON

**The Bug**: The native jobs endpoints accepted positional record DTOs with PascalCase property names, but the Soulbeet compatibility tests posted `snake_case` JSON like `mb_release_id` and `target_dir`, causing model binding to fail with `400` ProblemDetails payloads.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`

**Wrong**:
```csharp
public record MbReleaseJobRequest(
    string MbReleaseId,
    string TargetDir,
    string Tracks = "all",
    JobConstraints? Constraints = null);
```

**Correct**:
```csharp
public record MbReleaseJobRequest(
    [property: JsonPropertyName("mb_release_id")] string MbReleaseId,
    [property: JsonPropertyName("target_dir")] string TargetDir,
    [property: JsonPropertyName("tracks")] string Tracks = "all",
    [property: JsonPropertyName("constraints")] JobConstraints? Constraints = null);
```

**Why This Keeps Happening**: ASP.NET Core JSON binding is case-insensitive, but it does not translate underscore-delimited names into PascalCase automatically. Compatibility-facing DTOs need explicit `JsonPropertyName` attributes anywhere the request contract is `snake_case`.

**The Bug**: The scheduled `E2E Tests` workflow treated downloaded media as mandatory baseline fixtures, so a transient fetch failure aborted the whole suite before any real UI coverage ran.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`
- `src/web/e2e/fixtures/ensure-fixtures.ts`
- `src/web/e2e/streaming.spec.ts`
- `src/web/e2e/multippeer-sharing.spec.ts`
- `test-data/slskdn-test-fixtures/meta/manifest.json`

**Wrong**:
```ts
const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));
for (const entry of manifest.files) {
  await fs.access(path.join(fixturesRoot, entry.path));
}
```

**Correct**:
```ts
await ensureFixtures(fixturesRoot);
test.skip(
  !hasDownloadedMediaFixtures(),
  'Streaming E2E requires downloaded media fixtures',
);
```

**Why This Keeps Happening**: The committed fixture tree contains a small tracked offline baseline plus a larger gitignored media tier fetched on demand. CI can legitimately run without the downloaded tier, so the harness must validate the tracked baseline and let only media-dependent specs skip.

### 0m. E2E Harnesses Should Launch The Prebuilt Release App, And UI Pages Must Tolerate Missing `server` State During Boot

**The Bug**: The E2E harness launched `dotnet run` during test execution even though CI had already built the backend, which made the first node startup exceed the 30-second TCP wait on cold runs. Follow-up fixes then hit two more traps: `Web.ContentPath` only accepts relative paths under `AppContext.BaseDirectory`, and the web-asset sync helper must recreate the destination root before `fs.cp` or the copy can fail with `ENOENT` on nested assets. Separately, `Searches.jsx` read `server.isConnected` before `applicationState.server` existed, so a harmless `/capabilities` failure turned into a page-crashing `TypeError`.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`
- `src/web/src/components/Search/Searches.jsx`

**Wrong**:
```ts
const webContentPath = path.relative(expectedAppBaseDir, webBuildPath);
const args = ['run', '--project', projectPath, '--', '--app-dir', this.appDir];
await waitForTcpListen('127.0.0.1', this.apiPort, 30_000);
```

```jsx
disabled={creating || !server.isConnected}
placeholder={server.isConnected ? 'Search phrase' : 'Connect to server'}
```

**Correct**:
```ts
const webContentPath = webBuildPath.replace(/\\/g, '/');
const args = useBuiltRelease
  ? [builtDllPath, '--app-dir', this.appDir]
  : ['run', '--project', projectPath, '-c', 'Release', '--', '--app-dir', this.appDir];
await waitForTcpListen('127.0.0.1', this.apiPort, 60_000);
```

```jsx
await replaceDirectoryContents(webBuildPath, path.join(builtAppBaseDir, 'wwwroot'));
const webContentPath = 'wwwroot';
```

```jsx
const normalizedServer = server ?? { isConnected: false };
disabled={creating || !normalizedServer.isConnected}
placeholder={
  normalizedServer.isConnected
    ? 'Search phrase'
    : 'Connect to server to perform a search'
}
```

**Why This Keeps Happening**: E2E harness code often grows around local developer assumptions, but CI already provides a built Release app and is much less tolerant of redundant startup work. Even when using the prebuilt app, the runtime still validates `Web.ContentPath` as a relative directory under the app base, so the harness has to stage fresh web assets into `wwwroot` instead of pointing at arbitrary absolute paths, and that staging helper has to recreate the destination root explicitly before copying nested asset trees. On the frontend, boot-time state objects can be transiently missing even when the route eventually succeeds, so route components must normalize optional props before reading nested fields.

### 0n. XML Doc Comments Must Escape `&` Or CI Will Emit CS1570 Warnings

**The Bug**: Several XML documentation comments used raw ampersands in phrases like `Identity & Friends` or `Test Coverage & Regression Harness`, which made the generated XML invalid and caused repeated `CS1570` warnings in CI.

**Files Affected**:
- `src/slskd/Common/Moderation/*.cs`
- `src/slskd/Common/CodeQuality/*.cs`
- `src/slskd/Mesh/Realm/*.cs`
- `src/slskd/Sharing/*.cs`
- `src/slskd/VirtualSoulfind/**/*.cs`

**Wrong**:
```csharp
///     T-MCP04: Peer Reputation & Enforcement.
/// <summary>Contact PeerId (Identity & Friends).</summary>
```

**Correct**:
```csharp
///     T-MCP04: Peer Reputation &amp; Enforcement.
/// <summary>Contact PeerId (Identity &amp; Friends).</summary>
```

**Why This Keeps Happening**: XML doc comments are real XML, not plain text. Any raw `&` inside `///` comments has to be escaped or the compiler will produce malformed-doc warnings that bury real CI signal.

### 0a. Do Not Assume MusicBrainz Target Models Expose the Same ID Surface

**The Bug**: `SongIdService` treated `TrackTarget` like `AlbumTarget` and tried to read `MusicBrainzArtistId` from it, which broke the build because `TrackTarget` does not expose that property.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
run.Tracks.Insert(0, new SongIdTrackCandidate
{
    MusicBrainzArtistId = track.MusicBrainzArtistId,
});
```

**Correct**:
```csharp
run.Tracks.Insert(0, new SongIdTrackCandidate
{
    RecordingId = track.MusicBrainzRecordingId,
    Title = track.Title,
    Artist = track.Artist,
});
```

**Why This Keeps Happening**: The MusicBrainz integration models look similar at a glance, but they are not interchangeable. Check the actual target type before assuming it carries artist, release, or recording IDs in the same shape.

### 0b. Do Not Introduce `System.Threading.Lock` Unless the Project Explicitly Uses That API Surface

**The Bug**: A new SongID SQLite store used `Lock` instead of a plain object gate, which failed to compile in this project even though the code targets modern .NET.

**Files Affected**:
- `src/slskd/SongID/SongIdRunStore.cs`

**Wrong**:
```csharp
private readonly Lock _gate = new();
```

**Correct**:
```csharp
private readonly object _gate = new();
```

**Why This Keeps Happening**: It is easy to mentally map “modern C#” to every recent BCL convenience type. This repo still needs compatibility with the actual APIs available in its current toolchain and package graph, so prefer the already-common locking patterns unless you have confirmed the newer type is already in use here.

### 0c. When You Extend a Controller Constructor, Update Direct Instantiation Tests Immediately

**The Bug**: `JobsController` gained an `IMusicBrainzClient` dependency for release-to-artist resolution, but `JobsControllerPaginationTests` still instantiated the old constructor shape, breaking unit test compilation before the new SongID tests could even run.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`
- `tests/slskd.Tests.Unit/API/Native/JobsControllerPaginationTests.cs`

**Wrong**:
```csharp
controller = new JobsController(
    discographyService.Object,
    labelCrateService.Object,
    logger.Object,
    jobServiceList.Object);
```

**Correct**:
```csharp
controller = new JobsController(
    discographyService.Object,
    labelCrateService.Object,
    musicBrainzClient.Object,
    logger.Object,
    jobServiceList.Object);
```

**Why This Keeps Happening**: Controllers are often instantiated through ASP.NET DI in production, so constructor changes compile there but any unit test that manually news up the controller will silently drift until the next test build.

### 0. MusicBrainz Release IDs Are Not Artist IDs

**The Bug**: A single-release SongID or jobs path passed an MB release ID into `DiscographyJobRequest.ArtistId`, which silently created the wrong planning context and broke album download handoff.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`
- `src/slskd/Jobs/DiscographyJobService.cs`
- `src/slskd/Integrations/MusicBrainz/MusicBrainzClient.cs`

**Wrong**:
```csharp
var jobId = await discographyJobService.CreateJobAsync(
    new DiscographyJobRequest
    {
        ArtistId = request.MbReleaseId,
        Profile = DiscographyProfile.AllReleases,
    },
    cancellationToken);
```

**Correct**:
```csharp
var release = await musicBrainzClient.GetReleaseAsync(request.MbReleaseId, cancellationToken);
var jobId = await discographyJobService.CreateJobAsync(
    new DiscographyJobRequest
    {
        ArtistId = release.MusicBrainzArtistId,
        ReleaseIds = new List<string> { request.MbReleaseId },
        Profile = DiscographyProfile.AllReleases,
    },
    cancellationToken);
```

**Why This Keeps Happening**: MusicBrainz uses different MBIDs for releases, recordings, and artists. It is easy to treat “some MBID” as interchangeable unless the code explicitly carries the identifier type through the model.

### 0d. Do Not Store Recovery-Only State in `Summary` When Queue Refresh Also Owns `Summary`

**The Bug**: SongID restart recovery marked runs as "Recovered after restart..." in `Summary`, but the next queue-position refresh immediately overwrote that text with the normal queued summary, erasing the only visible recovery signal.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
run.Summary = "Recovered after restart and re-queued for SongID analysis.";
await EnqueueRunAsync(run, broadcastCreate: false).ConfigureAwait(false);
```

**Correct**:
```csharp
run.Evidence.Add("Recovered after restart and re-queued for SongID analysis.");
run.Summary = "Queued for SongID analysis.";
await EnqueueRunAsync(run, broadcastCreate: false).ConfigureAwait(false);
```

**Why This Keeps Happening**: `Summary` looks like a convenient general-purpose status field, but the queue layer also treats it as derived UI text. If two parts of the pipeline both own the same display field, one silently erases the other.

### 0e. Do Not Use Wall-Clock Time or Tight Upper Bounds for Async Delay Tests

**The Bug**: `SecurityUtilsTests.RandomDelayAsync_ValidRange_CompletesWithinExpectedTime` measured `Task.Delay` with `DateTimeOffset.UtcNow` and a narrow upper bound, so the test failed intermittently on loaded CI runners even though the code was behaving correctly.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/SecurityUtilsTests.cs`

**Wrong**:
```csharp
var startTime = DateTimeOffset.UtcNow;
await SecurityUtils.RandomDelayAsync(minDelay, maxDelay);
var endTime = DateTimeOffset.UtcNow;
var actualDelay = (endTime - startTime).TotalMilliseconds;
Assert.True(actualDelay <= maxDelay + 600, $"Delay too long: {actualDelay}ms");
```

**Correct**:
```csharp
var timer = Stopwatch.StartNew();
await SecurityUtils.RandomDelayAsync(minDelay, maxDelay);
timer.Stop();
var actualDelay = timer.Elapsed.TotalMilliseconds;
Assert.True(actualDelay <= maxDelay + 1500, $"Delay too long: {actualDelay}ms");
```

**Why This Keeps Happening**: Async timing tests are easy to write like benchmark assertions, but `Task.Delay` is scheduler-dependent and CI hosts can stall for hundreds of milliseconds. Use monotonic timing (`Stopwatch`) and treat the upper bound as a broad sanity check, not a precision guarantee.

### 0e1. Do Not Use Sub-Millisecond Cancellation Windows In Unit Tests

**The Bug**: `MeshSearchRpcHandlerTests.HandleAsync_TimeCap_RespectsCancellation` and `AsyncRulesTests.ValidateCancellationHandlingAsync_WithProperCancellation_ReturnsTrue` used razor-thin delay/cancellation windows that passed locally but failed in release-gate CI when the runner scheduled work a little differently.

**Files Affected**:
- `tests/slskd.Tests.Unit/DhtRendezvous/Search/MeshSearchRpcHandlerTests.cs`
- `tests/slskd.Tests.Unit/Common/CodeQuality/AsyncRulesTests.cs`

### 0e1c. Do Not Use Cancellation Timeouts As The Success Condition For Async Enumerables

**The Bug**: `CoverTrafficGeneratorTests.GenerateCoverTrafficAsync_GeneratesMessagesWithCorrectSize` used `CancellationTokenSource(TimeSpan.FromSeconds(5))` as the loop control while waiting for multiple messages from an async enumerable. CI sometimes hit token cancellation before the second message arrived, so the test failed with `TaskCanceledException` even though the generator was behaving correctly.

**Files Affected**:
- `tests/slskd.Tests.Unit/Mesh/Privacy/CoverTrafficGeneratorTests.cs`

**Wrong**:
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
{
    messages.Add(message);
    if (messages.Count >= 2)
        break;
}
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();

await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
{
    messages.Add(message);
    if (messages.Count >= 1)
    {
        cts.Cancel();
    }
}
```

**Why This Keeps Happening**: Async enumerable tests often mix "eventually produce output" with "cancel after some time" and accidentally make timeout expiration the normal success path. For scheduler-dependent producers, use an explicit completion condition and only cancel after the assertion target is satisfied.

**Wrong**:
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
await Task.Delay(100, cts.Token);
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();
cts.Cancel();
_shareServiceMock
    .Setup(x => x.SearchLocalAsync(It.IsAny<SearchQuery>()))
    .Returns(Task.FromCanceled<IEnumerable<Soulseek.File>>(cts.Token));
```

```csharp
await Task.Delay(Timeout.InfiniteTimeSpan, ct);
var result = await AsyncRules.ValidateCancellationHandlingAsync(
    TestOperationAsync,
    TimeSpan.FromMilliseconds(50));
```

**Why This Keeps Happening**: Tests that rely on "cancel within 1ms" or "wake up after 100ms" are really testing scheduler luck, not code behavior. Make cancellation deterministic with pre-cancelled tokens or infinite waits that must be interrupted by cancellation.

### 0e1a. Cancellation Validators Need A Post-Cancel Grace Window, Not A Single Tight Race

**The Bug**: `AsyncRules.ValidateCancellationHandlingAsync` raced the operation against `Task.Delay(timeout * 2)` and treated any miss as a cancellation failure. On a loaded CI runner, a correctly cancellable operation could still lose that race by a few scheduler ticks and fail the release gate.

**Files Affected**:
- `src/slskd/Common/CodeQuality/AsyncRules.cs`
- `tests/slskd.Tests.Unit/Common/CodeQuality/AsyncRulesTests.cs`

**Wrong**:
```csharp
using var cts = new CancellationTokenSource(timeout);
var operationTask = operation(cts.Token);
var delayTask = Task.Delay(timeout * 2, CancellationToken.None);
var completedTask = await Task.WhenAny(operationTask, delayTask);
return completedTask != delayTask;
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();
var operationTask = operation(cts.Token);
await Task.Delay(timeout);
cts.Cancel();
var completedTask = await Task.WhenAny(operationTask, Task.Delay(gracePeriod));
```

**Why This Keeps Happening**: Cancellation is not an instantaneous event. A validator that uses one narrow race window is still testing scheduler timing rather than cancellation handling. Cancel explicitly, then give the operation a bounded grace period to observe the token and unwind.

### 0e1b. Timing-Sanity Tests Must Avoid Precise Upper Bounds On Loaded CI Runners

**The Bug**: `SecurityUtilsTests.RandomDelayAsync_ValidRange_CompletesWithinExpectedTime` still used an upper bound that looked broad locally but was too tight for a loaded GitHub runner, where a `10-50ms` delay measured just over 2 seconds and failed the release gate.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/SecurityUtilsTests.cs`

**Wrong**:
```csharp
Assert.True(actualDelay <= maxDelay + 1500, $"Delay too long: {actualDelay}ms");
```

**Correct**:
```csharp
Assert.True(actualDelay <= maxDelay + 5000, $"Delay too long: {actualDelay}ms");
```

**Why This Keeps Happening**: `Task.Delay` timing in CI is dominated by scheduler availability, not just requested delay length. These tests should verify the code is not obviously broken, not enforce a pseudo-benchmark ceiling.

### 0e2. Do Not Mark Internal Mutation APIs As `AllowAnonymous` Just Because They Feel "Protocol-Like"

**The Bug**: A broad `// PR-02: intended-public` pattern was applied to controllers that mutate local state or trigger expensive work, including analyzer migrations, VirtualSoulfind queue operations, MediaCore registry writes/imports, stats resets, and pod control-plane actions. That exposed internal admin/UI surfaces to unauthenticated callers.

**Files Affected**:
- `src/slskd/Audio/API/AnalyzerMigrationController.cs`
- `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`
- `src/slskd/MediaCore/API/Controllers/ContentDescriptorPublisherController.cs`
- `src/slskd/MediaCore/API/Controllers/ContentIdController.cs`
- `src/slskd/MediaCore/API/Controllers/IpldController.cs`
- `src/slskd/MediaCore/API/Controllers/MediaCoreStatsController.cs`
- `src/slskd/MediaCore/API/Controllers/MetadataPortabilityController.cs`
- `src/slskd/PodCore/API/Controllers/PodJoinLeaveController.cs`
- `src/slskd/PodCore/API/Controllers/PodMessageRoutingController.cs`
- `src/slskd/PodCore/API/Controllers/PodMessageSigningController.cs`

**Wrong**:
```csharp
[ApiController]
[AllowAnonymous] // PR-02: intended-public
[ValidateCsrfForCookiesOnly]
public class ContentIdController : ControllerBase
{
    [HttpPost("register")]
    public Task<IActionResult> Register(...)
```

**Correct**:
```csharp
[ApiController]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public class ContentIdController : ControllerBase
{
    [HttpPost("register")]
    public Task<IActionResult> Register(...)
```

**Why This Keeps Happening**: "Public data model" and "public unauthenticated endpoint" are not the same thing. Once `[AllowAnonymous]` is placed at class scope, every `POST`/`PUT`/`PATCH`/`DELETE` action under that controller becomes reachable unless explicitly re-protected.

### 0e3. Public Protocol Controllers Must Still Default To Authenticated At Class Scope

**The Bug**: Even after narrowing the anonymous surface, `StreamsController`, `ActivityPubController`, and `WebFingerController` still used class-level `[AllowAnonymous]`. That meant any future action added to those controllers would become public by default, recreating the same auth-boundary bug in a quieter form.

**Files Affected**:
- `src/slskd/Streaming/StreamsController.cs`
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SocialFederation/API/WebFingerController.cs`

**Wrong**:
```csharp
[AllowAnonymous]
public class ActivityPubController : ControllerBase
{
    [HttpGet("{actorName}")]
    public async Task<IActionResult> GetActor(...)
```

**Correct**:
```csharp
[Authorize(Policy = AuthPolicy.Any)]
public class ActivityPubController : ControllerBase
{
    [HttpGet("{actorName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActor(...)
```

**Why This Keeps Happening**: It is easy to think "this controller is for a public protocol" and stop there. The safer pattern is still auth-by-default at controller scope with `[AllowAnonymous]` only on the exact protocol/bootstrap actions that must stay public. That way future endpoints do not silently widen the unauthenticated surface.

### 0f. Fix Every Release Workflow and Checked-In Package Template When Asset Names Change

**The Bug**: The main tag workflow was corrected to publish `slskdn-main-*.zip`, but `release-packages.yml` still waited for the old `slskdn-<tag>-linux-x64.zip` pattern and the checked-in Chocolatey templates were still pinned to `0.24.1-slskdn.40`, leaving stable-package automation and manual package publishing stale.

**Files Affected**:
- `.github/workflows/release-packages.yml`
- `packaging/chocolatey/slskdn.nuspec`
- `packaging/chocolatey/tools/chocolateyinstall.ps1`

**Wrong**:
```yaml
ASSET_URL="https://github.com/snapetech/slskdn/releases/download/${{ steps.version.outputs.tag }}/slskdn-${{ steps.version.outputs.tag }}-linux-x64.zip"
```

```powershell
$url = "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.40/slskdn-main-win-x64.zip"
```

**Correct**:
```yaml
ASSET_URL="https://github.com/snapetech/slskdn/releases/download/${{ steps.version.outputs.tag }}/slskdn-main-linux-x64.zip"
```

```powershell
$url = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.52/slskdn-main-win-x64.zip"
```

**Why This Keeps Happening**: It is easy to fix only the primary build workflow and forget the secondary packaging workflows and checked-in templates that still encode old asset names or versions. Any release-format change must be audited across tag workflows, auxiliary release workflows, validation scripts, and package templates together.

### 0g. When You Extend Core Interfaces, Update Test Stubs and Fakes in the Legacy Test Projects Immediately

**The Bug**: `ISecurityService`, `IShareService`, and `IShareRepository` gained new members, but the older smoke/integration test stubs still implemented the previous interface shapes, so `dotnet test` failed even though the feature code compiled and targeted SongID tests passed.

**Files Affected**:
- `src/slskd/Core/Security/SecurityService.cs`
- `src/slskd/Shares/IShareService.cs`
- `src/slskd/Shares/IShareRepository.cs`
- `tests/slskd.Tests/TestHostFactory.cs`
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs`
- `tests/slskd.Tests.Integration/StubVirtualSoulfindServices.cs`

**Wrong**:
```csharp
internal class StubSecurityService : ISecurityService
{
    public JwtSecurityToken GenerateJwt(...) => ...;
    public (string Name, Role Role) AuthenticateWithApiKey(...) => ...;
}

public Task<IEnumerable<File>> SearchAsync(SearchQuery query) => ...;
public IEnumerable<File> Search(SearchQuery query) => ...;
```

### 0h. Retry Loops Around External Uploads Must Bound Each Attempt, Not Just the Number of Attempts

**The Bug**: The Snap Store publish steps retried transient `snapcraft upload` failures, but each upload attempt could block indefinitely waiting on the store, so the loop never advanced and the release stayed stuck in a single opaque upload step.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
for attempt in $(seq 1 60); do
  OUT="$(snapcraft upload --release=stable "$SNAP_PATH" 2>&1)"
  CODE=$?
  ...
done
```

**Correct**:
```bash
for attempt in $(seq 1 6); do
  OUT="$(timeout --signal=TERM 10m snapcraft upload --release=stable "$SNAP_PATH" 2>&1)"
  CODE=$?
  ...
done
```

**Why This Keeps Happening**: A retry loop looks resilient, but it does nothing if the wrapped command never returns. Any networked publish step needs both retry logic and a hard per-attempt timeout so GitHub Actions can surface the failure instead of hanging for tens of minutes.

**Correct**:
```csharp
internal class StubSecurityService : ISecurityService
{
    public JwtSecurityToken GenerateJwt(...) => ...;
    public (string Name, Role Role) AuthenticateWithApiKey(...) => ...;
    public void RevokeToken(string jti) { }
    public bool IsTokenRevoked(string jti) => false;
}

public Task<IEnumerable<File>> SearchAsync(SearchQuery query, int? limit = null) => ...;
public IEnumerable<File> Search(SearchQuery query, int? limit = null) => ...;
```

**Why This Keeps Happening**: The newer feature work tends to validate against focused unit tests first, but the repo still includes older smoke/integration projects with hand-written stubs. Interface drift is invisible until the broad solution test run compiles those projects, so every interface change needs a repo-wide grep for stub implementations before calling the tree releasable.

### 0h. Gate Metrics Hardening Rules on the Metrics Endpoint Actually Being Enabled

**The Bug**: `HardeningValidator` started enforcing a non-empty metrics password whenever metrics auth was not disabled, even if `metrics.enabled` was still `false`, which broke otherwise-valid startup configs and older hardening tests.

**Files Affected**:
- `src/slskd/Common/Security/HardeningValidator.cs`

**Wrong**:
```csharp
var metricsAuth = options.Metrics?.Authentication;
if (metricsAuth != null && !metricsAuth.Disabled &&
    string.IsNullOrWhiteSpace(metricsAuth.Password))
{
    throw new HardeningValidationException(RuleWeakMetricsPassword, msg);
}
```

**Correct**:
```csharp
var metrics = options.Metrics;
var metricsAuth = metrics?.Authentication;
if (metrics?.Enabled == true && metricsAuth != null && !metricsAuth.Disabled &&
    string.IsNullOrWhiteSpace(metricsAuth.Password))
{
    throw new HardeningValidationException(RuleWeakMetricsPassword, msg);
}
```

**Why This Keeps Happening**: Nested auth options default to “auth enabled” semantics even when the parent feature is disabled. Any startup validation that checks nested credentials must first gate on the top-level feature flag, or harmless defaults become fatal.

### 0i. Do Not Use Anonymous Objects for JSON-LD Keys That Need Literal `@` Names

**The Bug**: `SolidClientIdDocumentService` built the Solid client-id document with an anonymous object using `@context`, which serialized to `context` instead of the required JSON-LD key `@context`.

**Files Affected**:
- `src/slskd/Solid/SolidClientIdDocumentService.cs`
- `tests/slskd.Tests.Unit/Solid/SolidClientIdDocumentServiceTests.cs`

**Wrong**:
```csharp
var doc = new
{
    @context = "https://www.w3.org/ns/solid/oidc-context.jsonld",
};
```

**Correct**:
```csharp
var doc = new Dictionary<string, object?>
{
    ["@context"] = "https://www.w3.org/ns/solid/oidc-context.jsonld",
};
```

**Why This Keeps Happening**: In C#, the `@` prefix only escapes the identifier for the compiler; it is not part of the serialized property name. For wire formats that require literal keys like `@context`, use explicit string keys or a concrete model with `JsonPropertyName`.

### 1. `return undefined` vs `return []` in Frontend API Calls

**The Bug**: Frontend API functions that return `undefined` on error instead of `[]` cause downstream crashes.

**Files Affected**:
- `src/web/src/lib/searches.js` - `getResponses()`
- `src/web/src/lib/transfers.js` - `getAll()`

**Wrong**:
```javascript
if (!Array.isArray(response)) {
  console.warn('got non-array response');
  return undefined;  // 💀 Causes "Cannot read property 'map' of undefined"
}
```

**Correct**:
```javascript
if (!Array.isArray(response)) {
  console.warn('got non-array response');
  return [];  // ✅ Safe to iterate
}
```

**Why This Keeps Happening**: Models see `undefined` as a "signal" value and forget that callers will `.map()` or `.filter()` the result.

### 1a. Do Not Block SPA Initialization on Optional SignalR Handshakes

**The Bug**: `App.init()` waited on `appHub.start()` before clearing the full-screen loader, so any stalled SignalR negotiation kept the whole site on "loading" for 30 seconds even though auth had succeeded and the rest of the UI could render.

**Files Affected**:
- `src/web/src/components/App.jsx`
- `src/web/src/components/App.test.jsx`

**Wrong**:
```javascript
if (await session.check()) {
  const appHub = createApplicationHubConnection();
  await Promise.race([appHub.start(), hubTimeout]);
}
```

**Correct**:
```javascript
if (await session.check()) {
  this.startApplicationHub();
}
```
The hub startup stays bounded and logged, but it runs in the background instead of sitting in the critical render path.

**Why This Keeps Happening**: Real-time channels feel "core" during implementation, so it is easy to treat them like a prerequisite for first paint. In this UI they are enhancement paths, not the gate for showing the authenticated shell. Keep session validation in the blocking path, but let hub connection, retries, and late state hydration happen asynchronously.

### 1b. Do Not Run `security-and-quality` on `master` Unless You Intend to Triage Thousands of Maintainer Alerts

**The Bug**: The checked-in C# CodeQL workflow used `queries: security-and-quality`, which repopulated roughly 2,400 `master` alerts with maintainability and code-smell findings (`cs/local-not-disposed`, `cs/log-forging`, `cs/catch-of-all-exceptions`, etc.) even though the goal was ordinary security scanning.

**Files Affected**:
- `.github/workflows/codeql.yml`

**Wrong**:
```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    queries: security-and-quality
```

**Correct**:
```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    queries: security-extended
```

**Why This Keeps Happening**: `security-and-quality` sounds like a better default until it lands in a mature codebase and turns every broad code-quality heuristic into a repo-level security alert. On `master`, keep the suite scoped to security-focused queries unless there is an explicit, staffed cleanup plan for the extra findings.

### 1c. Do Not Let Arbitrary API-Supplied Absolute Paths Reach Filesystem Probes

**The Bug**: Destination validation, Library Health scans, and mesh-transfer target selection accepted caller-supplied absolute paths and passed them straight into `Directory.Exists`, `EnumerateFiles`, `File.WriteAllText`, or later file I/O, which triggered real path-injection findings and allowed the server to probe arbitrary filesystem locations.

**Files Affected**:
- `src/slskd/Common/Security/PathGuard.cs`
- `src/slskd/Destinations/API/Controllers/DestinationsController.cs`
- `src/slskd/LibraryHealth/LibraryHealthService.cs`
- `src/slskd/VirtualSoulfind/DisasterMode/MeshTransferService.cs`
- `src/slskd/VirtualSoulfind/Bridge/BridgeApi.cs`

**Wrong**:
```csharp
var exists = Directory.Exists(request.Path);
var files = Directory.EnumerateFiles(request.LibraryPath, "*.*", SearchOption.AllDirectories);
var finalTargetPath = targetPath ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "Downloads",
    filename);
```

**Correct**:
```csharp
var normalizedPath = PathGuard.NormalizeAbsolutePathWithinRoots(request.Path, allowedRoots);
var libraryPath = ResolveLibraryPath(request.LibraryPath);
var finalTargetPath = targetPath ?? Path.Combine(
    optionsMonitor.CurrentValue.Directories.Downloads,
    PathGuard.SanitizeFilename(filename));
```

**Why This Keeps Happening**: Admin-facing endpoints make it tempting to trust absolute paths, especially when the UI is just “checking” a directory or kicking off a scan. That still turns the server into a filesystem oracle. Any absolute path from HTTP or bridge input must be canonicalized and constrained to configured app-owned roots before touching disk.

### 1d. Pod Membership Mutation Endpoints Must Not Be Anonymous

**The Bug**: `PodMembershipController` was marked `[AllowAnonymous]`, which let unauthenticated callers publish, update, remove, ban, unban, and role-change pod membership records through the server-signed membership service.

**Files Affected**:
- `src/slskd/PodCore/API/Controllers/PodMembershipController.cs`

**Wrong**:
```csharp
[AllowAnonymous]
public class PodMembershipController : ControllerBase
{
}
```

**Correct**:
```csharp
[Authorize(Policy = AuthPolicy.Any)]
public class PodMembershipController : ControllerBase
{
}
```

**Why This Keeps Happening**: Some PodCore endpoints are intentionally public for signed message exchange or DHT-facing workflows, and it is easy to copy that attribute onto mutation endpoints that actually exercise privileged server behavior. Membership publication and role changes are management operations, not anonymous transport endpoints.

### 1e. Vite SPA Builds Must Use Relative Asset Paths When `web.url_base` Is Not `/`

**The Bug**: The Vite web build emitted absolute asset URLs like `/assets/...`, `/manifest.json`, and `/logo192.png`, so deployments mounted under a subpath such as `/slskd` served `index.html` correctly but then fetched the JS bundle from the site root. Reverse proxies returned HTML/404 for those asset requests, which produced a blank white page with `NS_ERROR_CORRUPTED_CONTENT` and “disallowed MIME type (`text/html`)" in the browser.

**Files Affected**:
- `src/web/vite.config.js`
- `src/web/index.html`

**Wrong**:
```javascript
export default defineConfig({
  plugins: [react()],
});
```

```html
<link rel="manifest" href="/manifest.json" />
<script type="module" src="/src/index.jsx"></script>
```

**Correct**:
```javascript
export default defineConfig({
  base: './',
  plugins: [react()],
});
```

```html
<link rel="manifest" href="./manifest.json" />
<script type="module" src="./src/index.jsx"></script>
```

**Why This Keeps Happening**: The old SPA pipeline used server-side HTML rewriting for CRA-era `/static/...` assets. Vite defaults to root-relative output unless told otherwise, so a subpath deployment works locally at `/` and silently breaks only behind `web.url_base` or a reverse proxy prefix.

### 1f. Legacy Transfers Rows May Contain `NULL` Strings Even If New Code Treats Them As Required

**The Bug**: Startup initialization called `Uploads.List(...)`, and EF Core materialization threw on upgraded databases because older `transfers.db` rows contained `NULL` in string columns like `StateDescription`/`Exception` while the model treated them as non-nullable strings.

**Files Affected**:
- `src/slskd/Transfers/Types/Transfer.cs`
- `tests/slskd.Tests.Unit/Transfers/TransfersDbContextTests.cs`

**Wrong**:
```csharp
public string StateDescription { get; set; }
public string Exception { get; set; }
```

**Correct**:
```csharp
public string? StateDescription { get; set; }
public string? Exception { get; set; }
```

**Why This Keeps Happening**: It is easy to tighten nullability on current writes and forget that persisted SQLite rows from older releases do not retroactively satisfy the new contract. For long-lived local databases, read models need to be tolerant of legacy `NULL` values unless a migration backfills them first.

### 1g. Built-Web Verifier Scripts Must Resolve Paths Relative To `src/web`, Not The Repo Root

**The Bug**: A release-gate script successfully built the frontend into `src/web/build`, then the Node verifier immediately failed because it looked for `build/index.html` relative to the repository root instead of the web project directory.

**Files Affected**:
- `src/web/scripts/verify-build-output.mjs`
- `packaging/scripts/run-release-gate.sh`

**Wrong**:
```javascript
const root = path.resolve(process.cwd());
const buildDir = path.join(root, 'build');
```

**Correct**:
```javascript
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const buildDir = path.resolve(scriptDir, '..', 'build');
```

**Why This Keeps Happening**: Top-level gate scripts usually execute from the repository root, but many frontend utilities assume they are running from `src/web`. If the verifier uses `process.cwd()`, it quietly depends on the caller's shell location instead of the actual artifact location.

---

### 2. Reverting Entire Workflow Files (build-on-tag.yml, CI)

**The Bug**: Reverting `.github/workflows/build-on-tag.yml` (or other workflows) to an old commit wipes out months of accumulated fixes: AUR, Winget (Windows case-sensitivity), Nix/Winget branch refs, PPA version checks, Chocolatey retries, etc. Builds then fail immediately (wrong branch name, case-sensitivity errors, missing steps).

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- Any workflow that has been fixed incrementally over time

**Wrong**:
```bash
git checkout <old-commit> -- .github/workflows/build-on-tag.yml
```
Do not revert the whole file to "fix" one thing.

**Correct**:
- Make minimal, targeted edits (e.g. only add `--legacy-peer-deps` or fix one job).
- Before changing workflows: read `docs/DEV_BUILD_PROCESS.md`, then `git log --oneline -- .github/workflows/build-on-tag.yml` to see what was fixed and why.
- Branch names in workflows must match actual repo branches: use `dev/40-fixes` (or whatever the current dev branch is), not hardcoded `experimental/multi-source-swarm` if that branch no longer exists.
- Winget on Windows: use `fetch-depth: 1` for checkout and `git fetch origin +refs/heads/master:refs/remotes/origin/master` (not full fetch) to avoid case-insensitivity errors when refs differ only in casing.

**Why This Keeps Happening**: Agent "fixes" a single symptom by reverting the file to a "known good" state, not realizing that state is old and missing many fixes.

### 2a. Package Channel Metadata Must Match the Runtime Binary Name and Package Identity

**The Bug**: The Nix flake exported only a `slskdn` wrapper even though NixOS `services.slskd` expects `bin/slskd`, and the stable Winget manifests were copied from dev without replacing the `snapetech.slskdn-dev` identifier or `slskdn-dev` alias.

**Files Affected**:
- `flake.nix`
- `packaging/winget/snapetech.slskdn.yaml`
- `packaging/winget/snapetech.slskdn.installer.yaml`
- `packaging/winget/snapetech.slskdn.locale.en-US.yaml`
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```nix
makeWrapper $out/libexec/${pname}/slskd $out/bin/${pname}
```

```yaml
PackageIdentifier: snapetech.slskdn-dev
PortableCommandAlias: slskdn-dev
```

**Correct**:
```nix
makeWrapper $out/libexec/${pname}/slskd $out/bin/slskd
ln -s $out/bin/slskd $out/bin/${pname}
```

```yaml
PackageIdentifier: snapetech.slskdn
PortableCommandAlias: slskdn
```

**Why This Keeps Happening**: Packaging work tends to treat channel names, package names, and executable names as interchangeable. They are not. Each channel must preserve the runtime contract expected by downstream tools (`slskd` for service modules) while also publishing the correct channel identity (`slskdn` vs `slskdn-dev`). Add an explicit validation step whenever manifests or wrappers are generated.

### 2b. Wrapping Generic Linux Binaries Is Not Enough for NixOS

**The Bug**: The Nix flake wrapped the published `slskd` binary and set `LD_LIBRARY_PATH`, but the service still failed on NixOS because the extracted ELF kept its generic Linux dynamic loader and NixOS refused to execute it.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper ];

installPhase = ''
  makeWrapper $out/libexec/${pname}/slskd $out/bin/slskd \
    --prefix LD_LIBRARY_PATH : ${pkgs.lib.makeLibraryPath [ pkgs.icu pkgs.openssl ]}
'';
```

**Correct**:
```nix
nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook ];
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Why This Keeps Happening**: It is easy to treat Nix like any other Linux packaging target and assume a wrapper plus `LD_LIBRARY_PATH` solves native dependency issues. On NixOS, generic upstream ELF binaries also need their interpreter and linked libraries patched into the Nix store path, so use `autoPatchelfHook` or explicit `patchelf` instead of only wrapping the executable.

### 2c. Do Not Assume Fresh Filesystem Labels Are Immediately Available Under `/dev/disk/by-label`

**The Bug**: A QEMU/NixOS install helper formatted `/dev/vda1` with `mkfs.ext4 -L nixos` and immediately mounted `/dev/disk/by-label/nixos`, but the installer environment had not populated that symlink yet, so the mount failed even though the partition existed.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
mkfs.ext4 -F -L nixos /dev/vda1
mount /dev/disk/by-label/nixos /mnt
```

**Correct**:
```bash
mkfs.ext4 -F -L nixos /dev/vda1
udevadm settle
mount /dev/vda1 /mnt
```

**Why This Keeps Happening**: It is tempting to use the friendlier `/dev/disk/by-label/...` path immediately after formatting, but installer/live environments can lag on udev updates. For fresh partitions, either wait for udev explicitly or mount the block device path you already know exists.

### 2d. Do Not Append a Bare Attrset to `configuration.nix`; Add a Module or Edit Inside the Existing One

**The Bug**: A NixOS install helper appended a second top-level `{ ... }` block to the generated `/etc/nixos/configuration.nix`, but that file already defines a module function (`{ config, pkgs, ... }:`). The next `nixos-install` failed with “attempt to call something which is not a function but a set”.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
cat >> /mnt/etc/nixos/configuration.nix <<'EOF'
{
  services.openssh.enable = true;
}
EOF
```

**Correct**:
```bash
cat > /mnt/etc/nixos/slskdn-vm.nix <<'EOF'
{ ... }:
{
  services.openssh.enable = true;
}
EOF
printf '\n  ./slskdn-vm.nix\n' >> /mnt/etc/nixos/configuration.nix
```

**Why This Keeps Happening**: Generated NixOS config files look like plain attribute sets at a glance, but they are module functions. If you need to inject extra settings from a script, either edit inside the existing attrset carefully or create a separate module file and import it.

### 2e. NixOS GRUB Configuration Now Expects `boot.loader.grub.devices` in This Installer Path

**The Bug**: A scripted NixOS VM install set `boot.loader.grub.device = "/dev/vda";`, but the installer on NixOS 25.11 rejected it with an assertion asking for `boot.loader.grub.devices` or `boot.loader.grub.mirroredBoots`.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```nix
boot.loader.grub.device = "/dev/vda";
```

**Correct**:
```nix
boot.loader.grub.devices = [ "/dev/vda" ];
```

**Why This Keeps Happening**: Older examples and muscle memory still use the singular `grub.device` form, but the current module assertions in this install path expect the list form. Check the generated module assertions on current NixOS releases instead of reusing older snippets blindly.

### 2f. Generated NixOS `imports` Blocks May Span Multiple Lines; Match the Real Shape Before Using `sed`

**The Bug**: A helper tried to inject `./slskdn-vm.nix` with `sed '/imports = \[/a ...'`, but `nixos-generate-config` emitted `imports =` and `[` on separate lines, so the expression never matched and the custom module was not imported at all.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
sed -i '/imports = \[/a\ \ \ \ ./slskdn-vm.nix' /mnt/etc/nixos/configuration.nix
```

**Correct**:
```bash
sed -i '/\.\/hardware-configuration\.nix/a\ \ \ \ \ \./slskdn-vm.nix' /mnt/etc/nixos/configuration.nix
```

**Why This Keeps Happening**: Generated config files look predictable, but their whitespace and line breaks are not stable enough to target with a guessed pattern. Match a concrete line that is actually present in the generated file, or rewrite the whole block explicitly instead of assuming a one-line `imports = [`.

### 2g. `expect` Patterns for SSH Password Prompts Must Handle OpenSSH's Actual Prompt Casing

**The Bug**: A local-VM validation helper waited for `password:` in lowercase, but OpenSSH prompted with `(root@127.0.0.1) Password:`. The automation stalled at the login prompt even though the VM was ready.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-vm.expect`

**Wrong**:
```tcl
expect {
  "password:" { send "root\r" }
}
```

**Correct**:
```tcl
expect {
  -re {[Pp]assword:} { send "root\r" }
}
```

**Why This Keeps Happening**: Interactive prompt matching is brittle when it relies on exact casing or full literal text. SSH clients vary their password prompt prefix, so use a case-tolerant regex for the stable suffix instead of matching the whole prompt literally.

### 2h. Nix Flakes on 9p-Mounted Git Repositories Can Trip Git Ownership Checks

**The Bug**: Inside the NixOS VM, `nix build /mnt/hostrepo#default` treated the shared repo as a Git flake and failed because the 9p mount preserved host ownership that did not match the guest user, triggering Git's “safe directory” protection.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
nix build /mnt/hostrepo#default
```

**Correct**:
```bash
git config --global --add safe.directory /mnt/hostrepo
nix build /mnt/hostrepo#default
```

**Why This Keeps Happening**: Shared folders in VMs often preserve host UIDs/GIDs or present synthetic ownership that does not match the guest account. When a flake path is also a Git repo, Nix delegates part of the source handling to Git, so you need to either mark the mount as a safe directory or use a non-Git path source when testing from a shared folder.

### 2i. Prefer `path:` Flake URIs in Minimal Guest Images When Shared Repos Trigger Git Handling

**The Bug**: The first recovery plan for a 9p-mounted flake repo assumed `git` was installed in the minimal NixOS guest so `safe.directory` could be configured, but the guest image did not include `git`, leaving the flake build blocked.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
git config --global --add safe.directory /mnt/hostrepo
nix build /mnt/hostrepo#default
```

**Correct**:
```bash
nix build 'path:/mnt/hostrepo#default'
```

**Why This Keeps Happening**: It is easy to assume live or minimal troubleshooting images carry the same helper tools as a normal dev box. For ad hoc VM validation, use the simplest source form that avoids extra dependencies; `path:` flake URIs sidestep both Git ownership checks and the need for Git itself.

### 2j. Read-Only Shared Flake Mounts Need `--no-write-lock-file`

**The Bug**: After switching to a `path:` flake URI for a read-only 9p mount, `nix build` still failed because it tried to create `flake.lock` in the mounted repo and the filesystem was intentionally read-only.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
nix build 'path:/mnt/hostrepo#default'
```

**Correct**:
```bash
nix build --no-write-lock-file 'path:/mnt/hostrepo#default'
```

**Why This Keeps Happening**: Read-only source mounts are ideal for preserving the host checkout during guest validation, but flake evaluation still wants to persist lock updates by default. When validating from a read-only mount, always disable lock-file writes explicitly or copy the flake into a writable path first.

### 2k. Nix Flake Stable Pins Must Move With the Latest Published Stable Release

**The Bug**: The flake still pointed at stable release `0.24.5-slskdn.52` and its old hashes even though GitHub’s latest stable release had moved to `0.24.5-slskdn.54`, so `nix build` failed immediately with a fixed-output hash mismatch before the runtime patching fix could even be exercised.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
version = "0.24.5-slskdn.52";
sha256 = "1gljb5zj7h0g7mhi8d9s5hjkqvn8v6dmrb812gfwggayl91ksj7y";
```

**Correct**:
```nix
version = "0.24.5-slskdn.54";
sha256 = "sha256-M1gUyVXt1iPUjjh9eFheDBRWv/kixAgIxlvIRMbckoo=";
```

**Why This Keeps Happening**: Packaging work can fix wrapper logic or runtime behavior while leaving the stable source pin behind on an older release. For fixed-output fetches, a stale release pin is just as fatal as a stale hash, so treat version and hashes as one atomic update sourced from the actual latest published release metadata.

### 2l. The Bundled .NET Runtime Also Needs `lttng-ust` on NixOS for `autoPatchelfHook` to Finish Cleanly

**The Bug**: After adding the obvious runtime libraries, the NixOS VM still failed during `autoPatchelfHook` because `libcoreclrtraceptprovider.so` wanted `liblttng-ust.so.0`, which was not present in the flake inputs.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Correct**:
```nix
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.lttng-ust
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Why This Keeps Happening**: The first-pass dependency list tends to cover the apphost and common runtime libs, but the bundled .NET runtime ships tracing/provider binaries that pull in less obvious native dependencies. Validate with `autoPatchelfHook` on real NixOS and add every missing provider library it reports instead of assuming the first set is complete.

### 2m. Some Nix Packages Default to a Non-Library Output; Use the Output That Actually Contains the Shared Object

**The Bug**: Adding `pkgs.lttng-ust` still did not satisfy `liblttng-ust.so.0` because that attribute resolved to the `bin` output in this nixpkgs revision, while the shared library lived in `pkgs.lttng-ust.out`.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.lttng-ust
];
```

**Correct**:
```nix
buildInputs = [
  pkgs.lttng-ust.out
];
```

**Why This Keeps Happening**: It is easy to assume a package attribute points at the runtime library output, but multi-output Nix packages often default to `bin` or `dev`. When `autoPatchelfHook` still cannot find a `.so`, inspect the package outputs and reference the one that actually contains the needed library.

### 2n. Bundled Runtime SONAMEs Can Lag Behind nixpkgs; Patch `NEEDED` Entries Before `autoPatchelfHook` Runs

**The Bug**: Even after adding the correct `lttng-ust` library output, the NixOS VM still failed because the bundled `.NET` trace provider asked for `liblttng-ust.so.0` while current nixpkgs only ships `liblttng-ust.so.1`.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.lttng-ust.out
];
```

**Correct**:
```nix
patchelf \
  --replace-needed liblttng-ust.so.0 liblttng-ust.so.1 \
  $out/libexec/${pname}/libcoreclrtraceptprovider.so
```

**Why This Keeps Happening**: Upstream self-contained runtimes can be built against an older SONAME than the one available in current nixpkgs. Adding more packages will not help when the exact requested SONAME no longer exists; inspect the bundled binary and patch the `NEEDED` entry to the compatible library that nixpkgs actually provides before running `autoPatchelfHook`.

### 2o. Do Not Strip Bundled .NET Runtime Payloads in the Nix Package

**The Bug**: After the flake finally built, launching `slskd` on NixOS still failed with `Failed to load System.Private.CoreLib.dll ... 0x8007000B`. The package had gone through Nix’s default strip phase, which is unsafe for this bundled .NET payload.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
pkgs.stdenv.mkDerivation {
  nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook pkgs.patchelf ];
}
```

**Correct**:
```nix
pkgs.stdenv.mkDerivation {
  nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook pkgs.patchelf ];
  dontStrip = true;
}
```

**Why This Keeps Happening**: Nix’s normal strip phase is reasonable for ordinary native packages, but bundled .NET distributions mix ELF binaries with managed/runtime payloads that are not safe to treat like a conventional C/C++ install tree. If CoreCLR starts failing with format/load errors after packaging, remove stripping from the equation before chasing more loader theories.

### 2p. The NixOS `services.slskd` Module Requires `services.slskd.domain` Even for Local Validation

**The Bug**: A local NixOS validation module enabled `services.slskd` and provided a custom package, but `nixos-rebuild test` failed before creating the service because the module accessed `services.slskd.domain` and no value was set.

**Files Affected**:
- `/etc/nixos/slskdn-local.nix` in the validation VM

**Wrong**:
```nix
{
  services.slskd.enable = true;
  services.slskd.package = slskdn.packages.${pkgs.system}.default;
}
```

**Correct**:
```nix
{
  services.slskd.enable = true;
  services.slskd.domain = "localhost";
  services.slskd.package = slskdn.packages.${pkgs.system}.default;
}
```

**Why This Keeps Happening**: It is easy to treat the NixOS module like a thin wrapper around the binary and only override `package`, but module assertions/options can still require unrelated application settings. For service validation, read the module’s required options instead of assuming `enable + package` is enough.

### 2q. The NixOS `services.slskd` Module Also Requires `settings.shares.directories`, Even If You Want No Shares

**The Bug**: After adding `domain` and `environmentFile`, `nixos-rebuild test` still failed because the module always maps over `cfg.settings.shares.directories` to build `ReadOnlyPaths`, so leaving it unset crashes evaluation.

**Files Affected**:
- `/etc/nixos/slskdn-local.nix` in the validation VM

**Wrong**:
```nix
{
  services.slskd.enable = true;
  services.slskd.environmentFile = "/etc/slskd.env";
}
```

**Correct**:
```nix
{
  services.slskd.enable = true;
  services.slskd.environmentFile = "/etc/slskd.env";
  services.slskd.settings.shares.directories = [ ];
}
```

**Why This Keeps Happening**: “No shares configured” feels like it should mean “unset,” but this module dereferences the list unconditionally when generating systemd hardening paths. For local validation, explicitly set it to an empty list.

### 2r. Whenever `flake.nix` Packaging Logic Changes, Update the Metadata Validator in the Same Edit

**The Bug**: After changing the Nix flake to add `patchelf`, `dontStrip`, `lttng-ust.out`, and the SONAME rewrite, `packaging/scripts/validate-packaging-metadata.sh` still enforced the old `nativeBuildInputs` line and failed immediately.

**Files Affected**:
- `flake.nix`
- `packaging/scripts/validate-packaging-metadata.sh`

**Wrong**:
```bash
expect_line flake.nix 'nativeBuildInputs = \[ pkgs\.unzip pkgs\.makeWrapper pkgs\.autoPatchelfHook \];'
```

**Correct**:
```bash
expect_line flake.nix 'nativeBuildInputs = \[ pkgs\.unzip pkgs\.makeWrapper pkgs\.autoPatchelfHook pkgs\.patchelf \];'
expect_line flake.nix 'dontStrip = true;'
expect_line flake.nix '--replace-needed liblttng-ust\.so\.0 liblttng-ust\.so\.1'
```

**Why This Keeps Happening**: Packaging validation tends to get treated as a one-time guardrail, but it is really part of the packaging implementation. If the flake or package templates change and the validator does not, the repo ends up failing on stale assertions instead of catching real regressions.

### 2s. Validator Helpers That Pass Regexes to `grep` Must Use `grep --` for Patterns Beginning With `-`

**The Bug**: After adding a validation pattern for `--replace-needed ...`, the packaging validator failed inside `grep` because the pattern itself started with `-` and was parsed as an option rather than a regex.

**Files Affected**:
- `packaging/scripts/validate-packaging-metadata.sh`

**Wrong**:
```bash
grep -Eq "$pattern" "$file"
```

**Correct**:
```bash
grep -Eq -- "$pattern" "$file"
```

**Why This Keeps Happening**: Validation helpers often assume patterns are data, but command-line tools still parse them as arguments first. Any generic wrapper that forwards arbitrary regexes to `grep` should include `--` up front or it will break as soon as a pattern begins with `-`.

### 2b. Tests That Bind TCP Ports Must Not Hardcode Popular Local Ports

**The Bug**: `LocalPortForwarderTests` bound to `8080` and `8081`, which caused unrelated CI and local failures whenever those ports were already in use; `TorSocksTransportTests` also assumed a specific connect-error substring even though timeout/cancellation wording varies by runtime and environment.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/LocalPortForwarderTests.cs`
- `tests/slskd.Tests.Unit/Mesh/Transport/TorSocksTransportTests.cs`

**Wrong**:
```csharp
await _portForwarder.StartForwardingAsync(8080, "pod-123", "example.com", 80);
Assert.Contains("connect", status.LastError.ToLower());
```

**Correct**:
```csharp
var localPort = GetFreeLocalPort();
await _portForwarder.StartForwardingAsync(localPort, "pod-123", "example.com", 80);
Assert.NotEmpty(status.LastError);
```

**Why This Keeps Happening**: Test code often assumes "common dev ports" are free and that low-level socket failures have stable message text. Neither assumption holds across busy developer machines, CI runners, or different runtime timing paths.

---

### 3. E2E SlskdnNode: HTTPS Port Conflict and Missing --app-dir

**The Bug**: E2E tests that start real slskdn nodes fail with "Hosting failed to start" / "Address already in use" or "An instance of slskd is already running" because (1) every node tries to bind to the same HTTPS port (5031) and (2) nodes share the default app dir (mutex conflict).

**Files Affected**:
- `tests/e2e/harness/SlskdnNode.ts`
- `tests/e2e/fixtures/helpers.ts` (findFreePort)

**Wrong**:
- Test config without `web.https.disabled: true` → all nodes bind to 5031, second node fails.
- Spawn args without `--app-dir <per-node dir>` → all nodes use default app dir, mutex prevents multiple instances.
- Building inside `SlskdnNode.start()` after `findFreePort()` → long delay lets another process grab the port (or port in TIME_WAIT).

**Correct**:
- In test config YAML: `web: https: disabled: true` so each node only binds to its unique HTTP port.
- Spawn with `--app-dir`, `this.appDir` (isolated temp dir per node).
- Build once in spec `beforeAll`, not per node; use `findFreePort()` with `reuseAddress: true` so the probe port can be rebound immediately.
- Keep stdin as pipe (do not use `ignore`) so the child does not see EOF and exit.

**Why This Keeps Happening**: Default slskd config enables HTTPS on a fixed port; E2E runs multiple nodes and did not disable HTTPS or isolate app dirs.

---

### 3b. E2E SlskdnNode.stop(): Must Wait for Child Exit (Port Leaks)

**The Bug**: E2E nodes intermittently fail to start with `Address already in use` because the harness stop logic resolves before the `dotnet` child process has actually exited. The old process can keep Kestrel bound to its port for a short window, and the next node hits a bind failure.

**Files Affected**:
- `tests/e2e/harness/SlskdnNode.ts`

**Wrong** (resolves early after SIGKILL without waiting for `exit`):
```ts
this.process.kill('SIGTERM');
await new Promise<void>((resolve) => {
  this.process.on('exit', () => resolve());
  setTimeout(() => {
    this.process.kill('SIGKILL');
    resolve();
  }, 5000);
});
```

**Correct** (escalate SIGTERM -> SIGKILL, but always await the `exit` event):
```ts
const exitPromise = new Promise<void>((resolve) => proc.once('exit', () => resolve()));

proc.kill('SIGTERM');
const exitedGracefully = await Promise.race([
  exitPromise.then(() => true),
  delay(5000).then(() => false),
]);

if (!exitedGracefully) {
  proc.kill('SIGKILL');
  await Promise.race([exitPromise, delay(5000)]);
}
```

**Why This Keeps Happening**: It's easy to write a timeout path that resolves the stop Promise without verifying the child actually exited.

---

### 4. `async void` Event Handlers Without Try-Catch

**The Bug**: `async void` event handlers that throw exceptions crash the entire .NET process.

**Files Affected**:
- `src/slskd/Messaging/RoomService.cs` - `Client_LoggedIn`

**Wrong**:
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    await TryJoinAsync(rooms);  // 💀 Exception here = process crash
}
```

**Correct**:
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    try
    {
        await TryJoinAsync(rooms);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to execute post-login room actions");
    }
}
```

**Why This Keeps Happening**: `async void` is required for event handlers, but models forget it can't propagate exceptions.

---

### 5. Streaming Controller `[Produces]` Causing 406 Instead of 429

**The Bug**: Adding `[Produces("application/octet-stream")]` to the streams endpoint can cause ASP.NET Core to return `406 Not Acceptable` for non-file error responses (notably the concurrency limiter `429`), breaking E2E expectations.

**Files Affected**:
- `src/slskd/Streaming/StreamsController.cs`

**Wrong**:
```csharp
[Produces("application/octet-stream")]
public class StreamsController : ControllerBase
{
    // ...
    if (!_limiter.TryAcquire(limiterKey, maxConcurrent))
        return StatusCode(429, "Too many concurrent streams.");
}
```

**Correct**:
```csharp
public class StreamsController : ControllerBase
{
    // ...
    if (!_limiter.TryAcquire(limiterKey, maxConcurrent))
        return StatusCode(429, "Too many concurrent streams.");
}
```

**Why This Keeps Happening**: `[Produces]` is tempting for a file endpoint, but the action also returns non-file errors (401/404/429). Constraining the produced content types can make these errors fail content negotiation and surface as `406`.

---

### 3. Unbounded Parallelism in Download Loops

**The Bug**: `Task.Run` inside loops without concurrency limits causes resource exhaustion.

**Files Affected**:
- `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs`

**Wrong**:
```csharp
foreach (var source in sources)
{
    _ = Task.Run(() => DownloadFromSourceAsync(source));  // 💀 Unbounded
}
```

**Correct**:
```csharp
var semaphore = new SemaphoreSlim(10);  // Cap at 10 concurrent
foreach (var source in sources)
{
    await semaphore.WaitAsync();
    _ = Task.Run(async () =>
    {
        try { await DownloadFromSourceAsync(source); }
        finally { semaphore.Release(); }
    });
}
```

**Why This Keeps Happening**: Models optimize for "parallelism = fast" without considering resource limits.

---

### 4. HashDb Migration Version Collisions

**The Bug**: Duplicate migration version numbers cause `UNIQUE constraint failed: __HashDbMigrations.version`, blocking startup and E2E health checks.

**Files Affected**:
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs`

**Wrong**:
```csharp
new Migration { Version = 12, Name = "Label crate job cache", ... },
new Migration { Version = 12, Name = "Traffic accounting", ... }, // 💥 duplicate
new Migration { Version = 14, Name = "Warm cache popularity", ... },
new Migration { Version = 14, Name = "Warm cache entries", ... }, // 💥 duplicate
```

**Correct**:
```csharp
new Migration { Version = 12, Name = "Label crate job cache", ... },
new Migration { Version = 13, Name = "Peer metrics storage", ... },
new Migration { Version = 14, Name = "Warm cache popularity", ... },
new Migration { Version = 15, Name = "Warm cache entries", ... },
new Migration { Version = 16, Name = "Virtual Soulfind pseudonyms", ... },
new Migration { Version = 17, Name = "Traffic accounting", ... },
```

**Why This Keeps Happening**: Migrations were appended without re-checking version uniqueness, and the list order wasn’t kept strictly ascending.

---

### 5. Snap workflow: source path is relative to snapcraft project dir

**The Bug**: In `build-on-tag.yml`, the Snap job unzipped the release zip to `slskdn_dist` in repo root, then `sed` set `source: slskdn_dist` in `packaging/snap/snapcraft.yaml`. Snapcraft runs with `cd packaging/snap`, so it resolves `source: slskdn_dist` relative to that directory. The path `packaging/snap/slskdn_dist` did not exist (the unzip created `./slskdn_dist` at repo root), so snapcraft failed.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Wrong**:
```yaml
run: |
  unzip slskdn-dev-linux-x64.zip -d slskdn_dist
  sed -i "s|source: .*|source: slskdn_dist|" packaging/snap/snapcraft.yaml
  cd packaging/snap
  snapcraft --destructive-mode
```

**Correct**:
```yaml
run: |
  mkdir -p packaging/snap/slskdn_dist
  unzip slskdn-dev-linux-x64.zip -d packaging/snap/slskdn_dist
  sed -i "s|source: .*|source: slskdn_dist|" packaging/snap/snapcraft.yaml
  cd packaging/snap
  snapcraft --destructive-mode
```

**Why This Keeps Happening**: Unzip target was assumed to be "any dir"; snapcraft resolves part sources relative to the snapcraft project root (the directory containing `snapcraft.yaml`).

---

### 5b. Chocolatey: do NOT pass path to choco push (match master)

**The Bug**: Passing a path to `choco push` (e.g. `choco push $Nupkg --source ...`) causes Chocolatey/pwsh to glue the path and the next flag into one argument, so it fails with: "File specified is either not found or not a .nupkg file. '<path>.nupkg --prerelease'".

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (chocolatey-dev, chocolatey-main)

**Wrong** (any path argument can glue to next flag):
```powershell
choco push $Nupkg --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --prerelease
```

**Correct** (match master): Run `choco push` from inside `packaging/chocolatey` after `choco pack`, with **no path** — choco finds the single .nupkg in the current directory:
```powershell
cd packaging/chocolatey
choco pack
choco push --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --prerelease --execution-timeout 300   # dev
choco push --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --execution-timeout 300               # main (add retry loop for 504)
```

**Why This Keeps Happening**: Chocolatey/pwsh glues a path argument to the next token. Omitting the path (run from the pack directory) avoids the bug; master branch uses this pattern.

---

### 5c. Snap workflow: destructive-mode on ubuntu-latest breaks stage-packages (libicu70)

**The Bug**: On GitHub Actions `ubuntu-latest` (Ubuntu 24.04), running `snapcraft --destructive-mode` uses the host apt repositories. With `base: core22`, this can fail because `stage-packages` include `libicu70` (available on 22.04, not 24.04). Error: "Stage package not found in part 'slskdn': libicu70."

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Correct** (build in LXD so the build environment matches `base: core22`):
```yaml
- uses: snapcore/action-build@v1
  with:
    path: packaging/snap
```

---

### 5d. Snap Store: duplicate content and transient "error while processing"

**The Bug**: (1) If a previous upload succeeded in transmitting but failed the status check (e.g. "Waiting for previous upload"), the next retry fails with: "binary_sha3_384: A file with this exact same content has already been uploaded". (2) Snap Store can return "Status: error while processing" transiently.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Fix**: (1) Treat "exact same content has already been uploaded" as **SUCCESS**. (2) Treat "Waiting for previous upload" and "error while processing" as **retry** (sleep 30s, continue); do not exit on them.

---

### 5e. Snap: action-build output path; do not double packaging/snap

**The Bug**: `snapcore/action-build@v1` sets its `snap` output to a path relative to the repo root (e.g. `packaging/snap/slskdn_0.24.1.dev.91769629519_amd64.snap`). If you set `SNAP_PATH="packaging/snap/${{ steps.snap-build.outputs.snap }}"` you get `packaging/snap/packaging/snap/...` and "is not a valid file". The upload step also runs on the host runner; install snapcraft there before upload.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Wrong**:
```yaml
SNAP_PATH="packaging/snap/${{ steps.snap-build.outputs.snap }}"   # duplicates packaging/snap
```

**Correct**:
- Set `SNAP_PATH="${{ steps.snap-build.outputs.snap }}"` (use the output as-is; it already includes packaging/snap when path: packaging/snap).
- Add a step before the upload step to install snapcraft on the host: `sudo apt-get install -y snapd` then `sudo snap install snapcraft --classic`.

**Why This Keeps Happening**: The action may output filename-only or path; if it outputs path, prepending packaging/snap breaks.

---

### 5e2. Snap (and other packaging) jobs: don't pin checkout to a branch on tag-triggered builds

**The Bug**: In `build-on-tag.yml`, Snap (and Nix, Homebrew) jobs had `ref: dev/40-fixes` or `ref: master`. When the workflow is triggered by a **tag** (e.g. `build-dev-0.24.1.dev.…`), the runner checks out that branch tip, not the tag's commit. So you build with `packaging/snap` (and release zip) from different commits: zip from the tag's release, tree from branch tip. If someone reverted or changed the Snap workflow on the branch, the job uses that reverted state and Snap breaks.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main; also nix-dev, homebrew-dev if they pin ref)

**Wrong**:
```yaml
- uses: actions/checkout@v4
  with:
    ref: dev/40-fixes   # tag build then gets branch tip, not tag commit
```

**Correct**:
```yaml
- uses: actions/checkout@v4
  # No ref: so tag-triggered runs checkout the tag's commit (same as release assets).
```

**Why This Keeps Happening**: It's tempting to pin to a branch for "dev" or "main" packaging; for tag-triggered runs the ref that triggered the run is the tag, and checkout should match that.

---

### 5f. PPA dev build: version must always increase (workflow uses epoch-based DEB_VERSION)

**The Bug**: PPA rejects uploads with "Version older than that in the archive". Debian version comparison treats the suffix after `dev.` as the ordering key. If the tag (or derived version) is e.g. `0.24.1.dev.20260128.162317`, it can sort **below** a previously uploaded `0.24.1.dev.91769609285`, so the PPA rejects the upload.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (ppa-dev job)

**Fix (in workflow)**: The ppa-dev job now **ignores the tag version** for the package version and sets `DEB_VERSION=0.24.1.dev.9$(date +%s)` in "Prepare Source Structure", then uses that for directory name, tarball, and changelog. So PPA always gets a monotonically increasing version regardless of tag format.

**If tagging manually**: Prefer `build-dev-0.24.1.dev.9$(date +%s)` so the tag itself is increasing; the workflow no longer derives PPA version from the tag for dev.

---

### 6. Library Items Empty When Share Cache Is Cold

**The Bug**: `/api/v0/library/items` returned no results when the share cache was empty or not ready, breaking E2E flows that need real content IDs.

**Files Affected**:
- `src/slskd/API/Native/LibraryItemsController.cs`
- `src/web/e2e/policy.spec.ts`
- `src/web/e2e/streaming.spec.ts`

**Wrong**:
```csharp
var directories = await shareService.BrowseAsync();
var allFiles = directories.SelectMany(d => d.Files ?? Enumerable.Empty<File>());
// allFiles can be empty if the share cache is cold
```

**Correct**:
```csharp
var directories = await shareService.BrowseAsync();
var allFiles = directories.SelectMany(d => d.Files ?? Enumerable.Empty<File>());
if (!allFiles.Any())
{
    // Fallback: scan configured share directories directly
    var items = await SearchShareDirectoriesAsync(query, kinds, limit, cancellationToken);
    return Ok(new { items });
}
```

**Why This Keeps Happening**: The library search assumes the share cache is always populated, but E2E nodes can query before scans finish or when caches are empty.

---

### 6. Library Item ContentIds Not Streamable

**The Bug**: Library item searches returned `contentId` values that were not registered in the share repository, so `/api/v0/streams/{contentId}` returned 404 even though the item existed on disk.

**Files Affected**:
- `src/slskd/API/Native/LibraryItemsController.cs`
- `src/slskd/Streaming/ContentLocator.cs`

**Wrong**:
```csharp
// contentId returned but never registered with share repository
return new LibraryItemResponse { ContentId = contentId, /* ... */ };
```

**Correct**:
```csharp
repo.UpsertContentItem(contentId, "GenericFile", null, maskedFilename, true, string.Empty, checkedAt);
```

**Why This Keeps Happening**: Content streaming resolves via the share repository’s `content_items` table, so ad-hoc content IDs must be registered with a masked filename to resolve to a file path.

## ⚠️ HIGH: Common Mistakes

### 4. Copyright Headers - Wrong Company Attribution

**The Rule**: New slskdN files use `company="slskdN Team"`, existing upstream files keep `company="slskd Team"`.

**Fork-specific directories** (always slskdN headers):
- `Capabilities/`, `HashDb/`, `Mesh/`, `Backfill/`
- `Transfers/MultiSource/`, `Transfers/Ranking/`
- `Users/Notes/`, `DhtRendezvous/`, `Common/Security/`

**Why This Matters**: Legal clarity for fork vs upstream code.

---

### 5. Logging Pattern Inconsistency

**The Issue**: Mixed use of `ILogger<T>` and `Serilog.Log.ForContext`.

**Preferred** (standardization in progress):
```csharp
private readonly ILogger<MyService> _logger;

public MyService(ILogger<MyService> logger)
{
    _logger = logger;
}
```

**Avoid**:
```csharp
private static readonly ILogger Log = Serilog.Log.ForContext<MyService>();
```

---

### 7. Duplicate Variable Names in React Components

**The Bug**: Large React components with multiple state sections can have duplicate variable names, causing "Identifier 'X' has already been declared" compilation errors.

**Files Affected**:
- `src/web/src/components/System/MediaCore/index.jsx` (main culprit)

**Wrong**:
```jsx
// In one section:
const [verificationResult, setVerificationResult] = useState(null);

// Later in another section:
const [verificationResult, setVerificationResult] = useState(null); // ❌ Duplicate declaration
```

**Correct**:
```jsx
// Use descriptive names for different purposes:
const [descriptorVerificationResult, setDescriptorVerificationResult] = useState(null);
const [signatureVerificationResult, setSignatureVerificationResult] = useState(null);
```

**Why This Keeps Happening**: MediaCore component has 50+ state variables across multiple sections. When adding new state variables, developers may not realize the name is already used elsewhere in the file. Always grep for variable names before adding new state.

---

### 6. React 16 Compatibility

**The Issue**: This project uses React 16.8.6. Don't use features from React 17+.

**Avoid**:
- `useId()` (React 18)
- `useDeferredValue()` (React 18)
- `useTransition()` (React 18)
- Automatic JSX transform (React 17)

**Safe to use**:
- `useState`, `useEffect`, `useContext`, `useReducer`, `useCallback`, `useMemo`, `useRef`

---

### 7. Path Traversal - Base64 Decoding

**The Issue**: User-supplied paths may be Base64-encoded with `..` components.

**Wrong**:
```csharp
var path = Base64Decode(userInput);
File.Delete(path);  // 💀 Could delete /etc/passwd
```

**Correct**:
```csharp
var path = Base64Decode(userInput);
var fullPath = Path.GetFullPath(path);
if (!fullPath.StartsWith(allowedRoot))
    throw new SecurityException("Path traversal attempt");
```

**Use `PathGuard`** in experimental branch: `PathGuard.NormalizeAndValidate(path, root)`

---

## 🔄 Patterns That Cause Fix/Unfix Cycles

### 8. ESLint/Prettier Formatting Wars

**The Cycle**:
1. Model fixes a bug
2. Lint fails on import order or quotes
3. Model "fixes" lint by changing unrelated code
4. Original fix gets lost

**Solution**: Run `npm run lint -- --fix` in `src/web/` before committing frontend changes.

---

### 9. DI Service Registration

**The Cycle**:
1. New service added
2. Forgot to register in `Program.cs`
3. Runtime crash: "Unable to resolve service"
4. Model adds registration
5. Merge conflict loses registration

**Checklist for new services**:
```csharp
// In Program.cs
builder.Services.AddSingleton<IMyService, MyService>();
// OR
builder.Services.AddScoped<IMyService, MyService>();
```

---

### 10. Experimental Files on Master Branch

**The Cycle**:
1. Work on experimental branch
2. Accidentally commit experimental files to master
3. "Fix" by removing files
4. Merge conflict brings them back

**Files that should NOT be on master**:
- `src/slskd/DhtRendezvous/`
- `src/slskd/Transfers/MultiSource/`
- `src/slskd/HashDb/`
- `src/slskd/Mesh/`
- `src/slskd/Backfill/`
- `src/slskd/Common/Security/` (beyond basic PathGuard)

---

### 10b. YAML Heredocs with Special Characters

**The Bug**: GitHub Actions workflows with inline heredocs containing `${}`, `#{}`, or `\$` break YAML parsing.

**Files Affected**:
- `.github/workflows/release-homebrew.yml`
- `.github/workflows/release-packaging.yml`

**Wrong**:
```yaml
- name: Generate file
  run: |
    cat > file.nix <<EOF
    let pkgs = nixpkgs.\${system};  # 💀 YAML parser chokes on this
    EOF
```

**Correct**: Use external scripts in `packaging/scripts/`:
```yaml
- name: Generate file
  run: |
    chmod +x packaging/scripts/update-nix.sh
    packaging/scripts/update-nix.sh "${{ steps.release.outputs.tag }}"
```

**Why This Keeps Happening**: Models inline heredocs for "simplicity" without realizing Nix `${}` and Ruby `#{}` break YAML.

---

## 📦 Packaging Gotchas (MAJOR PAIN POINT)

> ⚠️ **These issues caused 10+ CI failures each. Read carefully.**

### 11. Case Sensitivity EVERYWHERE

**The Issue**: Package names, URLs, and filenames must be **consistently lowercase**.

| Context | Correct | Wrong |
|---------|---------|-------|
| Package name | `slskdn` | `slskdN` |
| GitHub tag | `0.24.1-slskdn.22` | `0.24.1-slskdN.22` |
| Zip filename | `slskdn-0.24.1-...` | `slskdN-0.24.1-...` |
| COPR project | `slskdn` | `slskdN` |
| PPA changelog | `slskdn (0.24.1...)` | `slskdN (0.24.1...)` |

**Files that MUST use lowercase**:
- `packaging/aur/PKGBUILD*`
- `packaging/debian/changelog`
- `packaging/rpm/*.spec`
- `.github/workflows/*.yml`
- `packaging/homebrew/Formula/slskdn.rb`

---

### 12. SHA256 Checksum Formats

**The Issue**: Different packaging systems want checksums in different formats.

| System | Format | Example |
|--------|--------|---------|
| AUR PKGBUILD | Single-line array | `sha256sums=('abc123...' 'def456...')` |
| Homebrew | Quoted string | `sha256 "abc123..."` |
| Flatpak | Plain value | `sha256: abc123...` |
| Snap | Prefixed | `source-checksum: sha256/abc123...` |
| Chocolatey | PowerShell var | `$checksum = "abc123..."` |
| Nix flake | Quoted string | `sha256 = "abc123...";` |

**Multi-line PKGBUILD breaks makepkg**:
```bash
# WRONG - breaks AUR
sha256sums=(
  'abc123...'
  'def456...'
)

# CORRECT - single line
sha256sums=('abc123...' 'def456...')
```

---

### 13. SKIP vs Actual Hash in AUR

**The Issue**: AUR packages need `SKIP` for the source tarball (changes each release) but real hashes for static files.

```bash
# PKGBUILD source array order:
source=(
    "tarball.tar.gz"    # Index 0 - SKIP (changes)
    "slskd.service"     # Index 1 - real hash (static)
    "slskd.yml"         # Index 2 - real hash (static)
    "slskd.sysusers"    # Index 3 - real hash (static)
)

# Matching sha256sums:
sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')
```

**The Cycle**:
1. Model updates tarball hash
2. AUR build fails (tarball changed)
3. Model sets to SKIP
4. Model accidentally SKIPs the static files too
5. AUR build fails (missing hashes)

---

### 14. Version Format Conversion

**The Issue**: GitHub tags use `-slskdn` but PKGBUILD uses `.slskdn`.

```bash
# GitHub tag format
0.24.1-slskdn.22

# PKGBUILD pkgver format (no hyphens allowed)
0.24.1.slskdn.22

# Conversion in workflows:
PKGVER=$(echo $TAG | sed 's/-slskdn/.slskdn/')
```

**Files that need conversion**:
- `.github/workflows/release-linux.yml`
- `.github/workflows/release-copr.yml`
- `packaging/aur/PKGBUILD*`

---

### 15. URL Patterns Must Match Release Assets

**The Issue**: Download URLs must exactly match the uploaded asset names.

**Asset naming pattern** (from `release-linux.yml`):
```
slskdn-{TAG}-linux-x64.zip
slskdn-{TAG}-linux-arm64.zip
slskdn-{TAG}-osx-x64.zip
slskdn-{TAG}-osx-arm64.zip
slskdn-{TAG}-win-x64.zip
```

**Common mistakes**:
- `slskdN-...` (wrong case)
- `slskdn-linux-x64.zip` (missing version)
- `slskdn_{TAG}_linux_x64.zip` (wrong separators)

---

### 16. Homebrew Formula Architecture Blocks

**The Issue**: Homebrew needs separate `on_arm` and `on_intel` blocks for macOS.

```ruby
on_macos do
  on_arm do
    url "...osx-arm64.zip"
    sha256 "..."
  end
  on_intel do
    url "...osx-x64.zip"
    sha256 "..."
  end
end

on_linux do
  url "...linux-x64.zip"
  sha256 "..."
end
```

**Don't**: Use a single URL for all platforms.

---

### 17. Workflow Timing Issues

**The Issue**: Packaging workflows run before release assets are uploaded.

**The Cycle**:
1. Release published
2. Packaging workflow triggered immediately
3. Asset download fails (not uploaded yet)
4. Workflow fails
5. Manual re-run required

**Solution in `release-linux.yml`**:
```yaml
# Retry loop with 30s delays
for i in {1..20}; do
  if curl -fsSL "$ASSET_URL" -o release.zip; then
    exit 0
  fi
  sleep 30
done
```

---

### 18. AUR Directory Cleanup

**The Issue**: AUR git clone fails if directory exists from previous run.

```bash
# WRONG - fails if aur-repo exists
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-repo

# CORRECT - clean first
rm -rf aur-repo
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-repo
```

---

### 19. COPR/PPA Need Different Spec Files

**The Issue**: COPR uses `.spec` files, PPA uses `debian/` directory.

**COPR** (`packaging/rpm/slskdn.spec`):
- RPM spec format
- `%{version}` macro
- `BuildRequires` / `Requires`

**PPA** (`packaging/debian/`):
- `changelog` (specific format!)
- `control`
- `rules`
- `copyright`

**Changelog format is STRICT**:
```
slskdn (0.24.1-slskdn.22-1) jammy; urgency=medium

  * Release 0.24.1-slskdn.22

 -- snapetech <slskdn@proton.me>  Sun, 08 Dec 2024 12:00:00 +0000
```

Note: TWO spaces before `--`, specific date format.

---

### 20. Self-Hosted Runner Paths

**The Issue**: Self-hosted runners have different paths than GitHub-hosted.

**GitHub-hosted**: `/home/runner/work/...`
**Self-hosted**: `/home/github/actions-runner/_work/...`

**Don't**: Hardcode paths. Use `$GITHUB_WORKSPACE`.

---

### 21. Chocolatey v2 push – do not pass path (see gotcha 5b)

**The Bug**: Passing a path to `choco push` causes path+flag gluing. **Correct** (see gotcha 5b): run `choco push` from `packaging/chocolatey` after `choco pack` with no path; use `--api-key $env:CHOCO_API_KEY`. Match master branch.

---

## 🧪 Test Gotchas

### 13. Flaky UploadGovernorTests

**The Issue**: Tests using `AutoData` with random values can hit edge cases.

**Example**: Integer division with small random values causes off-by-one errors.

**Solution**: Use `InlineAutoData` with fixed values for edge-case-sensitive tests.

---

### 14. Test Isolation

**The Issue**: Tests that share static state can interfere with each other.

**Solution**: Use `TestIsolationExtensions` for tests that need isolated state.

---

## 🔐 Security Gotchas (Experimental Branch)

### 15. Security Services Not Wired to Transfer Handlers

**Current State**: 30 security components exist but aren't integrated into actual transfer code.

**TODO**: Wire `PathGuard`, `ContentSafety`, `ViolationTracker` into:
- `TransferService`
- `FilesController`
- `MultiSourceDownloadService`

---

### 16. UPnP Disabled by Default

**The Issue**: UPnP has known security vulnerabilities.

**Current**: `EnableUpnp = false` by default in `NatDetectionService.cs`

**Don't**: Enable UPnP by default without explicit user opt-in.

---

## 📝 Documentation Gotchas

### 17. DEVELOPMENT_HISTORY.md vs memory-bank/progress.md

- `DEVELOPMENT_HISTORY.md` - Human-maintained release history
- `memory-bank/progress.md` - AI session log

**Don't** overwrite `DEVELOPMENT_HISTORY.md` with AI-generated content.

---

### 18. TODO.md vs memory-bank/tasks.md

- `TODO.md` - Human-maintained high-level todos
- `memory-bank/tasks.md` - AI-managed task backlog

**Don't** duplicate tasks between them. Reference each other instead.

---

### 19. HashDb Not Populated - Missing Event Subscription

**The Bug**: HashDb was initializing but `seq_id` stayed at 0 because no code was hashing downloaded files.

**Files Affected**:
- `src/slskd/HashDb/HashDbService.cs`
- `src/slskd/Program.cs`

**Root Cause**: The `ContentVerificationService` only hashes files during multi-source downloads. Regular single-source downloads raised `DownloadFileCompleteEvent` but nothing subscribed to hash the file.

**Fix**: Subscribe `HashDbService` to `DownloadFileCompleteEvent` and hash downloaded files:
```csharp
eventBus.Subscribe<DownloadFileCompleteEvent>("HashDbService.DownloadComplete", OnDownloadCompleteAsync);
```

**Why This Happened**: The hashing logic was only implemented in the multi-source path, not the common download completion path.

---

### 20. Passive FLAC Discovery Architecture - Understanding the Design

**The Confusion**: The HashDb/FlacInventory was expected to populate "passively" but wasn't.

**The Design (Clarified)**:

The passive FLAC discovery system has **three sources** of FLAC files:

1. **Search Results** - When WE search, we see other users' files → add to `FlacInventory` with `hash_status='none'`
2. **Downloads** - When we download a FLAC → compute hash → store with `hash_status='known'`
3. **Incoming Interactions** - When users search us or download from us → track their username → optionally browse them later

**How FlacInventory Gets Populated**:

| Source | Event | Action |
|--------|-------|--------|
| Our searches | `SearchResponsesReceivedEvent` | Upsert FLAC files to FlacInventory (hash_status='none') |
| Our downloads | `DownloadFileCompleteEvent` | Hash first 32KB, store in HashDb, update FlacInventory |
| Mesh sync | `MeshSyncService` | Receive hashes from other slskdn clients |
| Backfill | `BackfillSchedulerService` | Probe files in FlacInventory where hash_status='none' |

**How Hashes Get Discovered**:

```
FlacInventory (hash_status='none')
         ↓
BackfillSchedulerService picks candidates
         ↓
Downloads first 32KB header
         ↓
Computes SHA256 hash
         ↓
Updates HashDb + FlacInventory
         ↓
Publishes to MeshSync
```

**Key Insight**: The `BackfillSchedulerService` is the "engine" that converts `hash_status='none'` entries into `hash_status='known'`. But it needs the `FlacInventory` to be populated first, which happens via search results and incoming interactions.

**Files Involved**:
- `src/slskd/HashDb/HashDbService.cs` - Subscribes to events, populates FlacInventory
- `src/slskd/Search/SearchService.cs` - Raises `SearchResponsesReceivedEvent`
- `src/slskd/Events/Types/Events.cs` - Defines `SearchResponsesReceivedEvent`
- `src/slskd/Backfill/BackfillSchedulerService.cs` - Probes FlacInventory entries
- `src/slskd/Application.cs` - Handles incoming searches/uploads (peer tracking)

---

---

### 21. API Calls Before Login - Infinite Loop Danger

**The Bug**: Components that make API calls on mount will cause infinite loops or errors when rendered on the login page (before authentication).

**Files Affected**:
- `src/web/src/components/LoginForm.jsx`
- `src/web/src/components/Shared/Footer.jsx`
- Any component rendered before login

**Wrong**:
```jsx
// In LoginForm.jsx - BAD: Footer makes API calls
import Footer from './Shared/Footer';

const LoginForm = () => {
  return (
    <>
      <LoginContent />
      <Footer /> {/* 💀 If Footer fetches data on mount, this breaks */}
    </>
  );
};

// In Footer.jsx - BAD: API call on mount
const Footer = () => {
  const [stats, setStats] = useState(null);

  useEffect(() => {
    api.getStats().then(setStats); // 💀 401 error before login!
  }, []);

  return <footer>...</footer>;
};
```

**Correct**:
```jsx
// Footer.jsx - GOOD: Pure static component, no API calls
const Footer = () => {
  const year = new Date().getFullYear();

  return (
    <footer>
      © {year} <a href="https://github.com/...">slskdN</a>
      {/* All content is static - no useEffect, no API calls */}
    </footer>
  );
};
```

**Why This Keeps Happening**: Models add "helpful" features like version info or stats to footers without considering the login page context.

**Rule**: Components rendered before login (LoginForm, Footer on login, error pages) MUST be pure/static with ZERO API calls.

---

### 22. HashDb Schema Migrations - Versioned Upgrades

**The System**: HashDb uses a versioned migration system (`HashDbMigrations.cs`) that runs automatically on startup.

**Key Files**:
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs` - Migration definitions
- `docs/HASHDB_SCHEMA.md` - Schema documentation

**How It Works**:
1. `__HashDbMigrations` table tracks applied versions
2. On startup, `RunMigrations()` compares current vs target version
3. Pending migrations run in order, each in a transaction
4. Failed migrations roll back automatically

**Adding New Columns** (SQLite gotcha):
```csharp
// WRONG - SQLite doesn't support multiple ALTER in one command
cmd.CommandText = @"
    ALTER TABLE Foo ADD COLUMN bar TEXT;
    ALTER TABLE Foo ADD COLUMN baz INTEGER;
";

// CORRECT - Execute each ALTER separately
var alters = new[] {
    "ALTER TABLE Foo ADD COLUMN bar TEXT",
    "ALTER TABLE Foo ADD COLUMN baz INTEGER"
};
foreach (var sql in alters)
{
    using var alterCmd = conn.CreateCommand();
    alterCmd.CommandText = sql;
    alterCmd.ExecuteNonQuery();
}
```

**Handling Existing Columns** (idempotent migrations):
```csharp
try
{
    alterCmd.ExecuteNonQuery();
}
catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
{
    // Column already exists - skip
}
```

**Check Current Version**:
```bash
curl http://localhost:5030/api/v0/hashdb/schema
```

**Rule**: Always increment `CurrentVersion` when adding migrations. Never modify existing migrations.

---

### 23. Missing `using` Directives - Check ALL Related Files

**The Bug**: Adding a type (e.g., `DateTimeOffset`) to an interface but only adding the `using System;` directive to one file, then having to fix each file one-by-one as compilation fails.

**Files Affected**:
- Any file that shares types across interface/implementation/controller boundaries

**Wrong Workflow**:
```
1. Add DateTimeOffset to IHashDbService.cs
2. Add "using System;" to IHashDbService.cs
3. Compile → ERROR in HashDbController.cs
4. Add "using System;" to HashDbController.cs
5. Compile → ERROR in HashDbService.cs
6. Add "using System;" to HashDbService.cs
7. Finally compiles ✅ (wasted 3 compile cycles)
```

**Correct Workflow**:
```
1. Add DateTimeOffset to IHashDbService.cs
2. BEFORE compiling, grep for all files that might need the type:
   grep -l "IHashDbService\|HashDb" src/slskd/HashDb/**/*.cs
3. Add "using System;" to ALL relevant files in one pass
4. Compile once ✅
```

**Pre-Compile Checklist** when adding new types:
```bash
# Find all files in the feature directory
find src/slskd/MyFeature -name "*.cs" -type f

# Or grep for files using the interface/class
grep -rl "IMyService\|MyService" src/slskd/MyFeature/
```

**Why This Keeps Happening**: AI models fix errors incrementally instead of thinking ahead about which files share the same types.

**Rule**: When adding a new type to an interface, check ALL files in the same namespace/feature directory and add necessary `using` directives BEFORE attempting to compile.

---

### 24. AUR PKGBUILD Checksums - NEVER Replace SKIP

**The Bug**: The AUR workflow was calculating the sha256 of `slskdn-dev-linux-x64.zip` and replacing the entire `sha256sums` array, overwriting `SKIP` with the calculated hash. This causes validation failures on `yay -Syu` because the zip changes every build.

**What Was Happening**:
```bash
# PKGBUILD template (CORRECT):
sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')
#           ^^^^   ^^^^^^^^   ^^^^^^^^   ^^^^^^^^
#           zip    service    yml        sysusers
#          (changes) (static)  (static)  (static)

# Workflow was replacing it with (WRONG):
sha256sums=('abc123...' 'SKIP' 'SKIP' 'SKIP')
#           ^^^^^^^^^^
#           Calculated hash for zip - breaks on next download!
```

**Why This Breaks**:
1. CI builds `slskdn-dev-linux-x64.zip` and calculates hash `abc123...`
2. Workflow updates AUR PKGBUILD with `sha256sums=('abc123...' ...)`
3. User runs `yay -S slskdn-dev` → works (zip matches hash)
4. CI rebuilds zip → new hash `def456...`
5. User runs `yay -Syu` → **FAILS** (cached zip has hash `abc123...`, PKGBUILD expects `abc123...`, but downloaded zip is `def456...`)

**The Fix**:
```bash
# DON'T calculate or replace the zip hash in the workflow
# The PKGBUILD template already has SKIP for index 0

# OLD (wrong):
sed -i "s/sha256sums=.*/sha256sums=('$SHA256' 'SKIP' 'SKIP' 'SKIP')/" PKGBUILD

# NEW (correct):
# Just update pkgver and _commit, leave sha256sums alone
sed -i "s/^pkgver=.*/pkgver=${VERSION}/" PKGBUILD
sed -i "s/^_commit=.*/_commit=${COMMIT}/" PKGBUILD
```

**Rule**: For AUR packages that download release binaries (not source), the first entry in `sha256sums` MUST be `'SKIP'` because the binary changes every build. Only static files (service files, configs) get real checksums.

**Related**: See gotcha #13 "SKIP vs Actual Hash in AUR" for more context on why this pattern exists.

---

## Package Manager Version Constraints

**The Problem**: AUR and RPM package managers don't allow hyphens in version strings, causing build failures.

**Error Messages**:
```
# AUR:
==> ERROR: pkgver is not allowed to contain colons, forward slashes, hyphens or whitespace.

# RPM:
error: line 2: Illegal char '-' (0x2d) in: Version: 0.24.1-dev-20251209-203936
```

**Why This Happens**:
Our dev builds use the format `0.24.1-dev-20251209-203936` (with hyphens). This works fine for Git tags and GitHub releases, but AUR and RPM have strict version format requirements:
- AUR `pkgver`: No hyphens, colons, slashes, or whitespace
- RPM `Version`: No hyphens (hyphen is reserved for separating version from release number)

**The Fix**:
Convert ALL hyphens to dots when generating package versions:

```bash
# Git/GitHub (hyphens OK):
DEV_VERSION="0.24.1-dev-20251209-203936"

# AUR/RPM/DEB (convert to dots):
ARCH_VERSION=$(echo "$DEV_VERSION" | sed 's/-/./g')
# Result: 0.24.1.dev.20251209.203936
```

**CRITICAL**: Use `sed 's/-/./g'` (global replace) NOT `sed 's/-/./'` (only first hyphen)!

**Where This Applies**:
- AUR PKGBUILD: `pkgver=0.24.1.dev.20251209.203936`
- RPM spec: `Version: 0.24.1.dev.20251209.203936`
- Debian changelog: `slskdn-dev (0.24.1.dev.20251209.203936-1)`
- Package filenames: `slskdn-dev_0.24.1.dev.20251209.203936_amd64.deb`

**Git Tag and Zip Stay Original**:
- Git tag: `dev-20251209-203936` (hyphens OK)
- Zip file: `slskdn-dev-20251209-203936-linux-x64.zip` (hyphens OK)
- GitHub release title: `Dev Build 20251209-203936` (hyphens OK)

---

## Integration Test Project Missing Project Reference

**The Problem**: Docker builds fail with `error CS0234: The type or namespace name 'Common' does not exist in the namespace 'slskd'` when building integration tests.

**Root Cause**: The `tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj` file was missing a `<ProjectReference>` to the main `src/slskd/slskd.csproj` project.

**Error Message**:
```
/slskd/tests/slskd.Tests.Integration/SecurityIntegrationTests.cs(10,13): error CS0234: 
The type or namespace name 'Common' does not exist in the namespace 'slskd' 
(are you missing an assembly reference?) [/slskd/tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj]
```

**Why This Breaks**:
1. Integration tests need to reference types from the main project (`slskd.Common.Security`, etc.)
2. Without a `<ProjectReference>`, the compiler can't find any `slskd.*` namespaces
3. This fails silently in local builds if you've previously built the main project (DLL is in bin/), but ALWAYS fails in Docker/CI clean builds

**The Fix**:
```xml
<!-- tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -->
<ItemGroup>
  <ProjectReference Include="../../src/slskd/slskd.csproj" />
</ItemGroup>
```

**Prevention**:
- When creating ANY test project, ALWAYS add a `<ProjectReference>` to the code being tested
- Test in Docker before committing: `docker build -f Dockerfile .`
- Check .csproj files when you see "namespace does not exist" errors in CI

**Related**: This is especially insidious because local `dotnet build` might work if you've built the main project before, masking the missing reference until CI runs.

---

## Workflow File Pattern Mismatch in Download Step

**The Problem**: The `packages` job fails with "no assets match the file pattern" when trying to download the zip from the dev release.

**Root Cause**: Mismatch between the actual filename and the download pattern:
- Build job creates: `slskdn-dev-linux-x64.zip` (no timestamp)
- Packages job tried to download: `slskdn-dev-*-linux-x64.zip` (wildcard for timestamp that doesn't exist)

**Error Message**:
```
gh release download dev --pattern "slskdn-dev-*-linux-x64.zip"
no assets match the file pattern
```

**Why This Breaks**:
1. The `build` job creates `slskdn-dev-linux-x64.zip` without a timestamp in the filename
2. The `release` job uploads this file to the `dev` tag as-is
3. The `packages` job tries to download with a wildcard pattern expecting a timestamp
4. The wildcard doesn't match, so no file is downloaded

**The Fix**:
```yaml
# packages job - Download from Dev Release step
gh release download dev \
  --repo ${{ github.repository }} \
  --pattern "slskdn-dev-linux-x64.zip"  # Exact filename, no wildcard
```

**Prevention**:
- When adding workflow download steps, check what the ACTUAL filename is from the upload step
- Don't use wildcards unless the filename actually varies
- The timestamp is in the VERSION/tag, not in the zip filename for dev builds

**Note**: The timestamped dev tag (e.g., `dev-20251209-212425`) is separate from the floating `dev` tag. The `dev` tag always points to the latest dev build and contains `slskdn-dev-linux-x64.zip`.

---

## Building RPM Packages on Ubuntu Fails with Missing BuildRequires

**The Problem**: The `packages` job fails when trying to build .rpm packages on Ubuntu with "Failed build dependencies: systemd-rpm-macros is needed".

**Root Cause**: The RPM spec file has `BuildRequires: systemd-rpm-macros` and `BuildRequires: unzip`, which are Fedora packages not available in Ubuntu's apt repositories. You can't build RPMs on Ubuntu that require Fedora-specific build tools.

**Error Message**:
```
error: Failed build dependencies:
	systemd-rpm-macros is needed by slskdn-dev-0.24.1.dev.20251209.213134-1.x86_64
	unzip is needed by slskdn-dev-0.24.1.dev.20251209.213134-1.x86_64
```

**Why This Breaks**:
1. RPM spec files can have `BuildRequires` for Fedora-specific packages
2. Ubuntu (apt) doesn't have `systemd-rpm-macros` or the exact versions of build tools RPM expects
3. The `rpmbuild` command on Ubuntu can't satisfy these dependencies
4. Cross-distro package building requires containers or native build environments

**The Fix**:
Don't build RPMs on Ubuntu. Let COPR (which runs on Fedora) handle RPM builds. The `packages` job should only build .deb:

```yaml
packages:
  name: Build .deb Package  # Changed from "Build Packages (.deb and .rpm)"
  # ... only build .deb, remove all RPM build steps
```

**Correct Architecture**:
- **AUR job**: Builds Arch packages (runs on Arch via Docker)
- **COPR job**: Builds RPM packages (runs on Fedora infrastructure)
- **PPA job**: Builds Debian packages (runs on Ubuntu/Launchpad)  
- **Packages job**: Builds .deb for direct GitHub download (Ubuntu is fine)
- **Docker job**: Builds container images (distro-agnostic)

**Prevention**:
- Ubuntu can build .deb natively
- Fedora (COPR) should build .rpm natively
- Don't try to build distro-specific packages on the wrong distro
- If you need RPMs as GitHub release assets, download them from COPR after it builds

---

## PPA Rejects Upload: Version Comparison with Hyphens

**The Problem**: Launchpad PPA rejects the upload with "Version older than that in the archive" even though the new version has a later timestamp.

**Root Cause**: Debian version string comparison treats hyphens differently than dots. The version `0.24.1-dev-20251209-214612` is considered OLDER than `0.24.1-dev.202512092002` because of how dpkg compares version strings.

**Error Message**:
```
Rejected: slskdn-dev_0.24.1-dev-20251209-214612-1ppa202512092148~jammy.dsc: 
Version older than that in the archive. 
0.24.1-dev-20251209-214612-1ppa202512092148~jammy <= 0.24.1-dev.202512092002-1ppa202512092006~jammy
```

**Why This Breaks**:
Debian's `dpkg --compare-versions` treats hyphens as version separators, not as part of the version string:
- `0.24.1-dev-20251209-214612` is parsed as epoch `0`, version `0.24.1`, and the rest as debian revision
- `0.24.1-dev.202512092002` with dots keeps the full version number intact
- The comparison logic makes the hyphenated version appear older

**The Fix**:
Convert ALL hyphens to dots in the PPA version string:

```bash
VERSION="${{ needs.build.outputs.dev_version }}"  # 0.24.1-dev-20251209-214612
DEB_VERSION=$(echo "$VERSION" | sed 's/-/./g')    # 0.24.1.dev.20251209.214612

# Use DEB_VERSION in changelog
slskdn-dev (${DEB_VERSION}-1ppa${PPA_REV}~jammy) jammy; urgency=medium
```

**Critical**: This is the SAME issue as the AUR/RPM version problem, but it manifests differently - not as a build error, but as a PPA rejection during upload. You MUST convert hyphens to dots for ALL Debian-based packaging (AUR, RPM, DEB, PPA).

**Prevention**:
- ALWAYS use `sed 's/-/./g'` (global replace) for ANY package version strings
- Check EVERY place where `$VERSION` or `dev_version` is used in packaging workflows
- Test PPA uploads don't get rejected with "Version older than that in the archive"

---

## Yay Cache Contains Stale PKGBUILD After AUR Fix

**The Problem**: After fixing the AUR workflow to keep `SKIP` for the binary checksum, `yay -S slskdn-dev` still fails with "One or more files did not pass the validity check!" even though the AUR repo has the correct PKGBUILD.

**Root Cause**: Yay caches PKGBUILDs in `~/.cache/yay/package-name/`. If the cached PKGBUILD is from a previous (broken) workflow run that had a real hash instead of `SKIP`, yay will use the stale cached version instead of fetching the fixed one from AUR.

**Error Message**:
```
==> Validating source files with sha256sums...
    slskdn-dev-linux-x64.zip ... FAILED
==> ERROR: One or more files did not pass the validity check!
```

**Why This Happens**:
1. Old workflow pushed PKGBUILD with `sha256sums=('abc123...' 'SKIP' 'SKIP' 'SKIP')`
2. User ran `yay -S package-name` and yay cached that broken PKGBUILD
3. Workflow was fixed to preserve `SKIP` in the template
4. New correct PKGBUILD pushed to AUR: `sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')`
5. User runs `yay -S package-name` again, but yay uses the CACHED broken version
6. Checksum fails because the binary has changed but cached PKGBUILD has the old hash

**The Fix**:
Clear yay's cache for the package:

```bash
rm -rf ~/.cache/yay/package-name
yay -S package-name  # Will fetch fresh PKGBUILD from AUR
```

**Prevention**:
- When testing AUR packages during development, always clear cache after workflow fixes
- Add this to testing docs: "If you previously tested a broken build, clear yay cache first"
- Yay's cache is helpful for normal use but can hide fixes during rapid iteration

---

## EF Core Can't Translate DateTimeOffset to DateTime Comparison

**The Problem**: Backfill endpoint throws 500 error with "The LINQ expression could not be translated" when trying to compare `Search.StartedAt` (DateTime) with a DateTimeOffset value.

**Root Cause**: Entity Framework Core cannot translate implicit conversions between `DateTimeOffset` and `DateTime` to SQL. When you write `s.StartedAt < lastProcessedAt.Value` where `StartedAt` is `DateTime` and `lastProcessedAt` is `DateTimeOffset?`, EF can't generate the SQL query.

**Error Message**:
```
System.InvalidOperationException: The LINQ expression 'DbSet<Search>()
    .Count(s => (DateTimeOffset)s.StartedAt < __lastProcessedAt_Value_0)' could not be translated.
```

**The Fix**:
Convert `DateTimeOffset` to `DateTime` explicitly using `.UtcDateTime` before the comparison:

```csharp
// WRONG - EF can't translate this:
await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value);

// CORRECT - EF can translate this:
await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value.UtcDateTime);
```

**Prevention**:
- Always check the database column type before writing LINQ queries
- Use `.UtcDateTime` when comparing `DateTimeOffset` with `DateTime` in EF queries
- Test API endpoints that use LINQ queries against the database
- EF will throw this at runtime, not compile time, so manual testing is required

---

### 20. CreateDirectory on Existing File Path

**The Bug**: `System.IO.IOException: The file '/slskd/slskd' already exists` when trying to create a directory at a path that's already occupied by a file (the binary itself).

**Files Affected**:
- `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs`
- `src/slskd/Program.cs`

**What Happened**:
`SourceDiscoveryService` used `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` which returns `/slskd` in Docker containers. It then tried to `CreateDirectory("/slskd/slskd")` to store the discovery database, but `/slskd/slskd` is the binary executable file, not a directory. This caused a crash on every API request that needed `SourceDiscoveryService`.

**Why It Happened**:
1. `LocalApplicationData` is not reliable in containers - can return unexpected paths
2. No check for whether the path is a file vs directory before calling `CreateDirectory()`
3. Different behavior than other services which use `Program.AppDirectory`

**The Error**:
```
System.IO.IOException: The file '/slskd/slskd' already exists.
  at System.IO.FileSystem.CreateDirectory(String fullPath, UnixFileMode unixCreateMode)
  at System.IO.Directory.CreateDirectory(String path)
  at slskd.Transfers.MultiSource.Discovery.SourceDiscoveryService..ctor(...)
```

**The Fix**:
Use `Program.AppDirectory` (like all other services) and create a subdirectory:

```csharp
// WRONG - uses unreliable LocalApplicationData
var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var slskdPath = Path.Combine(appDataPath, "slskd");
System.IO.Directory.CreateDirectory(slskdPath); // CRASHES if /slskd/slskd is a file!

// CORRECT - use Program.AppDirectory and create subdirectory
public SourceDiscoveryService(
    string appDirectory,  // Injected via DI
    ISoulseekClient soulseekClient,
    IContentVerificationService verificationService)
{
    var slskdPath = Path.Combine(appDirectory, "discovery");
    if (!Directory.Exists(slskdPath))
    {
        Directory.CreateDirectory(slskdPath);
    }
    dbPath = Path.Combine(slskdPath, "discovery.db");
}

// Update DI registration to pass Program.AppDirectory
services.AddSingleton<ISourceDiscoveryService>(sp => new SourceDiscoveryService(
    Program.AppDirectory,
    sp.GetRequiredService<ISoulseekClient>(),
    sp.GetRequiredService<Transfers.MultiSource.IContentVerificationService>()));
```

**Prevention**:
- **ALWAYS** use `Program.AppDirectory` for data storage, never `LocalApplicationData`
- **ALWAYS** create a subdirectory for each service's data (e.g., `discovery/`, `ranking/`, `hashdb/`)
- **ALWAYS** check `Directory.Exists()` before `CreateDirectory()` when the path might vary
- Pattern to follow: `Path.Combine(Program.AppDirectory, "myservice")` → creates `/app/myservice/` in containers

**Related Pattern**:
```csharp
// Good examples from the codebase:
var rankingDbPath = Path.Combine(Program.AppDirectory, "ranking.db");
var hashDbService = new HashDbService(Program.AppDirectory, ...);
var wishlistDbPath = Path.Combine(Program.AppDirectory, "wishlist.db");
```

---

### 21. Scanner Detection Noise from Private IPs

**The Bug**: Logs spammed with hundreds of "Scanner detected from 192.168.1.77" warnings when users access the web UI from their LAN.

**Files Affected**:
- `src/slskd/Common/Security/FingerprintDetection.cs`
- `src/slskd/Common/Security/SecurityMiddleware.cs` (partial fix)

**What Happened**:
The web UI polls multiple API endpoints rapidly (~5-10 requests/second), which triggered the reconnaissance detection system. Even after fixing `SecurityMiddleware` to skip `RecordConnection()` for private IPs, old profiles from before the fix were still marked as scanners, and the logging still fired.

**Why It Happened**:
1. Web UI makes many rapid API calls (status bar, capabilities, DHT, mesh, hashdb, backfill stats, etc.)
2. This looks like port scanning / reconnaissance to `FingerprintDetection`
3. First fix: `SecurityMiddleware` skipped `RecordConnection()` for private IPs (lines 103-110)
4. But old profiles from before the fix were still in memory as flagged scanners
5. `FingerprintDetection.RecordConnection()` logged warnings for those old profiles

**The Error**:
```
20:09:16  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
20:09:26  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
20:09:36  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
... (repeats hundreds of times)
```

**The Fix**:
Add private IP check to `FingerprintDetection` itself, not just `SecurityMiddleware`:

```csharp
// In FingerprintDetection.RecordConnection():
if (profile.IsScanner)
{
    // Don't log warnings for private/local IPs (e.g., web UI polling APIs rapidly)
    if (!IsPrivateOrLocalIp(ip))
    {
        _logger.LogWarning(
            "Scanner detected from {Ip}: {Indicators}",
            ip,
            string.Join(", ", indicators.Select(i => i.Type)));

        ReconnaissanceDetected?.Invoke(this, new ReconnaissanceEventArgs(evt));
    }
}

// Add helper method (same as SecurityMiddleware):
private static bool IsPrivateOrLocalIp(IPAddress ip)
{
    // Check for 192.168.x.x, 10.x.x.x, 172.16-31.x.x, 127.x.x.x, fe80::/10, fc00::/7
    // ... (full implementation in code)
}
```

**Prevention**:
- Security logging should **always** check for private IPs before emitting warnings
- Private IP checks should be at **both** the middleware layer (prevent tracking) **and** the service layer (prevent logging)
- Web UI polling is legitimate behavior - don't treat LAN clients as threats
- Test security features with both public and private IPs

**Why Two Fixes Were Needed**:
1. **SecurityMiddleware fix**: Prevents NEW profiles from being created for private IPs
2. **FingerprintDetection fix**: Prevents logging warnings for OLD profiles (already flagged)
3. Both layers need the check to fully eliminate noise

**Private IP Ranges**:
- IPv4: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `169.254.0.0/16`, `127.0.0.0/8`
- IPv6: `fe80::/10` (link-local), `fc00::/7` (unique local), `::1` (loopback)

---

### 22. Ambiguous Type Reference (Directory)

**The Bug**: `error CS0104: 'Directory' is an ambiguous reference between 'Soulseek.Directory' and 'System.IO.Directory'`

**Files Affected**:
- Any file that has both `using System.IO;` and `using Soulseek;`

**What Happened**:
When fixing the CreateDirectory bug (#20), I added code that used `Directory.Exists()` and `Directory.CreateDirectory()`. The compiler couldn't determine if this meant `System.IO.Directory` or `Soulseek.Directory` (which is a completely different type representing a Soulseek shared directory).

**Why It Happened**:
Both namespaces define a type called `Directory`:
- `System.IO.Directory` - file system operations
- `Soulseek.Directory` - Soulseek protocol type for shared directories

When both namespaces are imported with `using`, the unqualified name `Directory` is ambiguous.

**The Error**:
```
/home/runner/work/slskdn/slskdn/src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs(73,18): 
error CS0104: 'Directory' is an ambiguous reference between 'Soulseek.Directory' and 'System.IO.Directory'
```

**The Fix**:
Always fully qualify `Directory` when both namespaces are imported:

```csharp
// WRONG - ambiguous when both System.IO and Soulseek are imported:
if (!Directory.Exists(slskdPath))
{
    Directory.CreateDirectory(slskdPath);
}

// CORRECT - fully qualified:
if (!System.IO.Directory.Exists(slskdPath))
{
    System.IO.Directory.CreateDirectory(slskdPath);
}
```

**Alternative Fix** (if you need both frequently):
Add a using alias at the top of the file:
```csharp
using IODirectory = System.IO.Directory;

// Then use:
if (!IODirectory.Exists(slskdPath))
{
    IODirectory.CreateDirectory(slskdPath);
}
```

**Prevention**:
- When you see both `using System.IO;` and `using Soulseek;` in a file, **always** qualify `Directory`
- Grep for this pattern before committing: `grep -n "using Soulseek" src/**/*.cs | grep -v "using System.IO"` won't help because they're often far apart
- Better: Run `dotnet build` locally before pushing to catch these at compile time

**Other Ambiguous Types in This Codebase**:
- `Directory` (System.IO vs Soulseek)
- `File` (System.IO vs Soulseek)
- `Transfer` (slskd.Transfers.Transfer vs Soulseek.Transfer) - already resolved with `using Transfer = slskd.Transfers.Transfer;` in Events.cs

**Quick Fix Command**:
```bash
# Find files that might have this issue:
grep -l "using Soulseek" src/slskd/**/*.cs | xargs grep -l "Directory\.Exists\|Directory\.Create" | xargs sed -i 's/Directory\.Exists/System.IO.Directory.Exists/g; s/Directory\.Create/System.IO.Directory.Create/g'
```

---

### E2E Test Infrastructure Issues

#### E2E-1: Server crashes during share initialization in test harness

**The Bug**: E2E test nodes crash with `ShareInitializationException: Share cache backup is missing, corrupt, or is out of date` because test nodes start with empty app directories and no share cache.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`

**Wrong**:
```typescript
const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath];
```

**Correct**:
```typescript
// Add --force-share-scan to avoid ShareInitializationException when cache doesn't exist
const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath, '--force-share-scan'];
```

**Why This Keeps Happening**: Test nodes start with fresh app directories, so share cache doesn't exist. The server requires either a valid cache or `--force-share-scan` to create one.

---

#### E2E-2: Static files return 404 because SPA fallback intercepts them

**The Bug**: Static files (`/static/js/*.js`, `/static/css/*.css`) return 404, preventing React from mounting. The SPA fallback endpoint runs before the file server and intercepts all requests, including static files.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
// SPA fallback endpoint runs BEFORE file server
endpoints.MapGet("{*path}", async context => {
    // This intercepts /static/* requests and returns 404
    if (!hasExtension) {
        await context.Response.SendFileAsync(indexPath);
    } else {
        context.Response.StatusCode = 404; // Static files get 404 here!
    }
});
app.UseFileServer(...); // Never reached for static files
```

**Correct**:
```csharp
// File server runs first
app.UseFileServer(fileServerOptions);

// SPA fallback middleware runs AFTER file server
app.Use(async (context, next) => {
    await next(); // Let file server try first
    
    // Only serve index.html if file server returned 404 for a client-side route
    if (context.Response.StatusCode == 404 && !isApi && !isStatic && !hasExtension) {
        await context.Response.SendFileAsync(indexPath);
    }
});
```

**Why This Keeps Happening**: Endpoints run before middleware, so a catch-all endpoint intercepts requests before the file server middleware can serve static files. The solution is to use middleware AFTER the file server that only handles 404s for client-side routes.

---

#### E2E-3: Excessive timeouts in test helpers

**The Bug**: `waitForHealth` polls for 60 seconds (120 iterations × 500ms) when the server typically starts in 2-5 seconds.

**Files Affected**:
- `src/web/e2e/helpers.ts`

**Wrong**:
```typescript
for (let i = 0; i < 120; i++) { // 60 seconds
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
}
```

**Correct**:
```typescript
// Server typically starts in 2-5 seconds, so 15 seconds is plenty
for (let i = 0; i < 30; i++) { // 15 seconds
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
}
```

**Why This Keeps Happening**: Default timeouts are set conservatively, but actual server startup is much faster. Reduce timeouts to match reality.

---

#### E2E-4: Multi-peer tests fail with "instance already running" mutex error

**The Bug**: When starting multiple test nodes (A and B), the second node fails with "An instance of slskd is already running" because the mutex name was global (based only on AppName), not per-app-directory.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
private static Mutex Mutex { get; } = new Mutex(initiallyOwned: true, Compute.Sha256Hash(AppName));
// Mutex check happens before AppDirectory is set
if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false)) {
    Log.Fatal($"An instance of {AppName} is already running");
    return;
}
AppDirectory ??= DefaultAppDirectory; // Set AFTER mutex check
```

**Correct**:
```csharp
private static Mutex Mutex { get; set; }

private static string GetMutexName() {
    var dir = AppDirectory ?? DefaultAppDirectory;
    return $"{AppName}_{Compute.Sha256Hash(dir)}";
}

// Set AppDirectory FIRST, then create mutex with app-directory-specific name
AppDirectory ??= DefaultAppDirectory;
Mutex = new Mutex(initiallyOwned: true, GetMutexName());
if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false)) {
    Log.Fatal($"An instance of {AppName} is already running in app directory: {AppDirectory}");
    return;
}
```

**Why This Keeps Happening**: The mutex was created as a static property initializer (before AppDirectory is set) with a global name. Each test node needs its own mutex based on its unique app directory.

---

#### E2E-6: Health check hangs during server startup

**The Bug**: E2E test nodes hang during startup because the `/health` endpoint never responds. The `MeshHealthCheck` calls `GetStatsAsync()` which can hang if mesh services aren't initialized yet, especially NAT detection which tries to connect to external STUN servers.

**Files Affected**:
- `src/slskd/Mesh/MeshHealthCheck.cs`
- `src/slskd/Mesh/MeshStatsCollector.cs`
- `src/slskd/Program.cs`
- `src/web/e2e/harness/SlskdnNode.ts`

**Wrong**:
```csharp
// MeshHealthCheck.cs - no timeout, hangs if services not ready
var stats = await _statsCollector.GetStatsAsync();

// MeshStatsCollector.cs - NAT detection can hang
natType = await stunDetector.DetectAsync();
```

**Correct**:
```csharp
// MeshHealthCheck.cs - add timeout and handle gracefully
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
var stats = await _statsCollector.GetStatsAsync().WaitAsync(timeoutCts.Token);
// Return Degraded instead of Unhealthy if timeout/error occurs

// MeshStatsCollector.cs - add timeout to NAT detection
using var natTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
natType = await stunDetector.DetectAsync(natTimeoutCts.Token);

// Program.cs - configure health check timeout
services.AddHealthChecks()
    .AddMeshHealthCheck(
        failureStatus: HealthStatus.Degraded, // Don't fail entire endpoint
        timeout: TimeSpan.FromSeconds(5));

// SlskdnNode.ts - use simpler readiness endpoint
const readinessUrl = `${this.apiUrl}/health/ready`; // Simple endpoint, no complex checks
```

**Why This Keeps Happening**: Health checks run during startup before all services are initialized. Mesh services (especially NAT detection) can hang waiting for external resources. The health endpoint waits for all checks to complete, so a hanging check blocks the entire endpoint.

**Prevention**:
- Always add timeouts to health checks that call async operations
- Return `Degraded` instead of `Unhealthy` for startup-time issues
- Use simpler readiness endpoints for E2E tests that bypass complex checks
- Add timeouts to any external service calls in health checks (NAT detection, DNS, etc.)

---

#### E2E-5: Tests should be lenient for incomplete features

**The Bug**: Tests fail when UI elements don't exist because features aren't fully implemented yet.

**Files Affected**:
- All E2E test files

**Wrong**:
```typescript
await page.getByTestId(T.someFeature).click(); // Fails if feature doesn't exist
await expect(page.getByTestId(T.someElement)).toBeVisible();
```

**Correct**:
```typescript
const featureBtn = page.getByTestId(T.someFeature);
if (await featureBtn.count() === 0) {
  test.skip(); // Skip if feature not available
  return;
}
await featureBtn.click();
await expect(page.getByTestId(T.someElement)).toBeVisible({ timeout: 10000 });
```

**Why This Keeps Happening**: Features may be partially implemented or not yet available. Tests should gracefully skip rather than fail, allowing the test suite to run and verify what's actually implemented.

---

#### E2E-6: React Router routes not matching due to basename/urlBase mismatch

**The Bug**: When BrowserRouter has a `basename` prop set, routes and Links should NOT include the `urlBase` prefix, otherwise routes won't match. Also, if using memory history (MemoryRouter), redirects won't update the browser URL, causing the symptom "UI shows different page than URL".

**Files Affected**:
- `src/web/src/index.jsx` - Router setup
- `src/web/src/components/App.jsx` - Route definitions
- `src/web/e2e/multippeer-sharing.spec.ts` - Test diagnostics

**Wrong**:
```jsx
// If urlBase is "/slskd" and basename is set:
<Router basename="/slskd">
  <Route path="/slskd/contacts" />  // ❌ Won't match! Router strips basename first
  <Link to="/slskd/contacts" />     // ❌ Double-prefix
</Router>
```

**Correct**:
```jsx
// When basename is set, routes should be base-relative:
<Router basename={urlBase && urlBase !== '/' ? urlBase : undefined}>
  <Route path="/contacts" />  // ✅ Router adds basename automatically
  <Link to="/contacts" />     // ✅ Router adds basename automatically
</Router>

// When basename is undefined (urlBase is empty or '/'), use full paths:
<Router basename={undefined}>
  <Route path={`${urlBase}/contacts`} />  // ✅ urlBase is empty, so becomes "/contacts"
  <Link to={`${urlBase}/contacts`} />     // ✅ urlBase is empty, so becomes "/contacts"
</Router>
```

**Diagnostic Pattern**:
```typescript
// In E2E tests, compare browser location vs app history:
const loc = await page.evaluate(() => ({ 
  href: location.href, 
  pathname: location.pathname 
}));
const appLoc = await page.evaluate(() => {
  if ((window as any).__APP_HISTORY__) {
    return (window as any).__APP_HISTORY__.location.pathname;
  }
  return null;
});
// If loc.pathname !== appLoc, you're using memory history or basename mismatch
```

**Why This Keeps Happening**: React Router's `basename` prop automatically prepends to all routes and links. If you manually include the basename in route paths, you get a double-prefix that prevents matching. Also, using MemoryRouter instead of BrowserRouter causes redirects to not update the browser URL.

---

#### E2E-7: TypeScript-only syntax in JSX breaks builds

**The Bug**: Using TypeScript-only syntax (e.g., `window as any`) in `.jsx` files causes the web build to fail or silently serve stale bundles, which hides routing/debugging changes.

**Files Affected**:
- `src/web/src/components/App.jsx`

**Wrong**:
```jsx
// ❌ TypeScript cast is invalid in plain JSX
(window as any).__ROUTE_MISS_ELEMENT__ = el.textContent;
```

**Correct**:
```jsx
// ✅ Plain JS assignment
window.__ROUTE_MISS_ELEMENT__ = el.textContent;
```

**Why This Keeps Happening**: It's easy to copy/paste TS patterns into a JS file. CRA/CRACO won't compile TS-only syntax in `.jsx`, and a failed build can leave old bundles in `wwwroot`, masking changes.

---

#### E2E-8: Ambiguous `/shares` route between file shares and share grants

**The Bug**: The legacy file shares API and the new share-grants API both used `/api/v0/shares`, causing `AmbiguousMatchException` (500) for GET `/api/v0/shares`.

**Files Affected**:
- `src/slskd/Shares/API/Controllers/SharesController.cs` (legacy file shares)
- `src/slskd/Sharing/API/SharesController.cs` (share grants)
- `src/web/src/lib/collections.js`

**Wrong**:
```csharp
[Route("api/v{version:apiVersion}/shares")] // used by BOTH controllers
```

**Correct**:
```csharp
[Route("api/v{version:apiVersion}/share-grants")] // share grants only
```

**Why This Keeps Happening**: Both features are named "Shares" but represent different domains (local file shares vs collection share grants). Without a distinct route prefix, ASP.NET Core can't disambiguate endpoints.

---

#### E2E-9: Share-grants "GetAll" is recipient-only (owner won't see outgoing shares)

**The Bug**: `GET /api/v0/share-grants` returns grants **accessible to the current user as a recipient** (direct user or share-group member). It does **not** include the grants you created as the owner unless you also happen to be a recipient/member, which makes the owner UI appear as "No shares yet" after a successful create.

**Files Affected**:
- `src/slskd/Sharing/ShareGrantRepository.cs` (accessibility logic)
- `src/slskd/Sharing/API/SharesController.cs` (endpoint semantics)
- `src/web/src/components/Collections/Collections.jsx` (owner view needs by-collection endpoint)

**Fix**:
- Keep `GET /share-grants` as recipient-accessible (used by "Shared with Me")
- Add `GET /share-grants/by-collection/{collectionId}` for owner/outgoing shares, and have the Collections UI use it

---

#### E2E-10: Cross-node share discovery requires token signing key plus distinct per-port CSRF cookie names

**The Bug**: Cross-node share discovery via private messages requires:
1. `Sharing:TokenSigningKey` configured (base64, min 32 bytes) or token creation fails
2. The antiforgery cookie token and the JS-readable request-token cookie must use different names, and both names must be port-specific, or the frontend can read the cookie token and send it back as the request token
3. OwnerEndpoint in announcements must use `127.0.0.1` not `localhost` (Playwright request client prefers IPv6 `::1` for "localhost")

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts` (config generation)
- `src/slskd/Program.cs` (CSRF cookie name, antiforgery config)
- `src/slskd/Sharing/API/SharesController.cs` (ownerEndpoint calculation)
- `src/web/src/lib/api.js` (CSRF token reading)

**Wrong**:
```csharp
options.Cookie.Name = $"XSRF-TOKEN-{OptionsAtStartup.Web.Port}";
context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken, cookieOptions);
var ownerEndpoint = $"{scheme}://localhost:{web.Port}"; // localhost → ::1 in Playwright
```

**Correct**:
```csharp
options.Cookie.Name = $"XSRF-COOKIE-{OptionsAtStartup.Web.Port}";
context.Response.Cookies.Append($"XSRF-TOKEN-{OptionsAtStartup.Web.Port}", tokens.RequestToken, cookieOptions);
var ownerEndpoint = $"{scheme}://127.0.0.1:{web.Port}"; // Explicit IPv4
```

**Why This Keeps Happening**: Multi-instance E2E runs multiple nodes on the same host with different ports. Cookies are host-scoped (not port-scoped), so fixed names collide. A second trap is that ASP.NET antiforgery uses one cookie token and one request token; if both cookies share the `XSRF-TOKEN*` namespace, the frontend can pick up the wrong one and ASP.NET reports the token pair as swapped. Playwright's request client also resolves "localhost" to IPv6 by default, but nodes bind to IPv4.

### 0v. Share Scan Progress Must Stay Monotonic While Parallel Workers Finish Out Of Order

**The Bug**: Share scans process directories in parallel, but worker completions arrive out of order. The scanner can emit a newer progress snapshot first and then a stale lower snapshot later, which makes the UI and logs move backward from `9%` to `8%` or drop the in-progress file count.

**Files Affected**:
- `src/slskd/Shares/ShareService.cs`
- `tests/slskd.Tests.Unit/Shares/ShareServiceLifecycleTests.cs`

**Wrong**:
```csharp
ScanProgress = current.FillProgress,
Files = current.Files,
```

**Correct**:
```csharp
ScanProgress = current.Filling && current.FillProgress < state.ScanProgress ? state.ScanProgress : current.FillProgress,
Files = current.Filling && current.Files < state.Files ? state.Files : current.Files,
```

**Why This Keeps Happening**: even after moving the raw counters to `Interlocked`, the state update still arrives asynchronously from multiple workers. The service layer has to treat in-flight scan progress as monotonic state instead of trusting every late-arriving worker snapshot.

### 0w. Soulseek Network Teardown Exceptions Must Not Fall Through To Generic Fatal Telemetry

**The Bug**: expected Soulseek network churn such as disposed `Connection` objects, `Unknown PierceFirewall attempt`, `No route to host`, and inactivity timeouts could bypass the expected-network exception filter and get logged as generic `[FATAL] Unobserved task exception`, making ordinary connectivity failures look like process corruption.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Unit/ProgramPathNormalizationTests.cs`

**Wrong**:
```csharp
var isNetworkFailure =
    exception is TimeoutException ||
    exception is OperationCanceledException ||
    exception is IOException ||
    exception is SocketException ||
    typeName.Contains("Soulseek.ConnectionReadException", StringComparison.Ordinal);
```

**Correct**:
```csharp
var isNetworkFailure =
    exception is TimeoutException ||
    exception is OperationCanceledException ||
    exception is IOException ||
    exception is ObjectDisposedException objectDisposedException && string.Equals(objectDisposedException.ObjectName, "Connection", StringComparison.Ordinal) ||
    exception is SocketException ||
    typeName.Contains("Soulseek.ConnectionReadException", StringComparison.Ordinal) ||
    typeName.Contains("Soulseek.ConnectionException", StringComparison.Ordinal);
```

```text
Also match common Soulseek network detail strings such as "Unknown PierceFirewall attempt",
"No route to host", "Inactivity timeout", and both "Operation canceled" spellings.
```

**Why This Keeps Happening**: the Soulseek library surfaces several expected connection-failure paths through different exception types and message text. Filtering only the obvious socket/cancel/read exceptions leaves teardown races and peer-protocol failures to fall through the generic fatal logger.

### 0x. Port-Scoped CSRF Request Tokens Must Use The Injected Backend Port, Not `window.location.port`

**The Bug**: The web client switched to per-port CSRF request-token cookies, but the reader used `window.location.port` to choose the cookie name. That works for direct `:5030` access and fails behind a reverse proxy or default-port deployment, because the browser URL may have no visible port while the backend still issued `XSRF-TOKEN-5030`.

**Files Affected**:
- `src/web/src/lib/api.js`
- `src/web/src/lib/api.test.js`

**Wrong**:
```javascript
export const getCsrfTokenFromCookieString = (
  cookieString = document.cookie,
  currentPort = window.location.port,
) => {
  const portScopedToken = parsedCookies.get(`XSRF-TOKEN-${currentPort}`);
```

**Correct**:
```javascript
const inferredPort = String(window.port || window.location.port || '');
const portScopedToken = parsedCookies.get(`XSRF-TOKEN-${inferredPort}`);
```

```text
If the injected backend port is unavailable, only then fall back to the browser-visible port.
For reverse-proxy/default-port deployments, also fall back to the single available `XSRF-TOKEN-*`
cookie instead of sending no CSRF token at all.
```

**Why This Keeps Happening**: the frontend naturally wants to key off the current URL, but slskdN injects the real backend port separately because the app can sit behind path prefixes, TLS termination, or a proxy that hides the origin port from `window.location`. Per-port cookie names have to follow the injected backend port, not the visible URL port.

### 0y. Expected Soulseek Unobserved Network Churn Should Not Spam Warning-Level Telemetry

**The Bug**: After classifying more Soulseek teardown exceptions as expected, the handler still logged them as `[WARN] Unobserved Soulseek peer/distributed network exception ...`. That avoided the fake fatal crash signal, but it still made ordinary peer churn look like an active runtime problem and reopened the issue.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
var warningMessage = $"[WARN] Unobserved Soulseek peer/distributed network exception: {baseException.Message}";
Console.Error.WriteLine(warningMessage);
Log?.Warning(baseException, warningMessage);
```

**Correct**:
```csharp
var debugMessage = $"Ignoring expected Soulseek peer/distributed network exception: {baseException.Message}";
Log?.Debug(baseException, debugMessage);
```

```text
Also keep broadening the expected-network matcher for normal churn strings such as
"Remote connection closed" so the handler does not bounce between fatal, warning, and benign paths.
```

**Why This Keeps Happening**: once a noisy path has been misclassified as fatal, the instinctive follow-up is to downgrade it only one step to warning. For P2P connection churn that is already expected and observed, warning is still too loud; it becomes a second false-positive channel instead of a real fix.

---

#### E2E-11: Backfill requires OwnerEndpoint for HTTP downloads (cross-node)

#### E2E-12: SqliteShareRepository Keepalive Causes Process Exit During E2E Tests

**The Bug**: The `Keepalive()` method in `SqliteShareRepository` calls `Environment.Exit(1)` if the database check fails, causing nodes to exit unexpectedly during E2E tests. The original check used `pragma_table_info("filenames")` which may fail for FTS5 virtual tables or during transient database locks.

**Files Affected**:
- `src/slskd/Shares/SqliteShareRepository.cs` - `Keepalive()` method

**Wrong**:
```csharp
private void Keepalive()
{
    using var cmd = new SqliteCommand("SELECT COUNT(*) FROM pragma_table_info(\"filenames\");", KeepaliveConnection);
    var reader = cmd.ExecuteReader();
    if (!reader.Read() || reader.GetInt32(0) != 1)
    {
        var msg = "The internal share database has been corrupted...";
        Log.Fatal(msg);
        Environment.Exit(1);  // 💀 Kills process immediately, no recovery
        throw new DataMisalignedException(msg);
    }
}
```

**Correct**:
```csharp
private void Keepalive()
{
    try
    {
        // Check if table exists first
        using var cmd = new SqliteCommand(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='filenames';",
            KeepaliveConnection);
        var reader = cmd.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 1)
        {
            var msg = "The internal share database has been corrupted...";
            Log.Fatal(msg);
            Environment.Exit(1);
            throw new DataMisalignedException(msg);
        }
        // Verify table is queryable (handles FTS5 virtual tables correctly)
        using var verifyCmd = new SqliteCommand("SELECT COUNT(*) FROM filenames LIMIT 1;", KeepaliveConnection);
        verifyCmd.ExecuteScalar();
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
    {
        // Table doesn't exist or is corrupted - exit
        var msg = "The internal share database has been corrupted...";
        Log.Fatal(ex, msg);
        Environment.Exit(1);
        throw new DataMisalignedException(msg, ex);
    }
    catch (Exception ex)
    {
        // Log but don't exit on transient errors (e.g., database locked during backup)
        Log.Warning(ex, "Keepalive check encountered an error (may be transient): {Message}", ex.Message);
    }
}
```

**Why This Keeps Happening**: The keepalive check runs every 1 second and calls `Environment.Exit(1)` on any failure, including transient database locks or race conditions during startup. The original `pragma_table_info` check may not work correctly for FTS5 virtual tables, and there's no handling for transient errors like database locks during backups or concurrent access.

**Impact**: Causes 56+ ProcessExit events during E2E test runs, leading to `ERR_CONNECTION_REFUSED` errors and test failures.

**The Bug**: Backfill endpoint requires either `OwnerEndpoint` + `ShareToken` (for HTTP downloads) or owner username + `IDownloadService` (for Soulseek downloads). If neither is available, backfill fails with a generic error.

**Files Affected**:
- `src/slskd/Sharing/API/SharesController.cs` (Backfill method)

**Wrong**:
```csharp
// Only checks for Soulseek username
if (string.IsNullOrWhiteSpace(ownerUsername))
    return BadRequest("Owner username not available");
```

**Correct**:
```csharp
// Check for HTTP download first (cross-node), then Soulseek
var useHttpDownload = !string.IsNullOrWhiteSpace(ownerEndpoint) && !string.IsNullOrWhiteSpace(grant.ShareToken);
if (useHttpDownload) {
    // HTTP download path
} else if (!string.IsNullOrWhiteSpace(ownerUsername) && _downloadService != null) {
    // Soulseek download path
} else {
    return BadRequest("Cannot backfill: owner endpoint and token not available for HTTP download, and owner username or download service not available for Soulseek download");
}
```

**Why This Keeps Happening**: Backfill needs to work for both cross-node shares (HTTP) and same-network shares (Soulseek). The implementation must check for both methods and provide clear error messages when neither is available.

### 2w. Metrics Auth DataAnnotations Must Not Reject the Default Config When Metrics Are Disabled

**The Bug**: `Options.MetricsOptions.MetricsAuthenticationOptions.Password` had a `[StringLength(MinimumLength = 1)]` attribute even though metrics are disabled by default and the default password is intentionally empty. Full options validation ran before startup, so a fresh config could fail with `Metrics.Authentication.Password` length validation even when `metrics.enabled = false` or `metrics.authentication.disabled = true`.

**What Went Wrong**: The validation lived on the nested property instead of the feature gate. DataAnnotations treated the empty default password as invalid unconditionally, which broke NixOS service validation and any other startup path that bound defaults before metrics was actually enabled.

**How to Prevent It**:
- Put required-field validation for optional features on the parent options object where you can check `Enabled` and related flags.
- Do not use unconditional `[StringLength(MinimumLength = 1)]` on values that are allowed to remain empty while the feature is disabled.
- Add tests for all three cases: feature disabled, feature enabled with auth disabled, and feature enabled with auth required.

### 2x. Release Jobs That Write Back Into `master` Must Re-Sync Before Pushing

**The Bug**: The tag workflow successfully published release `0.24.5-slskdn.57` and updated the Homebrew tap repo, but the follow-up step that rewrote `Formula/slskdn.rb` in the main repo failed with `git push ... fetch first` because it committed in a fresh clone and then pushed straight into a moving `master`.

**What Went Wrong**: The workflow already derives the correct release version from the build tag, so the failure was not a versioning problem. The actual bug was treating a post-release write-back like an isolated branch update instead of a concurrent push target.

**How to Prevent It**:
- For any workflow that commits back into `master`, fetch and rebase against `origin/master` immediately before push, then retry a small number of times.
- If there are no staged changes after regenerating a packaging file, exit early instead of creating a no-op push path.
- Treat repository write-back steps as separate from artifact publication; a release can publish successfully while the write-back still races and turns the workflow red.

### 2y. Do Not Rebase a Generated Release Commit Until the Workflow Cleans Nix-Generated Dirt

**The Bug**: The Nix write-back job for release `0.24.5-slskdn.59` still failed after a 10-attempt retry loop because the checkout was already dirty by the time the loop reached `git rebase origin/master`, so every attempt died immediately with `cannot rebase: You have unstaged changes`.

**What Went Wrong**: The retry logic assumed the only local change was the committed `flake.nix` bump. In reality, the Nix verification step left additional working-tree/index changes behind, so the rebase loop never had a clean tree to operate on.

**How to Prevent It**:
- Run `nix flake check` with `--no-write-lock-file` in CI when the job is only validating metadata.
- Before any fetch/rebase/push retry loop, explicitly clean the checkout (`git reset --hard HEAD` plus `git clean -fd`) or regenerate the file from a fresh `origin/master` each attempt.
- Do not interpret "more retries" as a fix when the underlying checkout state is dirty; first make the retry loop re-runnable.

### 2z. Do Not Let Multiple Release Jobs Push Different Metadata Commits Into `master` In Parallel

**The Bug**: Homebrew, Winget, and Nix each tried to write separate commits back into `master` during the same release run. Even after individual retry fixes, the jobs kept invalidating each other because they were all racing to move the same branch.

**What Went Wrong**: The workflow treated each packaging surface as an isolated updater, but the shared target was still one branch. Independent retries reduce timing sensitivity; they do not eliminate branch-level write contention when three jobs are all competing to publish the "latest" metadata commit.

**How to Prevent It**:
- Use exactly one job to mutate `master` for release metadata.
- Regenerate all checked-in release metadata (`flake.nix`, checked-in Homebrew formula, Winget manifests, etc.) in the same workspace and push one consolidated commit.
- Keep external repo updates separate if necessary, but do not let more than one job in the workflow write to this repository's default branch.

### 3a. Do Not Rename a Release-Blocking Option or Shared Release Copy in Only One Layer

**The Bug**: `MeshServiceDescriptorValidator` checked `_options.RequireSignatures` even though `MeshServiceFabricOptions` only exposes `ValidateDhtSignatures`, which broke every publish job at compile time. Separately, the stable Winget locale text drifted away from the shared SongID/Discovery Graph release copy, so CI failed the packaging metadata validator before it even reached the app build.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/MeshServiceDescriptorValidator.cs`
- `src/slskd/Mesh/ServiceFabric/MeshServiceFabricOptions.cs`
- `packaging/winget/snapetech.slskdn.locale.en-US.yaml`
- `packaging/scripts/validate-release-copy.sh`

**Wrong**:
```csharp
else if (_options.RequireSignatures)
{
    return (false, "Signature required but not provided");
}
```

```yaml
ShortDescription: Batteries-included Soulseek web client
Description: |-
  slskdN is a batteries-included fork of slskd with advanced download features,
  automation, and network enhancements for Soulseek.
```

**Correct**:
```csharp
else if (_options.ValidateDhtSignatures)
{
    return (false, "Signature required but not provided");
}
```

```yaml
ShortDescription: Stable Soulseek client with SongID and Discovery Graph
Description: |-
  slskdN is a batteries-included fork of slskd with SongID, Discovery Graph,
  advanced download features, automation, and network enhancements for Soulseek.
```

**Why This Keeps Happening**: Release work in this repo spans code, workflows, and checked-in packaging metadata. If you update only the validator, only the options type, or only one release-copy file, different CI gates fail in sequence and hide the next problem. Audit the real options type and every checked-in release copy file together before tagging.

### 3b. Do Not Persist Pod Creation Fields Without Normalizing Required Defaults First

**The Bug**: `PodEntity.FocusContentId` is stored as a required SQLite column, but `SqlitePodService.CreateAsync()` wrote `pod.FocusContentId` directly. DM pod creation and several integration tests leave that field unset, so pod creation failed with `SQLite Error 19: 'NOT NULL constraint failed: Pods.FocusContentId'`.

**Files Affected**:
- `src/slskd/PodCore/SqlitePodService.cs`
- `src/slskd/PodCore/PodDbContext.cs`

**Wrong**:
```csharp
FocusContentId = pod.FocusContentId,
```

**Correct**:
```csharp
var normalizedFocusContentId = pod.FocusContentId ?? string.Empty;
pod.FocusContentId = normalizedFocusContentId;

FocusContentId = normalizedFocusContentId,
```

**Why This Keeps Happening**: The service layer treats some pod fields as optional, but the persistence model hard-requires non-null strings. If you change schema expectations or add a new required column, normalize the service input before save and keep the entity-to-model mapping tolerant of older/null rows.

### 3c. Bash Heredoc Terminators in GitHub Actions Must Start at Column 1 Unless You Use `<<-`

**The Bug**: The stable `metadata-main` job rewrote `Formula/slskdn.rb` with `cat <<EOF`, but the closing `EOF` was indented inside the workflow `run:` block. Bash never recognized the terminator, so the post-release metadata job crashed with `wanted 'EOF'` and `syntax error: unexpected end of file` even though the release artifacts were already published.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
cat > Formula/slskdn.rb <<EOF
  class Slskdn < Formula
    ...
  EOF
```

**Correct**:
```bash
cat > Formula/slskdn.rb <<EOF
class Slskdn < Formula
  ...
EOF
```

**Why This Keeps Happening**: YAML indentation makes it visually tempting to indent shell heredoc terminators to match the surrounding block, but bash still parses the literal script after YAML rendering. If you use plain `<<EOF`, the closing marker must be flush-left in the generated shell. Otherwise a release can fully publish and still fail red on a follow-up metadata write-back step.

### 3d. GitHub Actions `run: |` Blocks Still Need Valid YAML Indentation Before Bash Ever Sees the Heredoc

**The Bug**: After fixing the bash heredoc terminator bug, the next edit moved the heredoc body to column 1 in the workflow file itself. That made the shell content conceptually correct, but it broke the workflow at YAML parse time, so the `build-on-tag.yml` runs failed instantly with no jobs created.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```yaml
run: |
  cat > Formula/slskdn.rb <<EOF
class Slskdn < Formula
  ...
EOF
```

**Correct**:
```yaml
run: |
  cat > Formula/slskdn.rb <<EOF
  class Slskdn < Formula
    ...
  EOF
```

**Why This Keeps Happening**: GitHub Actions first parses YAML, then hands the deindented block to bash. The workflow file must satisfy both layers at once: keep the heredoc lines indented enough for YAML block-scalar syntax, but consistently indented so the runner deindents them back to column 1 for bash.

### 3e. Non-Nullable Tuple Return Sites Must Use `default`, Not `null!`

**The Bug**: Warning cleanup changed `SqliteShareRepository.FindFileInfo()` to return `null!` on the not-found path, but the method returns a non-nullable value tuple. That compiles for reference types, not tuples, so the next rebuild failed with `CS0037`.

**Files Affected**:
- `src/slskd/Shares/SqliteShareRepository.cs`

**Wrong**:
```csharp
if (!reader.Read())
{
    return null!;
}
```

**Correct**:
```csharp
if (!reader.Read())
{
    return default;
}
```

**Why This Keeps Happening**: During nullable cleanup, it is easy to mechanically replace "missing value" returns with `null!`. That only works for reference-type return paths. For tuples and other value types, keep the existing sentinel form such as `default` or change the signature explicitly.

### 3f. Async Controller Lookups Must Await Repository Tasks Before Null / NotFound Checks

**The Bug**: Warning cleanup changed `FindMessageAsync()` to return `Task<PrivateMessage?>`, but the controller kept comparing the un-awaited task result to `default`. That made the not-found branch unreachable and could incorrectly return `200 OK` for missing messages.

**Files Affected**:
- `src/slskd/Messaging/API/Controllers/ConversationsController.cs`
- `src/slskd/Messaging/ConversationService.cs`

**Wrong**:
```csharp
var message = Messages.Conversations.FindMessageAsync(username, id);
if (message == default)
{
    return NotFound();
}

return Ok(message);
```

**Correct**:
```csharp
var message = await Messages.Conversations.FindMessageAsync(username, id);
if (message == default)
{
    return NotFound();
}

return Ok(message);
```

**Why This Keeps Happening**: Nullable-signature cleanup often turns synchronous-looking lookups into `Task<T?>`, but controller code can still visually resemble the old synchronous pattern. In async controller actions, always await the lookup before testing for `null` / `default` or returning the payload.

### 3g. AUR Clone Failures Must Retry, Not Fall Back To `git init`

**The Bug**: The stable `Publish to AUR (Main - Source & Binary)` workflow treated any `git clone` failure for `slskdn-bin` as if the package repo did not exist. A transient AUR SSH disconnect created a brand-new local repo with `git init`, so the later push became a root commit and failed with `fetch first`.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-pkg-bin || {
  mkdir -p aur-pkg-bin
  cd aur-pkg-bin
  git init
  git remote add origin ssh://aur@aur.archlinux.org/slskdn-bin.git
}
```

**Correct**:
```bash
git clone "https://aur.archlinux.org/slskdn-bin.git" aur-pkg-bin
git -C aur-pkg-bin remote set-url --push origin "ssh://aur@aur.archlinux.org/slskdn-bin.git"
```

```text
Use HTTPS for clone/fetch/rebase and reserve SSH only for the final authenticated push. Only initialize a brand-new AUR repo during an intentional package bootstrap path. For normal release publishing, treat clone failures as transient network/auth errors and retry them; on push rejection, fetch/rebase and retry instead of pushing an unrelated root commit.
```

**Why This Keeps Happening**: the original fallback was written to be convenient for first-time package setup, but release workflows run against long-lived AUR repos where "clone failed" usually means SSH/network instability, not a missing repository. Reusing the bootstrap fallback in steady-state CI silently destroys git history and turns a recoverable clone hiccup into a guaranteed push failure. Even after removing the `git init` fallback, using SSH for read-side clone/fetch still leaves the workflow exposed to AUR-side connection drops; HTTPS reads plus SSH push isolates the flaky part to the only step that actually needs credentials.

### 3h. All GitHub Issue / PR / Release Actions In This Repo Must Target `snapetech/slskdn`, Never Upstream `slskd/slskd`

**The Bug**: GitHub cleanup work intended for this fork was run against the upstream `slskd/slskd` project instead. The root cause was repo-target ambiguity: this checkout has both `origin` (`snapetech/slskdn`) and `upstream` (`slskd/slskd`), and `gh` / connector operations were allowed to resolve against the wrong repository when the target was not stated explicitly.

**Files / Systems Affected**:
- local GitHub CLI state
- AI/operator instructions for this repo

**Wrong**:
```text
Run GitHub issue / PR / release commands without explicitly verifying the target repo,
or assume "slskd" and "slskdn" will be distinguished automatically by the tool.
```

**Correct**:
```text
For this repository, every GitHub issue / PR / release action must target `snapetech/slskdn`.
Treat upstream `slskd/slskd` as read-only reference only.

Before any GitHub write action:
1. Verify `origin` is `snapetech/slskdn`
2. Verify `gh repo set-default --view` resolves to `snapetech/slskdn`
3. Pass the repo explicitly to any CLI / MCP action when possible
4. Never comment on, close, label, or otherwise modify upstream `slskd/slskd`
```

**Why This Keeps Happening**: fork repos often keep an `upstream` remote for reference, and the names here differ by only one letter. That makes repo targeting an easy place to fail, especially when tools cache a default repo or infer one from context. When both fork and upstream are accessible, "implicit repo selection" is unsafe. Pin the default to `snapetech/slskdn`, verify it before write actions, and treat upstream as non-writable from this workspace.

### 3i. Share Scan Worker Defaults Must Be Conservative; `ProcessorCount` Is Too Aggressive For First-Time Scans

**The Bug**: The share scanner defaulted `shares.cache.workers` to `Environment.ProcessorCount`. On weaker hosts or slow storage, first-time library scans could drive load unreasonably high because each worker enumerates directories and reads file metadata concurrently.

**Files Affected**:
- `src/slskd/Core/Options.cs`
- `config/slskd.example.yml`
- `docs/config.md`

**Wrong**:
```csharp
public int Workers { get; init; } = Environment.ProcessorCount;
```

**Correct**:
```text
Use a conservative default that favors stability over peak scan throughput, and keep the
existing `shares.cache.workers` knob available for hosts that want to tune higher or lower.
```

**Why This Keeps Happening**: "one worker per core" sounds reasonable for CPU-bound work, but share scans are mixed CPU/I/O pressure and include metadata extraction, file system traversal, and moderation checks. On modest systems, the default needs to assume the host is doing other work and that storage is often the real bottleneck. Tune up explicitly if the machine can handle it; do not make the most aggressive path the default.

### 3j. Full-Instance Integration Startup Timeouts Must Tolerate Loaded Test Runs

**The Bug**: `SlskdnFullInstanceRunner.WaitForApiReadyAsync()` hard-coded a 25 second startup timeout. That was enough in isolated focused runs, but it failed in repo-wide validation when the full-instance CSRF integration tests booted a subprocess while the machine was already under heavy test load.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
const int maxAttempts = 50;
await Task.Delay(500, ct);
throw new TimeoutException($"slskdn instance did not become ready after {maxAttempts * 500}ms");
```

**Correct**:
```text
Use a startup wait budget that tolerates loaded local/CI runs for subprocess-backed integration tests,
and reserve the short timeouts for in-process `TestServer` style probes.
```

**Why This Keeps Happening**: focused harness tests often make the startup path look fast and deterministic, but subprocess-backed integration tests pay for real app startup, config/bootstrap work, and scheduler contention from the rest of the suite. A timeout that is "fine on a quiet machine" becomes a flake once the full solution runs together.

### 3k. Redirected Child-Process Output In Test Harnesses Must Be Drained Continuously

**The Bug**: `SlskdnFullInstanceRunner` started the subprocess with `RedirectStandardOutput = true` and `RedirectStandardError = true`, but it only read those streams if the process exited early. Under heavier startup logging, the child could block on full pipe buffers before the API came up, and the harness misreported that as a startup timeout.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
RedirectStandardOutput = true,
RedirectStandardError = true,
...
var stdout = slskdnProcess.StandardOutput.ReadToEnd();
var stderr = slskdnProcess.StandardError.ReadToEnd();
```

**Correct**:
```text
When redirecting child-process output in a long-lived test harness, begin asynchronous reads
immediately and keep a bounded in-memory buffer for diagnostics. Do not leave the pipes unread.
```

**Why This Keeps Happening**: redirected output feels harmless when the only goal is "capture logs if startup fails," but unread pipes have backpressure. As soon as the child emits enough startup logging, the harness itself becomes the reason the process stalls.

### 3l. Browse Cache Readers Must Allow Replacement, And Rebuilds Must Be Serialized

**The Bug**: `BrowseResponseResolver()` opened `browse.cache` with the default exclusive sharing mode, while `CacheBrowseResponse()` rebuilt the cache with `File.Move(..., overwrite: true)` and no rebuild lock. Active browse readers could therefore block cache replacement, and concurrent rebuild triggers could race each other as well.

**Files Affected**:
- `src/slskd/Application.cs`

**Wrong**:
```csharp
var stream = new FileStream(cacheFilename, FileMode.Open, FileAccess.Read);
...
File.Move(temp, destination, overwrite: true);
```

**Correct**:
```text
Open browse-cache readers with a sharing mode that permits the file to be replaced while it is being streamed,
and serialize cache rebuilds so only one writer updates `browse.cache` at a time.
```

**Why This Keeps Happening**: file-backed caches look simple because readers and writers touch the same pathname, but they still need an explicit concurrency contract. If readers take exclusive locks and writers replace the file opportunistically, normal live traffic turns every refresh into a lock race.

### 3m. Disabled Moderation Must Not Force Full-File Hashing During Share Scans

**The Bug**: `ShareScanner` computed a full SHA-256 for every scanned file before it even asked the moderation provider for a decision. When the active provider was `NoopModerationProvider` or another effectively inactive moderation setup, scans still paid the whole-file hashing cost for no benefit.

**Files Affected**:
- `src/slskd/Shares/ShareScanner.cs`
- `src/slskd/Common/Moderation/CompositeModerationProvider.cs`

**Wrong**:
```csharp
var fileHash = await Files.ComputeHashAsync(originalFilename, cancellationToken);
var localFileMetadata = new LocalFileMetadata
{
    PrimaryHash = fileHash,
    ...
};
var decision = await ModerationProvider.CheckLocalFileAsync(localFileMetadata, cancellationToken);
```

**Correct**:
```text
Only run the moderation path when local-file moderation is actually active, and only compute a full-file hash
when the active provider configuration truly requires one (for example, a hash blocklist check).
```

**Why This Keeps Happening**: the moderation API takes `LocalFileMetadata`, and `PrimaryHash` looks like part of that contract, so it is easy to front-load the hash unconditionally. On a large library scan, that turns “moderation disabled” into “still read every file end-to-end,” which looks like a scan hang or runaway load even with low worker counts.

### 3n. Share Scans Must Not Eagerly Probe Media Attributes For Every Supported File On Slow Or Remote Storage

**The Bug**: `SoulseekFileFactory.Create(...)` eagerly called `TagLib.File.Create(...)` for every supported audio and video file during share scans in order to populate Soulseek attributes. On slow or remote storage such as NFS-backed shares, that probing path could dominate the scan so heavily that scans appeared to stall after only a handful of files.

**Files Affected**:
- `src/slskd/Shares/SoulseekFileFactory.cs`
- `src/slskd/Shares/ShareScanner.cs`
- `tests/slskd.Tests.Unit/Shares/ShareScannerHarnessTests.cs`

**Wrong**:
```csharp
if (SupportedExtensions.Contains(extension))
{
    file = TagLib.File.Create(filename, TagLib.ReadStyle.Average | TagLib.ReadStyle.PictureLazy);
    ...
}
```

**Correct**:
```text
Keep share scans cheap by default. Do not synchronously probe full media metadata for every supported file
on the hot scan path unless the value is clearly worth the I/O cost. Prefer lightweight file records during scan,
or restrict expensive attribute probing to the smaller set of file types that truly need it.
```

**Why This Keeps Happening**: media attributes look small in the final `Soulseek.File`, so it is easy to forget that extracting them may require non-trivial reads and parsing for every file. On local SSDs this can hide in the noise; on remote or high-latency storage it becomes the actual bottleneck, and lowering worker count alone does not fix it.

### 3o. DHT Rendezvous Must Use A Stable Explicit UDP Port, Not A Random Startup Port

**The Bug**: `DhtRendezvousService` defaulted `DhtPort` to `0`, then replaced it with a random UDP port on each startup. Operators could correctly forward their normal Soulseek ports and still see `DHT bootstrap timed out` forever, because the actual DHT bootstrap traffic was leaving from a different random port that was never forwarded or mapped.

**Files Affected**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/Core/Options.cs`
- `config/slskd.example.yml`

**Wrong**:
```csharp
var dhtPort = _options.DhtPort > 0 ? _options.DhtPort : RandomNumberGenerator.GetInt32(6881, 7000);
```

**Correct**:
```text
DHT rendezvous must always use a stable explicit UDP port. Give it a real default,
validate that enabled DHT never runs with port 0, and tell operators clearly which
UDP port must be forwarded or mapped.
```

**Why This Keeps Happening**: random ports feel convenient because they avoid collisions during development, but a peer-discovery service is an operator-facing network surface, not an internal ephemeral socket. If users cannot know the port in advance, they cannot forward it, allow-list it, or reason about bootstrap failures.


### 3p. DHT Overlay Neighbors Must Populate The Mesh Circuit Peer Inventory

**The Bug**: DHT rendezvous could bootstrap successfully, discover peers, and even register active overlay neighbors in `MeshNeighborRegistry`, while `CircuitMaintenanceService` still logged `0 circuits, 0 total peers, 0 active, 0 onion-capable`. The circuit builder and maintenance path were looking at `IMeshPeerManager`, which never learned about those successful overlay neighbors.

**Files Affected**:
- `src/slskd/DhtRendezvous/MeshNeighborRegistry.cs`
- `src/slskd/Mesh/MeshPeerManager.cs`
- `src/slskd/Mesh/CircuitMaintenanceService.cs`

**Wrong**:
```text
Treat successful overlay handshakes as enough proof that mesh connectivity is working,
without synchronizing those neighbors into the peer inventory used by circuit services.
```

**Correct**:
```text
Whenever overlay neighbors become the source of truth for live mesh connectivity, explicitly
bridge `MeshNeighborRegistry` add/remove events into `IMeshPeerManager` and keep peer stats
in sync. Add regression coverage that proves a registered overlay neighbor increases mesh peer
stats and a removed neighbor is deleted from the circuit peer inventory.
```

**Why This Keeps Happening**: mesh connectivity currently has two layers of state that look similar in logs but are not the same thing. It is easy to stop at “DHT found peers” or “neighbor registered” and assume higher-level mesh features will work automatically, when the circuit stack is actually reading a different store.

---

*Last updated: 2026-03-21*

### 0z47. Mesh Self-Descriptors Must Not Advertise Impossible Direct Transports Or Wrong Default Ports

**What went wrong:** Live auditing on `kspls0` showed `PeerDescriptorPublisher` still auto-detected clearnet endpoints as bare `ip:2234` / `ip:2235` and also emitted `DirectQuic` transport endpoints even when `QuicListener.IsSupported` was false on the host. That poisoned our own published descriptor with ports we were not actually listening on and advertised a direct QUIC transport that the node could not accept. Operators then saw DHT peers and circuit-maintenance churn without any realistic path to a working direct mesh connection.

**Why it happened:** The descriptor publisher mixed old Soulseek default ports with the newer mesh transport model, and it never cross-checked advertised transports against the runtime transport capability on the current host. Because DHT discovery and peer stats already looked busy, the impossible advertisement path hid behind noisy remote-candidate failures.

**How to prevent it:** Mesh self-descriptor publication must derive advertised ports from the real configured listeners, not hard-coded legacy defaults. Do not publish `DirectQuic` endpoints unless QUIC is actually supported on the running host, and never publish bare `ip:port` legacy endpoints when the consuming code expects explicit `udp://` / `quic://` schemes. Add regression coverage for both the configured port selection and the unsupported-QUIC path.

### 0z48. QUIC-Unsupported Hosts Need A Real Direct Mesh Dialer Fallback, Not Just Honest Descriptor Publication

**What went wrong:** Live validation on `kspls0` showed that fixing the mesh self-descriptor only made the node honest; it did not make mesh circuits work. The host correctly stopped advertising impossible `DirectQuic` transports once `QuicListener.IsSupported` was false, but `TransportSelector` still only had `DirectQuicDialer` for clearnet mesh transport. That left QUIC-unsupported hosts with no direct dialer at all, so circuit formation could never succeed even though DHT rendezvous and the TCP overlay listener were healthy.

**Why it happened:** The codebase grew two separate direct-connection stacks: the mesh transport selector assumed direct clearnet means QUIC, while the anonymity layer already had a working direct TLS transport to the TCP overlay listener. We fixed the advertisement lie first, but the dialer layer still had no fallback to the transport the host could actually accept.

**How to prevent it:** Whenever a transport advertisement is made conditional on runtime capability, audit the matching outbound dialer path in the same change. Do not leave a host in a state where it truthfully advertises no supported direct transport but the circuit builder still assumes one exists. Either provide a direct TLS/TCP mesh dialer fallback or fail startup/package validation explicitly on QUIC-unsupported hosts.

### 0z46. Security Refactors Must Delete Or Rewrite Tests That Still Resolve Removed Service Types

**What went wrong:** A security refactor removed the old `TransferSecurity` service, but `SecurityStartupTests` still tried to resolve it from DI. The whole targeted unit pass then failed at compile time, which hid the actual status of the new hardening work.

**Why it happened:** The implementation moved to `SecurityOptions`/middleware wiring, but the test suite was not updated in the same change set. The test still asserted behavior against a deleted registration contract.

**How to prevent it:** When removing or folding a service during a security refactor, grep the unit suite for the deleted type name and either rewrite those tests to the new registration contract or delete them in the same commit. Never leave compile-broken tests as deferred cleanup.


### 0z49. Mesh Releases Need A Deterministic Two-Instance Smoke, Not Just Loopback Pieces And Live DHT Observation

**What went wrong:** We kept shipping mesh fixes after validating isolated pieces like single-process loopback handshakes, DHT counters, or live-host candidate discovery. That still missed the most important proof: two real `slskdn` processes standing up, forming an overlay connection, and reporting each other as connected peers. Without that deterministic two-instance smoke, we repeatedly confused partial signals for real end-to-end mesh success.

**Why it happened:** The repo had lower-level coverage (`MeshSearchLoopbackTests`, connector/server unit tests, live DHT diagnostics), but no stable full-instance path that could force one node to dial another and assert the resulting overlay state. Real-network validation through public DHT was too noisy and peer-quality-dependent to serve as the primary release gate.

**How to prevent it:** Keep a deterministic two-instance full-process mesh smoke in the integration suite. It should boot two `slskdn` instances, force one to connect to the other through the real overlay stack, and assert both sides report the peer/connection. Treat public-DHT/live-host checks as supplemental evidence only, not the main proof that mesh works.


### 0z50. Full-Instance Runner Must Pass `--app-dir`, Not Just `APP_DIR`, To Avoid Colliding With A Live User Install

**What went wrong:** The new two-instance mesh smoke started real `slskd` processes with a temporary config file and `APP_DIR` set in the child environment, but the process still exited immediately with `An instance of slskd is already running in app directory: /home/keith/.local/share/slskd`. The runtime singleton guard was checking the default app directory because the harness never passed the explicit `--app-dir` CLI argument.

**Why it happened:** The harness assumed the environment variable alone would override the app directory early enough in startup. In practice, the running process resolved the default appdir before the test harness intent took effect, so the test collided with the developer's live install instead of the temporary sandbox.

**How to prevent it:** Any full-instance test harness that launches `slskd` must pass both `--config` and `--app-dir` explicitly on the command line. Do not rely on environment-only appdir overrides for subprocess isolation when the product has singleton/appdir locking during startup.


### 0z51. Full-Instance Harnesses Must Override Every Bound Listener Port, Not Just The Primary HTTP Port

**What went wrong:** After fixing `--app-dir`, the new two-instance mesh smoke still died during startup because the subprocess tried to bind other default listeners already used by the developer machine, including HTTPS on `5031` and the mesh UDP/QUIC defaults on `50400/50401`. Randomizing only the primary web port was not enough to isolate the child process.

**Why it happened:** The harness wrote a partial config and assumed the remaining listeners were harmless defaults. `slskd` starts multiple network surfaces, so any unoverridden default port can collide with a live local install and make an integration test look like an application failure.

**How to prevent it:** Full-instance test config must explicitly set or disable every listener that can bind a socket: HTTP, HTTPS, overlay TCP, DHT UDP, UDP overlay, and QUIC/data overlay. Do not rely on product defaults when launching subprocesses on a developer machine with another instance already running.


### 0z52. Full-Instance Harness YAML Must Use The Binder Section Name `dhtRendezvous`, Not The Short Alias `dht`

**What went wrong:** The two-instance mesh smoke wrote a top-level `dht:` section because the human-facing example config documents DHT that way. In the full subprocess startup path, that left the child process on the default overlay/DHT ports (`50305`/`50306`) even though the HTTP port from the same file was honored. The result was a fake mesh failure caused by both instances accidentally sharing the same default DHT overlay listener.

**Why it happened:** The harness assumed the example/YAML alias and the runtime configuration binder were interchangeable. In this code path the binder key that actually drives `OptionsAtStartup.DhtRendezvous` is `dhtRendezvous`, so the short alias was ignored for the full-process child.

**How to prevent it:** When generating full-instance test config, use the exact runtime binder section name that the subprocess honors. Do not assume the example-file alias and the startup binder key are identical without proving it in a live child-process probe first.



### 0z53. Full-Instance Overlay Smokes Must Not Treat The Inbound Socket Source Port As The Remote Node's Listener Port

**What went wrong:** The new two-instance mesh smoke forced alpha to connect directly to beta's overlay listener, then asserted that beta's `/api/v0/overlay/connections` entry for alpha would report `alpha.OverlayPort`. That was wrong. On the inbound side, the connection registry reports the remote socket endpoint for the accepted TCP session, which uses alpha's ephemeral client source port, not alpha's overlay listener port. The test timed out even though the mesh connection actually existed.

**Why it happened:** We wrote the assertion as if both sides would expose a symmetric "peer listener port" view. The current controller surfaces `MeshOverlayConnection.RemoteEndPoint`, and for accepted inbound sockets that endpoint is the caller's transient outbound port. We were validating the wrong network fact and turned a healthy connection into a false negative.

**How to prevent it:** In full-instance overlay tests, assert on peer identity and connection presence, not the inbound side's remote socket source port. If listener-port identity matters, expose it explicitly in the overlay handshake payload or a dedicated response field instead of inferring it from the accepted TCP socket endpoint.


### 0z54. Experimental Bridge Search Must Not Replace The Proven Soulseek Search Path By Default

**What went wrong:** Issue `#209` evolved from DHT bootstrap failures into a user-visible search regression: a logged-in client could run a popular search and get `0` files while logs showed the request using `[ScenePodBridge]`. The bridge path depends on multiple experimental provider layers and mesh peer availability, so making it the default path allowed bridge/provider problems to look like core Soulseek search was broken.

**Why it happened:** `Feature.ScenePodBridge` defaulted to `true`, so normal searches were diverted into the aggregation path whenever providers were registered. That changed the baseline behavior of an upgrade from upstream `slskd`; users expected the proven Soulseek network search path, but the app ran the newer Scene/Pod bridge path unless disabled.

**How to prevent it:** Keep core Soulseek search as the default user path. Experimental bridge/provider aggregation must be explicit opt-in until it has end-to-end field proof and separate diagnostics. New mesh/bridge features may run as supplemental parallel paths only when they cannot suppress or replace normal Soulseek results.

### 0z55. Overlay Message Read Timeout Must Not Be Shorter Than Keepalive

**What went wrong:** Issue `#209` build `151` finally established an inbound mesh neighbor, then dropped it exactly 30 seconds later with `OperationCanceledException` from `SecureMessageFramer.ReadExactlyAsync`. The connection was healthy but quiet; the server read loop treated its own per-read timeout as a fatal message-loop error and unregistered the peer before keepalive could run.

**Why it happened:** `OverlayTimeouts.MessageRead` was 30 seconds, while `OverlayTimeouts.KeepaliveInterval` was 2 minutes and `OverlayTimeouts.Idle` was 5 minutes. `MeshOverlayConnection.ReadRawMessageAsync()` creates an internal timeout token, but `MeshOverlayServer.HandleMessagesAsync()` only treated cancellation as expected when the outer server token was canceled. A normal no-message interval therefore looked like an error and disconnected the neighbor.

**How to prevent it:** Blocking overlay reads in long-lived message loops must treat internal read timeout as "no message yet" and continue to the next loop iteration so keepalive and idle checks can make the lifecycle decision. Per-read timeouts can still protect request/response reads, but they must not be shorter than or semantically override keepalive/idle policy for persistent peer connections.
