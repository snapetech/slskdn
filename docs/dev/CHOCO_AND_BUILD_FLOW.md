# Chocolatey and Build Flow (for AI / future reference)

## Does a separate "nupkg CI task" run when we fire the build?

**No.** When you push a tag like `build-dev-0.24.1.dev.9...`:

- **Only `build-on-tag.yml` runs.**  
  `ci.yml` is **not** triggered by `build-dev-*`; it runs on version tags like `0.24.1-slskdn.40`.

- **The .nupkg is created inside the Chocolatey job.**  
  There is no other workflow that produces a nupkg. In `build-on-tag.yml`, the `chocolatey-dev` job:
  1. Downloads `slskdn-dev-win-x64.zip` from the GitHub release (created earlier in the same run by `release-dev`).
  2. Runs `choco pack` in `packaging/chocolatey` to produce the .nupkg.
  3. Runs `choco push` to publish that .nupkg to Chocolatey.org.

So the build does **not** need to reference anything from a separate nupkg CI task. The only input Choco needs is the **Windows zip from the release**, which is produced in the same workflow by the publish matrix and uploaded in `release-dev`.

## Dependency and timing

- **chocolatey-dev** has `needs: [parse, release-dev]`, so it only runs after:
  - `release-dev` has finished (and thus the release + zip assets exist).
- `release-dev` uses `softprops/action-gh-release` with `files: release/*`; the step completes only after the release and all assets are uploaded.
- So when `chocolatey-dev` runs `gh release download`, the release and `slskdn-dev-win-x64.zip` should already be there.

If you ever see "asset not found" or similar, it could be GitHub API/replication delay. The Chocolatey job can add a short retry loop around `gh release download` (e.g. 3 attempts, 15s apart) to rule that out.

## What to fix when Choco fails

Choco failures have been due to **argument parsing** (path + flag glued), not timing or missing nupkg from another workflow. See `memory-bank/decisions/adr-0001-known-gotchas.md` (gotcha 5b, 21) for what was tried and the current approach (no path to `choco push`; run from `packaging/chocolatey` after `choco pack`).
