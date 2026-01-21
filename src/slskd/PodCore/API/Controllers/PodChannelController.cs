// <copyright file="PodChannelController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore.API.Controllers;

using slskd.Core.Security;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
///     Pod channel management.
/// </summary>
[Route("api/v0/podcore/{podId}/channels")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodChannelController : ControllerBase
{
    private readonly IPodService _podService;
    private readonly ILogger<PodChannelController> _logger;

    public PodChannelController(
        IPodService podService,
        ILogger<PodChannelController> logger)
    {
        _podService = podService;
        _logger = logger;
    }

    /// <summary>
    ///     Creates a new channel in the specified pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channel">The channel to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created channel.</returns>
    /// <response code="201">The channel was created.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="404">The pod does not exist.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PodChannel), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateChannel(
        [FromRoute] string podId,
        [FromBody] PodChannel channel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (channel == null)
        {
            return BadRequest("Channel data is required");
        }

        try
        {
            // Verify pod exists
            var pod = await _podService.GetPodAsync(podId, cancellationToken);
            if (pod == null)
            {
                return NotFound($"Pod {podId} does not exist");
            }

            var createdChannel = await _podService.CreateChannelAsync(podId, channel, cancellationToken);
            return CreatedAtAction(nameof(GetChannel), new { podId, channelId = createdChannel.ChannelId }, createdChannel);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating channel in pod {PodId}", podId);
            return StatusCode(500, "An error occurred while creating the channel");
        }
    }

    /// <summary>
    ///     Gets all channels in the specified pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of channels.</returns>
    /// <response code="200">The channels were retrieved.</response>
    /// <response code="404">The pod does not exist.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PodChannel>), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetChannels(
        [FromRoute] string podId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        try
        {
            // Verify pod exists
            var pod = await _podService.GetPodAsync(podId, cancellationToken);
            if (pod == null)
            {
                return NotFound($"Pod {podId} does not exist");
            }

            var channels = await _podService.GetChannelsAsync(podId, cancellationToken);
            return Ok(channels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channels for pod {PodId}", podId);
            return StatusCode(500, "An error occurred while getting channels");
        }
    }

    /// <summary>
    ///     Gets a specific channel in the specified pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The channel.</returns>
    /// <response code="200">The channel was retrieved.</response>
    /// <response code="404">The pod or channel does not exist.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{channelId}")]
    [ProducesResponseType(typeof(PodChannel), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetChannel(
        [FromRoute] string podId,
        [FromRoute] string channelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            return BadRequest("Channel ID is required");
        }

        try
        {
            // Verify pod exists
            var pod = await _podService.GetPodAsync(podId, cancellationToken);
            if (pod == null)
            {
                return NotFound($"Pod {podId} does not exist");
            }

            var channel = await _podService.GetChannelAsync(podId, channelId, cancellationToken);
            if (channel == null)
            {
                return NotFound($"Channel {channelId} does not exist in pod {podId}");
            }

            return Ok(channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel {ChannelId} for pod {PodId}", channelId, podId);
            return StatusCode(500, "An error occurred while getting the channel");
        }
    }

    /// <summary>
    ///     Updates a channel in the specified pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="channel">The updated channel data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="200">The channel was updated.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="404">The pod or channel does not exist.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPut("{channelId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateChannel(
        [FromRoute] string podId,
        [FromRoute] string channelId,
        [FromBody] PodChannel channel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            return BadRequest("Channel ID is required");
        }

        if (channel == null)
        {
            return BadRequest("Channel data is required");
        }

        // Ensure channel ID matches route parameter
        if (channel.ChannelId != channelId)
        {
            channel.ChannelId = channelId;
        }

        try
        {
            // Verify pod exists
            var pod = await _podService.GetPodAsync(podId, cancellationToken);
            if (pod == null)
            {
                return NotFound($"Pod {podId} does not exist");
            }

            // Verify channel exists
            var existingChannel = await _podService.GetChannelAsync(podId, channelId, cancellationToken);
            if (existingChannel == null)
            {
                return NotFound($"Channel {channelId} does not exist in pod {podId}");
            }

            var success = await _podService.UpdateChannelAsync(podId, channel, cancellationToken);
            if (!success)
            {
                return StatusCode(500, "Failed to update channel");
            }

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating channel {ChannelId} in pod {PodId}", channelId, podId);
            return StatusCode(500, "An error occurred while updating the channel");
        }
    }

    /// <summary>
    ///     Deletes a channel from the specified pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="200">The channel was deleted.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="404">The pod or channel does not exist.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpDelete("{channelId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteChannel(
        [FromRoute] string podId,
        [FromRoute] string channelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            return BadRequest("Channel ID is required");
        }

        try
        {
            // Verify pod exists
            var pod = await _podService.GetPodAsync(podId, cancellationToken);
            if (pod == null)
            {
                return NotFound($"Pod {podId} does not exist");
            }

            // Verify channel exists
            var channel = await _podService.GetChannelAsync(podId, channelId, cancellationToken);
            if (channel == null)
            {
                return NotFound($"Channel {channelId} does not exist in pod {podId}");
            }

            var success = await _podService.DeleteChannelAsync(podId, channelId, cancellationToken);
            if (!success)
            {
                return StatusCode(500, "Failed to delete channel");
            }

            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting channel {ChannelId} from pod {PodId}", channelId, podId);
            return StatusCode(500, "An error occurred while deleting the channel");
        }
    }
}
