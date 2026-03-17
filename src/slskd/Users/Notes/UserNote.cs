// <copyright file="UserNote.cs" company="slskdn Team">
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
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the content of the note.
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        ///     Gets or sets the color category (e.g. "red", "green", "blue", or hex code).
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        ///     Gets or sets the icon name (e.g. fontawesome icon).
        /// </summary>
        public string Icon { get; set; }

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
