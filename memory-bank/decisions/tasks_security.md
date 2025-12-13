# Security Tasks: Trust Model & Cryptographic Identity Fixes

**Branch**: `experimental/whatAmIThinking`  
**Priority**: P0 (Security Critical)  
**Goal**: Fix trust-model and security logic failures in mesh overlay system

---

## Overview

This document breaks down the mesh security fixes into specific, actionable tasks for implementing:
- (A) QUIC certificate persistence + SPKI pinning
- (B) Control-plane signature authentication (peer-bound verification)
- (C) Stable cryptographic identity with subordinate key rotation
- (D) MeshGateway security fixes (0.0.0.0 handling + API key enforcement)
- (E) Documentation guardrails to prevent false claims

**Strategy**: Signed descriptor pinning (primary) with TOFU fallback.

---

## Section A: QUIC Certificate Persistence + SPKI Pinning

### A1: Add TLS Certificate Path Options

**File**: `src/slskd/Mesh/Overlay/OverlayOptions.cs`

**Task**: Add certificate persistence options

```csharp
// Add these properties:
public string TlsCertPath { get; set; } = "mesh-overlay-control.pfx";
public string? TlsCertPassword { get; set; }
```

**Acceptance**: Options exist and can be set via config file.

---

### A2: Add TLS Certificate Path Options (Data Plane)

**File**: `src/slskd/Mesh/Overlay/DataOverlayOptions.cs`

**Task**: Create if doesn't exist, or add to existing options class

```csharp
public string TlsCertPath { get; set; } = "mesh-overlay-data.pfx";
public string? TlsCertPassword { get; set; }
```

**Acceptance**: Data plane has separate cert path options.

---

### A3: Implement Persistent Certificate Helper

**File**: `src/slskd/Mesh/Overlay/PersistentCertificate.cs` (NEW)

**Task**: Create helper to load or create persistent certificates

```csharp
public static class PersistentCertificate
{
    public static X509Certificate2 LoadOrCreate(
        string path, 
        string? password, 
        string subjectCN, 
        int validityYears = 5)
    {
        // If file exists: load PFX with private key
        // Else: create self-signed cert (prefer ECDSA P-256, RSA 2048 ok)
        // Export as PFX to path
        // On Linux: chmod 0600 if possible
        // Return loaded cert
    }
}
```

**Details**:
- Check `File.Exists(path)` first
- Use `X509Certificate2.CreateFromPfxFile()` to load
- Use `CertificateRequest` + `ECDsa.Create(ECCurve.NamedCurves.nistP256)` to create
- Export with `cert.Export(X509ContentType.Pfx, password)`
- Set file permissions: `File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite)`

**Acceptance**: 
- Loading existing cert works
- Creating new cert works and persists
- File permissions are 0600 on Linux

**Test**: Unit test in `tests/slskd.Tests.Unit/Mesh/Overlay/PersistentCertificateTests.cs`

---

### A4: Use Persistent Cert in QuicOverlayServer

**File**: `src/slskd/Mesh/Overlay/QuicOverlayServer.cs`

**Task**: Replace ephemeral cert generation with persistent cert

**Change**: Line 59
```csharp
// OLD:
var certificate = SelfSignedCertificate.Create("CN=mesh-overlay-quic");

// NEW:
var certificate = PersistentCertificate.LoadOrCreate(
    options.TlsCertPath,
    options.TlsCertPassword,
    "CN=mesh-overlay-control",
    validityYears: 5
);
```

**Acceptance**: Server uses persistent cert, SPKI stays same across restarts.

---

### A5: Use Persistent Cert in QuicDataServer

**File**: `src/slskd/Mesh/Overlay/QuicDataServer.cs`

**Task**: Same as A4 but for data plane

**Change**: Find cert creation line, replace with:
```csharp
var certificate = PersistentCertificate.LoadOrCreate(
    dataOptions.TlsCertPath,
    dataOptions.TlsCertPassword,
    "CN=mesh-overlay-data",
    validityYears: 5
);
```

**Acceptance**: Data server uses persistent cert.

---

### A6: Create SPKI Hash Utility

**File**: `src/slskd/Mesh/Security/CertificatePins.cs` (NEW)

**Task**: Create utility to compute SPKI SHA-256 hash

```csharp
namespace slskd.Mesh.Security;

public static class CertificatePins
{
    public static byte[] ComputeSpkiSha256(X509Certificate2 cert)
    {
        // Try ECDsa first
        var ecdsa = cert.GetECDsaPublicKey();
        if (ecdsa != null)
        {
            var spki = ecdsa.ExportSubjectPublicKeyInfo();
            return SHA256.HashData(spki);
        }
        
        // Fallback to RSA
        var rsa = cert.GetRSAPublicKey();
        if (rsa != null)
        {
            var spki = rsa.ExportSubjectPublicKeyInfo();
            return SHA256.HashData(spki);
        }
        
        throw new InvalidOperationException("Certificate has no supported public key");
    }
    
    public static string ComputeSpkiSha256Base64(X509Certificate2 cert)
    {
        return Convert.ToBase64String(ComputeSpkiSha256(cert));
    }
}
```

