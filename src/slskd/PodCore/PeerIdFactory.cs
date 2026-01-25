// <copyright file="PeerIdFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;

/// <summary>
/// Factory for creating peer IDs from external identity sources (e.g. Soulseek usernames).
/// </summary>
public static class PeerIdFactory
{
    /// <summary>
    /// Creates a bridge peer ID from a Soulseek username.
    /// </summary>
    /// <param name="username">The Soulseek username. Must not be null or whitespace.</param>
    /// <returns>A peer ID in the form "bridge:{username}".</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="username"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="username"/> is empty or whitespace.</exception>
    public static string FromSoulseekUsername(string? username)
    {
        if (username == null)
            throw new ArgumentNullException(nameof(username));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty or whitespace.", nameof(username));
        return "bridge:" + username;
    }
}
