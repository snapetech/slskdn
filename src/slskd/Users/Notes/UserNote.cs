// <copyright file="UserNote.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Users.Notes
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Represents a note attached to a user.
    /// </summary>
    public class UserNote
    {
        /// <summary>
        ///     Gets or sets the username the note is attached to.
        /// </summary>
        [Key]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the content of the note.
        /// </summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the color category (e.g. "red", "green", "blue", or hex code).
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the icon name (e.g. fontawesome icon).
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets a value indicating whether this is a high priority note (e.g. ignore user).
        /// </summary>
        public bool IsHighPriority { get; set; }

        /// <summary>
        ///     Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        ///     Gets or sets the last update timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