**Acceptance**: Hash is deterministic for same cert.

**Test**: Unit test with known cert, verify hash matches expected value.

---

### A7: Add Pin Validation Callback for QuicOverlayClient

**File**: `src/slskd/Mesh/Overlay/QuicOverlayClient.cs`

**Task**: Replace `=> true` callback with pin validation

**Change**: Line 100
```csharp
// OLD:
RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true

// NEW:
RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
{
    if (certificate == null) return false;
    
    var cert = new X509Certificate2(certificate);
    
    // Get expected SPKI hash for this endpoint from peer descriptor
    var expectedSpki = GetExpectedSpkiForEndpoint(endpoint); // TODO: implement
    if (expectedSpki == null)
    {
        // No descriptor available - TOFU fallback
        logger.LogWarning("[Overlay-QUIC] No descriptor for {Endpoint}, accepting on first use", endpoint);
        // TODO: Store pin for future validation
        return true;
    }
    
    var actualSpki = CertificatePins.ComputeSpkiSha256Base64(cert);
    if (actualSpki != expectedSpki)
    {
        logger.LogError("[Overlay-QUIC] SPKI mismatch for {Endpoint}: expected {Expected}, got {Actual}", 
            endpoint, expectedSpki, actualSpki);
        return false;
    }
    
    // Also check cert validity and ALPN
    if (cert.NotBefore > DateTime.UtcNow || cert.NotAfter < DateTime.UtcNow)
    {
        logger.LogWarning("[Overlay-QUIC] Certificate time invalid for {Endpoint}", endpoint);
        return false;
    }
    
    return true;
}
```

**Note**: `GetExpectedSpkiForEndpoint` will be implemented in Section C when peer descriptors are available.

**Acceptance**: 
- Pin mismatch rejects connection
- Valid pin accepts connection
- TOFU fallback works when no descriptor

---

### A8: Add Pin Validation for QuicDataClient

**File**: `src/slskd/Mesh/Overlay/QuicDataClient.cs`

**Task**: Same as A7 but for data plane client

**Acceptance**: Data plane enforces SPKI pinning.

---

## Section B: Control-Plane Signature Authentication

### B1: Add MessageId and KeyId to ControlEnvelope

**File**: `src/slskd/Mesh/Overlay/ControlEnvelope.cs`

**Task**: Add replay resistance fields

```csharp
// Add these properties:
[Key(5)] public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
[Key(6)] public string? SignerKeyId { get; set; }
```

**Acceptance**: Envelope has unique MessageId per instance.

---

### B2: Include MessageId in Signature Payload

**File**: `src/slskd/Mesh/Overlay/KeyedSigner.cs` (currently named `ControlSigner`)

**Task**: Update `BuildSignablePayload` method

**Change**: Line 96
```csharp
// OLD:
private static string BuildSignablePayload(ControlEnvelope env) =>
    $"{env.Type}|{env.TimestampUnixMs}|{Convert.ToBase64String(env.Payload)}";

// NEW:
private static string BuildSignablePayload(ControlEnvelope env) =>
    $"{env.Type}|{env.TimestampUnixMs}|{env.MessageId}|{Convert.ToBase64String(env.Payload)}";
```

**Acceptance**: Signatures include MessageId, preventing simple replay across types.

---

### B3: Create PeerContext Model

**File**: `src/slskd/Mesh/Overlay/PeerContext.cs` (NEW)

**Task**: Create peer context for verification

```csharp
namespace slskd.Mesh.Overlay;

public class PeerContext
{
    public required string PeerId { get; init; }
    public required IPEndPoint RemoteEndPoint { get; init; }
    public required string Transport { get; init; } // "udp" | "quic"
    public required IReadOnlyList<byte[]> AllowedControlSigningKeys { get; init; }
}
```

**Acceptance**: Model exists and can be constructed.

---

### B4: Create Peer-Aware Verification API

**File**: `src/slskd/Mesh/Overlay/ControlVerification.cs` (NEW)

**Task**: Create verification that checks against allowed keys (not self-asserted)

