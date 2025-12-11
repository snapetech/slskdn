namespace slskd.VirtualSoulfind.Scenes;

using slskd.Jobs;

/// <summary>
/// Interface for scene-scoped job creation.
/// </summary>
public interface ISceneJobService
{
    /// <summary>
    /// Create a label crate job scoped to a scene's popular content.
    /// </summary>
    Task<string> CreateSceneLabelCrateJobAsync(
        string sceneId,
        string targetDir,
        int limit,
        CancellationToken ct = default);
    
    /// <summary>
    /// Create a discography job for a popular artist in a scene.
    /// </summary>
    Task<string> CreateSceneDiscographyJobAsync(
        string sceneId,
        string artistId,
        string targetDir,
        CancellationToken ct = default);
}

/// <summary>
/// Creates download jobs scoped to scene content.
/// </summary>
public class SceneJobService : ISceneJobService
{
    private readonly ILogger<SceneJobService> logger;
    private readonly ISceneMembershipTracker membershipTracker;
    private readonly ILabelCrateJobService labelCrateJobs;
    private readonly IDiscographyJobService discographyJobs;

    public SceneJobService(
        ILogger<SceneJobService> logger,
        ISceneMembershipTracker membershipTracker,
        ILabelCrateJobService labelCrateJobs,
        IDiscographyJobService discographyJobs)
    {
        this.logger = logger;
        this.membershipTracker = membershipTracker;
        this.labelCrateJobs = labelCrateJobs;
        this.discographyJobs = discographyJobs;
    }

    public async Task<string> CreateSceneLabelCrateJobAsync(
        string sceneId,
        string targetDir,
        int limit,
        CancellationToken ct)
    {
        logger.LogInformation("[VSF-SCENE-JOB] Creating label crate job for scene {SceneId}, limit={Limit}",
            sceneId, limit);

        // Get scene metadata to extract popular content
        var metadata = await membershipTracker.GetSceneMetadataAsync(sceneId, ct);
        if (metadata == null)
        {
            throw new InvalidOperationException($"Scene not found: {sceneId}");
        }

        // Extract label name from scene ID (e.g., "scene:label:warp-records" → "Warp Records")
        var labelName = ExtractLabelName(sceneId);

        // Create label crate job
        var jobId = await labelCrateJobs.CreateJobAsync(new LabelCrateJobRequest
        {
            LabelName = labelName,
            LabelId = null,
            Limit = limit
        }, ct);

        logger.LogInformation("[VSF-SCENE-JOB] Created label crate job {JobId} for scene {SceneId}",
            jobId, sceneId);

        return jobId;
    }

    public async Task<string> CreateSceneDiscographyJobAsync(
        string sceneId,
        string artistId,
        string targetDir,
        CancellationToken ct)
    {
        logger.LogInformation("[VSF-SCENE-JOB] Creating discography job for artist {ArtistId} in scene {SceneId}",
            artistId, sceneId);

        // Verify scene exists
        var metadata = await membershipTracker.GetSceneMetadataAsync(sceneId, ct);
        if (metadata == null)
        {
            throw new InvalidOperationException($"Scene not found: {sceneId}");
        }

        // Create discography job
        var jobId = await discographyJobs.CreateJobAsync(new DiscographyJobRequest
        {
            ArtistId = artistId,
            TargetDirectory = targetDir
        }, ct);

        logger.LogInformation("[VSF-SCENE-JOB] Created discography job {JobId} for scene {SceneId}",
            jobId, sceneId);

        return jobId;
    }

    private string ExtractLabelName(string sceneId)
    {
        // "scene:label:warp-records" → "Warp Records"
        if (!sceneId.StartsWith("scene:label:"))
        {
            return sceneId;
        }

        var labelSlug = sceneId.Split(':').Last();
        return string.Join(' ', labelSlug.Split('-').Select(w =>
            char.ToUpper(w[0]) + w.Substring(1)));
    }
}
