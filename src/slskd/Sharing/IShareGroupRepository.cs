// <copyright file="IShareGroupRepository.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Repository for ShareGroup and ShareGroupMember.</summary>
public interface IShareGroupRepository
{
    Task<ShareGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareGroup>> GetByOwnerAsync(string ownerUserId, CancellationToken cancellationToken = default);
    Task<ShareGroup> AddAsync(ShareGroup entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShareGroup entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddMemberAsync(Guid shareGroupId, string userId, CancellationToken cancellationToken = default);
    Task AddMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid shareGroupId, string userId, CancellationToken cancellationToken = default);
    Task RemoveMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetMemberIdsAsync(Guid shareGroupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareGroupMember>> GetMembersAsync(Guid shareGroupId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid shareGroupId, string userId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken cancellationToken = default);
}
