// <copyright file="AutoReplaceController.cs" company="slskd Team">
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

namespace slskd.Transfers.AutoReplace.API
{
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    ///     Auto-replace.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class AutoReplaceController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoReplaceController"/> class.
        /// </summary>
        public AutoReplaceController(AutoReplaceBackgroundService autoReplaceBackgroundService)
        {
            BackgroundService = autoReplaceBackgroundService;
        }

        private AutoReplaceBackgroundService BackgroundService { get; }

        /// <summary>
        ///     Gets the current auto-replace status.
        /// </summary>
        /// <returns>The auto-replace status.</returns>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetStatus()
        {
            return Ok(BackgroundService.GetStatus());
        }

        /// <summary>
        ///     Enables auto-replace.
        /// </summary>
        /// <returns>The updated status.</returns>
        [HttpPut("enable")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult Enable()
        {
            BackgroundService.Enable();
            return Ok(BackgroundService.GetStatus());
        }

        /// <summary>
        ///     Disables auto-replace.
        /// </summary>
        /// <returns>The updated status.</returns>
        [HttpPut("disable")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult Disable()
        {
            BackgroundService.Disable();
            return Ok(BackgroundService.GetStatus());
        }
    }
}