```csharp
namespace slskd.Mesh.Overlay;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

public interface IControlVerification
{
    bool Verify(ControlEnvelope envelope, IReadOnlyList<byte[]> allowedPublicKeys);
}

public class ControlVerification : IControlVerification
{
    private readonly ILogger<ControlVerification> logger;

    public ControlVerification(ILogger<ControlVerification> logger)
    {
        this.logger = logger;
    }

    public bool Verify(ControlEnvelope envelope, IReadOnlyList<byte[]> allowedPublicKeys)
    {
        if (allowedPublicKeys.Count == 0)
        {
            logger.LogWarning("[ControlVerification] No allowed keys for verification");
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.Signature))
        {
            logger.LogWarning("[ControlVerification] No signature in envelope");
            return false;
        }

        try
        {
            var signatureBytes = Convert.FromBase64String(envelope.Signature);
            if (signatureBytes.Length != 64)
            {
                logger.LogWarning("[ControlVerification] Invalid signature length: {Length}", signatureBytes.Length);
                return false;
            }

            var payload = BuildSignablePayload(envelope);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            // Try each allowed public key
            foreach (var publicKeyBytes in allowedPublicKeys)
            {
                if (publicKeyBytes.Length != 32) continue;

                try
                {
                    var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
                    if (SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Try next key
                    continue;
                }
            }

            logger.LogWarning("[ControlVerification] Signature did not match any allowed keys");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[ControlVerification] Verification failed");
            return false;
        }
    }

    private static string BuildSignablePayload(ControlEnvelope env) =>
        $"{env.Type}|{env.TimestampUnixMs}|{env.MessageId}|{Convert.ToBase64String(env.Payload)}";
}
```

**Acceptance**: Verification only succeeds if signature matches one of the allowed keys.

**Test**: Unit test with valid/invalid keys.

---

### B5: Create Replay Cache

**File**: `src/slskd/Mesh/Security/ReplayCache.cs` (NEW)

**Task**: Create anti-replay cache

```csharp
namespace slskd.Mesh.Security;

using System.Collections.Concurrent;

public interface IReplayCache
{
    bool CheckAndAdd(string peerId, string messageId, long timestampUnixMs);
}

public class ReplayCache : IReplayCache
{
    private readonly ConcurrentDictionary<string, PeerCache> peerCaches = new();
    private readonly int maxAgeMinutes;
    private readonly int maxTimestampSkewMinutes;

    public ReplayCache(int maxAgeMinutes = 10, int maxTimestampSkewMinutes = 2)
    {
        this.maxAgeMinutes = maxAgeMinutes;
        this.maxTimestampSkewMinutes = maxTimestampSkewMinutes;
    }

    public bool CheckAndAdd(string peerId, string messageId, long timestampUnixMs)
    {
        // Check timestamp skew
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ageSec = Math.Abs(now - timestampUnixMs) / 1000;
        if (ageSec > maxTimestampSkewMinutes * 60)
        {
            return false; // Too far in past or future
        }

        var cache = peerCaches.GetOrAdd(peerId, _ => new PeerCache());
        return cache.AddIfNew(messageId, timestampUnixMs, maxAgeMinutes);
    }

    private class PeerCache
    {
        private readonly ConcurrentDictionary<string, long> seenMessages = new();

        public bool AddIfNew(string messageId, long timestampUnixMs, int maxAgeMinutes)
        {
            // Clean old entries
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-maxAgeMinutes).ToUnixTimeMilliseconds();
            var toRemove = seenMessages.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
            foreach (var key in toRemove)
            {
                seenMessages.TryRemove(key, out _);
            }

            // Try add
            return seenMessages.TryAdd(messageId, timestampUnixMs);
        }
    }
}
```

**Acceptance**: 
- Duplicate MessageId rejected
- Old timestamp rejected
- Future timestamp rejected

**Test**: Unit tests for replay detection and timestamp skew.

---

### B6: Update IControlDispatcher Interface

**File**: `src/slskd/Mesh/Overlay/ControlDispatcher.cs`

**Task**: Add PeerContext parameter to HandleAsync

**Change**: Line 11
```csharp
// OLD:
Task<bool> HandleAsync(ControlEnvelope envelope, CancellationToken ct = default);

// NEW:
Task<bool> HandleAsync(ControlEnvelope envelope, PeerContext peer, CancellationToken ct = default);
```

**Acceptance**: Interface requires peer context.

---

### B7: Update ControlDispatcher Implementation

**File**: `src/slskd/Mesh/Overlay/ControlDispatcher.cs`

**Task**: Use peer-aware verification and replay cache

**Changes**:
1. Add dependencies (constructor):
```csharp
private readonly IControlVerification verification;
private readonly IReplayCache replayCache;

public ControlDispatcher(
    ILogger<ControlDispatcher> logger,
    IControlSigner signer,
    IControlVerification verification,
    IReplayCache replayCache)
{
    this.logger = logger;
    this.signer = signer;
    this.verification = verification;
    this.replayCache = replayCache;
}
```

