# First Pod & Social Framework Design

**Status**: DRAFT - Future Community Layer  
**Created**: December 11, 2025  
**Priority**: 🟡 MEDIUM (after core architecture, before public launch)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document describes:

- The **First Pod** (sometimes "F1000 Pod" during early testing) – the first canonical pod we bring online.
- The **social and community features** it runs by default:
  - Chat (Discord-like).
  - Forums (topics/threads).
  - Social feed / ActivityPub integration.
- The **framework** that allows future pods to add, extend, or remove these pieces in a modular way.

During early testing, the First 1000 (F1000) governance members will be **auto-joined** to this First Pod as part of the test program. That is a **social/governance choice**, not a hard technical dependency: pods remain sovereign, and this pod is not a control-plane.

Security and hardening are mandatory for all features described here.

---

## 1. The First Pod: What It Is

### 1.1 Definition

The **First Pod** is:

**A standard pod** as defined in `docs/pod-identity-lifecycle.md`:
- It has its own pod identity, keys, DB, config
- It can be backed up, transferred, retired, or destroyed like any other pod

**Preconfigured with:**
- Social modules:
  - `ChatModule` (Discord-like channels)
  - `ForumModule` (board/topic/thread)
  - `SocialFeedModule` (ActivityPub/Mastodon-style feeds)
- F1000-aware onboarding and access control

**It is the first reference implementation pod that:**
- Demonstrates how all these modules can be wired together
- Serves as a community home for F1000 members during the early phases
- Acts as a practical testbed for:
  - Social interactions
  - Governance/federation integration
  - Moderation and hardening

---

### 1.2 Not a Control Plane

The First Pod is **not**:
- ❌ A central control server for all pods
- ❌ A special MCP or identity authority
- ❌ Required for other pods to function

**Pods may:**
- ✅ Run without any social modules
- ✅ Run with some or all of the modules
- ✅ Federate with the First Pod, ignore it, or block it, based on their own policies

**The First Pod is "first" in history and configuration, not in technical authority.**

---

## 2. Pieces: Social Modules in the First Pod

The First Pod ships with a **small set of core social modules** that are also designed as a framework for future pods.

### 2.1 Module Model

Each social feature is implemented as a **module**:
- `ChatModule`
- `ForumModule`
- `SocialFeedModule` (ActivityPub)
- (Future) additional modules: polls, events, link-sharing, etc.

**Modules:**

**Are wired through:**
- A shared authentication/authorization layer
- MCP moderation hooks
- Logging/metrics that respect security guidelines

**Are configurable:**
- Can be enabled/disabled per pod
- Can be restricted (e.g., F1000-only channels/boards)

**MUST not directly access:**
- Low-level file-sharing components
- Pod identity keys
- MCP configuration

**Commitment:** The First Pod uses these modules as **examples** of how to compose them, not as special baked-in behavior that other pods must copy.

---

### 2.2 ChatModule (Discord-like Channels)

#### Features

**Channels:**
- Named rooms like `#general`, `#f1000-meta`, `#dev`
- Scoped visibility:
  - Public (if allowed)
  - F1000-only
  - Admin/moderator-only
  - Private groups (invite-based)

**Messages:**
- Text messages with optional structured metadata (links, references)
- Optional attachments (small files) if enabled
- Edit/delete with audit metadata

#### Interfaces

HTTP/REST + WebSocket for:
- Listing channels
- Joining channels
- Sending/receiving messages

#### Security/Hardening

**ACLs:**
- Role-based: `Admin`, `Moderator`, `F1000Member`, `User`, `Guest`
- Per-channel roles and membership lists

**Abuse protection:**
- Rate limiting per user/channel/IP
- MCP integration:
  - Optional content checks (text filters, abuse flags)

**Logging:**
- No storage of raw credentials or secrets
- Only structured, minimal audit logs (user ID, channel, timestamps, verdicts)

---

### 2.3 ForumModule (Boards / Topics / Threads)

#### Features

**Boards:**
- High-level categories (e.g., "Announcements", "Dev Notes", "Music Recs")
- Visibility and posting rules per board

