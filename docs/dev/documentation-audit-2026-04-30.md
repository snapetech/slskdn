# Documentation Audit - 2026-04-30

Scope: tracked project documentation and examples, excluding archived design notes,
vendored `node_modules`, generated publish output, and memory-bank working notes
unless explicitly called out.

## Executive Summary

> Update 2026-05-01: The first-run port/credential issues, root README logo
> TODO, docs index user-guide gaps, and missing System/Pods/SongID user-guide
> entries were addressed. Remaining audit items are mostly broken-link cleanup,
> VPN doc splitting, and historical snapshot banners.

The user-facing docs have good coverage for the newest Lidarr and VPN work in
`README.md`, `docs/FEATURES.md`, `docs/config.md`, `docs/lidarr-integration.md`,
and `src/slskdN.VpnAgent/README.md`. The biggest problems are stale entry-level
docs, broken links in current docs, and confusing terminology around the two
different "VPN" concepts:

- host VPN binding/port-forwarding for Soulseek traffic
- pod-scoped private service tunnels over the mesh

The docs should be cleaned in this order:

1. Fix broken links in non-archived tracked markdown.
2. Split and rename VPN docs so host VPN binding and pod private-service
   gateway docs cannot be mistaken for each other.
3. Move stale phase/audit docs into archive or mark them clearly as historical.
4. Soften or qualify public-readiness/security claims that overstate the
   current codebase.

## Overstatement Findings

The current public docs do over-state some areas. These should be fixed before
using the README/docs as release copy.

### 1. README Claims Advanced Features Are Production-Ready

`README.md` says:

```text
The following advanced features are fully implemented and production-ready
```

This is too broad. The same README later labels some of those systems
experimental, and the April 2026 security audit still lists verified high- and
medium-severity findings. It is especially risky because the section includes
mesh, security hardening, Pods, VirtualSoulfind, and Service Fabric.

Recommended replacement:

```text
The following advanced features are available in this fork. Maturity varies by
feature; see the status table below and current security notes before exposing
experimental mesh, gateway, or federation features.
```

Also change the status table entries:

- `DHT Peer Discovery`: `Experimental` or `Beta`, not `Stable`.
- `Security Hardening`: `Active hardening`, not `Stable`.
- `Multi-Source Downloads`: `Beta` unless release testing is current.
- `MusicBrainz Integration`: `Beta/Stable per subfeature`; fingerprinting and
  auto-tagging should not be advertised as blanket stable if optional native
  tooling and third-party services are required.

### 2. Security Docs Claim Universal SSRF Protection

`docs/FEATURES.md` says:

```text
Safe HTTP Client: All outbound HTTP goes through SSRF-safe client
```

This conflicts with `docs/security/full-app-audit-2026-04.md`, which verifies
that webhook delivery and ActivityPub inbox delivery lack an IP-range SSRF
guard. Solid WebID fetches and ActivityPub key fetching have good guards, but
the protection is not universal.

Recommended replacement:

```text
Selected outbound fetch paths use SSRF-safe policies, including Solid WebID
resolution and ActivityPub key fetching. Webhook and federation delivery paths
are tracked separately in the security audit and should not be described as
globally SSRF-safe until fixed.
```

### 3. Current Status Reads Like A Live Health Report

`docs/CURRENT_STATUS.md` is dated `2026-01-27` but uses present-tense claims:

- `All builds passing`
- `All unit tests passing (2430)`
- `All integration tests passing (190)`
- `All critical features complete`
- `Codebase is in excellent shape`

This is stale and too absolute for a manually maintained document. It should be
either archived as a January snapshot or rewritten as a dated status note that
points to CI for current test health.

Recommended replacement banner:

```text
Historical snapshot from 2026-01-27. Do not use as current release, CI, or
security status. Check GitHub Actions and the latest changelog/security audits
for current state.
```

### 4. Service Fabric And Mesh Completion Language Is Too Absolute

`docs/FEATURES.md`, `docs/HOW-IT-WORKS.md`, and `README.md` describe Service
Fabric and mesh layers as complete platform features. The code has real
implementations, but the docs also mention in-progress hardening and the
security audit still lists open risks. Service Fabric should be described as
available/experimental infrastructure, not a fully hardened platform.

Recommended language:

- Use `implemented experimental infrastructure` instead of `complete`.
- Use `local/opt-in gateway` instead of language that implies a broadly safe
  remote application platform.
- Keep "disabled by default" claims only where the config default is verified.

### 5. "Prevents" And "Guaranteed" Claims Should Become Guardrail Claims

