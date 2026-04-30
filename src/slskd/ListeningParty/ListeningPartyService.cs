// <copyright file="ListeningPartyService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.ListeningParty;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using slskd.NowPlaying;
using slskd.PodCore;
using slskd.Streaming;

/// <summary>
///     Stores and publishes metadata-only listen-along state for pods.
/// </summary>
public sealed class ListeningPartyService : IListeningPartyService
{
    private const int AnnouncementTtlSeconds = 900;
    private const string DirectoryIndexKey = "slskdn:listening-party:index:v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHubContext<ListeningPartyHub> _hub;
    private readonly IMeshDhtClient _dht;
    private readonly IPodMessageRouter _messageRouter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NowPlayingService _nowPlaying;
    private readonly IStreamTicketService _streamTickets;
    private readonly ILogger<ListeningPartyService> _logger;
    private readonly ConcurrentDictionary<string, ListeningPartyEvent> _states = new();
    private readonly ConcurrentDictionary<string, ListeningPartyAnnouncement> _directory = new();
    private long _sequence;

    public ListeningPartyService(
        IHubContext<ListeningPartyHub> hub,
        IMeshDhtClient dht,
        IPodMessageRouter messageRouter,
        IServiceScopeFactory scopeFactory,
        NowPlayingService nowPlaying,
        IStreamTicketService streamTickets,
        ILogger<ListeningPartyService> logger)
    {
        _hub = hub;
        _dht = dht;
        _messageRouter = messageRouter;
        _scopeFactory = scopeFactory;
        _nowPlaying = nowPlaying;
        _streamTickets = streamTickets;
        _logger = logger;
    }

    public Task<ListeningPartyEvent?> GetStateAsync(string podId, string channelId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(StateKey(podId, channelId), out var state);
        return Task.FromResult(state);
    }

    public Task<ListeningPartyEvent?> GetStateByPartyIdAsync(string partyId, CancellationToken cancellationToken = default)
    {
        var normalizedPartyId = partyId?.Trim() ?? string.Empty;
        var state = _states.Values.FirstOrDefault(x => string.Equals(x.PartyId, normalizedPartyId, StringComparison.Ordinal));
        return Task.FromResult(state);
    }

