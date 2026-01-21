// <copyright file="PodIdFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Messaging
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Factory for generating pod IDs.
    /// </summary>
    public static class PodIdFactory
    {
        /// <summary>
        /// Generates a unique pod ID.
        /// </summary>
        /// <param name="podName">The pod name.</param>
        /// <param name="timestamp">Optional timestamp.</param>
        /// <returns>A unique pod ID.</returns>
        public static string Generate(string podName, DateTimeOffset? timestamp = null)
        {
            var ts = timestamp ?? DateTimeOffset.UtcNow;
            var input = $"{podName}:{ts.ToUnixTimeMilliseconds()}";
            
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        }

        /// <summary>
        /// Generates a conversation-specific pod ID from two user IDs.
        /// </summary>
        /// <param name="userId1">First user ID.</param>
        /// <param name="userId2">Second user ID.</param>
        /// <returns>A deterministic pod ID for the conversation.</returns>
        public static string ConversationPodId(string userId1, string userId2)
        {
            // Sort user IDs to ensure consistent pod ID regardless of order
            var users = new[] { userId1, userId2 };
            Array.Sort(users, StringComparer.Ordinal);
            
            var input = $"conversation:{users[0]}:{users[1]}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return $"pod:conv:{Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant()}";
        }
    }
}

