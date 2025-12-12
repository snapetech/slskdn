# F1000 Governance Design (First 1000)

**Status**: DRAFT - Future Governance Layer  
**Created**: December 11, 2025  
**Priority**: 🟢 LOW (future layer, post-core architecture)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines the "First 1000" (F1000) governance layer and the concept of **governance identities**.

## Goals

- Provide a **clean, minimal governance layer** that sits above pods, MCP, and social federation.
- Reward early humans who join and help test with **persistent, meaningful, optionally transferable roles**.
- Distribute **trust and advisory power** across real people, without creating a centralized root of control over pods.
- Allow **master-admins** to:
  - Manually assign F1000 memberships up to a configured cap (e.g. 1000, 10001).
  - Hold F1000 status without counting against that cap.
- Ensure:
  - Local pods remain **sovereign**.
  - F1000 signals are **advisory**, not mandatory.
  - Governance roles and transfers are **heavily hardened** and auditable.

---

## 1. Concepts and Scope

### 1.1 Governance Identity (Per Human)

A **governance identity** represents a human, not a pod or account.

**Each governance identity has:**
- A cryptographic keypair `(pub_gov, priv_gov)`
- A canonical ID: `gov:<hash(pub_gov)>` (hash algorithm TBD, but collision-resistant and stable)

**A human MAY have:**
- Many pods / ActivityPub actors
- Many backend accounts (Soulseek, etc.)
- Exactly one primary governance identity for F1000 and governance purposes

**Governance private keys SHOULD be stored and controlled:**
- Preferably off-pod (hardware token, password manager, etc.)
- Only temporarily loaded into tools/clients for signing

**Compromise of a governance key** is a serious event; key rotation and revocation MUST be supported (see § 3.3).

---

### 1.2 Governance Root & Master-Admins

The system has a **governance root identity** at boot:
- `gov_root:<hash(pub_root)>`, controlled initially by the founder

We define **master-admins** as governance identities with a special role:
- `Role.MasterAdmin` (or equivalent)
- They collectively control:
  - F1000 membership (up to a hard cap)
  - Governance policy profiles
  - Governance role delegation and evolution

**Key constraints:**

**Master-admin power is scoped to the governance data layer only:**
- F1000 registry
- Policy profiles and governance docs

**Master-admins have NO direct authority over:**
- Individual pods' local configs
- MCP behavior on pods
- Local blocklists or moderation

**Pods can choose to follow or ignore** governance outputs at any time.

**Master-admins MAY also be F1000 members, but:**
- Master-admin identities **do not count** towards the configured F1000 cap (see § 2.1)
- This ensures:
  - The founder and other master-admins can always be considered "in" F1000 (for governance rights/visibility) without consuming scarce slots used to reward other humans

---

### 1.3 F1000 (First 1000) Membership

The **First 1000 (F1000)** is:
- A set of up to `F1000_CAP` governance identities (default recommendation: 1000, but the value is configurable and may be set to 10001 or similar)
- Recognized via a signed **F1000 registry** for a given epoch
- Intended to represent:
  - Early adopters who helped test/build
  - A distributed governance advisory body

**F1000 membership is:**
- Attached to governance identities (`gov:<hash(pub_gov)>`), not pods
- **Transferable** under strict rules (see § 3.2)
- Non-duplicative: At most one active F1000 membership per governance identity in a given epoch

**Additional rule:**
- Identities with `Role.MasterAdmin` are treated as F1000 for governance purposes, even if:
  - They are not currently occupying a numeric F1000 slot, OR
  - They do occupy a slot, but for cap accounting they must **not** count toward `F1000_CAP`

---

### 1.4 Advisory, Not Mandatory

F1000 exists **above** the technical layer and is:

**Used as an advisory governance signal:**
- Curated human moderation feeds
- Signed policy profiles
- Governance votes/recommendations

**Pods remain sovereign:**

Each pod can:
- Subscribe to F1000/governance signals
- Apply them as:
  - Inputs to MCP (e.g., HumanModerationProvider)
  - Default policy profiles
  - UI hints ("F1000 recommends …")
- Ignore or override them entirely

**Nothing in this design allows F1000 or master-admins to:**
- Log into pods
- Change pod configs remotely
- Override local admin decisions

---

## 2. F1000 Registry, Epochs, and Cap

