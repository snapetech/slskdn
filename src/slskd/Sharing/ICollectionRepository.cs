// <copyright file="ICollectionRepository.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Repository for Collection and CollectionItem.</summary>
public interface ICollectionRepository
{
    Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Collection>> GetByOwnerAsync(string ownerUserId, CancellationToken cancellationToken = default);
    Task<Collection> AddAsync(Collection entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Collection entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionItem>> GetItemsAsync(Guid collectionId, CancellationToken cancellationToken = default);
    Task<CollectionItem> AddItemAsync(CollectionItem item, CancellationToken cancellationToken = default);
    Task UpdateItemAsync(CollectionItem item, CancellationToken cancellationToken = default);
    Task<bool> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task ReorderItemsAsync(Guid collectionId, IReadOnlyList<Guid> itemIdsInOrder, CancellationToken cancellationToken = default);
}
