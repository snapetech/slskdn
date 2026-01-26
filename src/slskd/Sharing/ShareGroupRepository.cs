// <copyright file="ShareGroupRepository.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of IShareGroupRepository.</summary>
public sealed class ShareGroupRepository : IShareGroupRepository
{
    private readonly IDbContextFactory<CollectionsDbContext> _factory;

    public ShareGroupRepository(IDbContextFactory<CollectionsDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<ShareGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.ShareGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ShareGroup>> GetByOwnerAsync(string ownerUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.ShareGroups.AsNoTracking().Where(x => x.OwnerUserId == ownerUserId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<ShareGroup> AddAsync(ShareGroup entity, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        db.ShareGroups.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ShareGroup entity, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        entity.UpdatedAt = DateTime.UtcNow;
        db.ShareGroups.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var e = await db.ShareGroups.FindAsync([id], cancellationToken);
        if (e == null) return false;
        db.ShareGroups.Remove(e);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AddMemberAsync(Guid shareGroupId, string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (await db.ShareGroupMembers.AnyAsync(x => x.ShareGroupId == shareGroupId && x.UserId == userId, cancellationToken))
            return;
        db.ShareGroupMembers.Add(new ShareGroupMember { ShareGroupId = shareGroupId, UserId = userId });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (await db.ShareGroupMembers.AnyAsync(x => x.ShareGroupId == shareGroupId && x.PeerId == peerId, cancellationToken))
            return;
        // Use PeerId as UserId for backward compatibility (legacy code expects UserId)
        db.ShareGroupMembers.Add(new ShareGroupMember { ShareGroupId = shareGroupId, UserId = peerId, PeerId = peerId });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid shareGroupId, string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var m = await db.ShareGroupMembers.FirstOrDefaultAsync(x => x.ShareGroupId == shareGroupId && x.UserId == userId, cancellationToken);
        if (m == null) return;
        db.ShareGroupMembers.Remove(m);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var m = await db.ShareGroupMembers.FirstOrDefaultAsync(x => x.ShareGroupId == shareGroupId && x.PeerId == peerId, cancellationToken);
        if (m == null) return;
        db.ShareGroupMembers.Remove(m);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetMemberIdsAsync(Guid shareGroupId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.ShareGroupMembers.AsNoTracking().Where(x => x.ShareGroupId == shareGroupId).Select(x => x.UserId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShareGroupMember>> GetMembersAsync(Guid shareGroupId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.ShareGroupMembers.AsNoTracking().Where(x => x.ShareGroupId == shareGroupId).ToListAsync(cancellationToken);
    }

    public async Task<bool> IsMemberAsync(Guid shareGroupId, string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.ShareGroupMembers.AsNoTracking().AnyAsync(x => x.ShareGroupId == shareGroupId && x.UserId == userId, cancellationToken);
    }

    public async Task<bool> IsMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.ShareGroupMembers.AsNoTracking().AnyAsync(x => x.ShareGroupId == shareGroupId && x.PeerId == peerId, cancellationToken);
    }
}