2. Update HandleAsync (line 25):
```csharp
public Task<bool> HandleAsync(ControlEnvelope envelope, PeerContext peer, CancellationToken ct = default)
{
    // Check replay cache first
    if (!replayCache.CheckAndAdd(peer.PeerId, envelope.MessageId, envelope.TimestampUnixMs))
    {
        logger.LogWarning("[Overlay] Reject envelope: replay or timestamp skew from {PeerId}", peer.PeerId);
        return Task.FromResult(false);
    }

    // Verify signature against peer's allowed keys (NOT self-asserted key)
    if (!verification.Verify(envelope, peer.AllowedControlSigningKeys))
    {
        logger.LogWarning("[Overlay] Reject envelope: signature invalid for {PeerId}", peer.PeerId);
        return Task.FromResult(false);
    }

    logger.LogDebug("[Overlay] Received control {Type} from {PeerId} ts={Ts}", 
        envelope.Type, peer.PeerId, envelope.TimestampUnixMs);

    // ... rest of dispatch logic
}
```

**Acceptance**: 
- Replays rejected
- Unknown keys rejected
- Valid signatures accepted

---

### B8: Update QuicOverlayServer to Pass PeerContext

**File**: `src/slskd/Mesh/Overlay/QuicOverlayServer.cs`

**Task**: Resolve peer identity before dispatching

**Change**: Line 181 (in HandleStreamAsync)
```csharp
// OLD:
var handled = await dispatcher.HandleAsync(envelope, ct);

// NEW:
// Resolve PeerContext from remoteEndPoint
var peerContext = await ResolvePeerContextAsync(remoteEndPoint, ct);
if (peerContext == null)
{
    logger.LogWarning("[Overlay-QUIC] Cannot resolve peer context for {Endpoint}", remoteEndPoint);
    return;
}

var handled = await dispatcher.HandleAsync(envelope, peerContext, ct);
```

**Note**: `ResolvePeerContextAsync` will be added in Section C.

**Acceptance**: Server doesn't dispatch without peer context.

---

### B9: Update UdpOverlayServer to Pass PeerContext

**File**: `src/slskd/Mesh/Overlay/UdpOverlayServer.cs`

**Task**: Same as B8 but for UDP server

**Acceptance**: UDP server requires peer context for dispatch.

---

### B10: Register New Services in DI

**File**: `src/slskd/Program.cs`

**Task**: Register new security services

Add to service registration:
```csharp
services.AddSingleton<IControlVerification, ControlVerification>();
services.AddSingleton<IReplayCache, ReplayCache>();
```

**Acceptance**: Services resolve from DI container.

---

## Section C: Stable Cryptographic Identity

### C1: Create IdentityKeyStore

**File**: `src/slskd/Mesh/Security/IdentityKeyStore.cs` (NEW)

**Task**: Create stable identity key storage (no rotation)

```csharp
namespace slskd.Mesh.Security;

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

public interface IIdentityKeyStore
{
    byte[] PublicKey { get; }
    byte[] PrivateKey { get; }
    string ComputePeerId();
}

public class FileIdentityKeyStore : IIdentityKeyStore
{
    private readonly ILogger<FileIdentityKeyStore> logger;
    private readonly string keyPath;
    private byte[] publicKey;
    private byte[] privateKey;

    public FileIdentityKeyStore(ILogger<FileIdentityKeyStore> logger, string keyPath = "mesh-identity.key")
    {
        this.logger = logger;
        this.keyPath = keyPath;
        Load();
    }

    public byte[] PublicKey => publicKey;
    public byte[] PrivateKey => privateKey;

    public string ComputePeerId()
    {
        // PeerId = hex(SHA256(publicKey)) or base32 - use hex for simplicity
        var hash = SHA256.HashData(publicKey);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void Load()
    {
        if (!File.Exists(keyPath))
        {
            Generate();
            return;
        }

        try
        {
            var json = File.ReadAllText(keyPath);
            var model = JsonSerializer.Deserialize<KeyFileModel>(json);
            if (model == null) throw new InvalidOperationException("Invalid identity key file");

            publicKey = Convert.FromBase64String(model.PublicKey);
            privateKey = Convert.FromBase64String(model.PrivateKey);

            logger.LogInformation("[IdentityKeyStore] Loaded identity from {Path}, PeerId={PeerId}", 
                keyPath, ComputePeerId());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[IdentityKeyStore] Failed to load identity, generating new");
            Generate();
        }
    }

    private void Generate()
    {
        using var key = Key.Create(SignatureAlgorithm.Ed25519, 
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        publicKey = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        privateKey = key.Export(KeyBlobFormat.RawPrivateKey);

        var model = new KeyFileModel
        {
            PublicKey = Convert.ToBase64String(publicKey),
            PrivateKey = Convert.ToBase64String(privateKey),
            CreatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var json = JsonSerializer.Serialize(model);
        File.WriteAllText(keyPath, json);

        // Set file permissions on Linux
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        logger.LogInformation("[IdentityKeyStore] Generated new identity at {Path}, PeerId={PeerId}", 
            keyPath, ComputePeerId());
    }

    private class KeyFileModel
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public long CreatedMs { get; set; }
    }
}
```

