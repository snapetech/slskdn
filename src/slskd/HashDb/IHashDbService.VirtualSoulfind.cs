// <copyright file="IHashDbService.VirtualSoulfind.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;

namespace slskd.HashDb;

public partial interface IHashDbService
{
    // Pseudonym mappings for Virtual Soulfind
    Task UpsertPseudonymAsync(string soulseekUsername, string peerId, CancellationToken cancellationToken);
    Task<string?> GetPseudonymAsync(string soulseekUsername, CancellationToken cancellationToken);
    Task<string?> GetUsernameFromPseudonymAsync(string peerId, CancellationToken cancellationToken);
}
