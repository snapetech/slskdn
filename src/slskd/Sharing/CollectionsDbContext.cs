// <copyright file="CollectionsDbContext.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core context for ShareGroup, Collection, CollectionItem, ShareGrant. One database for all sharing entities.
/// </summary>
public class CollectionsDbContext : DbContext
{
    public CollectionsDbContext(DbContextOptions<CollectionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShareGroup> ShareGroups { get; set; }
    public DbSet<ShareGroupMember> ShareGroupMembers { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<CollectionItem> CollectionItems { get; set; }
    public DbSet<ShareGrant> ShareGrants { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<ShareGroup>(e =>
        {
            e.HasIndex(x => x.OwnerUserId);
            e.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.Property(x => x.UpdatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });

        mb.Entity<ShareGroupMember>(e =>
        {
            e.HasKey(x => new { x.ShareGroupId, x.UserId });
            e.HasOne(x => x.ShareGroup).WithMany().HasForeignKey(x => x.ShareGroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.PeerId).HasFilter("[PeerId] IS NOT NULL");
        });

        mb.Entity<Collection>(e =>
        {
            e.HasIndex(x => x.OwnerUserId);
            e.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.Property(x => x.UpdatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });

        mb.Entity<CollectionItem>(e =>
        {
            e.HasOne(x => x.Collection).WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CollectionId);
            e.HasIndex(x => new { x.CollectionId, x.Ordinal });
        });

        mb.Entity<ShareGrant>(e =>
        {
            e.HasOne(x => x.Collection).WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CollectionId);
            e.HasIndex(x => x.AudienceId);
            e.HasIndex(x => x.AudiencePeerId).HasFilter("[AudiencePeerId] IS NOT NULL");
            e.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.Property(x => x.UpdatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            e.Property(x => x.ExpiryUtc).HasConversion(
                v => v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
        });
    }
}
