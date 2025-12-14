// <copyright file="ITimingObfuscator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Interface for timing obfuscation implementations.
/// </summary>
public interface ITimingObfuscator
{
    /// <summary>
    /// Gets the delay to apply before sending the next message.
    /// </summary>
    /// <returns>The delay in milliseconds.</returns>
    Task<int> GetNextDelayAsync();
}

