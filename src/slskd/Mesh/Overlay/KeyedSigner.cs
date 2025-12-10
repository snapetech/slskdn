using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Signs and verifies control envelopes using Ed25519.
/// </summary>
public interface IControlSigner
{
    ControlEnvelope Sign(ControlEnvelope envelope);
    bool Verify(ControlEnvelope envelope);
}

public class ControlSigner : IControlSigner
{
    private readonly ILogger<ControlSigner> logger;
    private readonly IKeyStore keyStore;

    public ControlSigner(ILogger<ControlSigner> logger, IKeyStore keyStore)
    {
        this.logger = logger;
        this.keyStore = keyStore;
    }

    public ControlEnvelope Sign(ControlEnvelope envelope)
    {
        envelope.PublicKey = keyStore.Current.PublicKeyBase64;
        envelope.Signature = ComputeSignature(envelope, keyStore.Current.PrivateKey);
        return envelope;
    }

    public bool Verify(ControlEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.PublicKey) || string.IsNullOrWhiteSpace(envelope.Signature))
        {
            return false;
        }

        try
        {
            var sig = Convert.FromBase64String(envelope.Signature);
            var payload = BuildSignablePayload(envelope);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            foreach (var pub in keyStore.VerificationPublicKeys)
            {
                if (Ed25519.Verify(sig, payloadBytes, pub))
                {
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay] Verify failed");
            return false;
        }
    }

    private string ComputeSignature(ControlEnvelope envelope, byte[] privateKey)
    {
        var payload = BuildSignablePayload(envelope);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var sig = Ed25519.Sign(bytes, privateKey);
        return Convert.ToBase64String(sig);
    }

    private static string BuildSignablePayload(ControlEnvelope env) =>
        $"{env.Type}|{env.TimestampUnixMs}|{Convert.ToBase64String(env.Payload)}";
}

/// <summary>
/// Simple holder for Ed25519 key pair.
/// </summary>
public class Ed25519KeyPair
{
    public byte[] PrivateKey { get; }
    public byte[] PublicKey { get; }
    public string PublicKeyBase64 => Convert.ToBase64String(PublicKey);

    public Ed25519KeyPair()
    {
        // Generate ephemeral pair; in real deployments persist securely
        var seed = RandomNumberGenerator.GetBytes(32);
        PrivateKey = new byte[32];
        Buffer.BlockCopy(seed, 0, PrivateKey, 0, 32);
        PublicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.PublicKeyFromSeed(PrivateKey, PublicKey);
    }
}