Several docs use prevention/guarantee wording, for example:

- `prevents SSRF attacks`
- `prevents amplification`
- `playback guarantees`
- `Soulseek abuse ... is prevented by multiple enforcement layers`

These should be softened. Security and network-health controls reduce risk;
they do not guarantee prevention against all future code paths or configuration
states.

Recommended replacements:

- `blocks common SSRF targets on this path`
- `reduces amplification risk with work budgets and rate limits`
- `designed to preserve playback continuity when enough verified sources exist`
- `adds multiple guardrails against non-music Soulseek use`

### 6. Config Snippets May Present Planned Options As User-Ready

`docs/FEATURES.md` includes several top-level YAML snippets such as
`ServiceFabric`, `CatalogFetch`, `ContentRelay`, and `TrustedRelay`. Some
current code/config uses different naming or has implementation-specific option
classes. These snippets should be checked against `config/slskd.example.yml`
before being treated as user-facing configuration.

Recommended fix:

- Move speculative/planning config to design docs.
- In user docs, only show options present in `config/slskd.example.yml` or
  `docs/config.md`.
- Use the repo's actual casing and nesting.

### 7. Archive Docs Contain Strong Historical Claims

Many files under `docs/archive/` intentionally contain completion and release
claims such as "production-ready" and "feature-complete." That is acceptable if
the archive is treated as historical, but public docs should not link to those
files as current status without a warning.

Recommended fix:

- Add or keep a visible archive disclaimer.
- Prefer linking current user guides from `README.md` and `docs/README.md`.
- Move stale phase-completion docs out of the current-docs index.

## High-Priority Gaps

### 1. Broken Links In Current Docs

A tracked non-archive markdown link scan found 25 broken links.

Current broken links:

```text
docs/DHT_RENDEZVOUS_DESIGN.md:641 -> ./MULTI_SOURCE.md
docs/MULTI_SWARM_IMPLEMENTATION_GUIDE.md:7 -> ./docs/archive/duplicates/MULTI_SWARM_ROADMAP.md
docs/MULTI_SWARM_IMPLEMENTATION_GUIDE.md:1283 -> ./docs/archive/duplicates/MULTI_SWARM_ROADMAP.md
docs/MULTI_SWARM_IMPLEMENTATION_GUIDE.md:1285 -> ./BRAINZ_PROTOCOL_SPEC.md
docs/MULTI_SWARM_IMPLEMENTATION_GUIDE.md:1286 -> ./BRAINZ_CHUNK_TRANSFER.md
docs/MULTI_SWARM_IMPLEMENTATION_GUIDE.md:1287 -> ./BRAINZ_STATE_MACHINES.md
docs/MULTI_SWARM_IMPLEMENTATION_GUIDE.md:1288 -> ./docs/archive/duplicates/MULTI_SOURCE_DOWNLOADS.md
docs/MULTI_SWARM_IMPLEMENTATION_GUIDE.md:1290 -> ./HASHDB_SCHEMA.md
docs/MUSICBRAINZ_INTEGRATION.md:1203 -> ./docs/archive/duplicates/MULTI_SOURCE_DOWNLOADS.md
docs/README.md:28 -> security/DOCUMENTATION_AUDIT_SECURITY_CLAIMS.md
docs/anonymity/i2p-setup-guide.md:422 -> ../security/threat-model.md
docs/anonymity/tor-setup-guide.md:404 -> ../security/threat-model.md
docs/anonymity/tor-setup-guide.md:405 -> ../security/traffic-analysis.md
docs/dev/SONGID_INTEGRATION_MAP.md:86 -> <repo-root>/src/web/src/components/Search/Searches.jsx
docs/dev/release-checklist.md:18 -> <repo-root>/docs/dev/bugfix-verification-checklist.md
docs/dev/release-copy.md:7 -> <repo-root>/.github/release-notes/main.md.tmpl
docs/dev/release-copy.md:7 -> <repo-root>/.github/release-notes/dev.md.tmpl
docs/dev/release-copy.md:8 -> <repo-root>/packaging/scripts/render-release-notes.sh
docs/dev/release-copy.md:18 -> <repo-root>/packaging/winget/snapetech.slskdn.locale.en-US.yaml
docs/dev/release-copy.md:19 -> <repo-root>/packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml
docs/dev/testing-policy.md:18 -> <repo-root>/docs/dev/bugfix-verification-checklist.md
docs/dev/testing-policy.md:46 -> <repo-root>/docs/dev/release-checklist.md
docs/security/IMPLEMENTATION_EFFORT_ANALYSIS.md:168 -> docs/security/SECURITY_COMPARISON_ANALYSIS.md
docs/virtualsoulfind-v2-design.md:10 -> ../../README.md
tests/slskd.Tests.Performance/README.md:136 -> ../docs/dev/e2e-testing-guide.md
```

