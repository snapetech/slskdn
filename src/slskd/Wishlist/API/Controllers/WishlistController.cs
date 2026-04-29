// <copyright file="WishlistController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
            if (request == null)
            {
                return BadRequest("SearchText is required");
            }

            request.SearchText = request.SearchText?.Trim() ?? string.Empty;
            request.Filter = string.IsNullOrWhiteSpace(request.Filter) ? string.Empty : request.Filter.Trim();
            if (string.IsNullOrWhiteSpace(request.SearchText))
            {
                return BadRequest("SearchText is required");
            }

            if (request.MaxResults.HasValue && request.MaxResults.Value <= 0)
            {
                return BadRequest("MaxResults must be greater than 0");
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
            if (request == null)
            {
                return BadRequest("SearchText is required");
            }

            try
            {
                request.SearchText = request.SearchText?.Trim() ?? string.Empty;
                request.Filter = string.IsNullOrWhiteSpace(request.Filter) ? string.Empty : request.Filter.Trim();
                if (string.IsNullOrWhiteSpace(request.SearchText))
                {
                    return BadRequest("SearchText is required");
                }

                if (request.MaxResults.HasValue && request.MaxResults.Value <= 0)
                {
                    return BadRequest("MaxResults must be greater than 0");
                }

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

        /// <summary>
        ///     Imports wishlist searches from a CSV playlist export.
        /// </summary>
        /// <param name="request">The CSV import request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The import result.</returns>
        /// <response code="200">The CSV was imported successfully.</response>
        /// <response code="400">The request was invalid.</response>
        [HttpPost("import/csv")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(WishlistCsvImportResult), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> ImportCsv(
            [FromBody, Required] ImportWishlistCsvRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CsvText))
            {
                return BadRequest("CsvText is required");
            }

            request.Filter = string.IsNullOrWhiteSpace(request.Filter) ? string.Empty : request.Filter.Trim();
            if (request.MaxResults.HasValue && request.MaxResults.Value <= 0)
            {
                return BadRequest("MaxResults must be greater than 0");
            }

            var result = await WishlistService.ImportCsvAsync(
                request.CsvText,
                new WishlistCsvImportOptions
                {
                    Enabled = request.Enabled ?? true,
                    AutoDownload = request.AutoDownload ?? false,
                    Filter = request.Filter,
                    MaxResults = request.MaxResults ?? 100,
                    IncludeAlbum = request.IncludeAlbum ?? false,
                },
                cancellationToken);

            if (result.TotalRows == 0)
            {
                return BadRequest("CSV did not contain any track rows");
            }

            return Ok(result);
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
        public string SearchText { get; set; } = string.Empty;

        /// <summary>
        ///     Optional filter expression.
        /// </summary>
        public string Filter { get; set; } = string.Empty;

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
        public string SearchText { get; set; } = string.Empty;

        /// <summary>
        ///     Optional filter expression.
        /// </summary>
        public string Filter { get; set; } = string.Empty;

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
    ///     Request to import wishlist items from CSV text.
    /// </summary>
    public class ImportWishlistCsvRequest
    {
        /// <summary>
        ///     Raw CSV text from a playlist export.
        /// </summary>
        [Required]
        public string CsvText { get; set; } = string.Empty;

        /// <summary>
        ///     Optional filter expression to apply to every imported search.
        /// </summary>
        public string Filter { get; set; } = string.Empty;

        /// <summary>
        ///     Whether imported wishlist items are enabled.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        ///     Whether imported wishlist items should auto-download matches.
        /// </summary>
        public bool? AutoDownload { get; set; }

        /// <summary>
        ///     Maximum results to keep for each imported search.
        /// </summary>
        public int? MaxResults { get; set; }

        /// <summary>
        ///     Whether album names should be included in generated search text.
        /// </summary>
        public bool? IncludeAlbum { get; set; }
    }
}
