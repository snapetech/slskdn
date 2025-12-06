// <copyright file="IUserNoteService.cs" company="slskd Team">
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
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for managing user notes.
    /// </summary>
    public interface IUserNoteService
    {
        /// <summary>
        ///     Gets a note for a specific user.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The user note, or null if none exists.</returns>
        Task<UserNote> GetNoteAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets all user notes.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of all user notes.</returns>
        Task<IEnumerable<UserNote>> GetAllNotesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Sets (creates or updates) a note for a user.
        /// </summary>
        /// <param name="note">The note to set.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated user note.</returns>
        Task<UserNote> SetNoteAsync(UserNote note, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Deletes a note for a user.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteNoteAsync(string username, CancellationToken cancellationToken = default);
    }
}

