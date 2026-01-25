// <copyright file="ConversationPodCoordinator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Messaging;

/// <summary>
/// Coordinates creation and resolution of direct-message pods for Soulseek users.
/// </summary>
public sealed class ConversationPodCoordinator : IDisposable
{
    private readonly ILogger<ConversationPodCoordinator> _logger;
    private readonly IOptionsMonitor<MeshOptions> _meshOptions;
    private readonly IPodService _podService;
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _disposed;

    public ConversationPodCoordinator(
        ILogger<ConversationPodCoordinator> logger,
        IOptionsMonitor<MeshOptions> meshOptions,
        IPodService podService,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _meshOptions = meshOptions ?? throw new ArgumentNullException(nameof(meshOptions));
        _podService = podService ?? throw new ArgumentNullException(nameof(podService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <summary>
    /// Ensures a direct-message pod exists for the given Soulseek username and returns its pod ID and channel ID.
    /// </summary>
    /// <param name="username">Soulseek username (used to form bridge: peer ID).</param>
    /// <returns>(PodId, ChannelId) for the DM pod; channel is "dm".</returns>
    public async Task<(string PodId, string ChannelId)> EnsureDirectMessagePodAsync(string? username)
    {
        if (username == null)
            throw new ArgumentNullException(nameof(username));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty or whitespace.", nameof(username));

        var selfPeerId = _meshOptions.CurrentValue.SelfPeerId;
        var remotePeerId = PeerIdFactory.FromSoulseekUsername(username);
        var podId = PodIdFactory.ConversationPodId(new[] { selfPeerId, remotePeerId });
        const string channelId = "dm";

        var existing = await _podService.GetPodAsync(podId);
        if (existing != null)
            return (podId, channelId);

        var pod = new Pod
        {
            PodId = podId,
            Name = username,
            Visibility = PodVisibility.Private,
            Tags = new List<string> { "dm" },
            Channels = new List<PodChannel>
            {
                new PodChannel
                {
                    ChannelId = channelId,
                    Name = "DM",
                    Kind = PodChannelKind.DirectMessage,
                    BindingInfo = $"soulseek-dm:{username}"
                }
            }
        };

        await _podService.CreateAsync(pod);
        var now = DateTimeOffset.UtcNow;
        await _podService.JoinAsync(podId, new PodMember { PeerId = selfPeerId, Role = PodRoles.Owner, JoinedAt = now });
        await _podService.JoinAsync(podId, new PodMember { PeerId = remotePeerId, Role = PodRoles.Member, JoinedAt = now });

        return (podId, channelId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
    }
}
