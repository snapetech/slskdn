# Adapter Skeletons (ForgeHub + IssueHub) — MVP

## Goal
Provide implementable skeletons for:
- Forge providers: GitHub/GitLab/Bitbucket/Generic
- Issue providers: Jira

Adapters must:
- be deterministic
- expose capabilities
- centralize auth + token storage (via Hub)
- return canonical data shapes
- support caching and rate-limit handling

---

# Part A — Common adapter patterns

## Capability declaration
Adapters must declare supported capabilities at startup based on:
- provider type
- connection scope (read vs write)
- server version (for GitLab/Jira self-hosted)

Example:
- If token lacks write scopes → omit PRCreate, PRComment, etc.

## Error model (canonical)
Adapters return errors as:
- `UNSUPPORTED` (capability not available)
- `AUTH_REQUIRED` (missing/expired token)
- `FORBIDDEN` (token lacks scope / permission)
- `NOT_FOUND`
- `RATE_LIMITED`
- `UPSTREAM_ERROR` (5xx, timeouts)

All errors include:
- providerId
- endpoint label (not URL)
- http status (if applicable)
- request correlation id (if available)

## HTTP client requirements
- timeouts: connect 3s, request 15s (configurable)
- retry with backoff on:
  - 429
  - transient 5xx
- never retry non-idempotent writes automatically unless safe (comment create usually idempotent only if you provide idempotency key)

## Logging
- log request metadata (method, endpoint label, status, duration)
- never log tokens
- optionally log a redacted URL host/path without query

---

# Part B — Canonical data shapes

## Repo (Forge)
- `host`, `ownerPath`, `name`
- `defaultBranch`
- `webUrl`
- `cloneUrls { ssh, https }`

## Pull Request / Merge Request
- `number` (or iid)
- `title`, `body`
- `state` (open/closed/merged)
- `baseBranch`, `headBranch`, `headSha`
- `author`
- timestamps
- `webUrl`
- `filesChanged[]` (filenames; optionally with stats)
- `reviewers[]` (best-effort)

## Checks
- `refSha`
- `overall` (success/failure/pending/unknown)
- `jobs[]` (name, state, url)

## Issue (Jira)
- `key`
- `summary`
- `description` (rendered/plain)
- `status`
- `assignee`
- `labels[]`
- `webUrl`

---

# Part C — Forge adapters (skeleton)

## IForgeAdapter interface (conceptual)

```cpp
class IForgeAdapter {
public:
  virtual QString providerId() const = 0;
  virtual QVector<ForgeCapability> capabilities() const = 0;
  virtual ProviderHealth health() = 0;

  // Auth
  virtual AuthStartResult beginAuth(AuthMode mode) = 0;
  virtual AuthFinishResult finishAuth(AuthFinishInput in) = 0;

  // Data
  virtual QVector<ForgeRepo> listRepos(ListReposQuery q) = 0;
  virtual QVector<ForgePullRequest> listPRs(ForgeRepoRef repo, ListPRQuery q) = 0;
  virtual ForgePullRequest getPR(ForgeRepoRef repo, int number) = 0;

  // Writes (gated by Toolbus)
  virtual CreatePROutput createPR(ForgeRepoRef repo, CreatePRInput in) = 0;
  virtual void commentPR(ForgeRepoRef repo, int number, QString text) = 0;
  virtual void requestReviewers(ForgeRepoRef repo, int number, QStringList reviewers) = 0;

  // CI/Checks
  virtual ChecksStatus getChecks(ForgeRepoRef repo, QString ref) = 0;
};
```

### GitHubAdapter notes

* Prefer API v3 REST or GraphQL for some fields; either is fine.
* Required features:

  * repos list
  * PR list/get/create
  * PR comments
  * reviewers request
  * checks status summary

### GitLabAdapter notes

* Support `gitlab.com` and self-hosted by base URL.
* Group path nesting is common; preserve `ownerPath`.
* Merge requests have `iid` and internal numeric IDs; store canonical `number=iid`.

### BitbucketAdapter notes

* Implement PR list/get/create/comment as must-have.
* Reviewer request and checks are best-effort; declare capability if supported.

### GenericAdapter notes

* Minimal: health + repo identity match, no repo list.
* Optional configuration can enable PR endpoints later.

---

# Part D — Issue adapters (skeleton)

## IIssueAdapter interface (conceptual)

```cpp
class IIssueAdapter {
public:
  virtual QString providerId() const = 0; // "jira"
  virtual QVector<IssueCapability> capabilities() const = 0;
  virtual ProviderHealth health() = 0;

  virtual AuthStartResult beginAuth(AuthMode mode) = 0;
  virtual AuthFinishResult finishAuth(AuthFinishInput in) = 0;

  virtual QVector<IssueProject> listProjects(ProjectQuery q) = 0;
  virtual QVector<Issue> searchIssues(IssueSearchQuery q) = 0;
  virtual Issue getIssue(QString keyOrId) = 0;

  // Writes (gated)
  virtual void commentIssue(QString keyOrId, QString text) = 0;
  virtual QVector<IssueTransition> listTransitions(QString keyOrId) = 0;
  virtual void transitionIssue(QString keyOrId, QString transitionId) = 0;
  virtual Issue createIssue(CreateIssueInput in) = 0;
};
```

### JiraAdapter notes

* Cloud vs Data Center: detect by base URL and capabilities.
* Use JQL if provided; otherwise map simple search to JQL best-effort.
* Transitions require fetching available transitions first.

---

# Part E — Adapter packaging

## Compile-time plugins (MVP recommended)

Keep it simple:

* build adapters into the app binary
* behind feature flags

## Runtime plugin ABI (later)

Introduce stable ABI only when necessary; MVP should not.

---

# Part F — Tests

* Stub HTTP servers with recorded fixtures per provider
* Capability matrix tests: token scopes -> capabilities
* Normalization tests: remote identity parsing for each provider