### 2.1 F1000 Cap and Master-Admin Exception

We define a configuration parameter:

**`F1000_CAP`:**
- Maximum number of **non-master-admin** F1000 member slots in an epoch
- Default recommendation: `1000`
- May be set higher (e.g., `10001`) by governance decision, but MUST be treated as a hard upper bound

**Rules:**

At any time in a given epoch:
- There are at most `F1000_CAP` **active non-master-admin members**
- Any governance identity that has `Role.MasterAdmin` may be treated as F1000 "in spirit" without counting toward `F1000_CAP`
- If a master-admin is also listed in the F1000 registry, implementations MUST treat them as **cap-exempt** for the purpose of enforcement (i.e., do not reject new members because a master-admin occupies a numeric slot)

**This allows:**

Humans to hold both:
- A master-admin role
- A standard F1000 slot (if desired for recognition)

Without reducing the number of normal F1000 slots available to others.

---

### 2.2 F1000 Epochs

F1000 membership is defined per **epoch**:

```csharp
public class F1000Epoch
{
    public int EpochId { get; init; }          // Starting at 1
    public DateTime CreatedAt { get; init; }
    public string Description { get; init; }
    public int F1000Cap { get; init; }         // Cap for this epoch
    public string[] SignedBy { get; init; }    // Governance root and/or council
}
```

**Motivation:**
- Allows:
  - A clean "start" for initial F1000 (epoch 1)
  - Future resets/expansions as new epochs (2, 3, …) with their own registries and caps

**Pods:**
- SHOULD default to the latest active epoch, unless configured otherwise
- MAY ignore deprecated epochs entirely

---

### 2.3 F1000 Registry Structure

For a given epoch, the F1000 membership registry is a signed document (or small set of documents):

```csharp
public class F1000Registry
{
    public int EpochId { get; init; }
    public int F1000Cap { get; init; }
    public F1000Entry[] Entries { get; init; }
    public RegistrySignature[] RegistrySignatures { get; init; }
}

public class F1000Entry
{
    public int Slot { get; init; }                    // Nominal slot number
    public string GovernanceId { get; init; }         // gov:<hash(pub_gov)>
    public F1000Status Status { get; init; }          // Active, Revoked, Transferred
    public F1000Metadata Metadata { get; init; }
    public EntrySignature[] Signatures { get; init; }
}

public enum F1000Status
{
    Active = 0,
    Revoked = 1,
    Transferred = 2,
}

public class F1000Metadata
{
    public string? DisplayName { get; init; }
    public string? PreferredActorUrl { get; init; }   // Optional AP actor
    public DateTime AddedAt { get; init; }
    public string? Notes { get; init; }
}

public class EntrySignature
{
    public string SignedBy { get; init; }             // Governance signer with role
    public byte[] Signature { get; init; }
}

public class RegistrySignature
{
    public string SignedBy { get; init; }             // Governance root and/or council
    public byte[] Signature { get; init; }            // Over entire registry or merkle root
}
```

**Enforcement and security:**

- At most `F1000_CAP` entries with `Status = Active` AND `GovernanceId` that is **not** a master-admin
- Master-admin identities MAY appear in the registry but MUST be treated as **cap-exempt**
- All entries must be:
  - Signed by a valid registrar/master-admin according to governance rules
  - Covered by registry-level signatures from root/council

**The registry can be published via:**
- Static hosting (HTTPS)
- ActivityPub governance actors
- Or both, with versioning and integrity checks

---

### 2.4 Founder Guarantee

We guarantee that the **founder** governance identity:
- Is always eligible to be F1000, even if the cap is reached
- Does not permanently consume one of the cap-limited slots unless desired

**In `F1000Epoch` with `epoch_id = 1`:**

We define:
- A special entry for the founder governance ID, either:
  - As `slot = 1`, but cap-exempt due to `Role.MasterAdmin`, or
  - As a separate "founder" record referenced by the epoch

**In effect:**

The founder's `gov_root` identity:
- Has `Role.MasterAdmin` from the beginning
- Is treated as F1000 by role, regardless of whether a numeric slot is occupied

If the founder wishes, they can:
- Occupy a numeric slot (for aesthetic reasons)
- Transfer that slot later (see § 3.2), while still remaining F1000-equivalent via `Role.MasterAdmin`
- Eventually step down from master-admin via governance delegation/transition

