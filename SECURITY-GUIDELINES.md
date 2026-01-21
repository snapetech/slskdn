# Security Guidelines

> **H-GLOBAL01: Logging and Telemetry Hygiene Audit**
>
> This document defines security guidelines for logging and telemetry to prevent accidental leakage of sensitive information.

## Overview

slskdN handles sensitive user data including file paths, IP addresses, external usernames, and cryptographic material. This document establishes mandatory patterns for safe logging and telemetry collection.

## Core Principle

**NEVER log sensitive data in plain text.** Always use sanitization utilities before logging.

## Sanitization Utilities

Use `slskd.Common.Security.LoggingSanitizer` for all logging operations:

### File Paths
```csharp
// ❌ BAD - logs full path
_logger.LogInformation("Processing file: {Path}", "/home/user/secret/document.pdf");

// ✅ GOOD - logs only filename
_logger.LogInformation("Processing file: {SanitizedPath}", LoggingSanitizer.SanitizeFilePath("/home/user/secret/document.pdf"));
// Result: "Processing file: document.pdf"
```

### IP Addresses
```csharp
// ❌ BAD - logs plain IP
_logger.LogInformation("Connection from: {Ip}", "192.168.1.100");

// ✅ GOOD - logs hashed IP
_logger.LogInformation("Connection from: {SanitizedIp}", LoggingSanitizer.SanitizeIpAddress("192.168.1.100"));
// Result: "Connection from: a1b2c3d4e5f6..." (16-char hash)
```

### External Identifiers (Usernames, ActivityPub handles, etc.)
```csharp
// ❌ BAD - logs full username
_logger.LogInformation("User login: {Username}", "john_doe_12345");

// ✅ GOOD - logs sanitized identifier
_logger.LogInformation("User login: {SanitizedId}", LoggingSanitizer.SanitizeExternalIdentifier("john_doe_12345"));
// Result: "User login: j***5 (13 chars)"
```

### Sensitive Data (API keys, tokens, passwords)
```csharp
// ❌ BAD - logs secret data
_logger.LogInformation("API key: {Key}", "sk-1234567890abcdef");

// ✅ GOOD - logs redacted placeholder
_logger.LogInformation("API key: {Redacted}", LoggingSanitizer.SanitizeSensitiveData("sk-1234567890abcdef"));
// Result: "API key: [redacted-18-chars]"
```

### URLs
```csharp
// ❌ BAD - logs full URL with potential tokens
_logger.LogInformation("Request to: {Url}", "https://api.example.com/users/123?token=secret");

// ✅ GOOD - logs sanitized URL
_logger.LogInformation("Request to: {SanitizedUrl}", LoggingSanitizer.SanitizeUrl("https://api.example.com/users/123?token=secret"));
// Result: "Request to: https://api.example.com"
```

### Cryptographic Hashes
```csharp
// ❌ BAD - logs full hash (may be sensitive in some contexts)
_logger.LogInformation("File hash: {Hash}", fullSha256Hash);

// ✅ GOOD - logs truncated hash
_logger.LogInformation("File hash: {SanitizedHash}", LoggingSanitizer.SanitizeHash(fullSha256Hash));
// Result: "File hash: a1b2c3d4...567890ab" (first 8 + last 8 chars)
```

## Telemetry (Metrics) Guidelines

### Label Restrictions

**NEVER include sensitive data in metric labels:**

```csharp
// ❌ BAD - filename in metric label
Metrics.FileProcessing.WithLabels(fileName: "/secret/path/file.pdf").Inc();

// ✅ GOOD - use generic labels only
Metrics.FileProcessing.WithLabels(status: "success").Inc();
```

### Approved Label Values

- **Cardinality ≤ 10**: `status` ("success", "failure", "timeout")
- **Cardinality ≤ 100**: `method` ("GET", "POST", "PUT", "DELETE")
- **Cardinality ≤ 1000**: `endpoint` ("/api/v1/users", "/api/v1/files")

### Forbidden Labels

- ❌ File names, paths, or extensions
- ❌ IP addresses or hostnames
- ❌ User identifiers or usernames
- ❌ Full URLs or query parameters
- ❌ Cryptographic hashes or keys
- ❌ Free-form text from user input

## Implementation Patterns

