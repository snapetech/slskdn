namespace slskd.VirtualSoulfind.ShadowIndex;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Derives DHT keys for Virtual Soulfind shadow index.
/// </summary>
public static class DhtKeyDerivation
{
    private const string NAMESPACE_MBID_RELEASE = "slskdn-vsf-mbid-release-v1";
    private const string NAMESPACE_MBID_RECORDING = "slskdn-vsf-mbid-recording-v1";
    private const string NAMESPACE_MBID_ARTIST = "slskdn-vsf-mbid-artist-v1";
    private const string NAMESPACE_SCENE = "slskdn-vsf-scene-v1";
    private const string NAMESPACE_SCENE_MEMBERS = "slskdn-vsf-scene-members-v1";

    /// <summary>
    /// Derive DHT key for a MusicBrainz release.
    /// </summary>
    public static byte[] DeriveReleaseKey(string mbReleaseId)
    {
        return SHA1.HashData(Encoding.UTF8.GetBytes($"{NAMESPACE_MBID_RELEASE}:{mbReleaseId}"));
    }

    /// <summary>
    /// Derive DHT key for a MusicBrainz recording.
    /// </summary>
    public static byte[] DeriveRecordingKey(string mbRecordingId)
    {
        return SHA1.HashData(Encoding.UTF8.GetBytes($"{NAMESPACE_MBID_RECORDING}:{mbRecordingId}"));
    }

    /// <summary>
    /// Derive DHT key for a MusicBrainz artist.
    /// </summary>
    public static byte[] DeriveArtistKey(string mbArtistId)
    {
        return SHA1.HashData(Encoding.UTF8.GetBytes($"{NAMESPACE_MBID_ARTIST}:{mbArtistId}"));
    }

    /// <summary>
    /// Derive DHT key for a scene.
    /// </summary>
    public static byte[] DeriveSceneKey(string sceneId)
    {
        return SHA1.HashData(Encoding.UTF8.GetBytes($"{NAMESPACE_SCENE}:{sceneId}"));
    }

    /// <summary>
    /// Derive DHT key for scene membership list.
    /// </summary>
    public static byte[] DeriveSceneMembersKey(string sceneId)
    {
        return SHA1.HashData(Encoding.UTF8.GetBytes($"{NAMESPACE_SCENE_MEMBERS}:{sceneId}"));
    }

    /// <summary>
    /// Convert DHT key to hex string for logging/debugging.
    /// </summary>
    public static string ToHexString(byte[] key)
    {
        return Convert.ToHexString(key).ToLowerInvariant();
    }

    /// <summary>
    /// Parse DHT key back to namespace and ID (best effort).
    /// </summary>
    public static bool TryParseKey(byte[] key, out string? keyType, out string? id)
    {
        // This is a one-way hash, so we can't truly reverse it
        // This method is primarily for debugging/logging
        keyType = null;
        id = null;
        return false;
    }
}
















