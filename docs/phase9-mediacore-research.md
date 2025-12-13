# Phase 9: MediaCore Foundation — Research Summary

Date: 2025-12-10  
Branch: `experimental/brainz`  
Scope: T-1039..T-1044

## Goals
- Define a media-identification substrate that can extend beyond audio (future domains).
- Establish addressing, hashing, and interoperability patterns for cross-domain content.

## Decisions & Findings

### ContentID Architecture (T-1039)
- ContentId = namespace + identifier + optional variant hash:
  - Example: `content:mb:recording:<mbid>`
  - Variant hash (when available): `hash:<algo>:<hex>` (e.g., `hash:sha256:abcd…`).
- Prefer stable third-party IDs (MusicBrainz) when available; fall back to perceptual/fuzzy IDs when not.
- Keep ContentId independent from storage location; no paths/PII.

### Multi-Domain Content Addressing (T-1040)
- Namespaces reserved: `mb:recording`, `mb:release`, `mb:artist`, `pod:<podid>`, `ext:<partner>`, future `vid`, `img`, `txt`.
- For audio, continue to prefer MBIDs + hashes; for future domains, allow provider-scoped IDs + perceptual signatures.
- Descriptor envelopes carry: `contentId`, `hashes[]`, `size`, `codec`, `duration(optional)`, `confidence`.

### IPLD/IPFS Integration Strategy (T-1041)
- Represent descriptors as IPLD (dag-cbor) for compatibility with IPFS/IPLD stacks.
- CIDs derived from descriptor content; keep MessagePack for DHT payloads but map fields 1:1 to IPLD schema.
- Optional pinning of descriptor sets; do not publish PII or direct paths.
- Gate IPFS usage behind config flag; default off.

### Perceptual Hash Systems (T-1042)
- Audio: keep existing Chromaprint/AcoustID + audio_sketch_hash (Phase 2).
- Other media: future-proof with generic perceptual hash slot `phash:<algo>:<hex>`.
- Require collision testing before enabling any new phash for matching.

### Fuzzy Content Matching (T-1043)
- Matching pipeline order: strong IDs (MBID/hash) → medium IDs (phash) → fuzzy text (title/artist, locale-aware).
- Score aggregation with confidence; never auto-merge low-confidence results.
- Keep fuzzy matches out of DHT; use locally or via trusted peers only.

### Metadata Portability (T-1044)
- Descriptor fields are self-contained and schema-stable; avoid embedding app-specific flags.
- Provide schema versioning and migration path; reject unknown-required fields.
- Transport envelope separates signature/meta from payload to allow reuse across transports.

## Risks & Guardrails
- No PII or file paths in descriptors; only hashes and public IDs.
- Signature on descriptors when published to DHT/overlay.
- Size limits (8–10 KB) and TTL limits inherit from MeshCore (Phase 8).
- Fuzzy matches are advisory only; do not publish to public DHT.

## Next Steps (implementation)
- Define IPLD schema and mapping for descriptors.
- Add phash slots and validation harness for collision testing.
- Implement local fuzzy matcher with confidence scoring and guardrails.
- Add config flags for IPFS/IPLD publishing (default off).
















