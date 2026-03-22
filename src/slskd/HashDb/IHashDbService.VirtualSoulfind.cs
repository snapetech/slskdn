// <copyright file="IHashDbService.VirtualSoulfind.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;

namespace slskd.HashDb;

/// <summary>
/// Partial hash database contract for Virtual Soulfind pseudonym mappings.
/// </summary>
public partial interface IHashDbService
{
    /// <summary>
    /// Upserts the Virtual Soulfind pseudonym mapping for a Soulseek username.
    /// </summary>
    Task UpsertPseudonymAsync(string soulseekUsername, string peerId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the pseudonym mapped to the specified Soulseek username.
    /// </summary>
    Task<string?> GetPseudonymAsync(string soulseekUsername, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the Soulseek username mapped to the specified pseudonym.
    /// </summary>
    Task<string?> GetUsernameFromPseudonymAsync(string peerId, CancellationToken cancellationToken);
}
