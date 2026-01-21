// <copyright file="MeshGatewayController.cs" company="slskd Team">
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.ServiceFabric;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.API.Mesh;

using slskd.Core.Security;

/// <summary>
/// HTTP gateway for mesh services.
/// Exposes mesh service calls via HTTP API for local/external clients.
/// </summary>
[Route("mesh/http")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class MeshGatewayController : ControllerBase
{
    private readonly ILogger<MeshGatewayController> _logger;
    private readonly MeshGatewayOptions _options;
    private readonly IMeshServiceDirectory _serviceDirectory;
    private readonly IMeshServiceClient _serviceClient;

    public MeshGatewayController(
        ILogger<MeshGatewayController> logger,
        IOptions<MeshGatewayOptions> options,
        IMeshServiceDirectory serviceDirectory,
        IMeshServiceClient serviceClient)
    {
        _logger = logger;
        _options = options.Value;
        _serviceDirectory = serviceDirectory;
        _serviceClient = serviceClient;
    }

    /// <summary>
    /// Calls a mesh service by name.
    /// POST /mesh/http/{serviceName}/{method}
    /// </summary>
    /// <param name="serviceName">Name of the service to call (e.g., "pods", "shadow-index")</param>
    /// <param name="method">Method name to invoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service reply</returns>
    [HttpPost("{serviceName}/{method}")]
    public async Task<IActionResult> CallService(
        string serviceName,
        string method,
        CancellationToken cancellationToken)
    {
        // Check if gateway is enabled (should be enforced by middleware, but double-check)
        if (!_options.Enabled)
        {
            _logger.LogWarning("[GatewayController] Gateway is disabled");
            return NotFound(new { error = "gateway_disabled" });
        }

        // Check service allowlist
        if (!_options.AllowedServices.Contains(serviceName))
        {
            _logger.LogWarning(
                "[GatewayController] Service {ServiceName} not in allowed list",
                serviceName);
            return StatusCode(403, new
            {
                error = "service_not_allowed",
                message = $"Service '{serviceName}' is not in the allowed services list"
            });
        }

        try
        {
            // Read request body as payload
            byte[] payload = Array.Empty<byte>();
            if (Request.ContentLength > 0)
            {
                // Enforce max body size (already enforced by middleware, but double-check)
                if (Request.ContentLength > _options.MaxRequestBodyBytes)
                {
                    _logger.LogWarning(
                        "[GatewayController] Request body too large: {Size} bytes",
                        Request.ContentLength);
                    return StatusCode(413, new
                    {
                        error = "payload_too_large",
                        message = $"Request body exceeds {_options.MaxRequestBodyBytes} bytes"
                    });
                }

                using var ms = new System.IO.MemoryStream();
                await Request.Body.CopyToAsync(ms, cancellationToken);
                payload = ms.ToArray();
            }

            // Create timeout for service call
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            _logger.LogInformation(
                "[GatewayController] Calling service {ServiceName}/{Method} with {PayloadSize} byte payload",
                serviceName, method, payload.Length);

            // Resolve service via directory
            var descriptors = await _serviceDirectory.FindByNameAsync(serviceName, linkedCts.Token);
            if (descriptors.Count == 0)
            {
                _logger.LogWarning(
                    "[GatewayController] No providers found for service {ServiceName}",
                    serviceName);
                return StatusCode(503, new
                {
                    error = "service_unavailable",
                    message = $"No providers found for service '{serviceName}'"
                });
            }

            // Use first descriptor for now (TODO: load balancing, reputation-based selection)
            var descriptor = descriptors[0];

            _logger.LogDebug(
                "[GatewayController] Using provider {PeerId} for {ServiceName}",
                descriptor.OwnerPeerId, serviceName);

            // Call service
            var reply = await _serviceClient.CallServiceAsync(
                serviceName,
                method,
                payload,
                linkedCts.Token);

            // Map ServiceReply to HTTP response
            if (reply.StatusCode == ServiceStatusCodes.OK)
            {
                _logger.LogInformation(
                    "[GatewayController] Service call succeeded: {ServiceName}/{Method}",
                    serviceName, method);

                // Return raw payload as JSON or binary depending on content
                if (reply.Payload != null && reply.Payload.Length > 0)
                {
                    // Try to parse as JSON for pretty response
                    try
                    {
                        var json = Encoding.UTF8.GetString(reply.Payload);
                        var parsed = JsonDocument.Parse(json);
                        return Ok(parsed.RootElement);
                    }
                    catch
                    {
                        // Not JSON, return as base64
                        return Ok(new
                        {
                            payload = Convert.ToBase64String(reply.Payload),
                            encoding = "base64"
                        });
                    }
                }

                return Ok(new { success = true });
            }
            else
            {
                // Map service status codes to HTTP status codes
                var httpStatus = MapServiceStatusToHttp(reply.StatusCode);
                
                _logger.LogWarning(
                    "[GatewayController] Service call failed: {ServiceName}/{Method} - Status={StatusCode}, Error={Error}",
                    serviceName, method, reply.StatusCode, reply.ErrorMessage);

                return StatusCode(httpStatus, new
                {
                    error = "service_error",
                    statusCode = reply.StatusCode,
                    message = reply.ErrorMessage ?? "Service returned error"
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "[GatewayController] Request cancelled: {ServiceName}/{Method}",
                serviceName, method);
            return StatusCode(499, new { error = "request_cancelled" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "[GatewayController] Request timeout: {ServiceName}/{Method}",
                serviceName, method);
            return StatusCode(504, new
            {
                error = "gateway_timeout",
                message = $"Service call timed out after {_options.RequestTimeoutSeconds} seconds"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GatewayController] Unexpected error calling service: {ServiceName}/{Method}",
                serviceName, method);
            return StatusCode(502, new
            {
                error = "gateway_error",
                message = "An error occurred while calling the service"
            });
        }
    }

    /// <summary>
    /// Gets available services from the directory.
    /// GET /mesh/http/services
    /// </summary>
    [HttpGet("services")]
    public async Task<IActionResult> GetServices(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return NotFound(new { error = "gateway_disabled" });
        }

        try
        {
            var services = new System.Collections.Generic.List<object>();

            foreach (var serviceName in _options.AllowedServices)
            {
                var descriptors = await _serviceDirectory.FindByNameAsync(serviceName, cancellationToken);
                services.Add(new
                {
                    serviceName,
                    providerCount = descriptors.Count,
                    available = descriptors.Count > 0
                });
            }

            return Ok(new
            {
                gateway = new
                {
                    enabled = _options.Enabled,
                    bindAddress = _options.BindAddress,
                    requestTimeoutSeconds = _options.RequestTimeoutSeconds,
                    maxRequestBodyBytes = _options.MaxRequestBodyBytes
                },
                services
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GatewayController] Error listing services");
            return StatusCode(500, new { error = "internal_error" });
        }
    }

    /// <summary>
    /// Maps service status codes to HTTP status codes.
    /// </summary>
    private static int MapServiceStatusToHttp(int serviceStatusCode)
    {
        return serviceStatusCode switch
        {
            ServiceStatusCodes.OK => 200,
            ServiceStatusCodes.ServiceNotFound => 404,
            ServiceStatusCodes.MethodNotFound => 404,
            ServiceStatusCodes.InvalidPayload => 400,
            ServiceStatusCodes.PayloadTooLarge => 413,
            ServiceStatusCodes.RateLimited => 429,
            ServiceStatusCodes.Timeout => 504,
            ServiceStatusCodes.UnknownError => 500,
            _ => 502 // Generic bad gateway for unknown status codes
        };
    }
}

