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
    private readonly Ed25519KeyPair keyPair;

    public ControlSigner(ILogger<ControlSigner> logger, Ed25519KeyPair keyPair)
    {
        this.logger = logger;
        this.keyPair = keyPair;
    }

    public ControlEnvelope Sign(ControlEnvelope envelope)
    {
        envelope.PublicKey = keyPair.PublicKeyBase64;
        envelope.Signature = ComputeSignature(envelope);
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
            var pub = Convert.FromBase64String(envelope.PublicKey);
            var sig = Convert.FromBase64String(envelope.Signature);
            var payload = BuildSignablePayload(envelope);
            return Ed25519.Verify(sig, Encoding.UTF8.GetBytes(payload), pub);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay] Verify failed");
            return false;
        }
    }

    private string ComputeSignature(ControlEnvelope envelope)
    {
        var payload = BuildSignablePayload(envelope);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var sig = Ed25519.Sign(bytes, keyPair.PrivateKey);
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