Recommended fixes:

- For links that accidentally include `docs/` while already inside `docs/`,
  change to relative paths like `archive/duplicates/...`.
- Replace missing `BRAINZ_*` / `HASHDB_SCHEMA` links with the current docs that
  actually exist, or mark them as removed historical specs.
- Either restore `docs/security/DOCUMENTATION_AUDIT_SECURITY_CLAIMS.md` or
  remove that index entry.
- Replace `<repo-root>/...` pseudo-links with plain code spans; Markdown treats
  them as real links.

### 2. Getting Started Is Stale

`docs/getting-started.md` still tells users to open `http://localhost:5000` and
check firewall port `2234`. Current defaults shown elsewhere are:

- Web UI: `5030`
- HTTPS: `5031`
- Soulseek listen port: `50300`

This is a direct first-run breakage risk.

Recommended update:

- Change all default web references from `5000` to `5030`.
- Change firewall/listen guidance from `2234` to `50300`.
- Add short "first-run ports" table: Web UI, HTTPS, Soulseek TCP, compact VPN
  UDP/DHT if enabled.
- Add quick links to Lidarr and VPN agent setup from the first-run guide.

### 3. Current Status Is Not Current

`docs/CURRENT_STATUS.md` is dated `2026-01-27`, references branch
`dev/40-fixes`, and claims `2430` unit tests passing with `0 skipped`. That is
not useful as a current status document on April 30, 2026.

Recommended update:

- Either archive it as a January snapshot or replace it with a generated/manual
  status summary dated April 30, 2026.
- Avoid absolute test-count claims unless they are generated in CI or updated
  in release automation.
- Move branch-specific `dev/40-fixes` status to `docs/dev/40-fixes-plan.md`.

### 4. VPN Docs Need Terminology Separation

There are two different VPN-shaped features:

- `src/slskdN.VpnAgent/README.md`: host VPN binding, fail-closed routing, and
  forwarded-port integration for Soulseek traffic.
- `docs/pod-vpn/*`: pod-scoped private service tunnels over the mesh.

Both use "VPN" heavily. Users can reasonably confuse them.

Recommended update:

- Rename user-facing pod docs to "Pod Private Service Gateway" or "Pod Private
  Service Tunnels".
- Keep "VPN Agent" for the host companion only.
- Add a comparison table:
  - Host VPN agent: routes Soulseek traffic through a real VPN interface.
  - Pod private service gateway: lets pod members reach explicit private
    services through mesh tunnels; it is not an internet exit node.

### 5. VPN Agent Docs Are Complete But Too Dense

`src/slskdN.VpnAgent/README.md` now contains modes, architecture, manual setup,
installer setup, API, watchdog, scaling, platform notes, and troubleshooting in
one long file. It is accurate enough, but hard for a new operator to follow.

Recommended split:

- `README.md`: overview, supported modes, quick decision tree.
- `manual-linux-wireguard.md`: full NAT-PMP/netns path.
- `external-tunnel.md`: OpenVPN/Tailscale/static-forward setup.
- `windows-macos.md`: platform-specific fail-closed behavior and limitations.
- `api-contract.md`: Gluetun compatibility and `/v1/slskdn/portforwards`.

Also document the recent single-public-pair decision:

- We are not building a sidecar mux just to reduce ports.
- In-app single TCP/UDP pair is possible only with deeper listener ownership
  changes and likely no direct QUIC in v1.
- Current supported reduction remains `core`/`compact` ingress modes.

### 6. Lidarr Is First-Class In Some Docs, Missing In First-Run Docs

Lidarr is well covered in:

- `README.md`
- `docs/FEATURES.md`
- `docs/config.md`
- `docs/lidarr-integration.md`

It is not surfaced in `docs/getting-started.md` or
`docs/advanced-features.md`, so new users may not discover it from the main
user-doc flow.

Recommended update:

- Add a "Lidarr integration" section to `docs/getting-started.md` with the safe
  rollout: status, wanted preview, one-time sync, then optional auto-download
  and import.
- Add a concise workflow to `docs/advanced-features.md`.
- Add troubleshooting entries for path mapping and ambiguous manual-import
  decisions.

### 7. Gold Star, Pods, Rooms, And Chat Need A User Path

