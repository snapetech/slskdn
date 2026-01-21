// <copyright file="PeerReputationStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Encrypted persistent store for peer reputation data.
    /// </summary>
    /// <remarks>
    ///     T-MCP04: Peer Reputation & Enforcement.
    ///     Implements encrypted persistence with DataProtection API.
    ///     Features ban threshold logic, reputation decay, and Sybil resistance.
    /// </remarks>
    public sealed class PeerReputationStore : IPeerReputationStore, IDisposable
    {
        private readonly ILogger<PeerReputationStore> _logger;
        private readonly IDataProtector _dataProtector;
        private readonly string _storagePath;
        private readonly ConcurrentDictionary<string, List<PeerReputationEvent>> _eventCache = new();
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private bool _dataLoaded;

        // Configuration constants
        private const int BanThreshold = 10; // 10 negative events = ban
        private const int MaxEventsPerPeer = 1000; // Prevent unbounded growth
        private const int DecayDays = 90; // Events older than 90 days decay
        private const double DecayFactor = 0.1; // Old events worth 10% of original value
        private const int MaxEventsPerHour = 100; // Sybil resistance: max events per hour per peer

        /// <summary>
        ///     Initializes a new instance of the <see cref="PeerReputationStore"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="dataProtector">The data protector for encryption.</param>
        /// <param name="storagePath">Path to store encrypted reputation data.</param>
        public PeerReputationStore(
            ILogger<PeerReputationStore> logger,
            IDataProtector dataProtector,
            string storagePath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataProtector = dataProtector ?? throw new ArgumentNullException(nameof(dataProtector));
            _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));

            // Ensure storage directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);

            // Data will be loaded lazily on first access
        }

        /// <inheritdoc/>
        public async Task RecordEventAsync(PeerReputationEvent @event, CancellationToken cancellationToken = default)
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            // Ensure data is loaded
            await EnsureDataLoadedAsync(cancellationToken);

            // Sybil resistance: rate limit events per peer
            if (await IsRateLimitedAsync(@event.PeerId, cancellationToken))
            {
                _logger.LogWarning("Rate limiting reputation event for peer {PeerId} (too many events per hour)", @event.PeerId);
                return;
            }

            // Add to cache
            var peerEvents = _eventCache.GetOrAdd(@event.PeerId, _ => new List<PeerReputationEvent>());
            lock (peerEvents)
            {
                peerEvents.Add(@event);

                // Keep only recent events
                if (peerEvents.Count > MaxEventsPerPeer)
                {
                    peerEvents.RemoveRange(0, peerEvents.Count - MaxEventsPerPeer);
                }
            }

            // Persist to disk
            await SaveToDiskAsync(cancellationToken);

            _logger.LogDebug("Recorded reputation event {EventType} for peer {PeerId}", @event.EventType, @event.PeerId);
        }

        /// <inheritdoc/>
        public async Task<int> GetReputationScoreAsync(string peerId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(peerId))
            {
                return 0;
            }

            // Ensure data is loaded
            await EnsureDataLoadedAsync(cancellationToken);

            if (!_eventCache.TryGetValue(peerId, out var events))
            {
                return 0;
            }

            // Calculate score with decay
            var now = DateTimeOffset.UtcNow;
            var score = 0;

            lock (events)
            {
                foreach (var @event in events)
                {
                    var age = now - @event.Timestamp;
                    var weight = age.TotalDays <= DecayDays ? 1.0 : DecayFactor;
                    score -= (int)(GetEventSeverity(@event.EventType) * weight);
                }
            }

            return score;
        }

        /// <inheritdoc/>
        public async Task<bool> IsPeerBannedAsync(string peerId, CancellationToken cancellationToken = default)
        {
            var score = await GetReputationScoreAsync(peerId, cancellationToken);
            return score <= -BanThreshold;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<PeerReputationEvent>> GetRecentEventsAsync(string peerId, int maxEvents = 50, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(peerId))
            {
                return Array.Empty<PeerReputationEvent>();
            }

            if (!_eventCache.TryGetValue(peerId, out var events))
            {
                return Array.Empty<PeerReputationEvent>();
            }

            lock (events)
            {
                return events
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxEvents)
                    .ToList();
            }
        }

        /// <inheritdoc/>
        public async Task DecayAndCleanupAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-DecayDays);
            var removedCount = 0;

            foreach (var kvp in _eventCache)
            {
                lock (kvp.Value)
                {
                    var originalCount = kvp.Value.Count;
                    kvp.Value.RemoveAll(e => e.Timestamp < cutoff);
                    removedCount += originalCount - kvp.Value.Count;
                }
            }

            if (removedCount > 0)
            {
                await SaveToDiskAsync(cancellationToken);
                _logger.LogInformation("Reputation decay removed {Count} old events", removedCount);
            }
        }

        /// <inheritdoc/>
        public async Task<PeerReputationStats> GetStatsAsync(CancellationToken cancellationToken = default)
        {
            // Ensure data is loaded
            await EnsureDataLoadedAsync(cancellationToken);

            var allEvents = _eventCache.Values.SelectMany(events =>
            {
                lock (events)
                {
                    return events.ToList();
                }
            }).ToList();

            var bannedPeers = 0;
            var totalScore = 0.0;

            foreach (var peerId in _eventCache.Keys)
            {
                if (await IsPeerBannedAsync(peerId, cancellationToken))
                {
                    bannedPeers++;
                }

                totalScore += await GetReputationScoreAsync(peerId, cancellationToken);
            }

            var eventsByType = allEvents
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            return new PeerReputationStats
            {
                TotalEvents = allEvents.Count,
                UniquePeers = _eventCache.Count,
                BannedPeers = bannedPeers,
                EventsByType = eventsByType,
                AverageReputationScore = _eventCache.Count > 0 ? totalScore / _eventCache.Count : 0
            };
        }

        /// <summary>
        ///     Disposes resources used by the store.
        /// </summary>
        public void Dispose()
        {
            _fileLock.Dispose();
        }

        /// <summary>
        ///     Ensures reputation data is loaded from disk.
        /// </summary>
        private async Task EnsureDataLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (_dataLoaded)
            {
                return;
            }

            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                if (!_dataLoaded)
                {
                    await LoadFromDiskAsync();
                    _dataLoaded = true;
                }
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        ///     Gets the severity score for a reputation event type.
        /// </summary>
        private static int GetEventSeverity(PeerReputationEventType eventType)
        {
            return eventType switch
            {
                PeerReputationEventType.AssociatedWithBlockedContent => 3,
                PeerReputationEventType.RequestedBlockedContent => 2,
                PeerReputationEventType.ServedBadCopy => 2,
                PeerReputationEventType.AbusiveBehavior => 4,
                PeerReputationEventType.ProtocolViolation => 1,
                _ => 1
            };
        }

        /// <summary>
        ///     Checks if a peer is rate limited for reputation events.
        /// </summary>
        private async Task<bool> IsRateLimitedAsync(string peerId, CancellationToken cancellationToken)
        {
            if (!_eventCache.TryGetValue(peerId, out var events))
            {
                return false;
            }

            var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);

            lock (events)
            {
                var recentEvents = events.Count(e => e.Timestamp >= oneHourAgo);
                return recentEvents >= MaxEventsPerHour;
            }
        }

        /// <summary>
        ///     Loads reputation data from encrypted disk storage.
        /// </summary>
        private async Task LoadFromDiskAsync()
        {
            if (!File.Exists(_storagePath))
            {
                return;
            }

            await _fileLock.WaitAsync();
            try
            {
                var encryptedData = await File.ReadAllBytesAsync(_storagePath);
                var decryptedData = _dataProtector.Unprotect(encryptedData);
                var json = Encoding.UTF8.GetString(decryptedData);

                var deserialized = JsonSerializer.Deserialize<Dictionary<string, List<PeerReputationEvent>>>(json);
                if (deserialized != null)
                {
                    foreach (var kvp in deserialized)
                    {
                        _eventCache[kvp.Key] = kvp.Value;
                    }
                }

                _logger.LogInformation("Loaded {Count} peer reputation records from disk", _eventCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load reputation data from disk");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        ///     Saves reputation data to encrypted disk storage.
        /// </summary>
        private async Task SaveToDiskAsync(CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, List<PeerReputationEvent>>();

            foreach (var kvp in _eventCache)
            {
                lock (kvp.Value)
                {
                    data[kvp.Key] = new List<PeerReputationEvent>(kvp.Value);
                }
            }

            var json = JsonSerializer.Serialize(data);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var encryptedData = _dataProtector.Protect(jsonBytes);

            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                await File.WriteAllBytesAsync(_storagePath, encryptedData, cancellationToken);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
