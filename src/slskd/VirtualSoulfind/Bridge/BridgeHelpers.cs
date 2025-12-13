namespace slskd.VirtualSoulfind.Bridge;

using slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// Interface for peer ID anonymization for legacy clients.
/// </summary>
public interface IPeerIdAnonymizer
{
    /// <summary>
    /// Get anonymized username for a peer ID.
    /// </summary>
    Task<string> GetAnonymizedUsernameAsync(string peerId, CancellationToken ct = default);
    
    /// <summary>
    /// Get peer ID from anonymized username.
    /// </summary>
    Task<string?> GetPeerIdFromUsernameAsync(string username, CancellationToken ct = default);
}

/// <summary>
/// Peer ID anonymizer - maps overlay peer IDs to friendly usernames.
/// </summary>
public class PeerIdAnonymizer : IPeerIdAnonymizer
{
    private readonly ILogger<PeerIdAnonymizer> logger;
    private readonly ConcurrentDictionary<string, string> peerIdToUsername = new();
    private readonly ConcurrentDictionary<string, string> usernameToPeerId = new();

    public PeerIdAnonymizer(ILogger<PeerIdAnonymizer> logger)
    {
        this.logger = logger;
    }

    public Task<string> GetAnonymizedUsernameAsync(string peerId, CancellationToken ct)
    {
        if (peerIdToUsername.TryGetValue(peerId, out var username))
        {
            return Task.FromResult(username);
        }

        // Generate friendly username: mesh-peer-abc123
        var hash = ComputeShortHash(peerId);
        username = $"mesh-peer-{hash}";

        peerIdToUsername[peerId] = username;
        usernameToPeerId[username] = peerId;

        logger.LogDebug("[VSF-BRIDGE] Anonymized {PeerId} → {Username}", peerId, username);

        return Task.FromResult(username);
    }

    public Task<string?> GetPeerIdFromUsernameAsync(string username, CancellationToken ct)
    {
        usernameToPeerId.TryGetValue(username, out var peerId);
        return Task.FromResult(peerId);
    }

    private string ComputeShortHash(string peerId)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(peerId));
        return Convert.ToHexString(hash).Substring(0, 6).ToLowerInvariant();
    }
}

/// <summary>
/// Interface for filename generation from variants.
/// </summary>
public interface IFilenameGenerator
{
    /// <summary>
    /// Generate friendly filename from variant hint.
    /// </summary>
    Task<string> GenerateFilenameAsync(
        string artist,
        string title,
        VariantHint variant,
        CancellationToken ct = default);
}

/// <summary>
/// Filename generator - creates friendly filenames for legacy clients.
/// </summary>
public class FilenameGenerator : IFilenameGenerator
{
    private readonly ILogger<FilenameGenerator> logger;

    public FilenameGenerator(ILogger<FilenameGenerator> logger)
    {
        this.logger = logger;
    }

    public Task<string> GenerateFilenameAsync(
        string artist,
        string title,
        VariantHint variant,
        CancellationToken ct)
    {
        // Generate: "Artist - Title [Codec BitrateKbps].ext"
        var filename = $"{artist} - {title} [{variant.Codec} {variant.BitrateKbps}kbps].{variant.Codec.ToLowerInvariant()}";

        // Sanitize for filesystem
        filename = SanitizeFilename(filename);

        logger.LogDebug("[VSF-BRIDGE] Generated filename: {Filename}", filename);

        return Task.FromResult(filename);
    }

    private string SanitizeFilename(string filename)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>
/// Interface for room-scene mapping.
/// </summary>
public interface IRoomSceneMapper
{
    /// <summary>
    /// Map legacy room name to scene ID.
    /// </summary>
    string MapRoomToScene(string roomName);
    
    /// <summary>
    /// Map scene ID to legacy room name.
    /// </summary>
    string MapSceneToRoom(string sceneId);
}

/// <summary>
/// Room-scene mapper for legacy compatibility.
/// </summary>
public class RoomSceneMapper : IRoomSceneMapper
{
    private readonly ILogger<RoomSceneMapper> logger;

    public RoomSceneMapper(ILogger<RoomSceneMapper> logger)
    {
        this.logger = logger;
    }

    public string MapRoomToScene(string roomName)
    {
        // "warp" → "scene:label:warp-records"
        // "techno" → "scene:genre:techno"
        
        var normalized = roomName.ToLowerInvariant().Replace(" ", "-");

        // Heuristic: if it looks like a label, treat as label scene
        if (IsLabelRoom(roomName))
        {
            return $"scene:label:{normalized}";
        }

        // Otherwise treat as genre scene
        return $"scene:genre:{normalized}";
    }

    public string MapSceneToRoom(string sceneId)
    {
        // "scene:label:warp-records" → "warp"
        // "scene:genre:techno" → "techno"
        
        var parts = sceneId.Split(':');
        if (parts.Length >= 3)
        {
            return parts[2].Replace("-", " ");
        }

        return sceneId;
    }

    private bool IsLabelRoom(string roomName)
    {
        // Heuristic: common label keywords
        var labelKeywords = new[] { "records", "music", "label", "recordings" };
        var lower = roomName.ToLowerInvariant();
        return labelKeywords.Any(keyword => lower.Contains(keyword));
    }
}
