**Acceptance**: 
- Identity persists across restarts
- PeerId is deterministic
- Never rotates

**Test**: Unit test PeerId derivation is stable.

---

### C2: Update MeshOptions to Use Computed PeerId

**File**: `src/slskd/Mesh/MeshOptions.cs`

**Task**: Remove static default, make PeerId computed at runtime

**Change**: Line 31
```csharp
// OLD:
public string SelfPeerId { get; set; } = "peer:mesh:self";

// NEW:
public string SelfPeerId { get; set; } = string.Empty; // Computed from identity key at startup
```

**Note**: Add comment that this is populated by `MeshBootstrapService` or similar.

**Acceptance**: Config no longer has hard-coded peer ID.

---

### C3: Extend MeshPeerDescriptor

**File**: `src/slskd/Mesh/Dht/MeshPeerDescriptor.cs`

**Task**: Add identity, pins, and signing keys

```csharp
// Add these properties:

[Key(5)]
public string IdentityPublicKey { get; set; } = string.Empty; // Base64 Ed25519 public key

[Key(6)]
public string TlsControlSpkiSha256 { get; set; } = string.Empty; // Base64 SPKI hash

[Key(7)]
public string TlsDataSpkiSha256 { get; set; } = string.Empty; // Base64 SPKI hash

[Key(8)]
public List<string> ControlSigningPublicKeys { get; set; } = new(); // Base64 Ed25519 keys (overlap for rotation)

[Key(9)]
public string Signature { get; set; } = string.Empty; // Descriptor signed by identity key
```

**Acceptance**: Descriptor has all security fields.

---

### C4: Create Descriptor Signer

**File**: `src/slskd/Mesh/Security/DescriptorSigner.cs` (NEW)

**Task**: Sign and verify descriptors

```csharp
namespace slskd.Mesh.Security;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using slskd.Mesh.Dht;

public interface IDescriptorSigner
{
    void Sign(MeshPeerDescriptor descriptor, byte[] identityPrivateKey);
    bool Verify(MeshPeerDescriptor descriptor);
}

public class DescriptorSigner : IDescriptorSigner
{
    private readonly ILogger<DescriptorSigner> logger;

    public DescriptorSigner(ILogger<DescriptorSigner> logger)
    {
        this.logger = logger;
    }

    public void Sign(MeshPeerDescriptor descriptor, byte[] identityPrivateKey)
    {
        var payload = BuildCanonicalPayload(descriptor);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var key = Key.Import(SignatureAlgorithm.Ed25519, identityPrivateKey, KeyBlobFormat.RawPrivateKey);
        var signature = SignatureAlgorithm.Ed25519.Sign(key, payloadBytes);

        descriptor.Signature = Convert.ToBase64String(signature);
    }

    public bool Verify(MeshPeerDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.IdentityPublicKey) || 
            string.IsNullOrWhiteSpace(descriptor.Signature))
        {
            logger.LogWarning("[DescriptorSigner] Missing identity or signature");
            return false;
        }

        try
        {
            // Verify PeerId matches identity public key
            var identityPubKey = Convert.FromBase64String(descriptor.IdentityPublicKey);
            var derivedPeerId = ComputePeerId(identityPubKey);
            if (derivedPeerId != descriptor.PeerId)
            {
                logger.LogWarning("[DescriptorSigner] PeerId mismatch: expected {Expected}, got {Actual}",
                    derivedPeerId, descriptor.PeerId);
                return false;
            }

            // Verify signature
            var payload = BuildCanonicalPayload(descriptor);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var signatureBytes = Convert.FromBase64String(descriptor.Signature);

            var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, identityPubKey, KeyBlobFormat.RawPublicKey);
            return SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[DescriptorSigner] Verification failed");
            return false;
        }
    }

    private static string BuildCanonicalPayload(MeshPeerDescriptor desc)
    {
        // Canonicalize fields (exclude Signature itself)
        var endpoints = string.Join(",", desc.Endpoints.OrderBy(e => e));
        var signingKeys = string.Join(",", desc.ControlSigningPublicKeys.OrderBy(k => k));

        return $"{desc.PeerId}|{endpoints}|{desc.NatType ?? ""}|{desc.RelayRequired}|" +
               $"{desc.TimestampUnixMs}|{desc.IdentityPublicKey}|" +
               $"{desc.TlsControlSpkiSha256}|{desc.TlsDataSpkiSha256}|{signingKeys}";
    }

    private static string ComputePeerId(byte[] publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

**Acceptance**: 
- Signing produces valid signature
- Verification rejects tampered descriptors
- Verification rejects PeerId mismatch

**Test**: Unit tests for sign/verify and PeerId derivation.

---

### C5: Update PeerDescriptorPublisher to Sign Descriptors

**File**: `src/slskd/Mesh/Dht/PeerDescriptorPublisher.cs`

**Task**: Populate and sign descriptor before publishing

Add dependencies:
```csharp
private readonly IIdentityKeyStore identityKeyStore;
private readonly IDescriptorSigner descriptorSigner;
```

Update publish logic:
```csharp
var descriptor = new MeshPeerDescriptor
{
    PeerId = identityKeyStore.ComputePeerId(),
    Endpoints = options.SelfEndpoints,
    NatType = detectedNatType,
    RelayRequired = needsRelay,
    TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    IdentityPublicKey = Convert.ToBase64String(identityKeyStore.PublicKey),
    TlsControlSpkiSha256 = LoadControlSpki(),
    TlsDataSpkiSha256 = LoadDataSpki(),
    ControlSigningPublicKeys = LoadControlSigningKeys()
};

