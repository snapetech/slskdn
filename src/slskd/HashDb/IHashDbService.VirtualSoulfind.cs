namespace slskd.HashDb;

public partial interface IHashDbService
{
    // Pseudonym mappings for Virtual Soulfind
    Task UpsertPseudonymAsync(string soulseekUsername, string peerId, CancellationToken cancellationToken);
    Task<string?> GetPseudonymAsync(string soulseekUsername, CancellationToken cancellationToken);
    Task<string?> GetUsernameFromPseudonymAsync(string peerId, CancellationToken cancellationToken);
}