**This ensures:**
- The founder is always allowed "in the club" even if the slots are full
- Transferability of actual F1000 slots does not threaten founder's baseline governance presence

---

## 3. Governance Roles, Transferability, and Hardening

### 3.1 Governance Roles & Delegation

We define governance roles, including:

- `root` – Initial founder root; can bootstrap epochs and initial delegations
- `master_admin` – High-trust role; can:
  - Assign/revoke F1000 memberships (within cap rules)
  - Sign policy profiles
  - Create/approve governance transitions
- `registrar` – Can manage F1000 entries under master-admin supervision
- `policy_signer` – Can sign policy profiles

**Roles are granted via delegation documents:**

```csharp
public class Delegation
{
    public string From { get; init; }                 // gov_root:<...> or council multi-sig
    public string To { get; init; }                   // gov:<hash(pub_gov)>
    public GovernanceRole Role { get; init; }
    public string Scope { get; init; }                // f1000_registry, policy_profiles, all
    public DateTime? ExpiresAt { get; init; }
    public byte[] SignatureByFrom { get; init; }
}

public enum GovernanceRole
{
    Root = 0,
    MasterAdmin = 1,
    Registrar = 2,
    PolicySigner = 3,
}
```

**Hardening constraints:**

**Delegations:**
- MUST be signed by a recognized root/council identity (or multi-sig)
- MUST have explicit scopes and optional durations

**Pods:**
- MUST validate delegation chains and expiration before accepting governance actions

---

### 3.2 Transferable F1000 Membership

F1000 membership is **transferable**, but under **strict, auditable rules**:

**Transfers are represented as:**

```csharp
public class F1000Transfer
{
    public int EpochId { get; init; }
    public string FromGovernanceId { get; init; }     // gov:<...>
    public string ToGovernanceId { get; init; }       // gov:<...>
    public int? Slot { get; init; }                   // Optional, for reference
    public DateTime InitiatedAt { get; init; }
    public TransferSignature[] Signatures { get; init; }
}

public class TransferSignature
{
    public string SignedBy { get; init; }
    public byte[] Signature { get; init; }
}
```

**Required signatures (at minimum):**
1. The **current holder** of the F1000 membership (`FromGovernanceId`)
2. A **master-admin or registrar** with authority over F1000 registry

**Security properties:**

**Prevents unilateral hijack:**
- No one can move F1000 membership away from a holder without their signature, unless:
  - A governance emergency process is explicitly defined (e.g., stolen key recovery with multi-sig council)

**Provides strong audit trail:**
- Every transfer is logged as a signed document
- Registry reflects historical `Status = Transferred` and new `Status = Active`

**Cap enforcement with transfer:**

When applying a transfer:
- Ensure that `ToGovernanceId` is not already an active F1000 member in this epoch
- Ensure that adding `ToGovernanceId` as active does not exceed `F1000_CAP` **for non-master-admins**
  - If `ToGovernanceId` is a master-admin, treat them as cap-exempt

**Optional emergency recovery:**

A future governance policy MAY define:
- A multi-sig council-based process to reclaim F1000 status from compromised keys (without holder signature), but:
  - MUST require at least a strong quorum (e.g., 2/3 of council)
  - MUST be clearly documented and visible to pods (so admins can decide whether to trust such powers)

---

### 3.3 Revocation, Rotation, and Hardening

**Revocation:**

F1000 membership can be revoked by:
- Holder request + master-admin signature, OR
- Governance emergency process (multi-sig) if defined

Registry marks entries as `Status = Revoked`.

**Key rotation:**

Governance IDs can rotate keys via:
- A "key replacement" document signed by the old key
- Optionally countersigned by a master-admin or registrar

**Hardening:**

All governance operations (delegations, transfers, revocations) MUST:
- Be signed and verifiable
- Be loggable in a safe way (no private keys or sensitive data)

Tools MUST:
- Require explicit human confirmation for transfers and revocations
- Show current and resulting registry state before committing

---

## 4. F1000 Outputs: What They Actually Do

F1000 membership and master-admin roles are useful via:

### 4.1 Human Moderation Feeds (Curated Lists)

**F1000 members can curate:**
- Hash blocklists
- Peer reputation signals
- WorkRef verdicts (content quality/safety assessments)