descriptorSigner.Sign(descriptor, identityKeyStore.PrivateKey);

// Publish to DHT
```

**Acceptance**: Published descriptors are signed.

---

### C6: Create Peer Resolver

**File**: `src/slskd/Mesh/Identity/PeerResolver.cs` (NEW)

**Task**: Resolve peer context from endpoint (fetch descriptor, verify, cache)

```csharp
namespace slskd.Mesh.Identity;

using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using slskd.Mesh.Overlay;
using slskd.Mesh.Security;

public interface IPeerResolver
{
    Task<PeerContext?> ResolveAsync(IPEndPoint endpoint, CancellationToken ct);
}

public class PeerResolver : IPeerResolver
{
    private readonly ILogger<PeerResolver> logger;
    private readonly IMeshDirectory directory;
    private readonly IDescriptorSigner descriptorSigner;
    private readonly ConcurrentDictionary<string, CachedPeerContext> cache = new();

    public PeerResolver(
        ILogger<PeerResolver> logger,
        IMeshDirectory directory,
        IDescriptorSigner descriptorSigner)
    {
        this.logger = logger;
        this.directory = directory;
        this.descriptorSigner = descriptorSigner;
    }

    public async Task<PeerContext?> ResolveAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        // Try cache first
        var endpointKey = endpoint.ToString();
        if (cache.TryGetValue(endpointKey, out var cached) && 
            cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Context;
        }

        // Fetch from directory
        try
        {
            // TODO: Query directory by endpoint to get PeerId
            // For now, this is a stub - need to implement reverse lookup
            var descriptor = await FetchDescriptorForEndpointAsync(endpoint, ct);
            if (descriptor == null)
            {
                logger.LogWarning("[PeerResolver] No descriptor found for {Endpoint}", endpoint);
                return null;
            }

            // Verify signature and PeerId derivation
            if (!descriptorSigner.Verify(descriptor))
            {
                logger.LogWarning("[PeerResolver] Invalid descriptor signature for {Endpoint}", endpoint);
                return null;
            }

            // Build PeerContext
            var signingKeys = descriptor.ControlSigningPublicKeys
                .Select(k => Convert.FromBase64String(k))
                .ToList();

            var context = new PeerContext
            {
                PeerId = descriptor.PeerId,
                RemoteEndPoint = endpoint,
                Transport = "quic", // TODO: detect from endpoint
                AllowedControlSigningKeys = signingKeys
            };

            // Cache for 5 minutes
            cache[endpointKey] = new CachedPeerContext
            {
                Context = context,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            };

            return context;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PeerResolver] Failed to resolve peer for {Endpoint}", endpoint);
            return null;
        }
    }

    private async Task<MeshPeerDescriptor?> FetchDescriptorForEndpointAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        // TODO: Implement directory query
        // This requires adding reverse lookup capability to MeshDirectory
        await Task.Delay(0, ct); // Suppress warning
        return null;
    }

    private class CachedPeerContext
    {
        public required PeerContext Context { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
    }
}
```

**Acceptance**: Resolver fetches and verifies descriptors.

**Note**: Requires directory reverse lookup implementation (separate task).

---

### C7: Update KeyStore for Rotation with Overlap

**File**: `src/slskd/Mesh/Overlay/KeyStore.cs`

**Task**: Keep multiple previous keys (not just one)

```csharp
// Change verifyKeys from List to keep last N keys
private readonly List<(byte[] PublicKey, DateTimeOffset ValidUntil)> verifyKeys = new();

