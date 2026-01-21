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
        public DbSet<PodEntity> Pods { get; set; }

        /// <summary>
        ///     Gets or sets the pod members.
        /// </summary>
        public DbSet<PodMemberEntity> Members { get; set; }

        /// <summary>
        ///     Gets or sets the pod messages.
        /// </summary>
        public DbSet<PodMessageEntity> Messages { get; set; }

        /// <summary>
        ///     Gets or sets the signed membership records.
        /// </summary>
        public DbSet<SignedMembershipRecordEntity> MembershipRecords { get; set; }

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
        public string PodId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public PodVisibility Visibility { get; set; }
        public bool IsPublic { get; set; }
        public int MaxMembers { get; set; }
        public bool AllowGuests { get; set; }
        public bool RequireApproval { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string FocusContentId { get; set; }
        public string Tags { get; set; } // JSON array
        public string Channels { get; set; } // JSON array
        public string ExternalBindings { get; set; } // JSON array
    }

    /// <summary>
    ///     Pod member entity for database storage.
    /// </summary>
    public class PodMemberEntity
    {
        public string PodId { get; set; }
        public string PeerId { get; set; }
        public string Role { get; set; }
        public string PublicKey { get; set; }
        public bool IsBanned { get; set; }
    }

    /// <summary>
    ///     Pod message entity for database storage.
    /// </summary>
    public class PodMessageEntity
    {
        public string PodId { get; set; }
        public string ChannelId { get; set; }
        public long TimestampUnixMs { get; set; }
        public string SenderPeerId { get; set; }
        public string Body { get; set; }
        public string Signature { get; set; }
        public int SigVersion { get; set; }
    }

    /// <summary>
    ///     Full-text search virtual table for pod messages.
    /// </summary>
    public class PodMessageFts
    {
        public string PodId { get; set; }
        public string ChannelId { get; set; }
        public long TimestampUnixMs { get; set; }
        public string SenderPeerId { get; set; }
        public string Body { get; set; }
    }

    /// <summary>
    ///     Signed membership record entity for database storage.
    /// </summary>
    public class SignedMembershipRecordEntity
    {
        public string PodId { get; set; }
        public string PeerId { get; set; }
        public long TimestampUnixMs { get; set; }
        public string Action { get; set; }
        public string Signature { get; set; }
    }
}















