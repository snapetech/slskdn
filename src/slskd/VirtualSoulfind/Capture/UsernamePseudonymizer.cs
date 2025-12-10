namespace slskd.VirtualSoulfind.Capture;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Maps Soulseek usernames to overlay peer IDs for privacy.
/// </summary>
public interface IUsernamePseudonymizer
{
    /// <summary>
    /// Get or create a pseudonymized peer ID for a Soulseek username.
    /// </summary>
    Task<string> GetPeerIdAsync(string soulseekUsername, CancellationToken ct = default);
    
    /// <summary>
    /// Reverse lookup: get Soulseek username from peer ID (if known).
    /// </summary>
    Task<string?> GetUsernameAsync(string peerId, CancellationToken ct = default);
}

/// <summary>
/// Pseudonymizes Soulseek usernames to protect privacy in shadow index.
/// </summary>
public class UsernamePseudonymizer : IUsernamePseudonymizer
{
    private readonly ILogger<UsernamePseudonymizer> logger;
    private readonly IHashDbService hashDb;
    private readonly byte[] salt;

    public UsernamePseudonymizer(
        ILogger<UsernamePseudonymizer> logger,
        IHashDbService hashDb)
    {
        this.logger = logger;
        this.hashDb = hashDb;
        
        // Generate or load a persistent salt for this instance
        // In production, this should be stored securely in config
        this.salt = Encoding.UTF8.GetBytes("slskdn-vsf-salt-v1");
    }

    public async Task<string> GetPeerIdAsync(string soulseekUsername, CancellationToken ct)
    {
        // Check if we already have a mapping
        var existingPeerId = await hashDb.GetPseudonymAsync(soulseekUsername, ct);
        if (existingPeerId != null)
        {
            return existingPeerId;
        }

        // Generate new pseudonymous peer ID
        var peerId = GeneratePeerId(soulseekUsername);
        
        // Store mapping
        await hashDb.UpsertPseudonymAsync(soulseekUsername, peerId, ct);
        
        logger.LogDebug("[VSF-PSEUDO] Generated peer ID for username {Username}", soulseekUsername);
        
        return peerId;
    }

    public async Task<string?> GetUsernameAsync(string peerId, CancellationToken ct)
    {
        return await hashDb.GetUsernameFromPseudonymAsync(peerId, ct);
    }

    private string GeneratePeerId(string soulseekUsername)
    {
        // Use HMAC-SHA256 with salt to generate deterministic but pseudonymous ID
        using var hmac = new HMACSHA256(salt);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(soulseekUsername));
        
        // Take first 20 bytes and encode as base32 for readability
        // Format: peer:vsf:<base32>
        var base32 = Base32Encode(hash.Take(20).ToArray());
        return $"peer:vsf:{base32}";
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
        var result = new StringBuilder();
        
        int buffer = data[0];
        int next = 1;
        int bitsLeft = 8;
        
        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer <<= 8;
                    buffer |= data[next++];
                    bitsLeft += 8;
                }
                else
                {
                    int pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }
            
            int index = (buffer >> (bitsLeft - 5)) & 0x1F;
            bitsLeft -= 5;
            result.Append(alphabet[index]);
        }
        
        return result.ToString();
    }
}