// In LoadOrCreate, keep last 2-3 keys for 90 days:
private const int MaxOverlapKeys = 3;
private const int OverlapDays = 90;

// When rotating, mark old keys with expiry:
verifyKeys.Add((current.PublicKey, DateTimeOffset.UtcNow.AddDays(OverlapDays)));

// Filter expired keys:
verifyKeys.RemoveAll(k => k.ValidUntil < DateTimeOffset.UtcNow);

// Expose for descriptor:
public IEnumerable<byte[]> ActiveSigningKeys => 
    verifyKeys.Where(k => k.ValidUntil > DateTimeOffset.UtcNow)
              .Select(k => k.PublicKey)
              .Concat(new[] { current.PublicKey });
```

**Acceptance**: 
- Multiple keys available during rotation
- Old keys expire after overlap period

---

### C8: Register New Services in DI

**File**: `src/slskd/Program.cs`

**Task**: Register identity and descriptor services

```csharp
services.AddSingleton<IIdentityKeyStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FileIdentityKeyStore>>();
    return new FileIdentityKeyStore(logger);
});
services.AddSingleton<IDescriptorSigner, DescriptorSigner>();
services.AddSingleton<IPeerResolver, PeerResolver>();
```

**Acceptance**: Services resolve from DI.

---

## Section D: MeshGateway Security Fixes

**Note**: No MeshGateway found in current codebase. This section is placeholder in case it's added later or exists in different branch.

### D1: Search for Gateway Implementation

**Task**: Verify if MeshGateway exists

```bash
grep -r "Gateway" src/slskd/Mesh/
```

**If not found**: Document that gateway doesn't exist yet and these fixes should be applied when implemented.

**If found**: Apply the following fixes.

---

### D2: Fix IsLocalhost to Exclude 0.0.0.0

**File**: `src/slskd/Mesh/ServiceFabric/MeshGatewayOptions.cs` (if exists)

**Task**: Remove 0.0.0.0 from localhost check

```csharp
// If method exists like:
public bool IsLocalhost(IPAddress address)
{
    // REMOVE: address.ToString() == "0.0.0.0"
    return IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any);
}

// Change to:
public bool IsLocalhost(IPAddress address)
{
    return IPAddress.IsLoopback(address);
}
```

**Acceptance**: 0.0.0.0 is NOT treated as localhost.

---

### D3: Enforce API Key for Non-Local Requests

**File**: `src/slskd/Mesh/ServiceFabric/MeshGatewayAuthMiddleware.cs` (if exists)

**Task**: Require API key when binding to 0.0.0.0

```csharp
// OLD:
if (!isLocalhost && !string.IsNullOrWhiteSpace(_options.ApiKey))
{
    // Check header
}

// NEW:
if (!isLocalhost)
{
    if (string.IsNullOrWhiteSpace(_options.ApiKey))
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("API key required for non-local binding");
        return;
    }
    
    // Check header with constant-time comparison
    var providedKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (!ConstantTimeEquals(_options.ApiKey, providedKey))
    {
        context.Response.StatusCode = 401;
        return;
    }
}
```

**Acceptance**: Non-local requests without key are rejected.

---

### D4: Add Options Validation

**File**: `src/slskd/Mesh/ServiceFabric/MeshGatewayOptions.cs` (if exists)

**Task**: Implement IValidateOptions

```csharp
public class MeshGatewayOptionsValidator : IValidateOptions<MeshGatewayOptions>
{
    public ValidateOptionsResult Validate(string name, MeshGatewayOptions options)
    {
        if (options.BindAddress == "0.0.0.0" && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail(
                "API key is required when binding to 0.0.0.0 (all interfaces)");
        }

        return ValidateOptionsResult.Success;
    }
}
```

Register in Program.cs:
```csharp
services.AddSingleton<IValidateOptions<MeshGatewayOptions>, MeshGatewayOptionsValidator>();
```

**Acceptance**: Startup fails with clear error if config is unsafe.

---

## Section E: Documentation Guardrails

### E1: Remove Premature Pinning Claims

**Files**: 
- `docs/DHT_RENDEZVOUS_DESIGN.md`
- `docs/IMPLEMENTATION_ROADMAP.md`
- `docs/MULTI_SOURCE_DOWNLOADS.md`
- `docs/SECURITY_HARDENING_ROADMAP.md`
- `docs/SECURITY_IMPLEMENTATION_SPECS.md`
- `FEATURES_IN_MERGED_BUILD.md`

**Task**: Update docs to reflect current state

Find lines claiming "certificate pinning" or "TOFU" and either:
1. Mark as "🔴 NOT YET IMPLEMENTED" if it's not done
2. Remove the claim entirely
3. Update to "🟡 IN PROGRESS" when implementing

**Acceptance**: No doc claims pinning is complete until Section A-C are done.

---

### E2: Create Docs Lint Script

**File**: `bin/docs-lint` (NEW)

**Task**: Create script to check docs claims

```bash
#!/usr/bin/env bash
set -e

