// <copyright file="IUserNoteService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
        Task<UserNote?> GetNoteAsync(string username, CancellationToken cancellationToken = default);

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
