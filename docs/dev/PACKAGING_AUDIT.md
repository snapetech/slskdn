# Packaging Audit: Silent Failures and Validation

This document summarizes packaging channels, how they get sources, and where builds can fail silently so maintainers can validate all channels.

## Summary

| Channel | Source of packaging files | User-visible failure? | Silent-fail risk |
|--------|---------------------------|----------------------|------------------|
| **AUR (slskdn-dev)** | Release assets (zip + slskd.service, slskd.yml, slskd.sysusers) | **Yes** – `yay -S slskdn-dev` fails sha256 | **Fixed**: URLs now point to release; checksums from CI. Was: raw.githubusercontent + branch URL (404). |
| **COPR (dev)** | Checkout at tag + zip from release | Only if COPR build fails | Low: copies from repo; ref now explicit. |
| **PPA (dev)** | Checkout at tag + zip from release | Only if dput fails | Low: copies from repo; ref now explicit. |
| **Chocolatey (dev)** | Zip + URL/checksum from release | Only if install fails | Low: workflow sets URL/checksum from release. |
| **Homebrew (dev)** | Zips from release; Formula generated in CI | Only if `brew install` fails | Low: URLs/checksums from release. |
| **Snap (dev)** | Zip from release, unzipped into snap | Only if snap install fails | Low. |
| **Docker (dev)** | Built from Dockerfile + zip from release | Only if pull/run fails | Low. |
| **Nix (dev)** | Zips from release; hashes computed in CI | Only if nix build fails | Low. |
| **Winget (dev)** | Zip from release; manifests updated in CI | Only if install fails | Low. |

## Silent-Fail Behavior (By Design)

When a secret is not configured, jobs **exit 0** so the whole workflow stays green. That means:

- **AUR**: No `AUR_SSH_KEY` → AUR push skipped, job succeeds. Users see old/broken package.
- **COPR**: No `COPR_LOGIN`/`COPR_TOKEN` → upload skipped, job succeeds.
- **PPA**: No `GPG_PRIVATE_KEY` → PPA upload skipped, job succeeds.
- **Chocolatey**: No `CHOCO_API_KEY` → Pack step not run (conditional), job can succeed.
- **Homebrew**: No `TAP_GITHUB_TOKEN` → Tap update skipped, job succeeds.
- **Snap**: No `SNAPCRAFT_STORE_CREDENTIALS` → Publish skipped, job succeeds.

So a green run does **not** mean every channel was updated. Check the job logs for "Skipping" / "not configured" if a channel is missing.

## What Was Fixed (AUR and Consistency)

1. **AUR slskdn-dev**
   - **Problem**: PKGBUILD fetched slskd.service, slskd.yml, slskd.sysusers from `raw.githubusercontent.com/.../experimental/multi-source-swarm/...`. That branch could 404 or serve different content → sha256 mismatch, `yay -S slskdn-dev` failed.
   - **Fix**: Those files are now release assets (uploaded with the zip). PKGBUILD downloads them from the release; CI injects checksums from the tag checkout. No dependency on branch URLs.

2. **Checkout ref for packaging jobs**
   - **Problem**: COPR/PPA (and previously AUR) did not explicitly set `ref: ${{ github.ref }}` on checkout. Theoretically re-runs or refs could use the wrong commit.
   - **Fix**: AUR, COPR, and PPA dev jobs now use `ref: ${{ github.ref }}` so they always use the tag’s commit for packaging files.

## How to Validate All Channels

1. **After a dev tag push**
   - Open the "Build on Tag" workflow run.
   - For each packager job (aur-dev, copr-dev, ppa-dev, chocolatey-dev, homebrew-dev, snap-dev, docker-dev, nix-dev, winget-dev), confirm it did not "Skipping" / "not configured" unless you expect that.
   - If a job was skipped, the channel was not updated.

2. **Smoke-test installs (when possible)**
   - AUR: `yay -Sy slskdn-dev` (or clean build from AUR).
   - COPR: `dnf install slskdn-dev` (with slskdn/slskdn-dev enabled).
   - PPA: `apt install slskdn-dev` (with PPA added).
   - Chocolatey: `choco install slskdn --pre`.
   - Homebrew: `brew install snapetech/slskdn/slskdn-dev`.
   - Docker: `docker pull ghcr.io/snapetech/slskdn:dev-latest`.

3. **Branch/URL references**
   - `build-on-tag.yml` does not rely on branch names for source URLs; dev release and AUR use the release tag.
   - Legacy workflows (`dev-release.yml`, `dev-snap-docker-only.yml`) may still reference branches like `experimental/whatAmIThinking` or `experimental/multi-source-swarm`; if those branches are removed or renamed, those workflows can fail or produce wrong artifacts.

## References

- AUR dev PKGBUILD: `packaging/aur/PKGBUILD-dev`
- Dev release assets: `build-on-tag.yml` → "Prepare Release Assets" (zips + slskd.service, slskd.yml, slskd.sysusers)
- AUR checksum injection: `build-on-tag.yml` → aur-dev → "Update PKGBUILD"
