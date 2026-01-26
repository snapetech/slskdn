// <copyright file="LanDiscoveryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zeroconf;

/// <summary>mDNS implementation of ILanDiscoveryService using Zeroconf.</summary>
public sealed class LanDiscoveryService : ILanDiscoveryService, IDisposable
{
    private const string ServiceType = "_slskdn._tcp";
    private const string ServiceDomain = "local.";

    private readonly IProfileService _profile;
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly ILogger<LanDiscoveryService> _log;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CancellationTokenSource _cts = new();
    private MdnsAdvertiser? _advertiser;
    private bool _advertising;
    private bool _disposed;

    public LanDiscoveryService(
        IProfileService profile,
        IOptionsMonitor<slskd.Options> options,
        ILogger<LanDiscoveryService> log,
        ILoggerFactory loggerFactory)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public event EventHandler<DiscoveredPeer>? PeerDiscovered;

    public async Task StartAdvertisingAsync(CancellationToken ct = default)
    {
        if (_advertising) return;

        try
        {
            var profile = await _profile.GetMyProfileAsync(ct).ConfigureAwait(false);
            var webOpts = _options.CurrentValue.Web;
            if (webOpts.Port <= 0)
            {
                _log.LogWarning("[LanDiscovery] Cannot advertise: Web.Port not configured");
                return;
            }

            var port = (ushort)webOpts.Port;
            var friendCode = _profile.GetFriendCode(profile.PeerId);
            var properties = new Dictionary<string, string>
            {
                ["peerCode"] = friendCode,
                ["displayName"] = profile.DisplayName,
                ["peerId"] = profile.PeerId,
                ["apiPort"] = webOpts.Port.ToString(),
                ["capabilities"] = profile.Capabilities.ToString()
            };

            var advertiserLogger = _loggerFactory.CreateLogger<MdnsAdvertiser>();
            _advertiser = new MdnsAdvertiser(advertiserLogger);
            await _advertiser.StartAsync(profile.DisplayName, ServiceType, port, properties, ct).ConfigureAwait(false);

            _advertising = true;
            _log.LogInformation("[LanDiscovery] Started advertising as {DisplayName} ({FriendCode}) on port {Port}", profile.DisplayName, friendCode, port);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[LanDiscovery] Failed to start advertising");
            _advertiser?.Dispose();
            _advertiser = null;
            throw;
        }
    }

    public async Task StopAdvertisingAsync()
    {
        if (!_advertising) return;
        _advertising = false;
        _advertiser?.Stop();
        _advertiser?.Dispose();
        _advertiser = null;
        _log.LogInformation("[LanDiscovery] Stopped advertising");
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DiscoveredPeer>> BrowseAsync(CancellationToken ct = default)
    {
        try
        {
            var protocol = $"{ServiceType}.{ServiceDomain}";
            var results = await ZeroconfResolver.ResolveAsync(protocol, scanTime: TimeSpan.FromSeconds(2), cancellationToken: ct).ConfigureAwait(false);
            var peers = new List<DiscoveredPeer>();

            foreach (var host in results)
            {
                try
                {
                    // IZeroconfHost has Services (dictionary), IPAddresses, DisplayName
                    foreach (var service in host.Services.Values)
                    {
                        var ip = host.IPAddresses.FirstOrDefault() ?? host.IPAddress;
                        var port = service.Port;
                        var peer = new DiscoveredPeer
                        {
                            PeerCode = host.DisplayName, // Default, will override from TXT
                            DisplayName = host.DisplayName,
                            PeerId = ip, // Default, will override from TXT
                            Endpoint = $"http://{ip}:{port}",
                            Capabilities = 0
                        };

                        // Extract from TXT properties (service.Properties is IReadOnlyList<IReadOnlyDictionary<string, string>>)
                        if (service.Properties != null && service.Properties.Count > 0)
                        {
                            var props = service.Properties[0]; // First property set
                            if (props.TryGetValue("peerCode", out var code)) peer.PeerCode = code;
                            if (props.TryGetValue("displayName", out var name)) peer.DisplayName = name;
                            if (props.TryGetValue("peerId", out var pid)) peer.PeerId = pid;
                            if (props.TryGetValue("apiPort", out var apiPort) && int.TryParse(apiPort, out var apiPortInt))
                                peer.Endpoint = $"http://{ip}:{apiPortInt}";
                            if (props.TryGetValue("capabilities", out var caps) && int.TryParse(caps, out var cap))
                                peer.Capabilities = cap;
                        }

                        peers.Add(peer);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "[LanDiscovery] Failed to parse discovered peer {Name}", host.DisplayName);
                }
            }

            return peers;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[LanDiscovery] Browse failed");
            return Array.Empty<DiscoveredPeer>();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _advertiser?.Dispose();
        _advertiser = null;
    }
}
