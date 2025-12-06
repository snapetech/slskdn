// <copyright file="DestinationsController.cs" company="slskd Team">
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

namespace slskd.Destinations.API
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Download destination management endpoints.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class DestinationsController : ControllerBase
    {
        public DestinationsController(IOptionsSnapshot<slskd.Options> optionsSnapshot)
        {
            OptionsSnapshot = optionsSnapshot;
        }

        private IOptionsSnapshot<slskd.Options> OptionsSnapshot { get; }

        /// <summary>
        ///     Gets all configured download destinations.
        /// </summary>
        /// <returns>The list of destinations.</returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(List<DestinationResponse>), 200)]
        public IActionResult GetAll()
        {
            var options = OptionsSnapshot.Value;
            var destinations = new List<DestinationResponse>();

            // Always include the default downloads directory
            var defaultPath = options.Directories.Downloads;
            destinations.Add(new DestinationResponse
            {
                Name = "Downloads",
                Path = defaultPath,
                IsDefault = true,
                Exists = Directory.Exists(defaultPath),
            });

            // Add configured destinations
            if (options.Destinations?.Folders != null)
            {
                foreach (var dest in options.Destinations.Folders)
                {
                    // Skip if this is the same as the default
                    if (dest.Path == defaultPath)
                    {
                        continue;
                    }

                    destinations.Add(new DestinationResponse
                    {
                        Name = dest.Name ?? Path.GetFileName(dest.Path) ?? dest.Path,
                        Path = dest.Path,
                        IsDefault = dest.Default,
                        Exists = Directory.Exists(dest.Path),
                    });
                }

                // If a custom default is set, update the flags
                var customDefault = options.Destinations.Folders.FirstOrDefault(d => d.Default);
                if (customDefault != null)
                {
                    foreach (var dest in destinations)
                    {
                        dest.IsDefault = dest.Path == customDefault.Path;
                    }
                }
            }

            return Ok(destinations);
        }

        /// <summary>
        ///     Gets the default download destination.
        /// </summary>
        /// <returns>The default destination.</returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("default")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(DestinationResponse), 200)]
        public IActionResult GetDefault()
        {
            var options = OptionsSnapshot.Value;

            // Check for custom default first
            var customDefault = options.Destinations?.Folders?.FirstOrDefault(d => d.Default);
            if (customDefault != null)
            {
                return Ok(new DestinationResponse
                {
                    Name = customDefault.Name ?? Path.GetFileName(customDefault.Path) ?? customDefault.Path,
                    Path = customDefault.Path,
                    IsDefault = true,
                    Exists = Directory.Exists(customDefault.Path),
                });
            }

            // Fall back to standard downloads directory
            var defaultPath = options.Directories.Downloads;
            return Ok(new DestinationResponse
            {
                Name = "Downloads",
                Path = defaultPath,
                IsDefault = true,
                Exists = Directory.Exists(defaultPath),
            });
        }

        /// <summary>
        ///     Validates that a destination path exists and is writable.
        /// </summary>
        /// <param name="request">The validation request.</param>
        /// <returns>Validation result.</returns>
        /// <response code="200">The validation completed.</response>
        [HttpPost("validate")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(ValidateDestinationResponse), 200)]
        public IActionResult Validate([FromBody] ValidateDestinationRequest request)
        {
            var exists = Directory.Exists(request.Path);
            var writable = false;

            if (exists)
            {
                try
                {
                    var testFile = Path.Combine(request.Path, $".slskd-write-test-{System.Guid.NewGuid()}");
                    System.IO.File.WriteAllText(testFile, "test");
                    System.IO.File.Delete(testFile);
                    writable = true;
                }
                catch
                {
                    writable = false;
                }
            }

            return Ok(new ValidateDestinationResponse
            {
                Path = request.Path,
                Exists = exists,
                Writable = writable,
            });
        }
    }

    /// <summary>
    ///     Response for a destination.
    /// </summary>
    public class DestinationResponse
    {
        /// <summary>
        ///     The display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     The path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        ///     Whether this is the default destination.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        ///     Whether the path exists.
        /// </summary>
        public bool Exists { get; set; }
    }

    /// <summary>
    ///     Request to validate a destination.
    /// </summary>
    public class ValidateDestinationRequest
    {
        /// <summary>
        ///     The path to validate.
        /// </summary>
        public string Path { get; set; }
    }

    /// <summary>
    ///     Response for destination validation.
    /// </summary>
    public class ValidateDestinationResponse
    {
        /// <summary>
        ///     The path that was validated.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        ///     Whether the path exists.
        /// </summary>
        public bool Exists { get; set; }

        /// <summary>
        ///     Whether the path is writable.
        /// </summary>
        public bool Writable { get; set; }
    }
}
