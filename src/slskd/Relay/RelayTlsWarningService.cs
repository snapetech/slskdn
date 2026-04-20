// <copyright file="RelayTlsWarningService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Relay;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     Periodically reminds operators that relay controller TLS validation is disabled.
/// </summary>
/// <remarks>
///     HARDENING-2026-04-20 H8: <c>Relay.Controller.IgnoreCertificateErrors=true</c> disables TLS
///     certificate validation wholesale. A one-shot warning at reconfiguration is easy to miss on a
///     long-running deploy, so this service re-logs at warn level every <see cref="IntervalMinutes"/>
///     minutes while the flag is set. Operators running labs with self-signed certs can acknowledge
///     the exposure; operators who flipped it and forgot get a periodic nudge.
/// </remarks>
public sealed class RelayTlsWarningService : BackgroundService
{
    private const int IntervalMinutes = 15;

    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly ILogger<RelayTlsWarningService> _logger;

    public RelayTlsWarningService(IOptionsMonitor<slskd.Options> options, ILogger<RelayTlsWarningService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // BackgroundService.StartAsync blocks host startup until the first await, so yield immediately.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var relay = _options.CurrentValue.Relay;

                if (relay?.Enabled == true && relay.Controller?.IgnoreCertificateErrors == true)
                {
                    _logger.LogWarning(
                        "[Relay] HARDENING-2026-04-20 H8: relay.controller.ignore_certificate_errors=true — " +
                        "TLS certificate validation for the relay controller is DISABLED. " +
                        "Every controller connection is vulnerable to an on-path MitM. " +
                        "Set a CA-signed controller cert or pin the SPKI; lab-only use is acceptable.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[Relay] RelayTlsWarningService tick failed (non-fatal)");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
