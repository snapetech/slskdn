// <copyright file="TofuPinStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

/// <summary>
/// Trust On First Use (TOFU) pin storage.
/// Stores SPKI pins learned from first connection when descriptor unavailable.
/// </summary>
public interface ITofuPinStore
{
    /// <summary>
    /// Records a pin learned on first connection.
    /// </summary>
    void RecordPin(IPEndPoint endpoint, string spkiHash, TofuPinType type);

    /// <summary>
    /// Gets a previously recorded pin.
    /// </summary>
    (string? spkiHash, DateTimeOffset? recordedAt) GetPin(IPEndPoint endpoint, TofuPinType type);

    /// <summary>
    /// Checks if an endpoint has a recorded pin.
    /// </summary>
    bool HasPin(IPEndPoint endpoint, TofuPinType type);
}

public enum TofuPinType
{
    Control,
    Data,
}

/// <summary>
/// In-memory TOFU pin storage.
/// TODO: Persist to disk for longer-term trust.
/// </summary>
public class TofuPinStore : ITofuPinStore
{
    private readonly ILogger<TofuPinStore> logger;
    private readonly ConcurrentDictionary<string, TofuPin> pins = new();

    public TofuPinStore(ILogger<TofuPinStore> logger)
    {
        this.logger = logger;
    }

    public void RecordPin(IPEndPoint endpoint, string spkiHash, TofuPinType type)
    {
        var key = GetKey(endpoint, type);
        var pin = new TofuPin
        {
            SpkiHash = spkiHash,
            RecordedAt = DateTimeOffset.UtcNow,
        };

        pins.AddOrUpdate(key, pin, (_, _) => pin);
        logger.LogInformation("[TofuPinStore] Recorded {Type} pin for {Endpoint}", type, endpoint);
    }

    public (string? spkiHash, DateTimeOffset? recordedAt) GetPin(IPEndPoint endpoint, TofuPinType type)
    {
        var key = GetKey(endpoint, type);
        if (pins.TryGetValue(key, out var pin))
        {
            return (pin.SpkiHash, pin.RecordedAt);
        }

        return (null, null);
    }

    public bool HasPin(IPEndPoint endpoint, TofuPinType type)
    {
        var key = GetKey(endpoint, type);
        return pins.ContainsKey(key);
    }

    private static string GetKey(IPEndPoint endpoint, TofuPinType type)
    {
        return $"{endpoint}:{type}";
    }

    private class TofuPin
    {
        public required string SpkiHash { get; init; }
        public required DateTimeOffset RecordedAt { get; init; }
    }
}