`README.md` links to design docs for PodCore, chat bridge, and Gold Star Club,
but there is no compact user guide that answers:

- Where does a user see the Gold Star room after login?
- Is it under Rooms, Chat, Pods, or another view?
- How does autojoin behave before/after Soulseek username availability?
- What happens if the user leaves?
- Which routes are user-facing versus design/API only?

Recommended update:

- Add `docs/pods-and-rooms.md` or expand `docs/design/gold-star-club.md` with a
  user-facing section.
- Link it from `docs/README.md`, `README.md`, and `docs/getting-started.md`.
- Include exact UI paths and API routes.

## Medium-Priority Staleness

### 8. Test Coverage Docs Are Date- and Count-Sensitive

These docs contain January counts and claims:

- `docs/TEST_COVERAGE_ASSESSMENT.md`
- `docs/TEST_COVERAGE_SUMMARY.md`
- `docs/dev/next-steps-summary.md`
- `docs/dev/backlog-verification-summary.md`

They may still be useful as historical records, but should not be linked as
"current" unless regenerated.

Recommendation: move dated snapshots into `docs/archive/` or add a banner:
"Historical snapshot; check CI for current test counts."

### 9. Placeholder/Stub Docs Contradict Recent Test Backfills

Several dev docs still describe tests as placeholders even after recent
backfills:

- `docs/TEST_COVERAGE_ASSESSMENT.md`
- `docs/dev/slskd-tests-unit-completion-plan.md`
- `docs/dev/slskd-tests-unit-lift-vs-requirements.md`
- `docs/dev/placeholder-null-heavy-inventory.md`

Recommendation: update these after the placeholder-test work lands, or move the
old plans into archive to avoid reintroducing already-fixed work.

### 10. Root README Still Has A Logo TODO

**Resolved 2026-05-01:** The root README TODO comment was removed.

`README.md` starts with:

```html
<!-- TODO: replace with slskdN-original logo. Upstream slskd PNG removed. -->
```

Recommendation: either add the logo asset and remove the TODO, or remove the
comment. The first line of the public README should not be an unresolved task.

### 11. Config Docs Are Mostly Current But Need First-Run Cross-Links

`docs/config.md` has a current Lidarr section and correct default ports. It
does not yet cross-link the host VPN agent near the Soulseek listen-port section,
where VPN users are most likely to look.

Recommendation: add a short note in "Listen IP Address and Port":

- If using a VPN provider with dynamic forwarded ports, use the VPN agent guide.
- Web UI/API should usually stay local while Soulseek traffic uses the VPN.
- `soulseek.listen_port` may be updated dynamically when VPN integration is
  enabled.

## Low-Priority Cleanup

### 12. Docs Index Needs Recategorization

`docs/README.md` currently mixes user guides, implementation guides, design
docs, and historical roadmap docs. It should be split into:

- Start here
- User guides
- Operator/deployment guides
- Developer docs
- Historical/archive

Add missing current docs:

- Lidarr
- VPN agent
- Pods/Rooms/Gold Star user guide
- Listening Party
- Integrated player/visualizers

### 13. Historical Docs Need Clear Banners

Many files under `docs/dev/`, `docs/research/`, and phase docs are valuable as
implementation history but read like current commitments. Add a standard banner
to historical snapshots:

```markdown
> Historical snapshot. This document may not reflect current defaults or code.
> Prefer README.md, docs/README.md, docs/config.md, and feature-specific user
> guides for current behavior.
```

### 14. Naming Consistency

The repo uses `slskdn`, `slskdN`, and `slskdN(OT)` in different contexts.

Recommendation:

- Use `slskdN` for product prose.
- Use `slskdn` only for package/binary/repo names where lowercase is required.
- Use `slskdN(OT)` only in branding/intro copy, not every guide.

## Suggested Next Patch Set

1. Fix all 25 broken non-archive links.
2. Add status banners to remaining stale January test/status docs.
3. Split or at least restructure `src/slskdN.VpnAgent/README.md`.
4. Rename pod-VPN docs in link text to "Pod Private Service Gateway" without
   moving files immediately.

## Commands Used

```bash
git ls-files '*.md' | rg -v '^(docs/archive|memory-bank|from-lack-workspace|node_modules|src/web/node_modules|tests/e2e/node_modules)/'
python3 <markdown-link-scan>
git grep -n 'kspls0\|kspld0\|/home/keith\|Documents/code\|Documents/Code'
rg -n '2025-|2026-01|dev/40-fixes|placeholder|stub|TODO|Lidarr|VPN|Gold Star'
```
