// <copyright file="PlanningMode.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Planning
{
    /// <summary>
    ///     Planning mode that controls which backends are allowed.
    /// </summary>
    /// <remarks>
    ///     Planning modes provide coarse-grained control over backend selection.
    ///     The planner respects these modes when generating acquisition plans.
    /// </remarks>
    public enum PlanningMode
    {
        /// <summary>
        ///     No network backends; catalogue + local library only.
        /// </summary>
        /// <remarks>
        ///     Use for:
        ///     - Offline browsing
        ///     - Gap analysis without network calls
        ///     - Testing
        /// </remarks>
        OfflinePlanning,

        /// <summary>
        ///     Only mesh/DHT, torrent, HTTP, and LAN backends.
        /// </summary>
        /// <remarks>
        ///     Soulseek is explicitly excluded.
        ///     Use for:
        ///     - Non-music domains (Video, Book, GenericFile)
        ///     - Music when Soulseek should be avoided
        /// </remarks>
        MeshOnly,

        /// <summary>
        ///     Soulseek allowed, but under strict caps (default for Music).
        /// </summary>
        /// <remarks>
        ///     Soulseek can be used with:
        ///     - H-08 caps enforced (MaxSearchesPerMinute, etc.)
        ///     - Work budget limits (H-02)
        ///     - MCP gating (no blocked/quarantined sources)
        ///
        ///     This is the "friendly neighbor" mode for Music domain.
        /// </remarks>
        SoulseekFriendly,
    }
}
