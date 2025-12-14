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

        // Private service limits
        public const int MaxAllowedDestinations = 50;
        public const int MaxRegisteredServices = 20;
        public const int MaxHostPatternLength = 255;
        public const int MaxServiceNameLength = 50;
        public const int MaxServiceDescriptionLength = 200;
        public const int MaxConcurrentTunnelsPerPeer = 10;
        public const int MaxConcurrentTunnelsPod = 50;

        // Allowed character patterns (prevent injection attacks)
        private static readonly Regex PodIdPattern = new Regex(@"^pod:[a-f0-9]{32}$", RegexOptions.Compiled);
        private static readonly Regex PeerIdPattern = new Regex(@"^[a-zA-Z0-9\-_.@]{1,255}$", RegexOptions.Compiled);
        private static readonly Regex ChannelIdPattern = new Regex(@"^[a-z0-9\-_]{1,50}$", RegexOptions.Compiled);
        private static readonly Regex SafeStringPattern = new Regex(@"^[a-zA-Z0-9\s\-_.,'!?()]+$", RegexOptions.Compiled);

        /// <summary>
        /// Validates pod for creation/update.
        /// </summary>
        public static (bool IsValid, string Error) ValidatePod(Pod pod, List<PodMember>? members = null)
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

            // Capabilities validation
            var memberCount = members?.Count ?? 0;
            var capabilitiesValidation = ValidateCapabilities(pod.Capabilities, pod.PrivateServicePolicy, memberCount);
            if (!capabilitiesValidation.IsValid)
                return capabilitiesValidation;

            // Additional validation: enforce MaxMembers <= 3 for VPN pods
            if (pod.Capabilities?.Contains(PodCapability.PrivateServiceGateway) == true)
            {
                if (memberCount > (pod.PrivateServicePolicy?.MaxMembers ?? 3))
                {
                    return (false, $"Pod has {memberCount} members but private service gateway allows maximum {pod.PrivateServicePolicy?.MaxMembers ?? 3} members");
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

        /// <summary>
        /// Validates pod private service policy.
        /// </summary>
        public static (bool IsValid, string Error) ValidatePrivateServicePolicy(PodPrivateServicePolicy policy, List<PodMember> members)
        {
            if (policy == null)
                return (false, "Private service policy cannot be null");

            // Max members validation
            if (policy.MaxMembers < 2 || policy.MaxMembers > 10)
                return (false, "MaxMembers must be between 2 and 10");

            // Gateway peer validation
            if (string.IsNullOrWhiteSpace(policy.GatewayPeerId))
                return (false, "GatewayPeerId is required");

            if (!IsValidPeerId(policy.GatewayPeerId))
                return (false, "Invalid GatewayPeerId format");

            if (!members.Any(m => m.PeerId == policy.GatewayPeerId))
                return (false, "GatewayPeerId must be a pod member");

            // Registered services validation (preferred approach)
            if (policy.RegisteredServices == null)
                return (false, "RegisteredServices cannot be null");

            if (policy.RegisteredServices.Count > MaxRegisteredServices)
                return (false, $"Cannot have more than {MaxRegisteredServices} registered services");

            foreach (var service in policy.RegisteredServices)
            {
                var serviceValidation = ValidateRegisteredService(service);
                if (!serviceValidation.IsValid)
                    return serviceValidation;
            }

            // Legacy allowed destinations validation
            if (policy.AllowedDestinations == null)
                return (false, "AllowedDestinations cannot be null");

            if (policy.AllowedDestinations.Count > MaxAllowedDestinations)
                return (false, $"Cannot have more than {MaxAllowedDestinations} allowed destinations");

            foreach (var dest in policy.AllowedDestinations)
            {
                var destValidation = ValidateAllowedDestination(dest);
                if (!destValidation.IsValid)
                    return destValidation;
            }

            // If both are empty and capability is enabled, reject
            if (policy.Enabled && policy.RegisteredServices.Count == 0 && policy.AllowedDestinations.Count == 0)
                return (false, "Cannot enable private service gateway without registered services or allowed destinations");

            // Quota validations
            if (policy.MaxConcurrentTunnelsPerPeer < 0 || policy.MaxConcurrentTunnelsPerPeer > MaxConcurrentTunnelsPerPeer)
                return (false, $"MaxConcurrentTunnelsPerPeer must be between 0 and {MaxConcurrentTunnelsPerPeer}");

            if (policy.MaxConcurrentTunnelsPod < 0 || policy.MaxConcurrentTunnelsPod > MaxConcurrentTunnelsPod)
                return (false, $"MaxConcurrentTunnelsPod must be between 0 and {MaxConcurrentTunnelsPod}");

            if (policy.MaxNewTunnelsPerMinutePerPeer < 0 || policy.MaxNewTunnelsPerMinutePerPeer > 100)
                return (false, "MaxNewTunnelsPerMinutePerPeer must be between 0 and 100");

            // Timeout validations
            if (policy.IdleTimeout < TimeSpan.FromSeconds(30) || policy.IdleTimeout > TimeSpan.FromHours(24))
                return (false, "IdleTimeout must be between 30 seconds and 24 hours");

            if (policy.MaxLifetime < TimeSpan.FromMinutes(1) || policy.MaxLifetime > TimeSpan.FromDays(7))
                return (false, "MaxLifetime must be between 1 minute and 7 days");

            if (policy.DialTimeout < TimeSpan.FromSeconds(1) || policy.DialTimeout > TimeSpan.FromMinutes(5))
                return (false, "DialTimeout must be between 1 second and 5 minutes");

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates a registered service.
        /// </summary>
        public static (bool IsValid, string Error) ValidateRegisteredService(RegisteredService service)
        {
            if (service == null)
                return (false, "RegisteredService cannot be null");

            if (string.IsNullOrWhiteSpace(service.Name))
                return (false, "Service name is required");

            if (service.Name.Length > MaxServiceNameLength)
                return (false, $"Service name exceeds {MaxServiceNameLength} characters");

            if (ContainsDangerousContent(service.Name))
                return (false, "Service name contains invalid characters");

            if (service.Description?.Length > MaxServiceDescriptionLength)
                return (false, $"Service description exceeds {MaxServiceDescriptionLength} characters");

            if (string.IsNullOrWhiteSpace(service.Host))
                return (false, "Host is required");

            if (service.Host.Length > MaxHostPatternLength)
                return (false, $"Host exceeds {MaxHostPatternLength} characters");

            // Strict validation: no wildcards allowed in MVP
            if (service.Host.Contains('*') || service.Host.Contains('?'))
                return (false, "Wildcards are not allowed in registered services (use exact hostnames or IPs only)");

            if (!IsValidExactHostOrIP(service.Host))
                return (false, "Invalid host format (must be exact hostname or IP address)");

            if (service.Port < 1 || service.Port > 65535)
                return (false, "Port must be between 1 and 65535");

            if (service.Protocol != "tcp")
                return (false, "Only TCP protocol is currently supported");

            // Check for proxy ports and warn
            if (IsProxyPort(service.Port))
            {
                // In MVP, we could reject these, but for now just log the concern
                // The gateway operator needs to explicitly understand the risk
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates an allowed destination (legacy - prefer RegisteredService).
        /// </summary>
        public static (bool IsValid, string Error) ValidateAllowedDestination(AllowedDestination destination)
        {
            if (destination == null)
                return (false, "AllowedDestination cannot be null");

            if (string.IsNullOrWhiteSpace(destination.HostPattern))
                return (false, "HostPattern is required");

            if (destination.HostPattern.Length > MaxHostPatternLength)
                return (false, $"HostPattern exceeds {MaxHostPatternLength} characters");

            // MVP: No wildcards allowed - must be exact hostname or IP
            if (destination.HostPattern.Contains('*') || destination.HostPattern.Contains('?'))
                return (false, "Wildcards are not allowed in allowed destinations (use exact hostnames or IPs only)");

            if (!IsValidExactHostOrIP(destination.HostPattern))
                return (false, "Invalid HostPattern format (must be exact hostname or IP address)");

            if (destination.Port < 1 || destination.Port > 65535)
                return (false, "Port must be between 1 and 65535");

            if (destination.Protocol != "tcp")
                return (false, "Only TCP protocol is currently supported");

            // MVP: disallow public destinations entirely
            if (destination.AllowPublic)
                return (false, "Public destinations are not allowed in MVP");

            // Check for proxy ports
            if (IsProxyPort(destination.Port))
            {
                // Allow but log the security concern
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates host pattern (hostname, wildcard, or IP).
        /// </summary>
        private static bool IsValidHostPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return false;

            // Remove dangerous characters
            if (ContainsDangerousContent(pattern))
                return false;

            // Check if it's a valid hostname pattern
            var hostnamePattern = new Regex(@"^[a-zA-Z0-9\-\.\*\?]+$", RegexOptions.Compiled);
            if (!hostnamePattern.IsMatch(pattern))
                return false;

            // Additional validation for wildcard patterns
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                // Ensure wildcards are used reasonably
                if (pattern.Count(c => c == '*') > 2 || pattern.Count(c => c == '?') > 10)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validates exact hostname or IP address (no wildcards).
        /// </summary>
        private static bool IsValidExactHostOrIP(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;

            // Allow IP addresses
            if (IPAddress.TryParse(host, out _))
                return true;

            // Exact hostname validation (no wildcards)
            if (host.Contains('*') || host.Contains('?'))
                return false;

            // Hostname validation (simplified, no wildcards allowed)
            var hostnamePattern = @"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$";
            return Regex.IsMatch(host, hostnamePattern) && host.Length <= 253;
        }

        /// <summary>
        /// Checks if a port is commonly used for proxy services.
        /// </summary>
        private static bool IsProxyPort(int port)
        {
            // Common proxy ports that could allow tunneling through tunnels
            var proxyPorts = new[] { 1080, 3128, 8080, 8118, 9050, 9150 };
            return proxyPorts.Contains(port);
        }

        /// <summary>
        /// Validates pod capabilities against member count and other constraints.
        /// </summary>
        public static (bool IsValid, string Error) ValidateCapabilities(List<PodCapability> capabilities, PodPrivateServicePolicy? policy, int memberCount)
        {
            if (capabilities == null)
                return (true, string.Empty); // No capabilities is fine

            if (capabilities.Contains(PodCapability.PrivateServiceGateway))
            {
                if (policy == null)
                    return (false, "PrivateServiceGateway capability requires a policy");

                if (!policy.Enabled)
                    return (false, "PrivateServiceGateway capability requires policy.Enabled = true");

                // Enforce MVP limit: MaxMembers <= 3 for VPN pods
                if (policy.MaxMembers > 3)
                    return (false, "PrivateServiceGateway allows maximum 3 members in MVP");

                if (memberCount > policy.MaxMembers)
                    return (false, $"Pod has {memberCount} members but private service gateway allows maximum {policy.MaxMembers}");

                // Require non-empty allowlist
                if (policy.AllowedDestinations == null || policy.AllowedDestinations.Count == 0)
                    return (false, "PrivateServiceGateway capability requires at least one allowed destination");

                // Validate gateway peer designation
                if (string.IsNullOrWhiteSpace(policy.GatewayPeerId))
                    return (false, "PrivateServiceGateway capability requires a designated gateway peer");

                if (!IsValidPeerId(policy.GatewayPeerId))
                    return (false, "Invalid GatewayPeerId format");

                var policyValidation = ValidatePrivateServicePolicy(policy, new List<PodMember>()); // Members validated separately
                if (!policyValidation.IsValid)
                    return policyValidation;
            }

            return (true, string.Empty);
        }
    }
}