    public async Task<IReadOnlyList<ListeningPartyAnnouncement>> ListDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await RefreshDirectoryFromDhtAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return _directory.Values
            .Where(x => x.ExpiresAtUnixMs > now)
            .OrderByDescending(x => x.LastSeenUnixMs)
            .ToList();
    }

    public async Task<ListeningPartyEvent> PublishAsync(ListeningPartyEvent partyEvent, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(partyEvent);
        var key = StateKey(normalized.PodId, normalized.ChannelId);

        if (normalized.Action == "stop")
        {
            _states.TryRemove(key, out _);
            _nowPlaying.Clear();
            if (!string.IsNullOrWhiteSpace(normalized.PartyId))
            {
                _directory.TryRemove(normalized.PartyId, out _);
                await UpdateDirectoryIndexAsync(normalized.PartyId, add: false, cancellationToken);
            }
        }
        else
        {
            _states[key] = normalized;
            if (normalized.Action == "play" && !string.IsNullOrWhiteSpace(normalized.Artist) && !string.IsNullOrWhiteSpace(normalized.Title))
            {
                _nowPlaying.SetTrack(normalized.Artist, normalized.Title, normalized.Album);
            }

            if (normalized.Listed)
            {
                await PublishAnnouncementAsync(normalized, cancellationToken);
            }
        }

        var message = new PodMessage
        {
            MessageId = $"listen-{Guid.NewGuid():N}",
            PodId = normalized.PodId,
            ChannelId = normalized.ChannelId,
            SenderPeerId = normalized.HostPeerId,
            Body = JsonSerializer.Serialize(normalized, JsonOptions),
            TimestampUnixMs = normalized.ServerTimeUnixMs,
            Signature = string.Empty,
        };

        using var scope = _scopeFactory.CreateScope();
        var messageStorage = scope.ServiceProvider.GetRequiredService<IPodMessageStorage>();
        await messageStorage.StoreMessageAsync(normalized.PodId, normalized.ChannelId, message, cancellationToken);
        var routing = await _messageRouter.RouteMessageAsync(message, cancellationToken);
        if (!routing.Success)
        {
            _logger.LogWarning("Failed to route listen-along message {MessageId}: {Error}", routing.MessageId, routing.ErrorMessage);
        }

        await _hub.Clients
            .Group(ListeningPartyHub.GroupName(normalized.PodId, normalized.ChannelId))
            .SendAsync("partyState", normalized, cancellationToken);

        return normalized;
    }

    private async Task PublishAnnouncementAsync(ListeningPartyEvent partyEvent, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var streamTicket = partyEvent.AllowMeshStreaming
            ? _streamTickets.Create(
                partyEvent.ContentId,
                $"listening-party:{partyEvent.PartyId}",
                TimeSpan.FromSeconds(AnnouncementTtlSeconds))
            : string.Empty;

        var announcement = new ListeningPartyAnnouncement
        {
            PartyId = partyEvent.PartyId,
            PodId = partyEvent.PodId,
            ChannelId = partyEvent.ChannelId,
            HostPeerId = partyEvent.HostPeerId,
            Title = partyEvent.Title,
            Artist = partyEvent.Artist,
            Album = partyEvent.Album,
            ContentId = partyEvent.ContentId,
            Description = partyEvent.Description,
            Tags = partyEvent.Tags.ToList(),
            AllowMeshStreaming = partyEvent.AllowMeshStreaming,
            StreamPath = partyEvent.AllowMeshStreaming
                ? $"/api/v0/listening-party/radio/{Uri.EscapeDataString(partyEvent.PartyId)}/{Uri.EscapeDataString(partyEvent.ContentId)}?ticket={Uri.EscapeDataString(streamTicket)}"
                : string.Empty,
            StartedAtUnixMs = partyEvent.ServerTimeUnixMs,
            LastSeenUnixMs = now.ToUnixTimeMilliseconds(),
            ExpiresAtUnixMs = now.AddSeconds(AnnouncementTtlSeconds).ToUnixTimeMilliseconds(),
        };

        _directory[announcement.PartyId] = announcement;
        await _dht.PutAsync(AnnouncementKey(announcement.PartyId), Serialize(announcement), AnnouncementTtlSeconds, cancellationToken);
        await UpdateDirectoryIndexAsync(announcement.PartyId, add: true, cancellationToken);
    }

    private async Task RefreshDirectoryFromDhtAsync(CancellationToken cancellationToken)
    {
        var index = await GetAsync<ListeningPartyIndex>(DirectoryIndexKey, cancellationToken);
        if (index == null)
        {
            return;
        }

        foreach (var partyId in index.PartyIds)
        {
            var announcement = await GetAsync<ListeningPartyAnnouncement>(AnnouncementKey(partyId), cancellationToken);
            if (announcement != null)
            {
                _directory[announcement.PartyId] = announcement;
            }
        }
    }

    private async Task UpdateDirectoryIndexAsync(string partyId, bool add, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var index = await GetAsync<ListeningPartyIndex>(DirectoryIndexKey, cancellationToken) ?? new ListeningPartyIndex();
        var partyIds = index.PartyIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (add && !partyIds.Contains(partyId, StringComparer.Ordinal))
        {
            partyIds.Add(partyId);
        }
        else if (!add)
        {
            partyIds.RemoveAll(x => string.Equals(x, partyId, StringComparison.Ordinal));
        }

        await _dht.PutAsync(
            DirectoryIndexKey,
            Serialize(new ListeningPartyIndex
            {
                PartyIds = partyIds,
                UpdatedAtUnixMs = now,
            }),
            AnnouncementTtlSeconds,
            cancellationToken);
    }

    private ListeningPartyEvent Normalize(ListeningPartyEvent partyEvent)
    {
        var action = (partyEvent.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (action is not ("play" or "pause" or "seek" or "stop"))
        {
            throw new ArgumentException("Action must be play, pause, seek, or stop.", nameof(partyEvent));
        }

        var contentId = action == "stop" ? string.Empty : (partyEvent.ContentId ?? string.Empty).Trim();
        if (action != "stop" && string.IsNullOrWhiteSpace(contentId))
        {
            throw new ArgumentException("ContentId is required for listen-along playback events.", nameof(partyEvent));
        }

        return partyEvent with
        {
            PartyId = string.IsNullOrWhiteSpace(partyEvent.PartyId)
                ? $"party:{Guid.NewGuid():N}"
                : partyEvent.PartyId.Trim(),
            Kind = ListeningPartyEvent.KindName,
            PodId = (partyEvent.PodId ?? string.Empty).Trim(),
            ChannelId = (partyEvent.ChannelId ?? string.Empty).Trim(),
            HostPeerId = (partyEvent.HostPeerId ?? string.Empty).Trim(),
            Action = action,
            ContentId = contentId,
            Title = (partyEvent.Title ?? string.Empty).Trim(),
            Artist = (partyEvent.Artist ?? string.Empty).Trim(),
            Album = string.IsNullOrWhiteSpace(partyEvent.Album) ? null : partyEvent.Album.Trim(),
            PositionSeconds = Math.Max(0, partyEvent.PositionSeconds),
            ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Sequence = Interlocked.Increment(ref _sequence),
            Description = (partyEvent.Description ?? string.Empty).Trim(),
            Tags = partyEvent.Tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList(),
        };
    }

    private static string StateKey(string podId, string channelId)
    {
        return $"{podId?.Trim()}:{channelId?.Trim()}";
    }

    private static string AnnouncementKey(string partyId)
    {
        return $"slskdn:listening-party:party:{partyId}";
    }

    private async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var raw = await _dht.GetRawAsync(key, cancellationToken).ConfigureAwait(false);
        return raw == null ? default : JsonSerializer.Deserialize<T>(raw, JsonOptions);
    }

    private static byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
    }
}
