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
        /// <returns>A deterministic pod ID for the conversation (format: pod: + 32 hex chars).</returns>
        public static string ConversationPodId(string userId1, string userId2)
        {
            // Sort user IDs to ensure consistent pod ID regardless of order
            var users = new[] { userId1, userId2 };
            Array.Sort(users, StringComparer.Ordinal);

            var input = $"conversation:{users[0]}:{users[1]}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            return $"pod:{hex.Substring(0, 32)}";
        }

        /// <summary>
        /// Generates a conversation-specific pod ID from an array of exactly two peer IDs.
        /// </summary>
        /// <param name="peerIds">Exactly two peer IDs (e.g. self and remote).</param>
        /// <returns>A deterministic pod ID for the conversation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="peerIds"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="peerIds"/> does not have exactly two elements.</exception>
        public static string ConversationPodId(string[] peerIds)
        {
            if (peerIds == null)
                throw new ArgumentNullException(nameof(peerIds));
            if (peerIds.Length != 2)
                throw new ArgumentException("Exactly two peer IDs are required.", nameof(peerIds));
            for (int i = 0; i < peerIds.Length; i++)
            {
                if (peerIds[i] == null)
                    throw new ArgumentException("Peer IDs must be non-null.", nameof(peerIds));
                if (string.IsNullOrWhiteSpace(peerIds[i]))
                    throw new ArgumentException("Peer IDs must be non-empty.", nameof(peerIds));
            }
            return ConversationPodId(peerIds[0], peerIds[1]);
        }
    }
}

