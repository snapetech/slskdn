// <copyright file="ModerationVerdict.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Common.Moderation
{
    /// <summary>
    ///     Represents the verdict of a moderation decision.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         MCP (Moderation / Control Plane) uses these verdicts to control
    ///         content shareability, advertisability, and peer reputation.
    ///     </para>
    ///     <para>
    ///         See `docs/moderation-v1-design.md` and `docs/MCP-HARDENING.md` for
    ///         complete moderation system design and security requirements.
    ///     </para>
    /// </remarks>
    public enum ModerationVerdict
    {
        /// <summary>
        ///     Content is allowed (no policy violation detected).
        /// </summary>
        /// <remarks>
        ///     Content may be shared, advertised, and relayed.
        /// </remarks>
        Allowed = 0,

        /// <summary>
        ///     Content is blocked (must not be shared or advertised).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Blocked content:
        ///         - MUST NOT be marked as shareable
        ///         - MUST NOT be advertised to peers
        ///         - MUST NOT be served via content relay
        ///         - MAY be kept locally for legal/compliance reasons
        ///     </para>
        ///     <para>
        ///         Reasons for blocking:
        ///         - Hash matches blocklist
        ///         - External moderation service flagged
        ///         - Policy violation detected
        ///     </para>
        /// </remarks>
        Blocked = 1,

        /// <summary>
        ///     Content is quarantined (kept locally but not shared).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Quarantined content:
        ///         - MUST NOT be shared or advertised
        ///         - MAY be kept for legal holds or compliance
        ///         - Similar restrictions to Blocked, but with different semantic intent
        ///     </para>
        ///     <para>
        ///         Use cases:
        ///         - Legal hold pending investigation
        ///         - Suspected but not confirmed violation
        ///         - Operator discretion
        ///     </para>
        /// </remarks>
        Quarantined = 2,

        /// <summary>
        ///     MCP has no data about this content (default when no providers have opinions).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Unknown content:
        ///         - Falls back to user share preferences
        ///         - Not explicitly allowed or blocked by MCP
        ///         - Default when no moderation providers are configured
        ///     </para>
        ///     <para>
        ///         This is the conservative default: MCP doesn't make assumptions.
        ///     </para>
        /// </remarks>
        Unknown = 3,
    }
}
