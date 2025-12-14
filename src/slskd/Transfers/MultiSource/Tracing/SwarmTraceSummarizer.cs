// <copyright file="SwarmTraceSummarizer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISwarmTraceSummarizer
    {
        Task<SwarmTraceSummary> SummarizeAsync(string jobId, CancellationToken ct = default);
    }

    public class SwarmTraceSummarizer : ISwarmTraceSummarizer
    {
        private readonly ISwarmEventStore store;

        public SwarmTraceSummarizer(ISwarmEventStore store)
        {
            this.store = store;
        }

        public async Task<SwarmTraceSummary> SummarizeAsync(string jobId, CancellationToken ct = default)
        {
            var events = await store.ReadAsync(jobId, limit: 50000, ct).ConfigureAwait(false);

            var summary = new SwarmTraceSummary
            {
                JobId = jobId,
                TotalEvents = events.Count,
            };

            var peerMap = new Dictionary<string, PeerContribution>(StringComparer.OrdinalIgnoreCase);

            foreach (var evt in events)
            {
                if (evt == null)
                {
                    continue;
                }

                // First/last
                summary.FirstEventAt = Min(summary.FirstEventAt, evt.Timestamp);
                summary.LastEventAt = Max(summary.LastEventAt, evt.Timestamp);

                // Event counts
                var etype = evt.EventType.ToString();
                summary.EventCounts.TryGetValue(etype, out var count);
                summary.EventCounts[etype] = count + 1;

                // Rescue marker
                if (evt.EventType == SwarmEventType.RescueInvoked)
                {
                    summary.RescueInvoked = true;
                }

                // Bytes by source/backend
                if (evt.Bytes.HasValue && evt.Bytes.Value > 0)
                {
                    var src = evt.Source.ToString();
                    AddBytes(summary.BytesBySource, src, evt.Bytes.Value);

                    if (!string.IsNullOrWhiteSpace(evt.Backend))
                    {
                        AddBytes(summary.BytesByBackend, evt.Backend, evt.Bytes.Value);
                    }
                }

                // Peer contributions
                if (!string.IsNullOrWhiteSpace(evt.PeerId))
                {
                    if (!peerMap.TryGetValue(evt.PeerId, out var peer))
                    {
                        peer = new PeerContribution { PeerId = evt.PeerId };
                        peerMap[evt.PeerId] = peer;
                    }

                    switch (evt.EventType)
                    {
                        case SwarmEventType.ChunkReceived:
                        case SwarmEventType.ChunkVerified:
                            peer.ChunksCompleted++;
                            peer.BytesServed += evt.Bytes ?? 0;
                            break;
                        case SwarmEventType.ChunkFailed:
                            peer.ChunksFailed++;
                            break;
                        case SwarmEventType.ChunkTimedOut:
                            peer.ChunksTimedOut++;
                            break;
                    }
                }
            }

            summary.Peers = peerMap.Values
                .OrderByDescending(p => p.BytesServed)
                .ThenByDescending(p => p.ChunksCompleted)
                .ToList();

            return summary;
        }

        private static void AddBytes(Dictionary<string, long> dict, string key, long bytes)
        {
            if (!dict.TryGetValue(key, out var val))
            {
                val = 0;
            }
            dict[key] = val + bytes;
        }

        private static DateTimeOffset? Min(DateTimeOffset? a, DateTimeOffset b)
        {
            if (a == null)
            {
                return b;
            }
            return a.Value <= b ? a : b;
        }

        private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset b)
        {
            if (a == null)
            {
                return b;
            }
            return a.Value >= b ? a : b;
        }
    }
}
