// <copyright file="SharingService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using slskd.Identity;

/// <summary>Implementation of ISharingService. Delegates to repos and IShareTokenService.</summary>
public sealed class SharingService : ISharingService
{
    private const string StreamsPath = "/api/v0/streams";

    private readonly IShareGroupRepository _shareGroups;
    private readonly ICollectionRepository _collections;
    private readonly IShareGrantRepository _grants;
    private readonly IShareTokenService _tokens;
    private readonly IServiceProvider _serviceProvider;

    public SharingService(
        IShareGroupRepository shareGroups,
        ICollectionRepository collections,
        IShareGrantRepository grants,
        IShareTokenService tokens,
        IServiceProvider serviceProvider)
    {
        _shareGroups = shareGroups ?? throw new ArgumentNullException(nameof(shareGroups));
        _collections = collections ?? throw new ArgumentNullException(nameof(collections));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<ShareGroup?> GetShareGroupAsync(Guid id, CancellationToken ct = default) => _shareGroups.GetByIdAsync(id, ct);
    public Task<IReadOnlyList<ShareGroup>> GetShareGroupsByOwnerAsync(string ownerUserId, CancellationToken ct = default) => _shareGroups.GetByOwnerAsync(ownerUserId, ct);
    public Task<ShareGroup> CreateShareGroupAsync(ShareGroup g, CancellationToken ct = default) => _shareGroups.AddAsync(g, ct);
    public Task UpdateShareGroupAsync(ShareGroup g, CancellationToken ct = default) => _shareGroups.UpdateAsync(g, ct);
    public Task<bool> DeleteShareGroupAsync(Guid id, CancellationToken ct = default) => _shareGroups.DeleteAsync(id, ct);
    public Task AddShareGroupMemberAsync(Guid shareGroupId, string userId, CancellationToken ct = default) => _shareGroups.AddMemberAsync(shareGroupId, userId, ct);
    public Task AddShareGroupMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken ct = default) => _shareGroups.AddMemberByPeerIdAsync(shareGroupId, peerId, ct);
    public Task RemoveShareGroupMemberAsync(Guid shareGroupId, string userId, CancellationToken ct = default) => _shareGroups.RemoveMemberAsync(shareGroupId, userId, ct);
    public Task RemoveShareGroupMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken ct = default) => _shareGroups.RemoveMemberByPeerIdAsync(shareGroupId, peerId, ct);
    public Task<IReadOnlyList<string>> GetShareGroupMembersAsync(Guid shareGroupId, CancellationToken ct = default) => _shareGroups.GetMemberIdsAsync(shareGroupId, ct);

    public async Task<IReadOnlyList<ShareGroupMemberInfo>> GetShareGroupMemberInfosAsync(Guid shareGroupId, CancellationToken ct = default)
    {
        var members = await _shareGroups.GetMembersAsync(shareGroupId, ct).ConfigureAwait(false);
        var contactService = _serviceProvider.GetService<Identity.IContactService>();
        var result = new List<ShareGroupMemberInfo>();

        foreach (var m in members)
        {
            var info = new ShareGroupMemberInfo
            {
                UserId = m.UserId,
                PeerId = m.PeerId
            };

            // Resolve contact nickname if PeerId is set
            if (!string.IsNullOrEmpty(m.PeerId) && contactService != null)
            {
                var contact = await contactService.GetByPeerIdAsync(m.PeerId, ct).ConfigureAwait(false);
                if (contact != null)
                {
                    info.ContactNickname = contact.Nickname;
                }
            }

            result.Add(info);
        }

        return result;
    }

    public Task<Collection?> GetCollectionAsync(Guid id, CancellationToken ct = default) => _collections.GetByIdAsync(id, ct);
    public Task<IReadOnlyList<Collection>> GetCollectionsByOwnerAsync(string ownerUserId, CancellationToken ct = default) => _collections.GetByOwnerAsync(ownerUserId, ct);
    public Task<Collection> CreateCollectionAsync(Collection c, CancellationToken ct = default) => _collections.AddAsync(c, ct);
    public Task UpdateCollectionAsync(Collection c, CancellationToken ct = default) => _collections.UpdateAsync(c, ct);
    public Task<bool> DeleteCollectionAsync(Guid id, CancellationToken ct = default) => _collections.DeleteAsync(id, ct);
    public Task<IReadOnlyList<CollectionItem>> GetCollectionItemsAsync(Guid collectionId, CancellationToken ct = default) => _collections.GetItemsAsync(collectionId, ct);
    public Task<CollectionItem> AddCollectionItemAsync(CollectionItem item, CancellationToken ct = default) => _collections.AddItemAsync(item, ct);
    public Task UpdateCollectionItemAsync(CollectionItem item, CancellationToken ct = default) => _collections.UpdateItemAsync(item, ct);
    public Task<bool> RemoveCollectionItemAsync(Guid itemId, CancellationToken ct = default) => _collections.RemoveItemAsync(itemId, ct);
    public Task ReorderCollectionItemsAsync(Guid collectionId, IReadOnlyList<Guid> itemIdsInOrder, CancellationToken ct = default) => _collections.ReorderItemsAsync(collectionId, itemIdsInOrder, ct);

    public Task<ShareGrant?> GetShareGrantAsync(Guid id, CancellationToken ct = default) => _grants.GetByIdAsync(id, ct);
    public Task<IReadOnlyList<ShareGrant>> GetShareGrantsByCollectionAsync(Guid collectionId, CancellationToken ct = default) => _grants.GetByCollectionIdAsync(collectionId, ct);
    public Task<IReadOnlyList<ShareGrant>> GetShareGrantsAccessibleByUserAsync(string userId, CancellationToken ct = default) => _grants.GetAccessibleByUserAsync(userId, ct);
    public Task<ShareGrant> CreateShareGrantAsync(ShareGrant g, CancellationToken ct = default) => _grants.AddAsync(g, ct);
    public Task UpdateShareGrantAsync(ShareGrant g, CancellationToken ct = default) => _grants.UpdateAsync(g, ct);
    public Task<bool> DeleteShareGrantAsync(Guid id, CancellationToken ct = default) => _grants.DeleteAsync(id, ct);

    public async Task<string> CreateTokenAsync(Guid shareGrantId, TimeSpan expiresIn, CancellationToken ct = default)
    {
        var g = await _grants.GetByIdAsync(shareGrantId, ct).ConfigureAwait(false);
        if (g == null) throw new InvalidOperationException($"ShareGrant {shareGrantId} not found.");
        return await _tokens.CreateAsync(
            shareId: g.Id.ToString(),
            collectionId: g.CollectionId.ToString(),
            audienceId: string.IsNullOrEmpty(g.AudienceId) ? null : g.AudienceId,
            allowStream: g.AllowStream,
            allowDownload: g.AllowDownload,
            maxConcurrentStreams: g.MaxConcurrentStreams,
            expiresIn,
            ct).ConfigureAwait(false);
    }

    public async Task<ShareManifestDto?> GetManifestAsync(Guid shareGrantId, string? tokenForStreamUrl, string? currentUserId, CancellationToken ct = default)
    {
        ShareGrant? g;
        if (tokenForStreamUrl != null)
            g = await _grants.GetByIdAsync(shareGrantId, ct).ConfigureAwait(false);
        else if (currentUserId != null)
        {
            var accessible = await _grants.GetAccessibleByUserAsync(currentUserId, ct).ConfigureAwait(false);
            g = accessible.FirstOrDefault(x => x.Id == shareGrantId);
        }
        else
            return null;

        if (g == null) return null;

        var c = await _collections.GetByIdAsync(g.CollectionId, ct).ConfigureAwait(false);
        if (c == null) return null;

        var items = await _collections.GetItemsAsync(g.CollectionId, ct).ConfigureAwait(false);
        var allowStream = g.AllowStream && (tokenForStreamUrl != null || currentUserId != null);

        var list = items.Select(i => new ShareManifestItemDto
        {
            ContentId = i.ContentId,
            MediaKind = i.MediaKind,
            StreamUrl = allowStream ? (tokenForStreamUrl != null ? $"{StreamsPath}/{i.ContentId}?token={Uri.EscapeDataString(tokenForStreamUrl)}" : $"{StreamsPath}/{i.ContentId}") : null,
        }).ToList();

        // Resolve owner contact nickname if Identity & Friends is enabled
        string? ownerNickname = null;
        string? ownerPeerId = null;
        var contactService = _serviceProvider.GetService<IContactService>();
        if (contactService != null)
        {
            // Try to find owner as a contact (if they're using Identity & Friends)
            var allContacts = await contactService.GetAllAsync(ct).ConfigureAwait(false);
            // For now, we can't directly map OwnerUserId to PeerId without additional info
            // This would require storing PeerId in Collection or looking up by some other means
            // For MVP, we'll leave this as a placeholder that can be enhanced later
        }

        return new ShareManifestDto
        {
            CollectionId = c.Id.ToString(),
            Title = c.Title,
            Description = c.Description,
            Type = c.Type,
            OwnerUserId = c.OwnerUserId,
            OwnerContactNickname = ownerNickname,
            OwnerPeerId = ownerPeerId,
            Items = list,
        };
    }
}
