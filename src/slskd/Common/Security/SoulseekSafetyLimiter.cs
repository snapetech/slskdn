// <copyright file="SoulseekSafetyLimiter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     Enforces Soulseek-specific safety caps to prevent abuse and network violations (H-08).
/// </summary>
public interface ISoulseekSafetyLimiter
{
    /// <summary>
    ///     Attempts to consume a search operation.
    /// </summary>
    /// <param name="source">The source of the search (user or mesh).</param>
    /// <returns>True if the operation is allowed; otherwise, false.</returns>
    bool TryConsumeSearch(string source = "user");

    /// <summary>
    ///     Attempts to consume a browse operation.
    /// </summary>
    /// <param name="source">The source of the browse (user or mesh).</param>
    /// <returns>True if the operation is allowed; otherwise, false.</returns>
    bool TryConsumeBrowse(string source = "user");

    /// <summary>
    ///     Gets current metrics for monitoring.
    /// </summary>
    /// <returns>Safety limiter metrics.</returns>
    SoulseekSafetyMetrics GetMetrics();
}

/// <summary>
///     Implements Soulseek safety caps with sliding window rate tracking.
/// </summary>
public class SoulseekSafetyLimiter : ISoulseekSafetyLimiter
{
    private readonly ILogger<SoulseekSafetyLimiter> _logger;
    private readonly IOptionsMonitor<slskd.Options> _options;
    
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _searchWindows = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _browseWindows = new();
    
    private const int WindowDurationSeconds = 60;

    public SoulseekSafetyLimiter(
        IOptionsMonitor<slskd.Options> options,
        ILogger<SoulseekSafetyLimiter> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool TryConsumeSearch(string source = "user")
    {
        var opts = _options.CurrentValue.Soulseek.Safety;
        
        if (!opts.Enabled || opts.MaxSearchesPerMinute <= 0)
        {
            return true; // Unlimited
        }

        var key = $"search:{source}";
        var window = _searchWindows.GetOrAdd(key, _ => new ConcurrentQueue<DateTime>());
        
        lock (window)
        {
            CleanExpiredEntries(window);
            
            if (window.Count >= opts.MaxSearchesPerMinute)
            {
                _logger.LogWarning(
                    "[SAFETY] Search rate limit exceeded for source={Source}. " +
                    "Limit={Limit}/min, Current={Current}",
                    source, opts.MaxSearchesPerMinute, window.Count);
                return false;
            }
            
            window.Enqueue(DateTime.UtcNow);
            return true;
        }
    }

    /// <inheritdoc/>
    public bool TryConsumeBrowse(string source = "user")
    {
        var opts = _options.CurrentValue.Soulseek.Safety;
        
        if (!opts.Enabled || opts.MaxBrowsesPerMinute <= 0)
        {
            return true; // Unlimited
        }

        var key = $"browse:{source}";
        var window = _browseWindows.GetOrAdd(key, _ => new ConcurrentQueue<DateTime>());
        
        lock (window)
        {
            CleanExpiredEntries(window);
            
            if (window.Count >= opts.MaxBrowsesPerMinute)
            {
                _logger.LogWarning(
                    "[SAFETY] Browse rate limit exceeded for source={Source}. " +
                    "Limit={Limit}/min, Current={Current}",
                    source, opts.MaxBrowsesPerMinute, window.Count);
                return false;
            }
            
            window.Enqueue(DateTime.UtcNow);
            return true;
        }
    }

    /// <inheritdoc/>
    public SoulseekSafetyMetrics GetMetrics()
    {
        var opts = _options.CurrentValue.Soulseek.Safety;
        
        var searchesBySource = _searchWindows.ToDictionary(
            kvp => kvp.Key.Replace("search:", ""),
            kvp =>
            {
                lock (kvp.Value)
                {
                    CleanExpiredEntries(kvp.Value);
                    return kvp.Value.Count;
                }
            });

        var browsesBySource = _browseWindows.ToDictionary(
            kvp => kvp.Key.Replace("browse:", ""),
            kvp =>
            {
                lock (kvp.Value)
                {
                    CleanExpiredEntries(kvp.Value);
                    return kvp.Value.Count;
                }
            });

        return new SoulseekSafetyMetrics
        {
            Enabled = opts.Enabled,
            MaxSearchesPerMinute = opts.MaxSearchesPerMinute,
            MaxBrowsesPerMinute = opts.MaxBrowsesPerMinute,
            SearchesLastMinute = searchesBySource.Values.Sum(),
            BrowsesLastMinute = browsesBySource.Values.Sum(),
            SearchesBySource = searchesBySource,
            BrowsesBySource = browsesBySource
        };
    }

    private static void CleanExpiredEntries(ConcurrentQueue<DateTime> window)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-WindowDurationSeconds);
        
        while (window.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            window.TryDequeue(out _);
        }
    }
}

/// <summary>
///     Metrics for Soulseek safety limiter.
/// </summary>
public record SoulseekSafetyMetrics
{
    /// <summary>
    ///     Gets a value indicating whether safety caps are enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    ///     Gets the maximum searches per minute limit.
    /// </summary>
    public int MaxSearchesPerMinute { get; init; }

    /// <summary>
    ///     Gets the maximum browses per minute limit.
    /// </summary>
    public int MaxBrowsesPerMinute { get; init; }

    /// <summary>
    ///     Gets the total searches in the last minute.
    /// </summary>
    public int SearchesLastMinute { get; init; }

    /// <summary>
    ///     Gets the total browses in the last minute.
    /// </summary>
    public int BrowsesLastMinute { get; init; }

    /// <summary>
    ///     Gets searches by source (user/mesh).
    /// </summary>
    public Dictionary<string, int> SearchesBySource { get; init; } = new();

    /// <summary>
    ///     Gets browses by source (user/mesh).
    /// </summary>
    public Dictionary<string, int> BrowsesBySource { get; init; } = new();
}
