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
    }
}

