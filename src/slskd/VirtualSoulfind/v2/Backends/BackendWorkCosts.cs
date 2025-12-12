// <copyright file="BackendWorkCosts.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Backends
{
    using slskd.Common.Security;

    /// <summary>
    ///     Work budget costs specific to VirtualSoulfind v2 content backends.
    /// </summary>
    /// <remarks>
    ///     These costs integrate with the H-02 work budget system to prevent
    ///     amplification attacks where a single mesh service call triggers
    ///     expensive operations (Soulseek searches, HTTP fetches, etc.).
    ///     
    ///     Cost guidelines:
    ///     - 0 units: Local operations (LocalLibrary)
    ///     - 1 unit: Cheap network operations (HEAD requests)
    ///     - 2 units: Medium network operations (Mesh/Torrent queries)
    ///     - 3-5 units: Expensive operations (Soulseek searches, full HTTP downloads)
    /// </remarks>
    public static class BackendWorkCosts
    {
        /// <summary>
        ///     Cost for a LocalLibrary backend query (0 units - local only).
        /// </summary>
        public static readonly WorkCost LocalLibraryQuery = new(0, "LocalLibrary query");

        /// <summary>
        ///     Cost for a Soulseek search (5 units - network broadcast, many responses).
        /// </summary>
        /// <remarks>
        ///     Soulseek searches are THE most expensive operation:
        ///     - Broadcast to entire network
        ///     - Hundreds of potential responses
        ///     - Rate-limited by H-08 (MaxSearchesPerMinute)
        ///     
        ///     This MUST be protected by work budget to prevent mesh peers
        ///     from triggering unlimited Soulseek searches.
        /// </remarks>
        public static readonly WorkCost SoulseekSearch = WorkCosts.SoulseekSearch; // 5 units

        /// <summary>
        ///     Cost for a MeshDHT query (2 units - DHT lookup + metadata fetch).
        /// </summary>
        public static readonly WorkCost MeshDhtQuery = new(2, "MeshDHT query");

        /// <summary>
        ///     Cost for a Torrent metadata fetch (3 units - tracker query + parse).
        /// </summary>
        public static readonly WorkCost TorrentMetadataFetch = WorkCosts.TorrentMetadataFetch; // 3 units

        /// <summary>
        ///     Cost for an HTTP HEAD request (1 unit - lightweight validation).
        /// </summary>
        public static readonly WorkCost HttpHeadRequest = new(1, "HTTP HEAD request");

        /// <summary>
        ///     Cost for a full HTTP download (4 units - bandwidth + time).
        /// </summary>
        public static readonly WorkCost HttpDownload = new(4, "HTTP download");

        /// <summary>
        ///     Cost for a LAN share query (1 unit - local network, fast).
        /// </summary>
        public static readonly WorkCost LanShareQuery = new(1, "LAN share query");

        /// <summary>
        ///     Cost for validating a candidate (0-1 units depending on backend).
        /// </summary>
        /// <remarks>
        ///     Validation should be cheap (HEAD requests, DHT lookups).
        ///     Actual downloads happen in the Resolver, not during validation.
        /// </remarks>
        public static readonly WorkCost CandidateValidation = new(1, "Candidate validation");
    }
}
