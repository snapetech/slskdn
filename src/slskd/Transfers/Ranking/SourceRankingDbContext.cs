// <copyright file="SourceRankingDbContext.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the GNU Affero General Public License v3.0.
// </copyright>

namespace slskd.Transfers.Ranking
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    ///     Database context for source ranking and download history.
    /// </summary>
    public class SourceRankingDbContext : DbContext
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SourceRankingDbContext"/> class.
        /// </summary>
        /// <param name="options">The database context options.</param>
        public SourceRankingDbContext(DbContextOptions<SourceRankingDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        ///     Gets or sets the download history entries.
        /// </summary>
        public DbSet<DownloadHistoryEntry> DownloadHistory { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DownloadHistoryEntry>(entity =>
            {
                entity.HasKey(e => e.Username);
                entity.HasIndex(e => e.LastUpdated);
            });
        }
    }

    /// <summary>
    ///     A download history entry for a user.
    /// </summary>
    public class DownloadHistoryEntry
    {
        /// <summary>
        ///     Gets or sets the username (primary key).
        /// </summary>
        [Key]
        [MaxLength(256)]
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the number of successful downloads.
        /// </summary>
        public int Successes { get; set; }

        /// <summary>
        ///     Gets or sets the number of failed downloads.
        /// </summary>
        public int Failures { get; set; }

        /// <summary>
        ///     Gets or sets the date/time the entry was last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
