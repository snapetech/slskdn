namespace slskd.PodCore;

/// <summary>
/// Pod service for create/list/join/leave.
/// </summary>
public interface IPodService
{
    Task<Pod> CreateAsync(Pod pod, CancellationToken ct = default);
    Task<IReadOnlyList<Pod>> ListAsync(CancellationToken ct = default);
    Task<bool> JoinAsync(string podId, PodMember member, CancellationToken ct = default);
    Task<bool> LeaveAsync(string podId, string peerId, CancellationToken ct = default);
    Task<bool> BanAsync(string podId, string peerId, CancellationToken ct = default);
}

/// <summary>
/// In-memory pod service (stub).
/// </summary>
public class PodService : IPodService
{
    private readonly Dictionary<string, Pod> pods = new();

    public Task<Pod> CreateAsync(Pod pod, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pod.PodId))
        {
            pod.PodId = $"pod:{Guid.NewGuid():N}";
        }
        pods[pod.PodId] = pod;
        return Task.FromResult(pod);
    }

    public Task<IReadOnlyList<Pod>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<Pod>)pods.Values.ToList());

    public Task<bool> JoinAsync(string podId, PodMember member, CancellationToken ct = default)
    {
        if (!pods.TryGetValue(podId, out var pod)) return Task.FromResult(false);
        var existing = pod.Channels; // no-op for now
        return Task.FromResult(true);
    }

    public Task<bool> LeaveAsync(string podId, string peerId, CancellationToken ct = default)
    {
        if (!pods.TryGetValue(podId, out var pod)) return Task.FromResult(false);
        return Task.FromResult(true);
    }

    public Task<bool> BanAsync(string podId, string peerId, CancellationToken ct = default)
    {
        if (!pods.TryGetValue(podId, out var pod)) return Task.FromResult(false);
        return Task.FromResult(true);
    }
}

/// <summary>
/// Pod messaging stub (signature/dedupe placeholder).
/// </summary>
public interface IPodMessaging
{
    Task<bool> SendAsync(PodMessage message, CancellationToken ct = default);
}

public class PodMessaging : IPodMessaging
{
    public Task<bool> SendAsync(PodMessage message, CancellationToken ct = default)
    {
        // TODO: signature validation, membership check, dedupe
        return Task.FromResult(true);
    }
}

/// <summary>
/// Soulseek chat bridge stub for bound channels.
/// </summary>
public interface ISoulseekChatBridge
{
    Task<bool> BindRoomAsync(string podId, string roomName, string mode, CancellationToken ct = default);
}

public class SoulseekChatBridge : ISoulseekChatBridge
{
    public Task<bool> BindRoomAsync(string podId, string roomName, string mode, CancellationToken ct = default)
    {
        // TODO: implement readonly/mirror wiring
        return Task.FromResult(true);
    }
}
