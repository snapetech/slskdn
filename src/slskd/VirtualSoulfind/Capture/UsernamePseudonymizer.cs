namespace slskd.VirtualSoulfind.Capture;

public interface IUsernamePseudonymizer
{
    Task<string> GetPeerIdAsync(string soulseekUsername, CancellationToken ct = default);
    Task<string?> GetUsernameAsync(string peerId, CancellationToken ct = default);
}

/// <summary>
/// Stub pseudonymizer; returns deterministic GUID-based IDs.
/// </summary>
public class UsernamePseudonymizer : IUsernamePseudonymizer
{
    public Task<string> GetPeerIdAsync(string soulseekUsername, CancellationToken ct = default) =>
        Task.FromResult($"peer:vsf:{Guid.NewGuid():N}");

    public Task<string?> GetUsernameAsync(string peerId, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);
}
