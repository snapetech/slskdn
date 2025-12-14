// <copyright file="IdentitySeparationEnforcer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using slskd.Core;

    /// <summary>
    ///     Enforces separation between different identity types to prevent cross-contamination.
    /// </summary>
    /// <remarks>
    ///     H-ID01: Identity Separation Enforcement.
    ///     Ensures mesh identities, Soulseek identities, pod identities, and local user identities
    ///     remain properly separated and don't leak or cross-contaminate each other.
    /// </remarks>
    public static class IdentitySeparationEnforcer
    {
        /// <summary>
        ///     Identity types that must remain separated.
        /// </summary>
        public enum IdentityType
        {
            /// <summary>
            ///     Mesh overlay identity (Ed25519 key pairs).
            /// </summary>
            Mesh,

            /// <summary>
            ///     Soulseek network identity (username/password).
            /// </summary>
            Soulseek,

            /// <summary>
            ///     Pod peer identity (internal pod communication).
            /// </summary>
            Pod,

            /// <summary>
            ///     Local user/operator identity (web UI, API).
            /// </summary>
            LocalUser,

            /// <summary>
            ///     ActivityPub actor identity (future federation).
            /// </summary>
            ActivityPub
        }

        /// <summary>
        ///     Validates that an identity belongs to the correct type.
        /// </summary>
        /// <param name="identity">The identity string to validate.</param>
        /// <param name="expectedType">The expected identity type.</param>
        /// <returns>True if the identity format matches the expected type.</returns>
        public static bool IsValidIdentityFormat(string identity, IdentityType expectedType)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                return false;
            }

            return expectedType switch
            {
                IdentityType.Mesh => IsValidMeshIdentity(identity),
                IdentityType.Soulseek => IsValidSoulseekIdentity(identity),
                IdentityType.Pod => IsValidPodIdentity(identity),
                IdentityType.LocalUser => IsValidLocalUserIdentity(identity),
                IdentityType.ActivityPub => IsValidActivityPubIdentity(identity),
                _ => false
            };
        }

        /// <summary>
        ///     Checks for potential identity cross-contamination.
        /// </summary>
        /// <param name="identity">The identity to check.</param>
        /// <param name="forbiddenTypes">Identity types that this identity must not match.</param>
        /// <returns>True if the identity matches any forbidden type.</returns>
        public static bool HasCrossContamination(string identity, params IdentityType[] forbiddenTypes)
        {
            if (string.IsNullOrWhiteSpace(identity) || forbiddenTypes.Length == 0)
            {
                return false;
            }

            return forbiddenTypes.Any(type => IsValidIdentityFormat(identity, type));
        }

        /// <summary>
        ///     Sanitizes a pod peer ID to remove any Soulseek identity leakage.
        /// </summary>
        /// <param name="podPeerId">The pod peer ID to sanitize.</param>
        /// <returns>A sanitized peer ID that doesn't leak external identities.</returns>
        public static string SanitizePodPeerId(string podPeerId)
        {
            if (string.IsNullOrWhiteSpace(podPeerId))
            {
                return podPeerId;
            }

            // If it's a bridge identity, convert to internal-only format
            if (podPeerId.StartsWith("bridge:", StringComparison.OrdinalIgnoreCase))
            {
                // Create a deterministic hash of the original identity
                // This preserves uniqueness without leaking the original username
                var hash = System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(podPeerId));
                return $"pod:{Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant()}";
            }

            return podPeerId;
        }

        /// <summary>
        ///     Validates that a pod peer ID doesn't leak external identities.
        /// </summary>
        /// <param name="podPeerId">The pod peer ID to validate.</param>
        /// <returns>True if the peer ID is safe (doesn't leak external identities).</returns>
        public static bool IsSafePodPeerId(string podPeerId)
        {
            if (string.IsNullOrWhiteSpace(podPeerId))
            {
                return false;
            }

            // Reject bridge: format as it leaks Soulseek usernames
            if (podPeerId.StartsWith("bridge:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Reject formats that might leak other external identities
            if (podPeerId.Contains("@") || // Email-like
                podPeerId.Contains("/") || // URL-like
                podPeerId.Contains("\\"))   // Windows path-like
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Gets the identity type for a given identity string.
        /// </summary>
        /// <param name="identity">The identity to classify.</param>
        /// <returns>The detected identity type, or null if unrecognized.</returns>
        public static IdentityType? DetectIdentityType(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                return null;
            }

            foreach (var type in Enum.GetValues<IdentityType>())
            {
                if (IsValidIdentityFormat(identity, type))
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        ///     Validates mesh identity format (Ed25519 public key hash).
        /// </summary>
        private static bool IsValidMeshIdentity(string identity)
        {
            // Mesh identities are typically Ed25519 public key hashes
            // Format: base64-encoded 32-byte public key, or hex-encoded
            return identity.Length >= 32 && identity.Length <= 64 &&
                   (identity.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=') || // base64
                    identity.All(c => char.IsLetterOrDigit(c) || "abcdefABCDEF".Contains(c))); // hex
        }

        /// <summary>
        ///     Validates Soulseek identity format (username).
        /// </summary>
        private static bool IsValidSoulseekIdentity(string identity)
        {
            // Soulseek usernames: alphanumeric, underscores, dots, max 30 chars
            return identity.Length >= 1 && identity.Length <= 30 &&
                   identity.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');
        }

        /// <summary>
        ///     Validates pod identity format.
        /// </summary>
        private static bool IsValidPodIdentity(string identity)
        {
            // Pod peer IDs: internal format like "pod:hexhash" or "mesh:self"
            return identity.StartsWith("pod:", StringComparison.OrdinalIgnoreCase) ||
                   identity.StartsWith("mesh:", StringComparison.OrdinalIgnoreCase) ||
                   identity.Equals("self", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Validates local user identity format.
        /// </summary>
        private static bool IsValidLocalUserIdentity(string identity)
        {
            // Local users: typically email-like or simple usernames
            return identity.Contains("@") || // Email format
                   (identity.Length >= 3 && identity.Length <= 50 &&
                    identity.All(c => char.IsLetterOrDigit(c) || "_-.@".Contains(c)));
        }

        /// <summary>
        ///     Validates ActivityPub identity format (future).
        /// </summary>
        private static bool IsValidActivityPubIdentity(string identity)
        {
            // ActivityPub actors: @username@domain format
            return identity.StartsWith("@") && identity.IndexOf('@', 1) > 0;
        }
    }
}
