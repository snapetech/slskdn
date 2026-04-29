# Copyright and Branding Attribution Audit

Date: 2026-04-29

## Canonical Attribution

Use this wording wherever the surface is visible to users, package consumers,
or downstream maintainers:

> [slskdN](https://github.com/snapetech/slskdn) is an unofficial fork of
> [slskd](https://github.com/slskd/slskd).

For constrained package descriptions, use:

> slskdN, an unofficial fork of slskd

## Updated Surfaces

- Repository docs now state the fork relationship in `README.md`, `NOTICE`,
  `CONTRIBUTING.md`, and `CHANGELOG.md`.
- Web-visible branding now identifies slskdN as an unofficial fork in the HTML
  title, meta description, PWA manifest, document title, and footer links.
- API/runtime metadata now routes slskdN support to
  `https://github.com/snapetech/slskdn` and labels Swagger as `slskdN API`.
- Default public Soulseek profile text now identifies the account as a slskdN
  user and links the slskdN project.
- Package metadata now uses slskdN-first descriptions across Docker OCI labels,
  Debian, RPM, Chocolatey, Snap, Flatpak, Winget, Homebrew, Nix, AUR, Helm,
  TrueNAS, Unraid, and Synology packaging.
- Release templates and release metadata update scripts now generate
  slskdN-first fork attribution instead of generic "batteries included" copy.
- Package support links now point slskdN users at the slskdN repository and keep
  upstream slskd as a separate attribution/reference link where useful.

## Compatibility Names To Keep

The following names intentionally remain `slskd` because changing them would
break user installs, automation, or upstream-compatible API expectations:

- Binary and service compatibility paths such as `/usr/bin/slskd`,
  `/usr/lib/slskd/slskd`, `slskd.service`, `/etc/slskd/slskd.yml`, and
  `/var/lib/slskd`.
- Environment variable prefix `SLSKD_`, command-line option names, config keys,
  and API route shapes inherited from slskd.
- Internal namespace/project paths such as `src/slskd` and
  `src/slskd/slskd.csproj`.
- Prometheus metric names and local browser storage keys that existing
  dashboards or sessions may already depend on.

## References To Leave As Upstream

Links to `https://github.com/slskd/slskd` remain appropriate when they refer to
upstream history, upstream issue discussions, upstream sync workflows, or
slskd-compatible configuration behavior. Those references should be framed as
upstream/reference links, not as slskdN support channels.

## Copyright Notice Pattern

Existing upstream-authored files should preserve the `slskd Team` notice. When
slskdN-specific changes are present in those files, a separate `slskdN Team`
notice may be added after the upstream notice. New files owned by the fork use
the slskdN notice described in `AGENTS.md`.