**Topics/threads:**
- Hierarchical threads of posts
- Support for pinned posts, locked topics, and archival

#### Interfaces

- HTTP/REST API for board/topic/post operations
- Optionally integrated into the same UI shell as Chat

#### Security/Hardening

- Same role/ACL framework as Chat
- MCP integration:
  - Forums are prime candidates for text moderation and abuse detection
- Anti-spam:
  - Rate limits on new topics, replies, and edits
  - Optional cooldowns for new accounts

---

### 2.4 SocialFeedModule (ActivityPub / Mastodon-style)

#### Features

**ActivityPub-based social actor(s)** for the First Pod, as per `docs/social-federation-design.md`:
- `@f1000@firstpod` (optional shared "hub" actor)
- Per-user social actors (optional)

**Timeline/feed views:**
- Local feed: posts from local users/boards
- Federated feed: posts from followed remote actors/instances, if federation is enabled

#### Integration

**WorkRefs and library data:**
- The module can attach references to works (Music/Book/Video) to posts

**F1000:**
- F1000 members may get special badges/labels in the feed

#### Security/Hardening

**Federation is ALWAYS:**
- Explicitly configured (`Mode: Off | Hermit | Federated` as per social design)
- Default for the First Pod during early testing SHOULD be conservative (`Hermit` or curated `Federated`)

**ActivityPub handlers:**
- Must pass through validation and signature checks
- Must be rate-limited and protected against abuse

**MCP hooks:**
- Notes/posts can be moderated pre/post ingestion

---

## 3. Framework to Add More Later

The social modules in the First Pod define a **framework pattern**:

**Each module:**
- Registers routes/endpoints
- Hooks into:
  - Auth/identity
  - Roles/ACLs
  - MCP (for moderation)
  - Logging/metrics

**Additional modules can be added without redesigning the pod:**

Examples of future modules:
- `PollModule` – voting/polling tools
- `EventModule` – calendar/events
- `FileDropModule` – small attachment sharing with strong quotas and abuse controls
- `RecommendationModule` – feed of recommended works based on social + library signals

**Pods are free to:**
- Include only some modules
- Implement their own modules following the same patterns
- Remain purely "headless" with no social layer

---

## 4. F1000 Auto-Join for the First Pod

### 4.1 What "Auto-Join" Means Here

During early F1000 testing, governance policy states:

**F1000 membership implies First Pod membership.**

**Concretely:**

When a governance identity enters F1000 for a given epoch:

**The First Pod:**
- Pre-creates a **pending user** tied to that GovernanceId
- Assigns default roles:
  - F1000Member + base User role
  - Optionally additional roles (e.g., early tester)

**The holder of that GovernanceId:**
- Activates the account by proving control of their governance key (e.g., signing a challenge)
- No separate "claim account with email/password" flow is required or allowed for that mapping

**"Auto-join" is policy plus pre-provisioning, not:**
- ❌ Silent login
- ❌ Background access
- ❌ Forced connection to other pods

**If an F1000 member never uses the First Pod:**
- The pending account remains dormant
- No messages, posts, or federation occur for that identity

---

### 4.2 Non-F1000 Participation

The First Pod MAY allow:

**Non-F1000 users under a separate policy:**
- Open registration
- Invite-only
- F1000-sponsored invites

**These users:**
- Get normal accounts via the Pod's chosen auth method (password + 2FA, OIDC, etc.)
- Do not get any special F1000 governance powers by default

---

## 5. Hardening & Security Requirements

The First Pod and its modules MUST adhere to all relevant security/hardening docs, including:
- `docs/security-hardening-guidelines.md`
- `docs/pod-identity-lifecycle.md`
- `docs/moderation-v1-design.md`
- `docs/llm-mcp-design.md` (if it exists)
- `docs/f1000-governance-design.md` (for F1000-specific behavior)

Additional specific requirements:

### 5.1 Isolation

**Social modules MUST be logically isolated from:**
- ❌ Low-level file-sharing/transport code
- ❌ Pod key storage (`keys/`)
- ❌ MCP internal configuration and blocklists

