// <copyright file="UsersController.cs" company="slskd Team">
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

using Microsoft.Extensions.Options;

namespace slskd.Users.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Net;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Core.Security;

    using Soulseek;

    /// <summary>
    ///     Users.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class UsersController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="browseTracker"></param>
        /// <param name="userService"></param>
        /// <param name="safetyLimiter">The Soulseek safety limiter (H-08).</param>
        /// <param name="optionsSnapshot"></param>
        public UsersController(
            ISoulseekClient soulseekClient,
            IBrowseTracker browseTracker,
            IUserService userService,
            slskd.Common.Security.ISoulseekSafetyLimiter safetyLimiter,
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            Client = soulseekClient;
            BrowseTracker = browseTracker;
            Users = userService;
            SafetyLimiter = safetyLimiter;
            OptionsSnapshot = optionsSnapshot;
        }

        private IBrowseTracker BrowseTracker { get; }
        private ISoulseekClient Client { get; }
        private IUserService Users { get; }
        private slskd.Common.Security.ISoulseekSafetyLimiter SafetyLimiter { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<UsersController>();

        /// <summary>
        ///     Retrieves the address of the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("{username}/endpoint")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IPEndPoint), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Endpoint([FromRoute, Required] string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

            try
            {
                var endpoint = await Users.GetIPEndPointAsync(username);
                return Ok(endpoint);
            }
            catch (UserOfflineException ex)
            {
                Log.Information(ex, "User {Username} is offline for endpoint lookup", username);
                return NotFound("User is offline");
            }
        }

        /// <summary>
        ///     Retrieves the files shared by the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/browse")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IEnumerable<Directory>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Browse([FromRoute, Required] string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

            // H-08: Check Soulseek safety caps before initiating browse
            if (!SafetyLimiter.TryConsumeBrowse("user"))
            {
                Log.Warning("[SAFETY] Browse rejected for user='{Username}': Rate limit exceeded", username);
                return StatusCode(429, "Browse rate limit exceeded. See Soulseek safety configuration.");
            }

            try
            {
                var result = await Client.BrowseAsync(username);
                BrowseTracker.TryGet(username, out var completedProgress);

                _ = ObserveBrowseCleanupAsync(username, completedProgress);

                return Ok(result);
            }
            catch (UserOfflineException ex)
            {
                Log.Information(ex, "User {Username} is offline for browse", username);
                return NotFound("User is offline");
            }
        }

        /// <summary>
        ///     Retrieves the status of the current browse operation for the specified <paramref name="username"/>, if any.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/browse/status")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(decimal), 200)]
        [ProducesResponseType(404)]
        public IActionResult BrowseStatus([FromRoute, Required] string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

            if (BrowseTracker.TryGet(username, out var progress))
            {
                return Ok(progress);
            }

            return NotFound();
        }

        private async Task ObserveBrowseCleanupAsync(string username, BrowseProgressUpdatedEventArgs? completedProgress)
        {
            try
            {
                await Task.Delay(5000).ConfigureAwait(false);

                if (completedProgress is null)
                {
                    BrowseTracker.TryRemove(username);
                    return;
                }

                BrowseTracker.TryRemove(username, completedProgress);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to clean up browse tracker entry for {Username}", username);
            }
        }

        /// <summary>
        ///     Retrieves the files from the specified directory from the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <param name="request">The directory contents request.</param>
        /// <returns></returns>
        [HttpPost("{username}/directory")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IEnumerable<Directory>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(503)]
        public async Task<IActionResult> Directory([FromRoute, Required] string username, [FromBody, Required] DirectoryContentsRequest request)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

            if (request == null)
            {
                return BadRequest();
            }

            request.Directory = request.Directory?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(request.Directory))
            {
                return BadRequest();
            }

            if (!Client.State.HasFlag(SoulseekClientStates.Connected | SoulseekClientStates.LoggedIn))
            {
                return StatusCode(503, "Soulseek server connection is not ready");
            }

            try
            {
                var result = await Client.GetDirectoryContentsAsync(username, request.Directory);

                Log.Debug("{Endpoint} response from {User} for directory '{Directory}': {@Response}", nameof(Directory), username, request.Directory, result);

                return Ok(result);
            }
            catch (UserOfflineException ex)
            {
                Log.Information(ex, "User {Username} is offline for directory browse", username);
                return NotFound("User is offline");
            }
            catch (SoulseekClientException ex) when (ex.InnerException is ConnectionException)
            {
                Log.Information(ex, "Unable to connect to user {Username} for directory browse", username);
                return StatusCode(503, "Unable to retrieve directory contents from user");
            }
        }

        /// <summary>
        ///     Retrieves information about the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <param name="quietUnavailable">When true, expected missing peer info returns 204 for optional UI badge lookups.</param>
        /// <returns></returns>
        [HttpGet("{username}/info")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Info), 200)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(503)]
        public async Task<IActionResult> Info(
            [FromRoute, Required] string username,
            [FromQuery] bool quietUnavailable = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

            try
            {
                var response = await Users.GetInfoAsync(username);
                return Ok(response);
            }
            catch (UserOfflineException ex)
            {
                Log.Information("User {Username} is offline for info: {Message}", username, ex.Message);
                if (quietUnavailable)
                {
                    return NoContent();
                }

                return NotFound("User is offline");
            }
            catch (SoulseekClientException ex) when (IsExpectedUserInfoFailure(ex))
            {
                Log.Information("Unable to connect to user {Username} for info: {Message}", username, ex.Message);
                if (quietUnavailable)
                {
                    return NoContent();
                }

                return StatusCode(503, "Unable to retrieve user info");
            }
            catch (TimeoutException ex)
            {
                Log.Information("Timed out retrieving info for user {Username}: {Message}", username, ex.Message);
                if (quietUnavailable)
                {
                    return NoContent();
                }

                return StatusCode(503, "Unable to retrieve user info");
            }
        }

        private static bool IsExpectedUserInfoFailure(SoulseekClientException exception)
        {
            return exception.InnerException is ConnectionException or TimeoutException;
        }

        /// <summary>
        ///     Retrieves status for the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/status")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Status), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Status([FromRoute, Required] string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

            try
            {
                var response = await Users.GetStatusAsync(username);
                return Ok(response);
            }
            catch (UserOfflineException ex)
            {
                Log.Information(ex, "User {Username} is offline for status", username);
                return NotFound("User is offline");
            }
        }

        /// <summary>
        ///     Retrieves the group for the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <returns></returns>
        [HttpGet("{username}/group")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(string), 200)]
        public IActionResult Group([FromRoute, Required] string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            username = username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Username is required");
            }

            var group = Users.GetGroup(username);
            return Ok(group);
        }
    }
}
