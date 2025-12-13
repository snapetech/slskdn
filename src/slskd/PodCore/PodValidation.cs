// <copyright file="PodValidation.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.PodCore
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Security validation for pod operations.
    /// </summary>
    public static class PodValidation
    {
        // Security limits
        public const int MaxPodNameLength = 100;
        public const int MaxMessageBodyLength = 10000; // 10KB
        public const int MaxChannelNameLength = 50;
        public const int MaxTagLength = 50;
        public const int MaxTagsCount = 20;
        public const int MaxChannelsCount = 50;
        public const int MaxPeerIdLength = 255;
        public const int MaxPublicKeyLength = 1024;

        // Allowed character patterns (prevent injection attacks)
        private static readonly Regex PodIdPattern = new Regex(@"^pod:[a-f0-9]{32}$", RegexOptions.Compiled);
        private static readonly Regex PeerIdPattern = new Regex(@"^[a-zA-Z0-9\-_.@]{1,255}$", RegexOptions.Compiled);
        private static readonly Regex ChannelIdPattern = new Regex(@"^[a-z0-9\-_]{1,50}$", RegexOptions.Compiled);
        private static readonly Regex SafeStringPattern = new Regex(@"^[a-zA-Z0-9\s\-_.,'!?()]+$", RegexOptions.Compiled);

        /// <summary>
        /// Validates pod for creation/update.
        /// </summary>
        public static (bool IsValid, string Error) ValidatePod(Pod pod)
        {
            if (pod == null)
                return (false, "Pod cannot be null");

            // Pod ID
            if (!string.IsNullOrEmpty(pod.PodId) && !PodIdPattern.IsMatch(pod.PodId))
                return (false, "Invalid pod ID format");

            // Pod name
            if (string.IsNullOrWhiteSpace(pod.Name))
                return (false, "Pod name is required");

            if (pod.Name.Length > MaxPodNameLength)
                return (false, $"Pod name exceeds {MaxPodNameLength} characters");

            if (ContainsDangerousContent(pod.Name))
                return (false, "Pod name contains invalid characters");

            // Tags
            if (pod.Tags != null)
            {
                if (pod.Tags.Count > MaxTagsCount)
                    return (false, $"Exceeds maximum {MaxTagsCount} tags");

                foreach (var tag in pod.Tags)
                {
                    if (tag.Length > MaxTagLength)
                        return (false, $"Tag exceeds {MaxTagLength} characters");

                    if (ContainsDangerousContent(tag))
                        return (false, "Tag contains invalid characters");
                }
            }

            // Channels
            if (pod.Channels != null)
            {
                if (pod.Channels.Count > MaxChannelsCount)
                    return (false, $"Exceeds maximum {MaxChannelsCount} channels");

                foreach (var channel in pod.Channels)
                {
                    if (string.IsNullOrWhiteSpace(channel.ChannelId))
                        return (false, "Channel ID is required");

                    if (!ChannelIdPattern.IsMatch(channel.ChannelId))
                        return (false, "Invalid channel ID format (alphanumeric, dash, underscore only)");

                    if (channel.Name.Length > MaxChannelNameLength)
                        return (false, $"Channel name exceeds {MaxChannelNameLength} characters");
                }
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates pod member.
        /// </summary>
        public static (bool IsValid, string Error) ValidateMember(PodMember member)
        {
            if (member == null)
                return (false, "Member cannot be null");

            if (string.IsNullOrWhiteSpace(member.PeerId))
                return (false, "Peer ID is required");

            if (member.PeerId.Length > MaxPeerIdLength)
                return (false, $"Peer ID exceeds {MaxPeerIdLength} characters");

            if (!PeerIdPattern.IsMatch(member.PeerId))
                return (false, "Invalid peer ID format");

            if (!string.IsNullOrEmpty(member.PublicKey) && member.PublicKey.Length > MaxPublicKeyLength)
                return (false, $"Public key exceeds {MaxPublicKeyLength} characters");

            // Validate role
            var validRoles = new[] { "owner", "mod", "member" };
            if (!string.IsNullOrEmpty(member.Role) && !validRoles.Contains(member.Role))
                return (false, "Invalid role (must be: owner, mod, member)");

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates pod message.
        /// </summary>
        public static (bool IsValid, string Error) ValidateMessage(PodMessage message)
        {
            if (message == null)
                return (false, "Message cannot be null");

            if (string.IsNullOrWhiteSpace(message.SenderPeerId))
                return (false, "Sender peer ID is required");

            if (!PeerIdPattern.IsMatch(message.SenderPeerId))
                return (false, "Invalid sender peer ID format");

            if (string.IsNullOrWhiteSpace(message.Body))
                return (false, "Message body is required");

            if (message.Body.Length > MaxMessageBodyLength)
                return (false, $"Message body exceeds {MaxMessageBodyLength} characters");

            // Check for potential XSS/injection attacks
            if (ContainsDangerousContent(message.Body))
                return (false, "Message contains potentially dangerous content");

            return (true, string.Empty);
        }

        /// <summary>
        /// Sanitizes string for safe storage/display.
        /// </summary>
        public static string Sanitize(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Trim and truncate
            input = input.Trim();
            if (input.Length > maxLength)
                input = input.Substring(0, maxLength);

            // Remove control characters
            return new string(input.Where(c => !char.IsControl(c) || c == '\n' || c == '\r').ToArray());
        }

        /// <summary>
        /// Checks for dangerous content (XSS, SQL injection attempts, etc.).
        /// </summary>
        private static bool ContainsDangerousContent(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // Check for script tags, SQL injection patterns, etc.
            var dangerousPatterns = new[]
            {
                "<script",
                "javascript:",
                "onerror=",
                "onload=",
                "'; DROP",
                "'; DELETE",
                "UNION SELECT",
                "../",
                "..\\",
                "%00", // null byte
            };

            var lowerInput = input.ToLowerInvariant();
            return dangerousPatterns.Any(pattern => lowerInput.Contains(pattern.ToLowerInvariant()));
        }

        /// <summary>
        /// Validates string ID format.
        /// </summary>
        public static bool IsValidPodId(string podId)
        {
            return !string.IsNullOrEmpty(podId) && PodIdPattern.IsMatch(podId);
        }

        /// <summary>
        /// Validates peer ID format.
        /// </summary>
        public static bool IsValidPeerId(string peerId)
        {
            return !string.IsNullOrEmpty(peerId) && PeerIdPattern.IsMatch(peerId);
        }

        /// <summary>
        /// Validates channel ID format.
        /// </summary>
        public static bool IsValidChannelId(string channelId)
        {
            return !string.IsNullOrEmpty(channelId) && ChannelIdPattern.IsMatch(channelId);
        }
    }
}














