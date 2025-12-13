using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Dht;

/// <summary>
/// Publishes our peer descriptor to the mesh DHT and refreshes it.
/// </summary>
public interface IPeerDescriptorPublisher
{
    Task PublishSelfAsync(CancellationToken ct = default);
}

public class PeerDescriptorPublisher : IPeerDescriptorPublisher
{
    private readonly ILogger<PeerDescriptorPublisher> logger;
    private readonly IMeshDhtClient dht;
    private readonly MeshOptions options;
    private readonly INatDetector natDetector;
    private readonly Security.IIdentityKeyStore identityKeyStore;
    private readonly Security.IDescriptorSigner descriptorSigner;
    private readonly Overlay.OverlayOptions overlayOptions;
    private readonly Overlay.DataOverlayOptions dataOverlayOptions;
    private readonly Overlay.IKeyStore controlKeyStore;

    private ulong nextSeq = 1; // Monotonically increasing per publish

    public PeerDescriptorPublisher(
        ILogger<PeerDescriptorPublisher> logger,
        IMeshDhtClient dht,
        IOptions<MeshOptions> options,
        INatDetector natDetector,
        Security.IIdentityKeyStore identityKeyStore,
        Security.IDescriptorSigner descriptorSigner,
        IOptions<Overlay.OverlayOptions> overlayOptions,
        IOptions<Overlay.DataOverlayOptions> dataOverlayOptions,
        Overlay.IKeyStore controlKeyStore)
    {
        this.logger = logger;
        this.dht = dht;
        this.options = options.Value;
        this.natDetector = natDetector;
        this.identityKeyStore = identityKeyStore;
        this.descriptorSigner = descriptorSigner;
        this.overlayOptions = overlayOptions.Value;
        this.dataOverlayOptions = dataOverlayOptions.Value;
        this.controlKeyStore = controlKeyStore;

        // Initialize nextSeq from current time (ensures uniqueness across restarts)
        nextSeq = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public async Task PublishSelfAsync(CancellationToken ct = default)
    {
        var nat = await natDetector.DetectAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // Load TLS certificates to get SPKI pins
        var controlCert = Security.PersistentCertificate.LoadOrCreate(
            this.overlayOptions.TlsCertPath,
            this.overlayOptions.TlsCertPassword,
            "CN=mesh-overlay-control",
            5);

        var dataCert = Security.PersistentCertificate.LoadOrCreate(
            this.dataOverlayOptions.TlsCertPath,
            this.dataOverlayOptions.TlsCertPassword,
            "CN=mesh-overlay-data",
            5);

        // Build TLS pin lists (current cert only for now; add previous on rotation)
        var controlPins = new List<TlsPin>
        {
            new TlsPin
            {
                SpkiSha256 = Security.CertificatePins.ComputeSpkiSha256Base64(controlCert),
                ValidFromUnixMs = now.ToUnixTimeMilliseconds(),
                ValidToUnixMs = now.AddYears(5).ToUnixTimeMilliseconds(),
            },
        };

        var dataPins = new List<TlsPin>
        {
            new TlsPin
            {
                SpkiSha256 = Security.CertificatePins.ComputeSpkiSha256Base64(dataCert),
                ValidFromUnixMs = now.ToUnixTimeMilliseconds(),
                ValidToUnixMs = now.AddYears(5).ToUnixTimeMilliseconds(),
            },
        };

        // Build control signing key list (current + overlap keys for rotation)
        var controlKeys = controlKeyStore.VerificationPublicKeys
            .Select(k => new ControlSigningKey
            {
                PublicKey = Convert.ToBase64String(k),
                ValidFromUnixMs = now.ToUnixTimeMilliseconds(),
                ValidToUnixMs = now.AddYears(1).ToUnixTimeMilliseconds(), // 1 year validity per key
            })
            .ToList();

        var descriptor = new MeshPeerDescriptor
        {
            PeerId = identityKeyStore.ComputePeerId(),
            Endpoints = options.SelfEndpoints
                .Concat(options.RelayEndpoints ?? new List<string>())
                .ToList(),
            NatType = nat.ToString().ToLowerInvariant(),
            RelayRequired = nat == NatType.Symmetric,
            TimestampUnixMs = now.ToUnixTimeMilliseconds(),
            IdentityPublicKey = Convert.ToBase64String(identityKeyStore.PublicKey),
            TlsControlPins = controlPins,
            TlsDataPins = dataPins,
            ControlSigningKeys = controlKeys,
            SchemaVersion = 1,
            IssuedAtUnixMs = now.ToUnixTimeMilliseconds(),
            ExpiresAtUnixMs = now.AddDays(7).ToUnixTimeMilliseconds(),
            DescriptorSeq = nextSeq++, // Increment sequence number
        };

        // Sign the descriptor with identity key
        descriptorSigner.Sign(descriptor, identityKeyStore.PrivateKey);

        var key = $"mesh:peer:{descriptor.PeerId}";
        await dht.PutAsync(key, descriptor, ttlSeconds: 3600, ct: ct);
        logger.LogInformation(
            "[MeshDHT] Published signed descriptor {PeerId} seq={Seq} endpoints={Count} nat={Nat} expires={Expires}",
            descriptor.PeerId,
            descriptor.DescriptorSeq,
            descriptor.Endpoints.Count,
            descriptor.NatType,
            DateTimeOffset.FromUnixTimeMilliseconds(descriptor.ExpiresAtUnixMs));
    }
}
