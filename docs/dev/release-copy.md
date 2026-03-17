# Release Copy Budgets

This repo has multiple release-description surfaces with different practical limits.

## GitHub Release Bodies

- Source of truth: [`.github/release-notes/main.md.tmpl`](/home/keith/Documents/code/slskdn/.github/release-notes/main.md.tmpl) and [`.github/release-notes/dev.md.tmpl`](/home/keith/Documents/code/slskdn/.github/release-notes/dev.md.tmpl)
- Rendered by: [`packaging/scripts/render-release-notes.sh`](/home/keith/Documents/code/slskdn/packaging/scripts/render-release-notes.sh)
- Enforced budget:
  - main: `<= 4800` chars
  - dev: `<= 4200` chars

GitHub does not publish a tight small cap for release bodies, so these are internal readability budgets chosen to keep release pages scannable while still highlighting the fork's first-class features.

## Winget Default Locale Copy

- Source files:
  - [`packaging/winget/snapetech.slskdn.locale.en-US.yaml`](/home/keith/Documents/code/slskdn/packaging/winget/snapetech.slskdn.locale.en-US.yaml)
  - [`packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml`](/home/keith/Documents/code/slskdn/packaging/winget/snapetech.slskdn-dev.locale.en-US.yaml)
- Official schema:
  - `ShortDescription <= 256`
  - `Description <= 10000`
  - `ReleaseNotes <= 10000`

The validator enforces the real Winget `ShortDescription` cap and also requires the copy to mention `SongID` and `Discovery Graph`.

## Update Rule

When a user-facing marquee feature changes, update:

1. the shared GitHub release-note templates
2. the Winget locale descriptions if the product positioning changed
3. the validator expectations if the branded first-class features change

Run:

```bash
bash packaging/scripts/validate-release-copy.sh
bash packaging/scripts/validate-packaging-metadata.sh
```