**They may only:**
- ✅ Call MCP via typed interfaces for moderation
- ✅ Read minimal, necessary identity/role information from auth layers

---

### 5.2 Least Privilege

**Module services run under least-privilege configuration:**
- Minimal DB permissions needed for their own tables
- No direct access to secrets beyond what is needed for their job

**Role system:**
- Must be explicit and auditable
- No hidden or "backdoor" roles

---

### 5.3 Abuse & Spam Protection

**All public-facing endpoints:**
- Protected by rate limiting and abuse detection
- Subject to MCP moderation on content where appropriate

**For First Pod specifically during F1000 tests:**
- Higher default protections:
  - Slower ramps for new non-F1000 accounts
  - Tighter per-IP/per-account rates

---

### 5.4 Logging & Privacy

**Logs MUST:**
- Avoid PII wherever possible
- Never log secrets, passwords, private keys, or full moderation/LLM prompts

**Social content logs:**
- For debugging and moderation only
- Subject to retention policies (configurable, default-minimal)

---

### 5.5 Configuration & Opt-Out

**Each module:**
- Can be enabled/disabled via config
- Has separate configuration for:
  - Access levels
  - Federation behavior
  - MCP integration

**F1000 governance integration (auto-join) MUST be:**
- Explicit in config for the First Pod
- Optional for other pods

**Pods that do NOT want:**
- F1000 auto-join behavior, OR
- Social features at all

**MUST be able to run with:**
- All social modules disabled
- Governance integration disabled

---

## 6. Implementation Phases

### Phase 1: Core Framework (T-SOCIAL-01)
- Module registration system
- Shared auth/ACL layer
- MCP integration hooks
- Logging/metrics framework

### Phase 2: ChatModule (T-SOCIAL-02)
- Channel management
- Real-time messaging (WebSocket)
- Per-channel ACLs
- Rate limiting and abuse protection

### Phase 3: ForumModule (T-SOCIAL-03)
- Board/topic/thread data model
- REST API for CRUD operations
- Moderation tools (pin, lock, archive)
- Anti-spam measures

### Phase 4: SocialFeedModule (T-SOCIAL-04)
- ActivityPub actor(s) for First Pod
- Timeline/feed views (local, federated)
- WorkRef integration
- Federation hardening

### Phase 5: F1000 Auto-Join Integration (T-SOCIAL-05)
- Governance ID → First Pod user mapping
- Pending user pre-provisioning
- Challenge-response activation (prove control of governance key)
- Role assignment (F1000Member, early tester)

### Phase 6: Non-F1000 Participation (T-SOCIAL-06, Optional)
- Standard auth flows (password + 2FA, OIDC)
- Invite system
- Registration policies (open, invite-only, F1000-sponsored)

---

## 7. Summary

**The First Pod is our initial, canonical pod instance:**
- ✅ Equipped with chat, forums, and social feed
- ✅ Serving as a community home for F1000 testers
- ✅ Reference implementation for social modules

**The social features are implemented as modules:**
- ✅ Reusable and extensible for future pods
- ✅ Hardened by design
- ✅ Isolated from core pod systems

**F1000 membership during testing:**
- ✅ Implies an **auto-joined, pre-provisioned account** on the First Pod
- ✅ Activated via governance keys (challenge-response)
- ✅ Policy choice, not technical dependency

**Security and hardening:**
- ✅ Are non-negotiable
- ✅ Govern all module code, identity handling, and governance integration
- ✅ Complete separation from pod internals

**This design gives us:**
- ✅ A concrete, testable First Pod experience for early humans
- ✅ A clean modular framework for pods that follow
- ✅ Strong security and sovereignty guarantees for every pod in the network

---

## Related Documents

- `docs/pod-identity-lifecycle.md` - Pod identity, keys, and lifecycle management
- `docs/f1000-governance-design.md` - F1000 governance layer
- `docs/social-federation-design.md` - ActivityPub integration
- `docs/moderation-v1-design.md` - MCP design and hardening
- `docs/security-hardening-guidelines.md` - Global security principles
- `TASK_STATUS_DASHBOARD.md` - T-SOCIAL-01 through T-SOCIAL-06 tasks
