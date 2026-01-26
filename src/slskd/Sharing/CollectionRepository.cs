// <copyright file="CollectionRepository.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of ICollectionRepository.</summary>
public sealed class CollectionRepository : ICollectionRepository
{
    private readonly IDbContextFactory<CollectionsDbContext> _factory;

    public CollectionRepository(IDbContextFactory<CollectionsDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Collection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Collections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Collection>> GetByOwnerAsync(string ownerUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Collections.AsNoTracking().Where(x => x.OwnerUserId == ownerUserId).OrderBy(x => x.Title).ToListAsync(cancellationToken);
    }

    public async Task<Collection> AddAsync(Collection entity, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        db.Collections.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Collection entity, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        entity.UpdatedAt = DateTime.UtcNow;
        db.Collections.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var e = await db.Collections.FindAsync([id], cancellationToken);
        if (e == null) return false;
        db.Collections.Remove(e);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CollectionItem>> GetItemsAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.CollectionItems.AsNoTracking().Where(x => x.CollectionId == collectionId).OrderBy(x => x.Ordinal).ToListAsync(cancellationToken);
    }

    public async Task<CollectionItem> AddItemAsync(CollectionItem item, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var max = await db.CollectionItems.Where(x => x.CollectionId == item.CollectionId).MaxAsync(x => (int?)x.Ordinal, cancellationToken) ?? -1;
        item.Ordinal = max + 1;
        db.CollectionItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task UpdateItemAsync(CollectionItem item, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        db.CollectionItems.Update(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var e = await db.CollectionItems.FindAsync([itemId], cancellationToken);
        if (e == null) return false;
        db.CollectionItems.Remove(e);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderItemsAsync(Guid collectionId, IReadOnlyList<Guid> itemIdsInOrder, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var items = await db.CollectionItems.Where(x => x.CollectionId == collectionId).ToListAsync(cancellationToken);
        var byId = items.ToDictionary(x => x.Id);
        for (var i = 0; i < itemIdsInOrder.Count; i++)
        {
            if (byId.TryGetValue(itemIdsInOrder[i], out var it))
                it.Ordinal = i;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
