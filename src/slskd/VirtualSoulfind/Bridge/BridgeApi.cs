namespace slskd.VirtualSoulfind.Bridge;

using slskd.VirtualSoulfind.ShadowIndex;
using slskd.VirtualSoulfind.Scenes;
using slskd.Integrations.MusicBrainz;

/// <summary>
/// Interface for bridge API endpoints.
/// </summary>
public interface IBridgeApi
{
    /// <summary>
    /// Bridge search (legacy client → mesh).
    /// </summary>
    Task<BridgeSearchResult> SearchAsync(string query, CancellationToken ct = default);
    
    /// <summary>
    /// Bridge download (legacy client → mesh).
    /// </summary>
    Task<string> DownloadAsync(string username, string filename, string targetPath, CancellationToken ct = default);
    
    /// <summary>
    /// Bridge rooms (legacy rooms → scenes).
    /// </summary>
    Task<List<BridgeRoom>> GetRoomsAsync(CancellationToken ct = default);
}

/// <summary>
/// Bridge search result (legacy format).
/// </summary>
public class BridgeSearchResult
{
    public string Query { get; set; } = string.Empty;
    public List<BridgeUser> Users { get; set; } = new();
}

/// <summary>
/// Bridge user result.
/// </summary>
public class BridgeUser
{
    public string Username { get; set; } = string.Empty;
    public List<BridgeFile> Files { get; set; } = new();
    public int FreeSlots { get; set; } = 1;
    public int Speed { get; set; } = 1000000; // 1 Mbps
}

/// <summary>
/// Bridge file result.
/// </summary>
public class BridgeFile
{
    public string Filename { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Extension { get; set; } = string.Empty;
    public int? Bitrate { get; set; }
    public int? Length { get; set; }
}

/// <summary>
/// Bridge room (scene → legacy room).
/// </summary>
public class BridgeRoom
{
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
}

/// <summary>
/// Bridge API implementation.
/// </summary>
public class BridgeApi : IBridgeApi
{
    private readonly ILogger<BridgeApi> logger;
    private readonly IShadowIndexQuery shadowIndex;
    private readonly IMusicBrainzClient musicBrainz;
    private readonly ISceneService sceneService;
    private readonly IPeerIdAnonymizer peerAnonymizer;
    private readonly IFilenameGenerator filenameGenerator;

    public BridgeApi(
        ILogger<BridgeApi> logger,
        IShadowIndexQuery shadowIndex,
        IMusicBrainzClient musicBrainz,
        ISceneService sceneService,
        IPeerIdAnonymizer peerAnonymizer,
        IFilenameGenerator filenameGenerator)
    {
        this.logger = logger;
        this.shadowIndex = shadowIndex;
        this.musicBrainz = musicBrainz;
        this.sceneService = sceneService;
        this.peerAnonymizer = peerAnonymizer;
        this.filenameGenerator = filenameGenerator;
    }

    public async Task<BridgeSearchResult> SearchAsync(string query, CancellationToken ct)
    {
        logger.LogInformation("[VSF-BRIDGE] Legacy search: {Query}", query);

        // Parse query to extract artist/title
        var (artist, title) = ParseLegacyQuery(query);

        if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
        {
            logger.LogWarning("[VSF-BRIDGE] Could not parse legacy query: {Query}", query);
            return new BridgeSearchResult { Query = query };
        }

        // Resolve to MBIDs
        var mbResults = await musicBrainz.SearchRecordingAsync(
            $"artist:\"{artist}\" AND recording:\"{title}\"",
            ct);

        if (mbResults == null || mbResults.Count == 0)
        {
            return new BridgeSearchResult { Query = query };
        }

        // Query shadow index for each MBID
        var users = new List<BridgeUser>();

        foreach (var recording in mbResults.Take(3))
        {
            var shadowResult = await shadowIndex.QueryAsync(recording.Id, ct);
            if (shadowResult != null)
            {
                foreach (var peerId in shadowResult.PeerIds)
                {
                    var username = await peerAnonymizer.GetAnonymizedUsernameAsync(peerId, ct);

                    var user = users.FirstOrDefault(u => u.Username == username);
                    if (user == null)
                    {
                        user = new BridgeUser { Username = username };
                        users.Add(user);
                    }

                    // Generate filenames from variants
                    foreach (var variant in shadowResult.CanonicalVariants)
                    {
                        var filename = await filenameGenerator.GenerateFilenameAsync(
                            artist, title, variant, ct);

                        user.Files.Add(new BridgeFile
                        {
                            Filename = filename,
                            Size = variant.SizeBytes,
                            Extension = variant.Codec.ToLowerInvariant(),
                            Bitrate = variant.BitrateKbps,
                            Length = (int)(variant.SizeBytes / (variant.BitrateKbps * 125))
                        });
                    }
                }
            }
        }

        logger.LogInformation("[VSF-BRIDGE] Legacy search complete: {UserCount} users, {FileCount} files",
            users.Count, users.Sum(u => u.Files.Count));

        return new BridgeSearchResult
        {
            Query = query,
            Users = users
        };
    }

    public Task<string> DownloadAsync(string username, string filename, string targetPath, CancellationToken ct)
    {
        logger.LogInformation("[VSF-BRIDGE] Legacy download: {Username}/{Filename}", username, filename);

        // Map anonymized username back to peer ID
        // Initiate mesh transfer
        // Return transfer ID

        // TODO: Implement mesh transfer initiation
        return Task.FromResult(Ulid.NewUlid().ToString());
    }

    public async Task<List<BridgeRoom>> GetRoomsAsync(CancellationToken ct)
    {
        logger.LogDebug("[VSF-BRIDGE] Legacy get rooms");

        // Map scenes to legacy rooms
        var scenes = await sceneService.GetJoinedScenesAsync(ct);

        var rooms = scenes.Select(scene => new BridgeRoom
        {
            Name = scene.DisplayName,
            UserCount = scene.MemberCount
        }).ToList();

        return rooms;
    }

    private (string artist, string title) ParseLegacyQuery(string query)
    {
        // Legacy format: "artist title" or "artist - title"
        var dashIndex = query.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            return (query.Substring(0, dashIndex).Trim(), query.Substring(dashIndex + 3).Trim());
        }

        // Try space-based splitting (first word = artist)
        var parts = query.Split(' ', 2);
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }

        return (string.Empty, query);
    }
}
