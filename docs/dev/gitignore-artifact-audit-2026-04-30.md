# Gitignore And Artifact Audit - 2026-04-30

Scope: repository ignore coverage for generated outputs, scratch directories,
test artifacts, local runtime files, package artifacts, and VPN/WireGuard
secrets.

## Summary

The repository now ignores generated and local artifact paths recursively, not
just at the repo root or under one known project directory. This covers
temporary directories, test-result directories, Playwright reports, coverage
outputs, build `obj` directories, logs, local databases, package bundles, and
VPN configuration secrets.

Validation:

```bash
git ls-files -o --exclude-standard
```

Result: no visible untracked files after the ignore update.
If this document has not been committed yet, it is the expected visible
untracked source file.

## Ignore Coverage Added Or Confirmed

New recursive coverage:

- `**/obj/`
- `**/[Tt]mp/` and `**/[Tt]mp*/`
- `**/[Tt]emp/` and `**/[Tt]emp*/`
- `**/[Aa]rtifact/`, `**/[Aa]rtifacts/`, and `**/*[Aa]rtifact*/`
- `**/[Tt]est[Rr]esults/` and `**/[Tt]est-[Rr]esults/`
- `**/playwright-report/`
- `**/coverage/`
- `**/.nyc_output/`

New local file coverage:

- `*.log`
- `*.pid`
- `*.trx`
- `*.coverage`
- `*.sqlite`, `*.sqlite-shm`, `*.sqlite-wal`
- `*.db`, `*.db-shm`, `*.db-wal`
- `*.bak`
- `*.tmp`
- `*.temp`
- `*.nupkg`
- `*.snupkg`
- `*.tgz`
- `*.tar.gz`

Existing useful coverage confirmed:

- Web and E2E Playwright output directories.
- `dist/`, `logs/`, `node_modules/`, and `test-artifacts/`.
- `src/slskdN.VpnAgent/bin/` and `src/slskdN.VpnAgent/obj/`.
- Local VPN and WireGuard configuration files, with only redacted examples
  allowed.

## Intentional Non-Coverage

The audit did not add a broad `**/bin/` ignore rule. This repository has a
tracked root `bin/` directory containing source-controlled helper scripts:

- `bin/build`
- `bin/cover`
- `bin/lint`
- `bin/lint-docs`
- `bin/publish`
- `bin/run`
- `bin/watch`

A recursive `bin/` ignore would make it too easy to miss future legitimate
scripts.

## Remaining Tracked Artifact Debt

`.gitignore` only affects untracked files. The audit found generated-looking
paths that are already tracked:

- `publish/` contains 429 tracked files, including compiled assemblies and
  generated web assets.
- `docs/archive/test-artifacts/orphaned_methods.txt`
- `docs/archive/test-artifacts/test-suspicious-path.exe`

Recommended cleanup:

1. Remove `publish/` from the index if it is not intentionally source-controlled:

   ```bash
   git rm -r --cached publish
   ```

2. Keep the archived test artifacts only if they are intentionally historical.
   Otherwise remove them from the index or replace them with a text note
   explaining what the archive represented.

3. Keep release bundles outside git, or attach them to CI build artifacts and
   GitHub releases instead of committing generated publish output.
