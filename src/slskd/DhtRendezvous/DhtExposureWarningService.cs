// <copyright file="DhtExposureWarningService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     Periodically reminds operators that enabling DHT rendezvous publishes their node's
///     <c>(ip, port)</c> to the public BitTorrent DHT.
/// </summary>
/// <remarks>
///     <para>
///         HARDENING-2026-04-20 H12: DHT is <c>Enabled=true</c> by default, which means a
///         first-run install announces the operator's residential IP to the public DHT
///         bootstrap nodes (router.bittorrent.com, router.utorrent.com, dht.transmissionbt.com)
///         and thereafter to every DHT peer that queries for our content hashes. This is the
///         standard BitTorrent DHT threat model but it surprises people.
///     </para>
///     <para>
///         A one-shot log at startup is easy to miss. This service re-emits a warning every
///         60 minutes for as long as DHT is on and <c>LanOnly</c> is off. Operators who accept
///         the exposure can acknowledge it and move on; operators who'd rather keep their IP
///         off the public DHT have a persistent nudge pointing them at the <c>LanOnly</c>
///         toggle.
///     </para>
/// </remarks>
public sealed class DhtExposureWarningService : BackgroundService
{
    private const int IntervalMinutes = 60;

    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly ILogger<DhtExposureWarningService> _logger;

    public DhtExposureWarningService(IOptionsMonitor<slskd.Options> options, ILogger<DhtExposureWarningService> logger)
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
                var dht = _options.CurrentValue.DhtRendezvous;

                if (dht != null && dht.Enabled && !dht.LanOnly)
                {
                    _logger.LogWarning(
                        "[DHT] HARDENING-2026-04-20 H12: DHT rendezvous is enabled and publishing " +
                        "this node's IP to public BitTorrent DHT bootstrap nodes ({BootstrapCount} configured). " +
                        "Residential IPs and listen ports are discoverable by anyone on the public DHT. " +
                        "Set dht.lan_only: true to disable public DHT bootstrap, or dht.enabled: false " +
                        "to disable DHT peer discovery entirely.",
                        dht.BootstrapRouters?.Length ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[DHT] DhtExposureWarningService tick failed (non-fatal)");
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