**Pods can consume these as:**
- Inputs to MCP (via `HumanModerationProvider`)
- Advisory signals (not mandatory gates)

**Security:**
- Local blocklists and admin decisions always win
- F1000 signals are additive, not overriding

---

### 4.2 Policy Profiles (Signed Configuration Bundles)

**F1000/council can publish:**
- Recommended default configs for:
  - MCP settings (failsafe modes, provider ordering)
  - Work budgets
  - Federation privacy modes

**Pods can:**
- Fetch and compare against their current config
- Apply with explicit admin consent
- Ignore entirely

**Security:**
- Never auto-apply changes that affect:
  - LLM providers
  - MCP decision thresholds
  - Federation settings

---

### 4.3 Advisory Votes (Recommendations)

**F1000/council can publish:**
- Governance recommendations on:
  - Protocol upgrades
  - Ecosystem-wide norms
  - Responses to network-wide issues

**Pods can:**
- Display these as UI hints
- Use them to inform admin decisions
- Treat as informational only

**Security:**
- Votes have no automatic enforcement
- Local admin decision is final

---

## 5. Pod-Side Consumption & Isolation

Pods that implement governance support:

**Have a governance client that:**
- Fetches F1000 registry and governance docs
- Validates signatures and delegations
- Applies local policy: **subscribe or ignore**

**Pods MUST:**

Keep governance logic **completely separate** from:
- Local admin authentication
- Pod key material
- MCP internal configuration

Ensure:
- Governance identities can **never** log into the pod or modify local config directly
- Governance data is used only as **input** to:
  - Optional MCP providers (e.g., `HumanModerationProvider(F1000)`)
  - Optional UI hints and default setups

**Pods MUST provide:**
- A config switch to completely **disable** governance/F1000 integration

---

## 6. Implementation Timing

This design is:

**A future layer on top of:**
- `docs/pod-identity-lifecycle.md`
- `docs/moderation-v1-design.md`
- `docs/llm-mcp-design.md` (if it exists)
- `docs/social-federation-design.md`

**For current experimental branches:**

It is sufficient to:
- Store this design
- Add appropriately-scoped tasks

Actual F1000 registry, transfer tooling, and governance feeds can be implemented in later branches, once the core architecture is stable.

**Nothing in this design changes:**
- Pods remain sovereign
- MCP and LLM are always locally controlled
- Governance is a **meta layer**, not a root-of-trust for the technical stack

---

## 7. Security Summary

### What F1000/Master-Admins CAN Do:

✅ Curate moderation feeds (advisory)  
✅ Sign policy profiles (advisory)  
✅ Assign/revoke F1000 memberships (within cap, requires signatures)  
✅ Transfer F1000 memberships (requires holder + master-admin signature)  
✅ Provide governance recommendations (informational)  

### What F1000/Master-Admins CANNOT Do:

❌ Log into pods  
❌ Change pod configs remotely  
❌ Override local admin decisions  
❌ Modify MCP behavior on pods  
❌ Access pod keys or local data  
❌ Hijack F1000 memberships unilaterally (requires holder signature)  

### Key Guarantees:

**Founder Guarantee:**
- Founder always eligible for F1000 via `Role.MasterAdmin`
- Does not consume cap-limited slots unless desired
- Can transfer numeric slot later while retaining governance presence

**Cap Exemption:**
- Master-admins do not count toward `F1000_CAP`
- Ensures governance core always fits without squeezing out early testers

**Transferability:**
- F1000 membership is transferable (not just account-bound)
- Requires dual signatures (holder + master-admin)
- Full audit trail

**Pod Sovereignty:**
- Pods can disable governance entirely
- Local admin always has final say
- Governance signals are advisory, not mandatory

**Hardening:**
- All governance operations are signed and verifiable
- Tools require explicit confirmation for transfers/revocations
- Governance keys stored off-pod (hardware tokens, password managers)
- Emergency recovery (if defined) requires strong quorum

---

## Related Documents

- `docs/pod-identity-lifecycle.md` - Pod identity, keys, and lifecycle management
- `docs/moderation-v1-design.md` - MCP design and hardening
- `docs/security-hardening-guidelines.md` - Global security principles
- `docs/social-federation-design.md` - ActivityPub integration
- `TASK_STATUS_DASHBOARD.md` - T-F1000-01 through T-F1000-06 tasks
