// <copyright file="UserNoteService.cs" company="slskdn Team">
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Implementation of <see cref="IUserNoteService"/>.
    /// </summary>
    public class UserNoteService : IUserNoteService
    {
        private readonly IDbContextFactory<UserNotesDbContext> contextFactory;
        private readonly ILogger<UserNoteService> logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UserNoteService"/> class.
        /// </summary>
        /// <param name="contextFactory">The database context factory.</param>
        /// <param name="logger">The logger.</param>
        public UserNoteService(
            IDbContextFactory<UserNotesDbContext> contextFactory,
            ILogger<UserNoteService> logger)
        {
            this.contextFactory = contextFactory;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<UserNote> GetNoteAsync(string username, CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.UserNotes.FindAsync(new object[] { username }, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<UserNote>> GetAllNotesAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.UserNotes.ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<UserNote> SetNoteAsync(UserNote note, CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await context.UserNotes.FindAsync(new object[] { note.Username }, cancellationToken);

            if (existing == null)
            {
                note.CreatedAt = DateTime.UtcNow;
                note.UpdatedAt = DateTime.UtcNow;
                context.UserNotes.Add(note);
            }
            else
            {
                existing.Note = note.Note;
                existing.Color = note.Color;
                existing.Icon = note.Icon;
                existing.IsHighPriority = note.IsHighPriority;
                existing.UpdatedAt = DateTime.UtcNow;
                note = existing;
            }

            await context.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Updated note for user {Username}", note.Username);
            return note;
        }

        /// <inheritdoc/>
        public async Task DeleteNoteAsync(string username, CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await context.UserNotes.FindAsync(new object[] { username }, cancellationToken);

            if (existing != null)
            {
                context.UserNotes.Remove(existing);
                await context.SaveChangesAsync(cancellationToken);
                logger.LogDebug("Deleted note for user {Username}", username);
            }
        }
    }
}