### Safe Logging Context
```csharp
// Use SafeContext for structured logging with sensitive data
_logger.LogInformation("Processing request {@Context}", LoggingSanitizer.SafeContext("user", username));
// Result: { Context = "user", Id = "j***5 (13 chars)" }
```

### Batch Sanitization
```csharp
// When logging collections
var sanitizedIps = ipAddresses.Select(ip => LoggingSanitizer.SanitizeIpAddress(ip));
_logger.LogInformation("Connections from: {SanitizedIps}", string.Join(", ", sanitizedIps));
```

## Audit Checklist

Before committing code:

1. **Grep for sensitive logging patterns:**
   ```bash
   grep -r "_logger\.Log.*{" src/ | grep -E "(Path|File|Ip|Address|User|Token|Key|Hash)"
   ```

2. **Check for unsanitized metric labels:**
   ```bash
   grep -r "WithLabels.*:" src/ | grep -v "status\|method\|endpoint"
   ```

3. **Run logging hygiene tests:**
   ```bash
   dotnet test --filter LoggingHygiene
   ```

## Enforcement

- **Pre-commit hook**: `SECURITY-AUDIT.md` validation
- **CI/CD**: Automated grep checks for forbidden patterns
- **Code review**: Mandatory security review for logging changes
- **Runtime**: LoggingSanitizer enforces sanitization patterns

## Identity Separation Guidelines

**H-ID01: Identity Separation Enforcement**

Different identity types must remain strictly separated to prevent cross-contamination and credential reuse attacks.

### Identity Types

1. **Mesh Identity**: Ed25519 public/private key pairs for overlay authentication
   - Format: Base64-encoded 32-byte public keys
   - Storage: Encrypted via `IKeyStore`
   - Usage: Peer-to-peer overlay communication

2. **Soulseek Identity**: Username/password for Soulseek network access
   - Format: Alphanumeric + underscore/dot, max 30 chars
   - Storage: Configuration file (encrypted)
   - Usage: Soulseek protocol authentication

3. **Pod Identity**: Internal peer identifiers within pods
   - Format: `pod:hexhash` (sanitized) or `mesh:self`
   - Storage: Pod membership database
   - Usage: Pod communication and access control

4. **Local User Identity**: Web UI/API authentication
   - Format: Email-like or simple usernames
   - Storage: Configuration or external auth provider
   - Usage: Administrative access control

### Separation Rules

```csharp
// ❌ BAD - Bridge format leaks Soulseek identity
var podPeerId = "bridge:soulseek_username";

// ✅ GOOD - Sanitized pod identity
var podPeerId = IdentitySeparationEnforcer.SanitizePodPeerId("bridge:soulseek_username");
// Result: "pod:a1b2c3d4..." (deterministic hash)
```

```csharp
// ❌ BAD - Reusing credentials
Options.Soulseek.Username = "admin";
Options.Web.Auth.Username = "admin";

// ✅ GOOD - Distinct identities
Options.Soulseek.Username = "soulseek_user123";
Options.Web.Auth.Username = "web_admin";
```

### Implementation

Use `IdentitySeparationEnforcer` for validation:

```csharp
// Validate identity format
bool isValid = IdentitySeparationEnforcer.IsValidIdentityFormat(identity, IdentityType.Pod);

// Check for cross-contamination
bool hasLeakage = IdentitySeparationEnforcer.HasCrossContamination(identity, IdentityType.Soulseek);

// Sanitize pod peer IDs
string safePeerId = IdentitySeparationEnforcer.SanitizePodPeerId(rawPeerId);
```

### Audit Tools

```csharp
// Audit configuration
var configAudit = IdentityConfigurationAuditor.AuditConfiguration(options, logger);

// Audit pod peer IDs
var peerIdAudit = IdentitySeparationValidator.AuditPodPeerIds(peerIds, logger);

// Validate identity collection
var validation = IdentitySeparationValidator.ValidateIdentities(identities, logger);
```

## Related Tasks

- **H-GLOBAL01**: Logging and Telemetry Hygiene Audit (this document)
- **H-ID01**: Identity Separation Enforcement
- **H-VF01**: VirtualSoulfind Input Validation & Domain Gating
- **H-TRANSPORT01**: Mesh/DHT/Torrent/HTTP Transport Hardening
- **H-MCP01**: Moderation Coverage Audit