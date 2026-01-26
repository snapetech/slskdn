// <copyright file="IShareGrantRepository.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Repository for ShareGrant (collection share grants). Avoids clash with Shares.IShareRepository (files).</summary>
public interface IShareGrantRepository
{
    Task<ShareGrant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareGrant>> GetByCollectionIdAsync(Guid collectionId, CancellationToken cancellationToken = default);
    /// <summary>Grants where AudienceType=User and AudienceId=userId, or AudienceType=ShareGroup and userId is in that group. Excludes expired.</summary>
    Task<IReadOnlyList<ShareGrant>> GetAccessibleByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<ShareGrant> AddAsync(ShareGrant entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShareGrant entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
