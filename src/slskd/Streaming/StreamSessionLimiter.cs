// <copyright file="StreamSessionLimiter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Streaming;

using System;
using System.Collections.Concurrent;

/// <summary>In-memory implementation of IStreamSessionLimiter. One concurrent count per key.</summary>
public sealed class StreamSessionLimiter : IStreamSessionLimiter
{
    private readonly ConcurrentDictionary<string, int> _counts = new();
    private readonly object _sync = new();

    public bool TryAcquire(string key, int maxConcurrent)
    {
        if (string.IsNullOrEmpty(key) || maxConcurrent <= 0) return false;
        lock (_sync)
        {
            var c = _counts.GetValueOrDefault(key);
            if (c >= maxConcurrent) return false;
            _counts[key] = c + 1;
            return true;
        }
    }

    public void Release(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        lock (_sync)
        {
            var c = _counts.GetValueOrDefault(key);
            if (c <= 0) return;
            if (c == 1)
                _counts.TryRemove(key, out _);
            else
                _counts[key] = c - 1;
        }
    }
}
