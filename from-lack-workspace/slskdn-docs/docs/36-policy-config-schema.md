# Policy Config (MVP) — Schema + Defaults

## File location

Per-workspace:
- `.lack/policy.yaml` (checked into repo optionally)

Per-user overrides:
- settings UI (stored in app config)

Effective policy = merge(user overrides, workspace policy) with user overrides winning.

---

## Defaults (recommended)

- IPC: response MAC required for spool
- Forge writes: approval required
- Jira writes: approval required
- Push: approval required unless trusted repo and not protected branch
- Protected branches: main/master/trunk/release/*
- Repo pinning enabled

---

## YAML schema (conceptual)

```yaml
policyVersion: 1

ipc:
  requireResponseMacForSpool: true
  ttlSeconds: 120
  replayWindowSeconds: 600

allowlists:
  forgeHosts:
    - github.com
    - gitlab.com
    - bitbucket.org
  jiraSites:
    - https://tenant.atlassian.net
  repos:
    - host: github.com
      owner: myorg
      repo: myrepo
      trusted: true

approvals:
  forge:
    prCreate: { require: true }
    prUpdate: { require: true }
    prComment: { require: true }
    prRequestReviewers: { require: true }

  jira:
    issueComment: { require: true }
    issueTransition: { require: true }
    issueCreate: { require: true }

  git:
    push:
      require: true
      autoAllowWhen:
        - repoTrusted: true
        - branchProtected: false
    rebase: { require: true }
    reset: { require: true }

protectedBranches:
  patterns:
    - main
    - master
    - trunk
    - "release/*"

repoPinning:
  enabled: true
```

---

## Implementation notes

* Patterns use gitignore-style globbing for branches.
* Host allowlist blocks connecting to unknown hosts unless user opts in.
* Repo allowlist + trust feeds approval auto-allow rules.
