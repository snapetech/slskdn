// <copyright file="WishlistController.cs" company="slskd Team">
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

namespace slskd.Wishlist.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Core.Security;

    /// <summary>
    ///     Wishlist management endpoints.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class WishlistController : ControllerBase
    {
        public WishlistController(IWishlistService wishlistService)
        {
            WishlistService = wishlistService;
        }

        private IWishlistService WishlistService { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<WishlistController>();

        /// <summary>
        ///     Gets all wishlist items.
        /// </summary>
        /// <returns>The list of wishlist items.</returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(List<WishlistItem>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var items = await WishlistService.ListAsync();
            return Ok(items);
        }

        /// <summary>
        ///     Gets a specific wishlist item.
        /// </summary>
        /// <param name="id">The wishlist item ID.</param>
        /// <returns>The wishlist item.</returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The wishlist item was not found.</response>
        [HttpGet("{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(WishlistItem), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get([FromRoute, Required] Guid id)
        {
            var item = await WishlistService.GetAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        /// <summary>
        ///     Creates a new wishlist item.
        /// </summary>
        /// <param name="request">The create request.</param>
        /// <returns>The created wishlist item.</returns>
        /// <response code="201">The wishlist item was created successfully.</response>
        /// <response code="400">The request was invalid.</response>
        [HttpPost]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(WishlistItem), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody, Required] CreateWishlistRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SearchText))
            {
                return BadRequest("SearchText is required");
            }

            var item = new WishlistItem
            {
                SearchText = request.SearchText,
                Filter = request.Filter,
                Enabled = request.Enabled ?? true,
                AutoDownload = request.AutoDownload ?? false,
                MaxResults = request.MaxResults ?? 100,
            };

            var created = await WishlistService.CreateAsync(item);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        /// <summary>
        ///     Updates a wishlist item.
        /// </summary>
        /// <param name="id">The wishlist item ID.</param>
        /// <param name="request">The update request.</param>
        /// <returns>The updated wishlist item.</returns>
        /// <response code="200">The wishlist item was updated successfully.</response>
        /// <response code="404">The wishlist item was not found.</response>
        [HttpPut("{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(WishlistItem), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(
            [FromRoute, Required] Guid id,
            [FromBody, Required] UpdateWishlistRequest request)
        {
            try
            {
                var item = new WishlistItem
                {
                    Id = id,
                    SearchText = request.SearchText,
                    Filter = request.Filter,
                    Enabled = request.Enabled ?? true,
                    AutoDownload = request.AutoDownload ?? false,
                    MaxResults = request.MaxResults ?? 100,
                };

                var updated = await WishlistService.UpdateAsync(item);
                return Ok(updated);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        ///     Deletes a wishlist item.
        /// </summary>
        /// <param name="id">The wishlist item ID.</param>
        /// <returns>No content.</returns>
        /// <response code="204">The wishlist item was deleted successfully.</response>
        [HttpDelete("{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        public async Task<IActionResult> Delete([FromRoute, Required] Guid id)
        {
            await WishlistService.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>
        ///     Manually triggers a search for a wishlist item.
        /// </summary>
        /// <param name="id">The wishlist item ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The search result.</returns>
        /// <response code="200">The search was executed successfully.</response>
        /// <response code="404">The wishlist item was not found.</response>
        [HttpPost("{id}/search")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Search.Search), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RunSearch(
            [FromRoute, Required] Guid id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var search = await WishlistService.RunSearchAsync(id, cancellationToken);
                return Ok(search);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }
    }

    /// <summary>
    ///     Request to create a wishlist item.
    /// </summary>
    public class CreateWishlistRequest
    {
        /// <summary>
        ///     The search text.
        /// </summary>
        [Required]
        public string SearchText { get; set; }

        /// <summary>
        ///     Optional filter expression.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        ///     Whether the wishlist item is enabled.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        ///     Whether to auto-download matches.
        /// </summary>
        public bool? AutoDownload { get; set; }

        /// <summary>
        ///     Maximum results to keep.
        /// </summary>
        public int? MaxResults { get; set; }
    }

    /// <summary>
    ///     Request to update a wishlist item.
    /// </summary>
    public class UpdateWishlistRequest
    {
        /// <summary>
        ///     The search text.
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        ///     Optional filter expression.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        ///     Whether the wishlist item is enabled.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        ///     Whether to auto-download matches.
        /// </summary>
        public bool? AutoDownload { get; set; }

        /// <summary>
        ///     Maximum results to keep.
        /// </summary>
        public int? MaxResults { get; set; }
    }
}




