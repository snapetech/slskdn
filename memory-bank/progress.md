# Progress Log

> Chronological log of development activity.  
> AI agents should append here after completing significant work.

---

## 2025-12-08

- 00:00: Initialized memory-bank structure for AI-assisted development
- 00:00: Created `projectbrief.md`, `tasks.md`, `activeContext.md`, `progress.md`, `scratch.md`
- 00:00: Created `.cursor/rules/` with project-specific AI instructions
- 00:00: Created `AGENTS.md` with development workflow guidelines

---

## Historical Releases (from DEVELOPMENT_HISTORY.md)

| Release | Date | Highlights |
|---------|------|------------|
| .1 | Dec 2 | Auto-replace stuck downloads |
| .2 | Dec 2 | Wishlist, Multiple destinations |
| .3 | Dec 2 | Clear all searches |
| .4 | Dec 3 | Smart ranking, History badges |
| .5 | Dec 3 | Search filters, Block users |
| .6 | Dec 3 | User notes, AUR binary |
| .7 | Dec 3 | Delete files, AUR source |
| .8 | Dec 3 | Push notifications |
| .9 | Dec 4 | Bug fixes |
| .10 | Dec 4 | Tabbed browse |
| .11 | Dec 4 | CI/CD automation |
| .12 | Dec 4 | Package fixes |
| .13 | Dec 5 | COPR, PPA, openSUSE |
| .14 | Dec 5 | Self-hosted runners, LRU cache |
| .15 | Dec 6 | Room/Chat UI, Bug fixes |
| .16 | Dec 6 | StyleCop cleanup |
| .17 | Dec 6 | Search pagination, Flaky test fix |
| .18 | Dec 7 | Upstream merge, Doc cleanup |

---

## 2025-12-09

### CI/CD Infrastructure Overhaul

**Morning Session: Dev Build Fixes (5 cascading bugs fixed)**

1. **Package Version Hyphens (Bug #1)**: AUR/RPM/DEB all reject hyphens in version strings. Fixed by using `sed 's/-/./g'` (global) instead of `sed 's/-/./'` (first only). Version now converts correctly: `0.24.1-dev-20251209-215513` → `0.24.1.dev.20251209.215513`

2. **Integration Test Missing Reference (Bug #2)**: Docker builds failed with namespace errors. `slskd.Tests.Integration.csproj` was missing `<ProjectReference>` to main project. Fixed by adding the reference.

3. **Filename Pattern Mismatch (Bug #3)**: Packages job failed with "no assets match pattern". Downloaded `slskdn-dev-*-linux-x64.zip` but file was `slskdn-dev-linux-x64.zip` (no timestamp). Fixed by removing wildcard.

4. **RPM Build on Ubuntu (Bug #4)**: Packages job tried to build RPM on Ubuntu, which lacks Fedora build tools (`systemd-rpm-macros`). Fixed by removing RPM from packages job - COPR handles RPM builds natively on Fedora.

5. **PPA Version Hyphens (Bug #5)**: PPA rejected uploads as "Version older than archive" because `dpkg` treats hyphens as separators. Same fix as #1 - convert all hyphens to dots for Debian changelog.

**Additional Fixes**:
- **Yay Cache Gotcha**: AUR PKGBUILD updates weren't visible until cache cleared (`rm -rf ~/.cache/yay/package-name`)
- **Dev Build Naming**: Established convention for `dev-YYYYMMDD-HHMMSS` format with documentation

**Afternoon Session: Runtime Bugs**

6. **Backfill 500 Error**: EF Core couldn't translate `DateTimeOffset` to `DateTime` comparison. Fixed by using `.UtcDateTime` for explicit conversion before querying.

7. **Scanner Detection Noise**: Port scanner was triggering on localhost/LAN traffic. Fixed by skipping `RecordConnection()` for all private IPs.

**Evening Session: Release Visibility**

8. **Timestamped Dev Releases**: Added creation of visible timestamped releases (e.g., `dev-20251209-222346`) in addition to hidden floating `dev` tag. Now visitors can find dev builds in the releases page without accidentally getting them from the homepage.

9. **README Auto-Update**: Added workflow step to update README.md with latest dev build links on every release.

### Documentation Updates

- **`adr-0001-known-gotchas.md`**: Added 6 new gotchas (version formats, project references, filename patterns, cross-distro builds, yay cache, EF Core translation)
- **`adr-0002-code-patterns.md`**: Updated dev build convention with comprehensive version conversion rules
- **`tasks.md`**: Updated with completed work
- **Cursor Memories**: Created 5 new memories for preventing bug recurrence

### Builds Pushed

- `dev-20251209-215513`: All 5 CI/CD fixes
- `dev-20251209-222346`: Backfill + scanner fixes

### Testing & Verification

- Upgraded kspls0 from old build (`0.24.1-dev.202512082233`) to latest (`0.24.1-dev-20251209-215541`)
- Verified DHT, mesh, and Soulseek connectivity working
- Confirmed backfill button now functional (was 500 error, now works)
- Verified scanner detection no longer spams logs with private IP warnings

---

## Historical Releases (from DEVELOPMENT_HISTORY.md)

| Bug | Status | Notes |
|-----|--------|-------|
| Async-void in RoomService | ✅ Fixed | Prevents crash on login errors |
| Undefined returns in searches.js | ✅ Fixed | Prevents frontend errors |
| Undefined returns in transfers.js | ✅ Fixed | Prevents frontend errors |
| Flaky UploadGovernorTests | ✅ Fixed | Integer division edge case |
| Search API lacks pagination | ✅ Fixed | Prevents browser hang |
| Duplicate message DB error | ✅ Fixed | Handle replayed messages |
| Version check crash | ✅ Fixed | Suppress noisy warning |
| ObjectDisposedException on shutdown | ✅ Fixed | Graceful shutdown |

