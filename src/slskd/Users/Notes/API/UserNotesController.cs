// <copyright file="UserNotesController.cs" company="slskd Team">
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

namespace slskd.Users.Notes.API
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    ///     Controller for managing user notes.
    /// </summary>
    [ApiController]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/users/notes")]
    [Authorize(Policy = AuthPolicy.Any)]
    public class UserNotesController : ControllerBase
    {
        private readonly IUserNoteService userNoteService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UserNotesController"/> class.
        /// </summary>
        /// <param name="userNoteService">The user note service.</param>
        public UserNotesController(IUserNoteService userNoteService)
        {
            this.userNoteService = userNoteService;
        }

        /// <summary>
        ///     Gets all user notes.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of user notes.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserNote>>> GetAll(CancellationToken cancellationToken)
        {
            return Ok(await userNoteService.GetAllNotesAsync(cancellationToken));
        }

        /// <summary>
        ///     Gets a specific user note.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The user note.</returns>
        [HttpGet("{username}")]
        public async Task<ActionResult<UserNote>> Get(string username, CancellationToken cancellationToken)
        {
            var note = await userNoteService.GetNoteAsync(username, cancellationToken);
            if (note == null)
            {
                return NotFound();
            }

            return Ok(note);
        }

        /// <summary>
        ///     Sets a user note.
        /// </summary>
        /// <param name="note">The note to set.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated note.</returns>
        [HttpPost]
        public async Task<ActionResult<UserNote>> Set([FromBody] UserNote note, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(note.Username))
            {
                return BadRequest("Username is required.");
            }

            var result = await userNoteService.SetNoteAsync(note, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        ///     Deletes a user note.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{username}")]
        public async Task<ActionResult> Delete(string username, CancellationToken cancellationToken)
        {
            await userNoteService.DeleteNoteAsync(username, cancellationToken);
            return NoContent();
        }
    }
}
