// <copyright file="PodDhtController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// Pod DHT publishing API controller.
/// </summary>
[Route("api/v0/podcore/dht")]
[ApiController]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodDhtController : ControllerBase
{
    private readonly ILogger<PodDhtController> _logger;
    private readonly IPodDhtPublisher _podPublisher;

    public PodDhtController(
        ILogger<PodDhtController> logger,
        IPodDhtPublisher podPublisher)
    {
        _logger = logger;
        _podPublisher = podPublisher;
    }

    /// <summary>
    /// Publishes pod metadata to the DHT.
    /// </summary>
    /// <param name="request">The publish request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish result.</returns>
    [HttpPost("publish")]
    public async Task<IActionResult> PublishPod([FromBody] PublishPodRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Pod == null)
        {
            return BadRequest(new { error = "Pod data is required" });
        }

        var normalizedRequest = request with { Pod = NormalizePod(request.Pod) };
        if (string.IsNullOrWhiteSpace(normalizedRequest.Pod.PodId))
        {
            return BadRequest(new { error = "Pod ID is required" });
        }

        try
        {
            var result = await _podPublisher.PublishAsync(normalizedRequest.Pod, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDht] Published pod {PodId} to DHT", result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDht] Failed to publish pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = "Failed to publish pod" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error publishing pod");
            return StatusCode(500, new { error = "Failed to publish pod" });
        }
    }

    /// <summary>
    /// Updates existing pod metadata in the DHT.
    /// </summary>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    [HttpPost("update")]
    public async Task<IActionResult> UpdatePod([FromBody] UpdatePodRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Pod == null)
        {
            return BadRequest(new { error = "Pod data is required" });
        }

        var normalizedRequest = request with { Pod = NormalizePod(request.Pod) };
        if (string.IsNullOrWhiteSpace(normalizedRequest.Pod.PodId))
        {
            return BadRequest(new { error = "Pod ID is required" });
        }

        try
        {
            var result = await _podPublisher.UpdateAsync(normalizedRequest.Pod, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDht] Updated pod {PodId} in DHT", result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDht] Failed to update pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = "Failed to update pod" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error updating pod");
            return StatusCode(500, new { error = "Failed to update pod" });
        }
    }

    /// <summary>
    /// Unpublishes pod metadata from the DHT.
    /// </summary>
    /// <param name="podId">The pod ID to unpublish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unpublish result.</returns>
    [HttpDelete("unpublish/{*podId}")]
    public async Task<IActionResult> UnpublishPod(string podId, CancellationToken cancellationToken = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "Pod ID is required" });
        }

        try
        {
            var result = await _podPublisher.UnpublishAsync(podId, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDht] Unpublished pod {PodId} from DHT", result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDht] Failed to unpublish pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = "Failed to unpublish pod" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error unpublishing pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to unpublish pod" });
        }
    }

    /// <summary>
    /// Gets published pod metadata from the DHT.
    /// </summary>
    /// <param name="podId">The pod ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pod metadata.</returns>
    [HttpGet("metadata/{*podId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPodMetadata(string podId, CancellationToken cancellationToken = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "Pod ID is required" });
        }

        try
        {
            var result = await _podPublisher.GetPublishedMetadataAsync(podId, cancellationToken);

            if (result.Found)
            {
                return Ok(result);
            }
            else
            {
                return NotFound(new { found = false, error = "Pod not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error retrieving pod metadata for {PodId}", podId);
            return StatusCode(500, new { error = "Failed to retrieve pod metadata" });
        }
    }

    /// <summary>
    /// Refreshes published pod metadata.
    /// </summary>
    /// <param name="podId">The pod ID to refresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh result.</returns>
    [HttpPost("refresh/{*podId}")]
    public async Task<IActionResult> RefreshPod(string podId, CancellationToken cancellationToken = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "Pod ID is required" });
        }

        try
        {
            var result = await _podPublisher.RefreshAsync(podId, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDht] Refreshed pod {PodId}, republished: {Republished}", result.PodId, result.WasRepublished);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDht] Failed to refresh pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = "Failed to refresh pod" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error refreshing pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to refresh pod" });
        }
    }

    /// <summary>
    /// Gets pod publishing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Publishing statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetPublishingStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _podPublisher.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error getting publishing stats");
            return StatusCode(500, new { error = "Failed to get publishing statistics" });
        }
    }

    private static Pod NormalizePod(Pod pod)
    {
        ArgumentNullException.ThrowIfNull(pod);

        return new Pod
        {
            PodId = pod.PodId?.Trim() ?? string.Empty,
            Name = pod.Name?.Trim() ?? string.Empty,
            Description = string.IsNullOrWhiteSpace(pod.Description) ? null : pod.Description.Trim(),
            Visibility = pod.Visibility,
            IsPublic = pod.IsPublic,
            MaxMembers = pod.MaxMembers,
            AllowGuests = pod.AllowGuests,
            RequireApproval = pod.RequireApproval,
            UpdatedAt = pod.UpdatedAt,
            FocusContentId = string.IsNullOrWhiteSpace(pod.FocusContentId) ? null : pod.FocusContentId.Trim(),
            Tags = pod.Tags?
                .Select(tag => tag?.Trim() ?? string.Empty)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .ToList()
                ?? new List<string>(),
            Channels = pod.Channels?
                .Select(channel => new PodChannel
                {
                    ChannelId = channel.ChannelId?.Trim() ?? string.Empty,
                    Kind = channel.Kind,
                    Name = channel.Name?.Trim() ?? string.Empty,
                    BindingInfo = string.IsNullOrWhiteSpace(channel.BindingInfo) ? null : channel.BindingInfo.Trim(),
                    Description = string.IsNullOrWhiteSpace(channel.Description) ? null : channel.Description.Trim(),
                })
                .ToList()
                ?? new List<PodChannel>(),
            Members = pod.Members?
                .Select(member => new PodMember
                {
                    PeerId = member.PeerId?.Trim() ?? string.Empty,
                    Role = member.Role?.Trim() ?? string.Empty,
                    IsBanned = member.IsBanned,
                    PublicKey = string.IsNullOrWhiteSpace(member.PublicKey) ? null : member.PublicKey.Trim(),
                    JoinedAt = member.JoinedAt,
                    LastSeen = member.LastSeen,
                })
                .ToList(),
            ExternalBindings = pod.ExternalBindings?
                .Select(binding => new ExternalBinding
                {
                    Kind = binding.Kind?.Trim() ?? string.Empty,
                    Mode = binding.Mode?.Trim() ?? string.Empty,
                    Identifier = binding.Identifier?.Trim() ?? string.Empty,
                })
                .ToList()
                ?? new List<ExternalBinding>(),
            Capabilities = pod.Capabilities,
            PrivateServicePolicy = pod.PrivateServicePolicy == null ? null : new PodPrivateServicePolicy
            {
                Enabled = pod.PrivateServicePolicy.Enabled,
                MaxMembers = pod.PrivateServicePolicy.MaxMembers,
                GatewayPeerId = pod.PrivateServicePolicy.GatewayPeerId?.Trim() ?? string.Empty,
                RegisteredServices = pod.PrivateServicePolicy.RegisteredServices?
                    .Select(service => new RegisteredService
                    {
                        Name = service.Name?.Trim() ?? string.Empty,
                        Description = service.Description?.Trim() ?? string.Empty,
                        Host = service.Host?.Trim() ?? string.Empty,
                        Port = service.Port,
                        Protocol = service.Protocol?.Trim() ?? string.Empty,
                        Kind = service.Kind,
                    })
                    .ToList()
                    ?? new List<RegisteredService>(),
                AllowedDestinations = pod.PrivateServicePolicy.AllowedDestinations?
                    .Select(destination => new AllowedDestination
                    {
                        HostPattern = destination.HostPattern?.Trim() ?? string.Empty,
                        Port = destination.Port,
                        Protocol = destination.Protocol?.Trim() ?? string.Empty,
                        AllowPublic = destination.AllowPublic,
                        Kind = destination.Kind,
                    })
                    .ToList()
                    ?? new List<AllowedDestination>(),
                AllowPrivateRanges = pod.PrivateServicePolicy.AllowPrivateRanges,
                AllowPublicDestinations = pod.PrivateServicePolicy.AllowPublicDestinations,
                MaxConcurrentTunnelsPerPeer = pod.PrivateServicePolicy.MaxConcurrentTunnelsPerPeer,
                MaxConcurrentTunnelsPod = pod.PrivateServicePolicy.MaxConcurrentTunnelsPod,
                MaxNewTunnelsPerMinutePerPeer = pod.PrivateServicePolicy.MaxNewTunnelsPerMinutePerPeer,
                MaxBytesPerDayPerPeer = pod.PrivateServicePolicy.MaxBytesPerDayPerPeer,
                IdleTimeout = pod.PrivateServicePolicy.IdleTimeout,
                MaxLifetime = pod.PrivateServicePolicy.MaxLifetime,
                DialTimeout = pod.PrivateServicePolicy.DialTimeout,
                MaxBufferedBytesPerTunnel = pod.PrivateServicePolicy.MaxBufferedBytesPerTunnel,
                MaxFrameSize = pod.PrivateServicePolicy.MaxFrameSize,
            },
        };
    }
}

/// <summary>
/// Request to publish a pod.
/// </summary>
public record PublishPodRequest(Pod Pod);

/// <summary>
/// Request to update a pod.
/// </summary>
public record UpdatePodRequest(Pod Pod);
