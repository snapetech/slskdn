namespace slskd.PodCore;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soulseek;

/// <summary>
/// Manages the Gold Star Club pod - auto-join for the first 1000 network members.
/// Once the pod reaches 1000 members, no new members can be added, even if people leave.
/// </summary>
public interface IGoldStarClubService
{
    /// <summary>
    /// Gets the Gold Star Club pod ID.
    /// </summary>
    string GoldStarClubPodId { get; }

    /// <summary>
    /// Gets the maximum membership limit (1000).
    /// </summary>
    int MaxMembership { get; }

    /// <summary>
    /// Gets whether the Gold Star Club is still accepting new members.
    /// </summary>
    Task<bool> IsAcceptingMembersAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current membership count.
    /// </summary>
    Task<int> GetMembershipCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Attempts to auto-join the current user to the Gold Star Club if eligible.
    /// </summary>
    Task<bool> TryAutoJoinAsync(string peerId, CancellationToken ct = default);

    /// <summary>
    /// Ensures the Gold Star Club pod exists (creates it if it doesn't).
    /// </summary>
    Task EnsurePodExistsAsync(CancellationToken ct = default);
}

/// <summary>
/// Implements Gold Star Club auto-join logic.
/// </summary>
public class GoldStarClubService : BackgroundService, IGoldStarClubService
{
    public const string GoldStarClubPodId = "pod:gold-star-club";
    public const int MaxMembership = 1000;

    private readonly IPodService podService;
    private readonly ISoulseekClient soulseekClient;
    private readonly ILogger<GoldStarClubService> logger;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private bool podInitialized;
    private bool? isAcceptingMembers; // null = not checked yet, true/false = cached result

    public GoldStarClubService(
        IPodService podService,
        ISoulseekClient soulseekClient,
        ILogger<GoldStarClubService> logger)
    {
        this.podService = podService;
        this.soulseekClient = soulseekClient;
        this.logger = logger;
    }

    string IGoldStarClubService.GoldStarClubPodId => "pod:gold-star-club";
    int IGoldStarClubService.MaxMembership => 1000;

    public async Task<bool> IsAcceptingMembersAsync(CancellationToken ct = default)
    {
        // If we've already checked and cached the result, return it
        if (isAcceptingMembers.HasValue)
        {
            return isAcceptingMembers.Value;
        }

        // Ensure pod exists first
        await EnsurePodExistsAsync(ct);

        // Check current membership count
        var count = await GetMembershipCountAsync(ct);
        var accepting = count < MaxMembership;
        
        // Cache the result
        isAcceptingMembers = accepting;

        if (!accepting)
        {
            logger.LogInformation("[GoldStarClub] Membership limit reached ({Count}/{Max}). No new members will be accepted.", count, MaxMembership);
        }

        return accepting;
    }

    public async Task<int> GetMembershipCountAsync(CancellationToken ct = default)
    {
        await EnsurePodExistsAsync(ct);
        var members = await podService.GetMembersAsync(GoldStarClubPodId, ct);
        return members.Count;
    }

    public async Task<bool> TryAutoJoinAsync(string peerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            logger.LogWarning("[GoldStarClub] Cannot auto-join: peerId is null or empty");
            return false;
        }

        // Ensure pod exists
        await EnsurePodExistsAsync(ct);

        // Check if already a member
        var members = await podService.GetMembersAsync(GoldStarClubPodId, ct);
        if (members.Any(m => m.PeerId == peerId))
        {
            logger.LogDebug("[GoldStarClub] Peer {PeerId} is already a member", peerId);
            return true; // Already a member
        }

        // Check if we're still accepting members
        var accepting = await IsAcceptingMembersAsync(ct);
        if (!accepting)
        {
            logger.LogInformation("[GoldStarClub] Cannot auto-join {PeerId}: membership limit reached", peerId);
            return false;
        }

        // Check current count before joining (race condition protection)
        var currentCount = members.Count;
        if (currentCount >= MaxMembership)
        {
            logger.LogInformation("[GoldStarClub] Cannot auto-join {PeerId}: membership limit reached (current: {Count})", peerId, currentCount);
            isAcceptingMembers = false; // Update cache
            return false;
        }

        // Attempt to join
        var member = new PodMember
        {
            PeerId = peerId,
            Role = "member"
        };

        var joined = await podService.JoinAsync(GoldStarClubPodId, member, ct);
        
        if (joined)
        {
            var newCount = await GetMembershipCountAsync(ct);
            logger.LogInformation("[GoldStarClub] ✓ Auto-joined {PeerId} to Gold Star Club ({Count}/{Max})", peerId, newCount, MaxMembership);
            
            // If we just hit the limit, update cache
            if (newCount >= MaxMembership)
            {
                isAcceptingMembers = false;
                logger.LogInformation("[GoldStarClub] 🎉 Gold Star Club is now full! No new members will be accepted.");
            }
        }
        else
        {
            logger.LogWarning("[GoldStarClub] Failed to auto-join {PeerId}", peerId);
        }

        return joined;
    }

    public async Task EnsurePodExistsAsync(CancellationToken ct = default)
    {
        if (podInitialized)
        {
            return; // Already checked
        }

        await initializationLock.WaitAsync(ct);
        try
        {
            if (podInitialized)
            {
                return; // Double-check after acquiring lock
            }

            // Check if pod already exists
            var existingPod = await podService.GetPodAsync(GoldStarClubPodId, ct);
            if (existingPod != null)
            {
                logger.LogDebug("[GoldStarClub] Pod already exists");
                podInitialized = true;
                return;
            }

            // Create the Gold Star Club pod
            var pod = new Pod
            {
                PodId = GoldStarClubPodId,
                Name = "Gold Star Club ⭐",
                Visibility = PodVisibility.Listed,
                Tags = new List<string> { "gold-star", "first-1000", "exclusive" },
                Channels = new List<PodChannel>
                {
                    new PodChannel
                    {
                        ChannelId = $"{GoldStarClubPodId}:general",
                        Kind = PodChannelKind.General,
                        Name = "General"
                    }
                }
            };

            await podService.CreateAsync(pod, ct);
            logger.LogInformation("[GoldStarClub] Created Gold Star Club pod (max {Max} members)", MaxMembership);
            
            podInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Soulseek client to be connected
        await WaitForConnectionAsync(stoppingToken);

        // Ensure pod exists
        await EnsurePodExistsAsync(stoppingToken);

        // Get current user's peer ID (username)
        var username = soulseekClient.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            logger.LogWarning("[GoldStarClub] Cannot determine username, skipping auto-join");
            return;
        }

        // Try to auto-join
        await TryAutoJoinAsync(username, stoppingToken);
    }

    private async Task WaitForConnectionAsync(CancellationToken stoppingToken)
    {
        // Wait up to 30 seconds for connection
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!string.IsNullOrWhiteSpace(soulseekClient.Username))
            {
                logger.LogDebug("[GoldStarClub] Soulseek client connected");
                return;
            }

            if (DateTime.UtcNow - startTime > timeout)
            {
                logger.LogWarning("[GoldStarClub] Timeout waiting for Soulseek connection");
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}















