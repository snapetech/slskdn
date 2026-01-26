// <copyright file="ISharingService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Orchestrates ShareGroups, Collections, ShareGrants; token creation; and manifest. For v1, CurrentUserId = Soulseek username.</summary>
public interface ISharingService
{
    // ShareGroups — delegate to IShareGroupRepository
    Task<ShareGroup?> GetShareGroupAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ShareGroup>> GetShareGroupsByOwnerAsync(string ownerUserId, CancellationToken ct = default);
    Task<ShareGroup> CreateShareGroupAsync(ShareGroup g, CancellationToken ct = default);
    Task UpdateShareGroupAsync(ShareGroup g, CancellationToken ct = default);
    Task<bool> DeleteShareGroupAsync(Guid id, CancellationToken ct = default);
    Task AddShareGroupMemberAsync(Guid shareGroupId, string userId, CancellationToken ct = default);
    Task AddShareGroupMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken ct = default);
    Task RemoveShareGroupMemberAsync(Guid shareGroupId, string userId, CancellationToken ct = default);
    Task RemoveShareGroupMemberByPeerIdAsync(Guid shareGroupId, string peerId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetShareGroupMembersAsync(Guid shareGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<ShareGroupMemberInfo>> GetShareGroupMemberInfosAsync(Guid shareGroupId, CancellationToken ct = default);

    // Collections — delegate to ICollectionRepository
    Task<Collection?> GetCollectionAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Collection>> GetCollectionsByOwnerAsync(string ownerUserId, CancellationToken ct = default);
    Task<Collection> CreateCollectionAsync(Collection c, CancellationToken ct = default);
    Task UpdateCollectionAsync(Collection c, CancellationToken ct = default);
    Task<bool> DeleteCollectionAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionItem>> GetCollectionItemsAsync(Guid collectionId, CancellationToken ct = default);
    Task<CollectionItem> AddCollectionItemAsync(CollectionItem item, CancellationToken ct = default);
    Task UpdateCollectionItemAsync(CollectionItem item, CancellationToken ct = default);
    Task<bool> RemoveCollectionItemAsync(Guid itemId, CancellationToken ct = default);
    Task ReorderCollectionItemsAsync(Guid collectionId, IReadOnlyList<Guid> itemIdsInOrder, CancellationToken ct = default);

    // ShareGrants — delegate to IShareGrantRepository
    Task<ShareGrant?> GetShareGrantAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ShareGrant>> GetShareGrantsByCollectionAsync(Guid collectionId, CancellationToken ct = default);
    /// <summary>Grants the given user can access (recipient or group member). Excludes expired.</summary>
    Task<IReadOnlyList<ShareGrant>> GetShareGrantsAccessibleByUserAsync(string userId, CancellationToken ct = default);
    Task<ShareGrant> CreateShareGrantAsync(ShareGrant g, CancellationToken ct = default);
    Task UpdateShareGrantAsync(ShareGrant g, CancellationToken ct = default);
    Task<bool> DeleteShareGrantAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates a share token for the grant. Caller must verify the current user owns the collection.</summary>
    Task<string> CreateTokenAsync(Guid shareGrantId, TimeSpan expiresIn, CancellationToken ct = default);

    /// <summary>Builds the manifest for a share. If tokenForStreamUrl is set, streamUrl includes ?token=; otherwise streamUrl has no token. When using normal auth, currentUserId must have access to the grant.</summary>
    Task<ShareManifestDto?> GetManifestAsync(Guid shareGrantId, string? tokenForStreamUrl, string? currentUserId, CancellationToken ct = default);
}

/// <summary>Manifest DTO: collection metadata plus ordered items with contentId and optional streamUrl.</summary>
public sealed class ShareManifestDto
{
    public string CollectionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string? OwnerContactNickname { get; set; }
    public string? OwnerPeerId { get; set; }
    public List<ShareManifestItemDto> Items { get; set; } = new();
}

public sealed class ShareManifestItemDto
{
    public string ContentId { get; set; } = string.Empty;
    public string? MediaKind { get; set; }
    public string? StreamUrl { get; set; }
}

/// <summary>ShareGroup member information with contact details when available.</summary>
public sealed class ShareGroupMemberInfo
{
    public string UserId { get; set; } = string.Empty;
    public string? PeerId { get; set; }
    public string? ContactNickname { get; set; }
    public bool IsContactBased => !string.IsNullOrEmpty(PeerId);
}
