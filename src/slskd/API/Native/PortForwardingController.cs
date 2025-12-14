// <copyright file="PortForwardingController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Common.Security;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace slskd.API.Native;

/// <summary>
/// Controller for managing local port forwarding through VPN tunnels.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("0")]
[ApiController]
[Authorize]
public class PortForwardingController : ControllerBase
{
    private readonly LocalPortForwarder _portForwarder;

    public PortForwardingController(LocalPortForwarder portForwarder)
    {
        _portForwarder = portForwarder;
    }

    /// <summary>
    /// Starts port forwarding from a local port to a remote service through a VPN tunnel.
    /// </summary>
    /// <param name="request">The port forwarding configuration.</param>
    /// <returns>A success response.</returns>
    [HttpPost("start")]
    public async Task<IActionResult> StartForwarding([FromBody] StartPortForwardingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            await _portForwarder.StartForwardingAsync(
                request.LocalPort,
                request.PodId,
                request.DestinationHost,
                request.DestinationPort,
                request.ServiceName);

            return Ok(new { Message = $"Port forwarding started on local port {request.LocalPort}" });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = $"Failed to start port forwarding: {ex.Message}" });
        }
    }

    /// <summary>
    /// Stops port forwarding on a specific local port.
    /// </summary>
    /// <param name="localPort">The local port to stop forwarding.</param>
    /// <returns>A success response.</returns>
    [HttpPost("stop/{localPort:int}")]
    public async Task<IActionResult> StopForwarding(int localPort)
    {
        try
        {
            await _portForwarder.StopForwardingAsync(localPort);
            return Ok(new { Message = $"Port forwarding stopped on local port {localPort}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = $"Failed to stop port forwarding: {ex.Message}" });
        }
    }

    /// <summary>
    /// Gets the status of all active port forwarders.
    /// </summary>
    /// <returns>A list of port forwarding status information.</returns>
    [HttpGet("status")]
    public IActionResult GetForwardingStatus()
    {
        try
        {
            var status = _portForwarder.GetForwardingStatus();
            return Ok(status);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = $"Failed to get forwarding status: {ex.Message}" });
        }
    }

    /// <summary>
    /// Gets the status of a specific port forwarder.
    /// </summary>
    /// <param name="localPort">The local port to check.</param>
    /// <returns>The port forwarding status information.</returns>
    [HttpGet("status/{localPort:int}")]
    public IActionResult GetForwardingStatus(int localPort)
    {
        try
        {
            var status = _portForwarder.GetForwardingStatus(localPort);
            if (status == null)
            {
                return NotFound(new { Error = $"No forwarding configured on port {localPort}" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = $"Failed to get forwarding status: {ex.Message}" });
        }
    }

    /// <summary>
    /// Lists all available local ports that can be used for forwarding (excluding already used ports).
    /// </summary>
    /// <param name="startPort">The starting port number (default: 1024).</param>
    /// <param name="endPort">The ending port number (default: 65535).</param>
    /// <returns>A list of available port numbers.</returns>
    [HttpGet("available-ports")]
    public IActionResult GetAvailablePorts(
        [FromQuery] int startPort = 1024,
        [FromQuery] int endPort = 65535)
    {
        try
        {
            // Validate port range
            if (startPort < 1 || startPort > 65535 || endPort < 1 || endPort > 65535 || startPort >= endPort)
            {
                return BadRequest(new { Error = "Invalid port range" });
            }

            var usedPorts = _portForwarder.GetForwardingStatus()
                .Select(s => s.LocalPort)
                .ToHashSet();

            var availablePorts = new List<int>();
            for (int port = startPort; port <= endPort; port++)
            {
                if (!usedPorts.Contains(port))
                {
                    availablePorts.Add(port);
                }
            }

            return Ok(new { AvailablePorts = availablePorts });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = $"Failed to get available ports: {ex.Message}" });
        }
    }

    /// <summary>
    /// Gets detailed stream mapping statistics for port forwarding.
    /// </summary>
    /// <returns>Detailed statistics about stream mappings and performance.</returns>
    [HttpGet("stream-stats")]
    public IActionResult GetStreamStatistics()
    {
        try
        {
            // This would require extending LocalPortForwarder to expose stream stats
            // For now, return basic forwarding status with additional metadata
            var status = _portForwarder.GetForwardingStatus();

            var stats = new
            {
                TotalForwardingRules = status.Count(),
                ActiveRules = status.Count(s => s.IsActive),
                TotalConnections = status.Sum(s => s.ActiveConnections),
                TotalBytesForwarded = status.Sum(s => s.BytesForwarded),
                Rules = status.Select(s => new
                {
                    s.LocalPort,
                    s.PodId,
                    s.DestinationHost,
                    s.DestinationPort,
                    s.ServiceName,
                    s.IsActive,
                    s.ActiveConnections,
                    s.BytesForwarded,
                    // Would include stream mapping stats here when available
                    StreamMappingEnabled = true, // Placeholder for future enhancement
                    PerformanceMetrics = new
                    {
                        AverageBytesPerConnection = s.ActiveConnections > 0
                            ? s.BytesForwarded / s.ActiveConnections
                            : 0,
                        IsHighThroughput = s.BytesForwarded > 1024 * 1024, // > 1MB
                    }
                })
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = $"Failed to get stream statistics: {ex.Message}" });
        }
    }
}

/// <summary>
/// Request model for starting port forwarding.
/// </summary>
public class StartPortForwardingRequest
{
    /// <summary>
    /// The local port to listen on (must be between 1024 and 65535).
    /// </summary>
    [Required]
    [Range(1024, 65535, ErrorMessage = "Local port must be between 1024 and 65535")]
    public int LocalPort { get; init; }

    /// <summary>
    /// The pod ID to use for the VPN tunnel.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "PodId must be between 1 and 100 characters")]
    public string PodId { get; init; } = string.Empty;

    /// <summary>
    /// The remote destination hostname or IP address.
    /// </summary>
    [Required]
    [StringLength(253, MinimumLength = 1, ErrorMessage = "Destination host must be between 1 and 253 characters")]
    public string DestinationHost { get; init; } = string.Empty;

    /// <summary>
    /// The remote destination port.
    /// </summary>
    [Required]
    [Range(1, 65535, ErrorMessage = "Destination port must be between 1 and 65535")]
    public int DestinationPort { get; init; }

    /// <summary>
    /// Optional service name for registered services in the pod.
    /// </summary>
    [StringLength(100, ErrorMessage = "Service name must be at most 100 characters")]
    public string? ServiceName { get; init; }
}
