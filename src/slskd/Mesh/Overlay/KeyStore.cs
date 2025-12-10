using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Overlay;

public interface IKeyStore
{
    Ed25519KeyPair Current { get; }
    IEnumerable<byte[]> VerificationPublicKeys { get; }
}

public class FileKeyStore : IKeyStore
{
    private readonly ILogger<FileKeyStore> logger;
    private readonly OverlayOptions options;
    private Ed25519KeyPair current;
    private readonly List<byte[]> verifyKeys = new();

    public FileKeyStore(ILogger<FileKeyStore> logger, IOptions<OverlayOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
        (current, var previous) = LoadOrCreate();
        if (previous != null)
        {
            verifyKeys.Add(previous.PublicKey);
        }
        verifyKeys.Add(current.PublicKey);
    }

    public Ed25519KeyPair Current => current;
    public IEnumerable<byte[]> VerificationPublicKeys => verifyKeys;

    private (Ed25519KeyPair current, Ed25519KeyPair? previous) LoadOrCreate()
    {
        var path = options.KeyPath;
        var prevPath = $"{path}.prev";

        Ed25519KeyPair? prev = null;

        if (File.Exists(prevPath))
        {
            prev = ReadFromFile(prevPath);
        }

        if (!File.Exists(path))
        {
            var fresh = Ed25519KeyPair.Generate();
            WriteToFile(path, fresh);
            logger.LogInformation("[Overlay] Generated new keypair at {Path}", path);
            return (fresh, prev);
        }

        var existing = ReadFromFile(path);
        if (ShouldRotate(existing))
        {
            if (prev == null)
            {
                WriteToFile(prevPath, existing);
                prev = existing;
            }
            var fresh = Ed25519KeyPair.Generate();
            WriteToFile(path, fresh);
            logger.LogInformation("[Overlay] Rotated keypair at {Path}", path);
            return (fresh, prev);
        }

        return (existing, prev);
    }

    private bool ShouldRotate(Ed25519KeyPair pair)
    {
        if (options.RotateDays <= 0) return false;
        var ageDays = (DateTimeOffset.UtcNow - pair.CreatedAt).TotalDays;
        return ageDays >= options.RotateDays;
    }

    private static void WriteToFile(string path, Ed25519KeyPair pair)
    {
        var json = JsonSerializer.Serialize(pair);
        File.WriteAllText(path, json);
    }

    private static Ed25519KeyPair ReadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var model = JsonSerializer.Deserialize<KeyFileModel>(json);
        if (model == null) throw new InvalidOperationException("Invalid key file");
        return Ed25519KeyPair.FromBase64(model.PublicKey, model.PrivateKey, model.CreatedMs);
    }

    private class KeyFileModel
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public long CreatedMs { get; set; }
    }
}

public class Ed25519KeyPair
{
    public byte[] PrivateKey { get; }
    public byte[] PublicKey { get; }
    public string PublicKeyBase64 => Convert.ToBase64String(PublicKey);
    public DateTimeOffset CreatedAt { get; }

    private Ed25519KeyPair(byte[] pub, byte[] priv, DateTimeOffset createdAt)
    {
        PublicKey = pub;
        PrivateKey = priv;
        CreatedAt = createdAt;
    }

    public static Ed25519KeyPair Generate()
    {
        var seed = RandomNumberGenerator.GetBytes(32);
        var priv = new byte[32];
        Buffer.BlockCopy(seed, 0, priv, 0, 32);
        var pub = new byte[Ed25519.PublicKeySize];
        Ed25519.PublicKeyFromSeed(priv, pub);
        return new Ed25519KeyPair(pub, priv, DateTimeOffset.UtcNow);
    }

    public static Ed25519KeyPair FromBase64(string pub, string priv, long createdMs)
    {
        return new Ed25519KeyPair(Convert.FromBase64String(pub), Convert.FromBase64String(priv),
            DateTimeOffset.FromUnixTimeMilliseconds(createdMs));
    }
}
