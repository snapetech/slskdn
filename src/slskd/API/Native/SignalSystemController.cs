// <copyright file="SignalSystemController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.API.Native;

using slskd.Core.Security;
using System.Linq;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using slskd.Signals;

/// <summary>
/// Provides API endpoints for signal system configuration.
/// </summary>
[ApiController]
[Route("api/v0/signals")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class SignalSystemController : ControllerBase
{
    private readonly IOptionsMonitor<SignalSystemOptions> _optionsMonitor;

    public SignalSystemController(
        IOptionsMonitor<SignalSystemOptions> optionsMonitor,
        ILogger<SignalSystemController> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _ = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get current signal system configuration.
    /// </summary>
    [HttpGet("config")]
    [Authorize]
    public IActionResult GetConfiguration()
    {
        var options = _optionsMonitor.CurrentValue;

        return Ok(new
        {
            enabled = options.Enabled,
            deduplication_cache_size = options.DeduplicationCacheSize,
            default_ttl_seconds = (int)options.DefaultTtl.TotalSeconds,
            mesh_channel = new
            {
                enabled = options.MeshChannel.Enabled,
                priority = options.MeshChannel.Priority,
                require_active_session = options.MeshChannel.RequireActiveSession
            },
            bt_extension_channel = new
            {
                enabled = options.BtExtensionChannel.Enabled,
                priority = options.BtExtensionChannel.Priority,
                require_active_session = options.BtExtensionChannel.RequireActiveSession
            }
        });
    }

    /// <summary>
    /// Get signal system status and statistics.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult GetStatus()
    {
        var options = _optionsMonitor.CurrentValue;
        var signalBus = HttpContext.RequestServices.GetService<ISignalBus>();
        var activeChannels = options.Enabled && signalBus != null
            ? new[]
            {
                options.MeshChannel.Enabled ? "mesh" : null,
                options.BtExtensionChannel.Enabled ? "bt_extension" : null,
            }.Where(channel => channel != null).Cast<string>().ToArray()
            : Array.Empty<string>();

        var statistics = signalBus != null ? signalBus.GetStatistics() : null;

        return Ok(new
        {
            enabled = options.Enabled,
            active_channels = activeChannels,
            statistics = statistics != null ? new
            {
                signals_sent = statistics.SignalsSent,
                signals_received = statistics.SignalsReceived,
                duplicate_signals_dropped = statistics.DuplicateSignalsDropped,
                expired_signals_dropped = statistics.ExpiredSignalsDropped
            }
            : new
            {
                signals_sent = 0L,
                signals_received = 0L,
                duplicate_signals_dropped = 0L,
                expired_signals_dropped = 0L
            }
        });
    }
}
