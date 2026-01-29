// <copyright file="ShareGrantAnnouncementService.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soulseek;

/// <summary>
/// Receives share-grant announcements via private messages and ingests them into the local sharing DB
/// so "Shared with Me" can function cross-node.
/// </summary>
public sealed class ShareGrantAnnouncementService
{
    private const string Prefix = "SHAREGRANT:";

    private readonly IDbContextFactory<CollectionsDbContext> _factory;
    private readonly ILogger<ShareGrantAnnouncementService> _log;
    private readonly IOptionsMonitor<slskd.Options> _options;

    public ShareGrantAnnouncementService(
        IDbContextFactory<CollectionsDbContext> factory,
        ILogger<ShareGrantAnnouncementService> log,
        IOptionsMonitor<slskd.Options> options,
        ISoulseekClient? soulseekClient = null)
    {
        _factory = factory;
        _log = log;
        _options = options;

        if (soulseekClient != null)
        {
            soulseekClient.PrivateMessageReceived += OnPrivateMessageReceived;
        }
    }

    private void OnPrivateMessageReceived(object? sender, PrivateMessageReceivedEventArgs e)
    {
        if (!e.Message.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _log.LogInformation("[ShareGrantInbox] Received SHAREGRANT message from {User}", e.Username);

        _ = Task.Run(async () =>
        {
            try
            {
                await HandleAnnouncementAsync(e.Message.Substring(Prefix.Length), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[ShareGrantInbox] Failed to handle announcement from {User}", e.Username);
            }
        });
    }

    private async Task HandleAnnouncementAsync(string payload, CancellationToken ct)
    {
        if (!_options.CurrentValue.Feature.CollectionsSharing)
        {
            return;
        }

        ShareGrantAnnouncement? msg;
        try
        {
            msg = JsonSerializer.Deserialize<ShareGrantAnnouncement>(payload);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[ShareGrantInbox] Invalid announcement JSON");
            return;
        }

        if (msg == null)
        {
            return;
        }

        await IngestAsync(msg, ct).ConfigureAwait(false);
    }

    public async Task IngestAsync(ShareGrantAnnouncement msg, CancellationToken ct)
    {
        if (!_options.CurrentValue.Feature.CollectionsSharing)
        {
            return;
        }

        if (msg.CollectionId == Guid.Empty || msg.ShareGrantId == Guid.Empty)
        {
            return;
        }

        var currentUserId = _options.CurrentValue.Soulseek.Username ?? string.Empty;
        // E2E: when Soulseek.Username is not set, use recipient from message so announce still ingests (API uses web auth user via GetCurrentUserIdAsync fallback)
        if (string.IsNullOrWhiteSpace(currentUserId) && Environment.GetEnvironmentVariable("SLSKDN_E2E_SHARE_ANNOUNCE") == "1" && !string.IsNullOrWhiteSpace(msg.RecipientUserId))
        {
            currentUserId = msg.RecipientUserId;
        }
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return;
        }

        // Only ingest if this message is intended for the current user.
        if (!string.Equals(msg.RecipientUserId, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        // Upsert collection (owner is remote user)
        var c = await db.Collections.FirstOrDefaultAsync(x => x.Id == msg.CollectionId, ct).ConfigureAwait(false);
        if (c == null)
        {
            c = new Collection
            {
                Id = msg.CollectionId,
                Title = msg.CollectionTitle ?? "Untitled",
                Description = msg.CollectionDescription,
                Type = string.IsNullOrWhiteSpace(msg.CollectionType) ? CollectionType.ShareList : msg.CollectionType!,
                OwnerUserId = msg.OwnerUserId ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Collections.Add(c);
        }
        else
        {
            c.Title = msg.CollectionTitle ?? c.Title;
            c.Description = msg.CollectionDescription;
            c.Type = string.IsNullOrWhiteSpace(msg.CollectionType) ? c.Type : msg.CollectionType!;
            c.OwnerUserId = msg.OwnerUserId ?? c.OwnerUserId;
            c.UpdatedAt = DateTime.UtcNow;
        }

        // Replace items (small lists; simplest and deterministic)
        var existingItems = await db.CollectionItems.Where(x => x.CollectionId == msg.CollectionId).ToListAsync(ct).ConfigureAwait(false);
        if (existingItems.Count > 0)
        {
            db.CollectionItems.RemoveRange(existingItems);
        }

        var items = msg.Items ?? new List<ShareGrantAnnouncementItem>();
        foreach (var (item, index) in items.Select((value, i) => (value, i)))
        {
            if (string.IsNullOrWhiteSpace(item.ContentId)) continue;
            db.CollectionItems.Add(new CollectionItem
            {
                Id = Guid.NewGuid(),
                CollectionId = msg.CollectionId,
                Ordinal = item.Ordinal ?? index,
                ContentId = item.ContentId!,
                MediaKind = item.MediaKind,
            });
        }

        // Upsert share grant as a direct "User" grant to current user
        var g = await db.ShareGrants.FirstOrDefaultAsync(x => x.Id == msg.ShareGrantId, ct).ConfigureAwait(false);
        if (g == null)
        {
            g = new ShareGrant
            {
                Id = msg.ShareGrantId,
                CollectionId = msg.CollectionId,
                AudienceType = AudienceTypes.User,
                AudienceId = currentUserId,
                AllowStream = msg.AllowStream,
                AllowDownload = msg.AllowDownload,
                AllowReshare = msg.AllowReshare,
                ExpiryUtc = msg.ExpiryUtc,
                MaxConcurrentStreams = msg.MaxConcurrentStreams <= 0 ? 1 : msg.MaxConcurrentStreams,
                MaxBitrateKbps = msg.MaxBitrateKbps,
                OwnerEndpoint = msg.OwnerEndpoint,
                ShareToken = msg.Token,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.ShareGrants.Add(g);
        }
        else
        {
            g.CollectionId = msg.CollectionId;
            g.AudienceType = AudienceTypes.User;
            g.AudienceId = currentUserId;
            g.AllowStream = msg.AllowStream;
            g.AllowDownload = msg.AllowDownload;
            g.AllowReshare = msg.AllowReshare;
            g.ExpiryUtc = msg.ExpiryUtc;
            g.MaxConcurrentStreams = msg.MaxConcurrentStreams <= 0 ? 1 : msg.MaxConcurrentStreams;
            g.MaxBitrateKbps = msg.MaxBitrateKbps;
            g.OwnerEndpoint = msg.OwnerEndpoint;
            g.ShareToken = msg.Token;
            g.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        _log.LogInformation("[ShareGrantInbox] Ingested incoming share {ShareId} for collection {CollectionId}", msg.ShareGrantId, msg.CollectionId);
    }
}

public sealed class ShareGrantAnnouncement
{
    public Guid ShareGrantId { get; set; }
    public Guid CollectionId { get; set; }
    public string? CollectionTitle { get; set; }
    public string? CollectionDescription { get; set; }
    public string? CollectionType { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerEndpoint { get; set; }
    public string? Token { get; set; }
    public string? RecipientUserId { get; set; }

    public bool AllowStream { get; set; } = true;
    public bool AllowDownload { get; set; } = true;
    public bool AllowReshare { get; set; }
    public DateTime? ExpiryUtc { get; set; }
    public int MaxConcurrentStreams { get; set; } = 1;
    public int? MaxBitrateKbps { get; set; }

    public List<ShareGrantAnnouncementItem>? Items { get; set; }
}

public sealed class ShareGrantAnnouncementItem
{
    public int? Ordinal { get; set; }
    public string? ContentId { get; set; }
    public string? MediaKind { get; set; }
}