ERRORS=0

# Check for false pinning claims
if grep -r "certificate pinning" docs/ | grep -v "NOT YET IMPLEMENTED" | grep -v "IN PROGRESS"; then
    echo "ERROR: Found uncaveated 'certificate pinning' claims in docs"
    echo "Ensure all pinning claims are marked as NOT YET IMPLEMENTED or IN PROGRESS until tests pass"
    ERRORS=$((ERRORS + 1))
fi

# Check for RemoteCertificateValidationCallback => true
if grep -r "RemoteCertificateValidationCallback.*=> true" src/slskd/; then
    echo "ERROR: Found insecure cert validation callback in code"
    echo "Replace with proper pinning validation"
    ERRORS=$((ERRORS + 1))
fi

# Check for self-asserted signature verification
if grep -r "envelope.PublicKey" src/slskd/Mesh/Overlay/ControlDispatcher.cs 2>/dev/null; then
    echo "WARNING: ControlDispatcher may be using self-asserted keys"
    echo "Ensure verification uses peer-bound allowed keys"
fi

if [ $ERRORS -gt 0 ]; then
    echo "Docs lint failed with $ERRORS error(s)"
    exit 1
fi

echo "Docs lint passed"
```

Make executable:
```bash
chmod +x bin/docs-lint
```

**Acceptance**: Script detects false claims and insecure patterns.

---

### E3: Add Docs Lint to CI

**File**: `.github/workflows/ci.yml`

**Task**: Add docs lint step

```yaml
- name: Lint Documentation
  run: ./bin/docs-lint
```

**Acceptance**: CI fails if docs drift from implementation.

---

## Testing Requirements

### Unit Tests Needed

1. **CertificatePins**:
   - `ComputeSpkiSha256` is deterministic
   - Works with ECDSA and RSA certs

2. **ControlVerification**:
   - Accepts valid signature from allowed key
   - Rejects signature from unknown key
   - Rejects invalid signature

3. **ReplayCache**:
   - Detects duplicate MessageId
   - Rejects old timestamps
   - Rejects future timestamps
   - Cleans up old entries

4. **IdentityKeyStore**:
   - PeerId derivation is stable
   - Key persists across restarts
   - Never rotates

5. **DescriptorSigner**:
   - Signing produces valid signature
   - Verification accepts valid descriptor
   - Verification rejects tampered descriptor
   - Verification rejects PeerId mismatch

### Integration Tests Needed

1. **QUIC Connection with Pinning**:
   - Connection succeeds with matching SPKI
   - Connection fails with mismatched SPKI
   - TOFU fallback works when no descriptor

2. **Control Envelope with Peer Auth**:
   - Envelope accepted with valid peer key
   - Envelope rejected with unknown peer
   - Replay rejected
   - Timestamp skew rejected

3. **Descriptor Publishing**:
   - Descriptor is signed before publish
   - Fetched descriptor verifies correctly

---

## Implementation Order

### Phase 1: Foundation (A1-A6, C1-C2)
- Certificate persistence
- SPKI computation
- Identity key store
- PeerId derivation

### Phase 2: Verification (B1-B5, A7-A8)
- Replay cache
- Peer-aware verification
- QUIC pinning validation

### Phase 3: Integration (B6-B10, C3-C7)
- Dispatcher updates
- Server updates
- Descriptor signing
- Peer resolver

### Phase 4: Polish (D1-D4, E1-E3)
- Gateway fixes (if applicable)
- Documentation updates
- Lint tooling

---

## Completion Criteria

- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] QUIC connections use persistent certs
- [ ] QUIC connections enforce SPKI pinning
- [ ] Control envelopes authenticated against peer keys (not self-asserted)
- [ ] Replay attacks blocked
- [ ] PeerId is stable and cryptographically derived
- [ ] Descriptors are signed and verified
- [ ] Docs match implementation
- [ ] CI enforces docs accuracy
- [ ] No TODO placeholders remain

---

## Notes for Implementer

1. **Copyright Headers**: All new files use `company="slskdN Team"` per [[memory:11969255]]
2. **No Stubs**: Every piece must be fully implemented or have a task created
3. **Grep First**: Before creating patterns, search for existing similar code
4. **Test Everything**: Minimum one unit test per new class/method
5. **Document Gotchas**: If you find bugs, add to `adr-0001-known-gotchas.md` immediately

---

**End of Security Tasks Document**

