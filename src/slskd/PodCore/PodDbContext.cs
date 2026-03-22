// <copyright file="PodDbContext.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.PodCore
{
    using System;
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    ///     Data context for pods.
    /// </summary>
    public class PodDbContext : DbContext
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PodDbContext"/> class.
        /// </summary>
        /// <param name="options">The options for this context.</param>
        public PodDbContext(DbContextOptions<PodDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        ///     Gets or sets the pods.
        /// </summary>
        public DbSet<PodEntity> Pods { get; set; } = null!;

        /// <summary>
        ///     Gets or sets the pod members.
        /// </summary>
        public DbSet<PodMemberEntity> Members { get; set; } = null!;

        /// <summary>
        ///     Gets or sets the pod messages.
        /// </summary>
        public DbSet<PodMessageEntity> Messages { get; set; } = null!;

        /// <summary>
        ///     Gets or sets the signed membership records.
        /// </summary>
        public DbSet<SignedMembershipRecordEntity> MembershipRecords { get; set; } = null!;

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Pod entity
            modelBuilder.Entity<PodEntity>()
                .HasKey(e => e.PodId);

            modelBuilder.Entity<PodEntity>()
                .Property(e => e.Visibility)
                .HasConversion<string>();

            // Pod member entity
            modelBuilder.Entity<PodMemberEntity>()
                .HasKey(e => new { e.PodId, e.PeerId });

            modelBuilder.Entity<PodMemberEntity>()
                .HasIndex(e => e.PodId);

            // Pod message entity
            modelBuilder.Entity<PodMessageEntity>()
                .HasKey(e => new { e.PodId, e.ChannelId, e.TimestampUnixMs, e.SenderPeerId });

            modelBuilder.Entity<PodMessageEntity>()
                .HasIndex(e => new { e.PodId, e.ChannelId });

            modelBuilder.Entity<PodMessageEntity>()
                .Property(e => e.TimestampUnixMs)
                .IsRequired();

            // Signed membership record entity
            modelBuilder.Entity<SignedMembershipRecordEntity>()
                .HasKey(e => new { e.PodId, e.PeerId, e.TimestampUnixMs });

            modelBuilder.Entity<SignedMembershipRecordEntity>()
                .HasIndex(e => e.PodId);

            // Full-text search virtual table for pod messages
            // Note: FTS tables are created manually in migrations or OnConfiguring
            // as EF Core doesn't support virtual tables directly
        }
    }

    /// <summary>
    ///     Pod entity for database storage.
    /// </summary>
    public class PodEntity
    {
        public string PodId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PodVisibility Visibility { get; set; }
        public bool IsPublic { get; set; }
        public int MaxMembers { get; set; }
        public bool AllowGuests { get; set; }
        public bool RequireApproval { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string FocusContentId { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty; // JSON array
        public string Channels { get; set; } = string.Empty; // JSON array
        public string ExternalBindings { get; set; } = string.Empty; // JSON array
        /// <summary>JSON array of <see cref="PodCapability"/> (e.g. [0] for PrivateServiceGateway).</summary>
        public string? Capabilities { get; set; }
        /// <summary>JSON of <see cref="PodPrivateServicePolicy"/> when Capabilities includes PrivateServiceGateway.</summary>
        public string? PrivateServicePolicy { get; set; }
    }

    /// <summary>
    ///     Pod member entity for database storage.
    /// </summary>
    public class PodMemberEntity
    {
        public string PodId { get; set; } = string.Empty;
        public string PeerId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public bool IsBanned { get; set; }
    }

    /// <summary>
    ///     Pod message entity for database storage.
    /// </summary>
    public class PodMessageEntity
    {
        public string PodId { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public long TimestampUnixMs { get; set; }
        public string SenderPeerId { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public int SigVersion { get; set; }
    }

    /// <summary>
    ///     Full-text search virtual table for pod messages.
    /// </summary>
    public class PodMessageFts
    {
        public string PodId { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public long TimestampUnixMs { get; set; }
        public string SenderPeerId { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }

    /// <summary>
    ///     Signed membership record entity for database storage.
    /// </summary>
    public class SignedMembershipRecordEntity
    {
        public string PodId { get; set; } = string.Empty;
        public string PeerId { get; set; } = string.Empty;
        public long TimestampUnixMs { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
