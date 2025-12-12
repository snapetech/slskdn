// <copyright file="PlanningMode.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
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
