using System.Security.Cryptography;

namespace slskd.MediaCore;

/// <summary>
/// Validates content descriptors for size, TTL, signatures.
/// </summary>
public interface IDescriptorValidator
{
    bool Validate(ContentDescriptor descriptor, out string reason);
}

public class DescriptorValidator : IDescriptorValidator
{
    private const int MaxDescriptorBytes = 10 * 1024; // 10 KB
    private const long MaxAgeMs = 3600_000; // 1 hour

    public bool Validate(ContentDescriptor descriptor, out string reason)
    {
        if (string.IsNullOrWhiteSpace(descriptor.ContentId))
        {
            reason = "missing contentId";
            return false;
        }

        // Require at least one strong hash
        if (descriptor.Hashes.Count == 0)
        {
            reason = "missing hashes";
            return false;
        }

        if (descriptor.Signature is null)
        {
            reason = "missing signature";
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - descriptor.Signature.TimestampUnixMs > MaxAgeMs)
        {
            reason = "descriptor too old";
            return false;
        }

        // Size check (serialize roughly to check size)
        var approxSize = EstimateSize(descriptor);
        if (approxSize > MaxDescriptorBytes)
        {
            reason = "descriptor too large";
            return false;
        }

        reason = "ok";
        return true;
    }

    private static int EstimateSize(ContentDescriptor d)
    {
        // Rough estimate to enforce max size
        var hashes = string.Join(",", d.Hashes.Select(h => $"{h.Algorithm}:{h.Hex}"));
        var phash = string.Join(",", d.PerceptualHashes.Select(h => $"{h.Algorithm}:{h.Hex}"));
        var sig = d.Signature is null ? "" : $"{d.Signature.PublicKey}:{d.Signature.Signature}";
        var payload = $"{d.ContentId}|{hashes}|{phash}|{d.SizeBytes}|{d.Codec}|{d.Confidence}|{sig}";
        return System.Text.Encoding.UTF8.GetByteCount(payload);
    }
}















