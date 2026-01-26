// <copyright file="IStreamSessionLimiter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Streaming;

/// <summary>
/// Limits concurrent stream sessions per key (e.g. shareId or "user:userId"). In-memory v1.
/// Caller must call Release with the same key when the stream ends (e.g. when the response stream is disposed).
/// </summary>
public interface IStreamSessionLimiter
{
    /// <summary>
    /// Tries to acquire a session slot. Returns true if count &lt; maxConcurrent and the slot was taken.
    /// </summary>
    /// <param name="key">Limit key (e.g. share grant id or "user:username").</param>
    /// <param name="maxConcurrent">Maximum concurrent streams for this key.</param>
    /// <returns>True if acquired; false if at limit.</returns>
    bool TryAcquire(string key, int maxConcurrent);

    /// <summary>
    /// Releases one session slot for the key. Idempotent if over-released (count not decremented below 0).
    /// </summary>
    /// <param name="key">The same key passed to TryAcquire.</param>
    void Release(string key);
}
