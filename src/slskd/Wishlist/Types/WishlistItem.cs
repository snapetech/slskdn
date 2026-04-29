// <copyright file="WishlistItem.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Wishlist
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     A wishlist item representing a saved search.
    /// </summary>
    public class WishlistItem
    {
        /// <summary>
        ///     Gets or sets the unique identifier.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        ///     Gets or sets the search text.
        /// </summary>
        [Required]
        public string SearchText { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the filter expression (optional).
        /// </summary>
        public string Filter { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets a value indicating whether the wishlist item is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets a value indicating whether to auto-download matches.
        /// </summary>
        public bool AutoDownload { get; set; } = false;

        /// <summary>
        ///     Gets or sets the maximum number of results to keep per search.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        ///     Gets or sets the date/time the item was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        ///     Gets or sets the date/time of the last search execution.
        /// </summary>
        public DateTime? LastSearchedAt { get; set; }

        /// <summary>
        ///     Gets or sets the number of matches found in the last search.
        /// </summary>
        public int LastMatchCount { get; set; } = 0;

        /// <summary>
        ///     Gets or sets the total number of searches performed.
        /// </summary>
        public int TotalSearchCount { get; set; } = 0;

        /// <summary>
        ///     Gets or sets the total number of files downloaded from this wishlist.
        /// </summary>
        public int TotalDownloadCount { get; set; } = 0;

        /// <summary>
        ///     Gets or sets the GUID of the most recent search.
        /// </summary>
        public Guid? LastSearchId { get; set; }
    }
}
