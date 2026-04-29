# slskdN License Rollback Plan — Handoff Document

**Status:** Awaiting execution
**Notice received:** 2026-04-28 (issue snapetech/slskdn#221)
**30-day cure deadline:** ~2026-05-28
**Owner:** Keith / snapetech

This document is self-contained. An agent or human picking it up cold should be able to execute the plan end-to-end without further context from prior conversations.

---

## 1. Background

On 2026-04-28, upstream slskd author JP Dillingham (jpdillingham) opened issue #221 on snapetech/slskdn alleging license non-compliance. The substance:

1. Beginning with slskd 0.25.0, slskd's LICENSE includes Additional Terms under AGPLv3 §7. These add specific compliance obligations on forks: rebranding (including a name change away from anything resembling "slskd"), per-file `Modified:` headers, removal of all user-facing slskd branding, OCI image annotations, package-registry naming rules, and more.
2. Soulseek.NET 10.0+ (a separate work, also by jpdillingham, that slskd depends on) added §5 to its license requiring clients to use a unique minor version identifier on the Soulseek network. The reserved range 760–7699999 belongs to slskd. slskdN currently uses 760 (`src/slskd/Program.cs:166`), conflicting.
3. Upstream asserts our license terminated under AGPLv3 §8 and that we have a 30-day cure window before requiring explicit reinstatement.

## 2. Decision

**We are NOT adopting the post-0.25.0 Additional Terms.** Instead we are reverting our codebase to a pre-0.25.0 base, which was released under plain AGPLv3 only. The plain AGPLv3 cannot be retroactively modified; pre-0.25.0 code remains under the terms it was originally distributed under.

**We are keeping the slskdN name** because:
- Plain AGPLv3 imposes no such rebranding requirement.
- The drop-in-replacement use case (binary at `/path/to/slskd`, systemd units, scripts, configs) makes a binary rename actively user-hostile.
- §4 of the post-0.25.0 Additional Terms doesn't apply to code we're not using.

**We ARE complying with Soulseek.NET §5** independently — that's a separate license from a separate work, applies regardless of which slskd version we sit on, and the fix is one line of code.

**Trade we're accepting:** open-ended hard-fork maintenance burden going forward (no upstream slskd syncs ≥ 0.25.0 ever again, including security and protocol fixes; we re-implement what we need from descriptions, not copy-paste).

## 3. Scope of action

What we keep:
- Project name: **slskdN**
- Binary name: **slskd** (operational compatibility preserved)
- Package names: `slskdn`, `slskdn-bin`, etc. across all registries
- All our original work: mesh, DHT, SongID, discovery graph, multi-source, security hardening, etc.
- Distribution channels: AUR, COPR, PPA, Snap, Homebrew, Chocolatey, Winget, Nix, GHCR

What we lose / give up:
- All slskd 0.25.x changes (notably the native transfer retry/resume foundation)
- Future upstream slskd improvements (perpetually)
- The "based on slskd 0.25.1" parity narrative

What still needs work even after rollback:
- Soulseek.NET §5 minor-version fix (one line + a registry PR)
- Yanking 0.25.1-based binaries from every distribution channel and re-releasing from the 0.24.x base
- Replacing the upstream-slskd PNG used as our README logo (`README.md:2` references `https://raw.githubusercontent.com/slskd/slskd/master/docs/slskd.png`)
- AUR `provides=('slskd' 'slskd-bin')` and `replaces=('slskd' 'slskd-bin')` cleanup (independent trademark hygiene)

## 4. Boundary commits (verified at planning time)

- **Last clean pre-0.25.0 main HEAD:** `0cf819292` — release commit for `0.24.5-slskdn.181`. This is the rollback target.
- **First tainted commit:** `39a4f2c16` — `chore(upstream): record upstream slskd sync through 0.25.1`.
- **Sync merge:** `881453d29` (PR #217) — brought 4681 upstream commits and ~2300 net lines of code into the tree.
- **Current HEAD at planning time:** `fe3fffcb4`.
- **Post-sync work:** 19 commits between `881453d29..HEAD`, mostly docs/release/CI plus a few targeted fixes.

Re-verify these with `git log` before acting; if the tree has moved since this doc was written, the commits to cherry-pick may have changed.

## 5. Response to upstream (issue 221)

**Do not post until the technical rollback is in place.** Posting "we will do X" invites argument; posting "we have done X" closes the matter.

Draft response:

> Hi JP,
>
> Thanks for the detailed notice and for taking the time to lay out the specifics. We'd like to acknowledge receipt within the 30-day window and confirm our chosen path.
>
> Rather than adopt the post-0.25.0 Additional Terms, we're going to revert our codebase to a pre-0.25.0 base and continue independently from there. Our reading is that pre-0.25.0 slskd was released under plain AGPLv3 without those Additional Terms, and that license cannot be retroactively modified for code we already received under it. Going forward, we will not pull in changes from slskd 0.25.0 or later, and we will not be conveying any post-0.25.0 work.
>
> Concretely, within the 30-day cure window we will:
>
> 1. Revert our `main` branch to its pre-0.25.0 state and remove all post-0.25.0 upstream code from our distributions.
> 2. Yank current 0.25.1-based binaries from AUR, COPR, PPA, Snap, Homebrew, Chocolatey, Winget, Nix, and GHCR, and re-release from the 0.24.x-based codebase.
> 3. Update our README, NOTICE, and package descriptions to reflect that slskdN is based on slskd 0.24.x and does not track later releases.
>
> Separately, on Soulseek.NET §5: you're correct that our use of minor version 760 conflicts with the slskd reservation. We will change it to a unique value outside the reserved range and submit a PR to add our entry to the Soulseek.NET README registry. This is independent of the slskd license question.
>
> Because we will no longer be using post-0.25.0 code, we don't read §4 (rebranding/naming) of the post-0.25.0 Additional Terms as applying to our continued distribution of pre-0.25.0-based work. We'll continue to identify slskdN as a fork of slskd in our README and package descriptions per AGPLv3 §5(a) and the descriptive-use carve-outs that have always been customary in open-source forks.
>
> We appreciate the work you've put into slskd over the years and we wish the project well. If anything in the above is unclear or you'd like to discuss the specifics, we're available here.
>
> — Keith / snapetech

Tone notes:
- Light on legal argument. We are stating a path, not winning a debate.
- Does not concede past violation. Acknowledges his reading without ratifying it.
- Treats Soulseek.NET §5 as entirely separate — it legally is.
- No apology, no concession on points we don't agree with.
- If he replies challenging the legal theory, we don't have to engage; actions speak.

## 6. Public communications (alongside technical work)

- **README.md** — replace "based on slskd 0.25.1" badge with "based on slskd 0.24.x"; remove "slskdN stays aligned with that retry foundation" framing; replace upstream-slskd PNG logo (line 2) with a slskdN-original placeholder image.
- **NOTICE** — keep existing fork attestation; add: "slskdN tracks slskd up to v0.24.x and does not incorporate changes from slskd v0.25.0 or later."
- **CHANGELOG.md** — add an entry explaining the rollback to users.
- **One announcement** — pinned issue, release note, or Discord post explaining: (a) we're rolling back to 0.24.x base, (b) auto-replace may be temporarily limited until we re-implement retry plumbing, (c) the binary remains drop-in compatible, (d) upgrade path for users who already pulled a 0.25.1-based slskdN release.

## 7. Revert strategy: branch-based, non-destructive

Don't force-push or rewrite history on `main`. Preserve existing history as a record.

1. Tag current `main` HEAD as `archive/pre-rollback-0.25.1` (local only until reviewed).
2. Create branch `rollback/0.24.x` starting from `0cf819292`.
3. Cherry-pick onto it the post-sync commits that are NOT dependent on 0.25.1 upstream code:
   - **Likely safe (slskdN-original code):** `fe3fffcb4` (DHT YAML alias), `3b1d3b623` (release note size cap), `b169968d8` (CI publishing fix), `97f802e63` (Nix smoke gotcha), `156a8247d` (parity doc — needs rewrite anyway).
   - **Audit before cherry-picking (may touch upstream-modified files):** `17603b6ee` (shutdown cancellation noise), `aed1ba25a` (directory browse failures), `69da16e2e` (user directory timeouts).
   - **Skip (release-tracking only, will be regenerated):** all `chore(release)` commits.
4. On `rollback/0.24.x`, change `src/slskd/Program.cs:166` `SoulseekMinorVersion` from `760` to a unique value outside the slskd reserved range 760–7699999. Suggested placeholder: **1880**, but flag for human confirmation before submitting the Soulseek.NET registry PR.
5. Decide auto-replace strategy:
   - **(a) Strip the 0.25.1 dependency** — simplify auto-replace to use only pre-0.25.0 retry primitives. Loses some functionality. Recommended for the initial rollback release.
   - **(b) Reimplement retry/resume** — port just the retry foundation as our own work. More effort, full functionality preserved. Recommended as a follow-on.
6. Once `rollback/0.24.x` builds and tests cleanly, fast-forward `main` to it (or merge with a clear commit message). Do this only after human review.

## 8. Distribution cleanup matrix

For each channel, yank the current 0.25.1-based release and replace with a 0.24.x-based one. Hosting non-compliant binaries continues to be conveyance, so this matters for cure.

| Channel | Current state | Action |
|---|---|---|
| GitHub Releases | 0.25.1-slskdn.185 etc. | Mark releases ≥ first 0.25.x as deprecated/license-incompatible in release notes. Re-release from 0.24.x as `0.24.6-slskdn.1` (or similar). |
| GHCR (`ghcr.io/snapetech/slskdn`) | 0.25.1-based images | Untag/delete all 0.25.x tags; re-push from 0.24.x base. |
| AUR `slskdn-bin`, `slskdn-dev` | 0.25.1.slskdn.185 | Update PKGBUILDs to point at 0.24.x release artifacts. **Also remove `provides=('slskd' 'slskd-bin')` and `replaces=('slskd' 'slskd-bin')`** — those claim the slskd namespace and are independent trademark hygiene problems regardless of license version. Keep `provides=('slskdn')` only. |
| COPR `slskdn/slskdn` | 0.25.1-based RPMs | Build new RPMs from 0.24.x; remove old. |
| PPA `~snapetech/+archive/ubuntu/slskdn` | 0.25.1-based debs | Build new debs from 0.24.x; supersede old. |
| Snap `slskdn` | 0.25.1-slskdn.185 | New snap revision from 0.24.x. |
| Homebrew (`snapetech/homebrew-slskdn`) | 0.25.1 formula | Update formula to point at new 0.24.x release. |
| Chocolatey | 0.25.1-based | New package version from 0.24.x; old version unlisted. |
| Winget (`snapetech/slskdn`) | 0.25.1-based manifest | Submit new manifest for 0.24.x; old manifest stays (winget manifests are point-in-time). |
| Nix flake | 0.25.1-based | Update flake to new release artifact. |

Keep records of when each yank happened in case it's ever needed as evidence of cure.

## 9. Source-file and metadata hygiene (under plain AGPLv3)

Plain AGPLv3 §5(a) requires "prominent notices stating that you modified it, and giving a relevant date" — but it's far less prescriptive than the post-0.25.0 §3(c). Existing patterns are mostly acceptable; **no per-file `Modified: <description>` sweep is required.**

Things to verify regardless:

- `LICENSE` (plain AGPLv3 only — strip the post-0.25.0 Additional Terms section since we're not using post-0.25.0 code) ships in the root of every binary archive and Docker image.
- `Dockerfile:91` — `org.opencontainers.image.licenses=AGPL-3.0` is fine for plain AGPLv3.
- `README.md:2` — replace upstream-slskd PNG logo URL with our own image.

**Important:** The current root `LICENSE` file in this repo includes the post-0.25.0 Additional Terms text starting at line 664. After rollback, that section should be **removed** from our LICENSE file — we're explicitly not under those terms anymore. The LICENSE distributed with our 0.24.x-based fork should be plain AGPLv3 only.

### Co-attribution / header sweep (low priority, can ship without)

The one place plain AGPLv3 §5(a) is technically not satisfied: files where we modified upstream slskd code AND replaced the original `company="slskd Team"` copyright header with `company="slskdN Team"`. The upstream copyright notice should have been preserved alongside ours, not overwritten.

This is not a 30-day cure-window emergency, but it's tidy-up worth doing. To find the affected files:

```bash
git log --diff-filter=M --name-only --pretty=format: 0cf819292..HEAD \
  | sort -u \
  | xargs grep -L 'company="slskd Team"' 2>/dev/null \
  | xargs grep -l 'company="slskdN Team"' 2>/dev/null
```

Fix pattern: restore the original `company="slskd Team"` header line and add ours below it (e.g., `// Copyright (c) <year> slskdN Team. All rights reserved.` as a separate copyright line). No `Modified: <description>` line is required — that was a post-0.25.0 invention.

Files entirely new in slskdN (no upstream ancestry) keep their single `company="slskdN Team"` header — fine as-is.

### .NET 10 — keep as-is

Our pre-rollback target (`0cf819292`) already specifies `<TargetFramework>net10.0</TargetFramework>`. .NET 10 was an independent slskdN engineering choice; upstream slskd 0.24.5 targeted net8.0, but we diverged forward on the slskdN side. **The rollback agent should NOT revert to net8.0** — keep .NET 10.

Note: `global.json` is gitignored (`.gitignore:45`), so it's never tracked by git and persists across branch operations automatically. No special handling needed.

## 10. Going-forward discipline

- **No more upstream syncs** from slskd ≥ 0.25.0. Disable or repurpose any `sync/upstream-*` workflow / automation that pulled upstream branches.
- Security fixes from upstream slskd ≥ 0.25.0 must be **re-implemented from a description**, not copy-pasted.
- Stay on Soulseek.NET 10.x and comply with §5 (one-line uniqueness fix). No need to revert that dependency.
- Set a calendar reminder ~6 months out to revisit whether the strategy is still right or whether costs have shifted enough to reconsider the rename.

## 11. Sequence and timing (within 30-day cure window, started 2026-04-28)

1. **Days 1–3** — Run the rollback agent (prompt in §13). Get rollback branch ready.
2. **Days 3–7** — Human review of the rollback branch. Decide auto-replace strategy. Land rollback to `main`.
3. **Days 7–14** — Build and publish 0.24.x-based release artifacts. Yank 0.25.1-based artifacts from each channel.
4. **Day 14 or so** — Post the issue 221 response *after* rollback is in place and binaries are replaced. Not before.
5. **Days 14–28** — Tail: registry cleanups, Soulseek.NET PR for the new minor version, public communications, Discord/announcements.

## 12. Risks and watchpoints

- **"But you already distributed it" angle.** jpdillingham could argue past 0.25.1-based releases were infringing and demand more than future cure. AGPLv3 §8 cure language covers ongoing violations once you stop them; doesn't time-travel to past distributions. Yanking the binaries is the strongest signal of cure. Document timestamps.
- **Auto-replace temporarily degraded.** Users will notice. Get ahead of it in release notes.
- **Hard-fork debt accrues.** Calendar reminder at 6 months.
- **Soulseek.NET registry PR** — get the new minor version registered before publishing the new release, so the registry shows the slskdN entry as legitimate. Makes §5 compliance visible.
- **Don't engage the issue thread in a back-and-forth.** Post once when action is complete, then leave it.
- **Past releases on GitHub** that contain 0.25.x-derived code: after re-releasing from 0.24.x, mark the 0.25.1-based releases as `pre-release` and edit their release notes to indicate license incompatibility, but don't outright delete them (they're part of the project's audit trail). Untag any container images though.

## 13. Agent prompt for the rollback execution

Hand this to a fresh agent in a new session:

```
Implement the slskdN rollback to a pre-0.25.0 slskd base. Context: we received a license-compliance notice on issue 221 from upstream (snapetech/slskdn#221) regarding new Additional Terms in slskd 0.25.0+. Rather than adopt those terms, we're rolling back to pre-0.25.0 code which is governed by plain AGPLv3.

Full plan and rationale: read memory-bank/license-rollback-plan.md before starting.

Boundary commits (verify these still match before acting):
- Last clean pre-0.25.0 main HEAD: 0cf819292 (0.24.5-slskdn.181)
- First tainted commit: 39a4f2c16 (sync entry point)
- Sync merge: 881453d29 (PR #217)
- Current HEAD at planning time: fe3fffcb4

Tasks:
1. Tag current main as `archive/pre-rollback-0.25.1` (LOCAL only — do not push).
2. Create branch `rollback/0.24.x` from 0cf819292.
3. Audit the 19 post-sync commits between 881453d29..HEAD for ones safe to cherry-pick onto the rollback branch. Specifically evaluate: fe3fffcb4, 3b1d3b623, b169968d8, 97f802e63, 17603b6ee, aed1ba25a, 69da16e2e, 156a8247d. Skip all `chore(release)` commits. Report which cherry-picks succeed cleanly and which conflict.
4. On the rollback branch, change src/slskd/Program.cs SoulseekMinorVersion from 760 to a value outside the slskd reserved range 760-7699999. Use 1880 as a placeholder; flag for human confirmation before final.
5. Audit auto-replace code for dependencies on the 0.25.1-introduced transfer retry/resume primitives. Likely under src/slskd/Transfers/. Report which symbols/types are 0.25.1-origin and need either reimplementation or feature scope-down.
6. Audit AUR PKGBUILDs (packaging/aur/PKGBUILD-bin and PKGBUILD-dev). Remove `provides=('slskd' 'slskd-bin')` and `replaces=('slskd' 'slskd-bin')` lines. Keep `provides=('slskdn')` only.
7. Update LICENSE in the rollback branch: remove the post-0.25.0 Additional Terms section (lines 664-906 in the current file, which begin with the `-----` separator and "ADDITIONAL TERMS" header). Keep only the plain AGPLv3 text above that.
8. Update README.md to remove "based on slskd 0.25.1" framing and replace the upstream-slskd PNG logo reference (README.md:2) with a placeholder note for a slskdN-original logo.
9. Update NOTICE to add: "slskdN tracks slskd up to v0.24.x and does not incorporate changes from slskd v0.25.0 or later."
10. Confirm `<TargetFramework>net10.0</TargetFramework>` in `src/slskd/slskd.csproj` is retained on the rollback branch. Do NOT downgrade. .NET 10 is our independent engineering choice. (`global.json` is gitignored and persists automatically — no action needed there.)
11. Verify the rollback branch builds and tests pass. Report failures honestly — don't paper over them.

DO NOT:
- Force-push or rewrite main's history.
- Delete any release artifacts or container images.
- Yank any packages from registries.
- Post anything on the issue 221 thread.
- Push any tags or branches to the remote.

All of those require human confirmation. Stop and report when the rollback branch is ready for review.

Output: a summary of what was done, what conflicts remain, what auto-replace dependencies need a decision, and a list of next steps that require human action (release yanking, publishing the new branch, posting the issue 221 reply, Soulseek.NET registry PR).
```

## 14. Quick-reference checklist

For tracking progress through the cure window:

- [ ] Rollback branch created (`rollback/0.24.x` from `0cf819292`)
- [ ] Post-sync commits audited; cherry-picks completed
- [ ] `Program.cs:166` SoulseekMinorVersion changed to unique value
- [ ] Auto-replace dependency audit complete; strategy chosen (a or b)
- [ ] AUR PKGBUILDs cleaned (`provides`/`replaces` slskd entries removed)
- [ ] LICENSE updated (Additional Terms section removed)
- [ ] README/NOTICE updated
- [ ] README upstream-slskd PNG replaced
- [ ] `<TargetFramework>net10.0</TargetFramework>` confirmed retained in `src/slskd/slskd.csproj`
- [ ] Optional: header sweep for files where `slskd Team` was overwritten (low priority)
- [ ] Branch builds and tests pass
- [ ] `main` updated from rollback branch
- [ ] 0.24.x-based release built
- [ ] Soulseek.NET PR opened to register new minor version
- [ ] GHCR 0.25.x tags untagged
- [ ] AUR packages updated
- [ ] COPR rebuilt
- [ ] PPA rebuilt
- [ ] Snap revision pushed
- [ ] Homebrew formula updated
- [ ] Chocolatey package updated
- [ ] Winget manifest submitted
- [ ] Nix flake updated
- [ ] CHANGELOG entry written
- [ ] User announcement posted (Discord/release notes)
- [ ] Issue 221 response posted (only after all of the above)
- [ ] Calendar reminder set for 6-month strategy review
