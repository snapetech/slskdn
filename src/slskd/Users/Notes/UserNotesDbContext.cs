// <copyright file="UserNotesDbContext.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
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
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    ///     Data context for user notes.
    /// </summary>
    public class UserNotesDbContext : DbContext
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UserNotesDbContext"/> class.
        /// </summary>
        /// <param name="options">The options for this context.</param>
        public UserNotesDbContext(DbContextOptions<UserNotesDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        ///     Gets or sets the user notes.
        /// </summary>
        public DbSet<UserNote> UserNotes { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserNote>()
                .HasKey(x => x.Username);
        }
    }
}

