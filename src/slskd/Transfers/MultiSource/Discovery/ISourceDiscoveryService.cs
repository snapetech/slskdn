// <copyright file="ISourceDiscoveryService.cs" company="slskd Team">
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

namespace slskd.Transfers.MultiSource.Discovery
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for continuous background discovery of file sources.
    /// </summary>
    public interface ISourceDiscoveryService
    {
        /// <summary>
        ///     Gets a value indicating whether discovery is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        ///     Gets the current search term.
        /// </summary>
        string CurrentSearchTerm { get; }

        /// <summary>
        ///     Gets statistics about the discovery process.
        /// </summary>
        DiscoveryStats GetStats();

        /// <summary>
        ///     Starts continuous discovery for the specified search term.
        /// </summary>
        /// <param name="searchTerm">The search term (e.g., artist name).</param>
        /// <param name="enableHashVerification">Whether to verify FLAC hashes (enabled by default for FLAC testing).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task StartDiscoveryAsync(string searchTerm, bool enableHashVerification = true, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Stops the discovery process.
        /// </summary>
        /// <returns>Task.</returns>
        Task StopDiscoveryAsync();

        /// <summary>
        ///     Gets discovered sources for a specific file size.
        /// </summary>
        /// <param name="size">The file size in bytes.</param>
        /// <param name="limit">Maximum number of results.</param>
        /// <returns>List of discovered sources.</returns>
        List<DiscoveredSource> GetSourcesBySize(long size, int limit = 100);

        /// <summary>
        ///     Gets discovered sources matching a filename pattern.
        /// </summary>
        /// <param name="filenamePattern">The filename pattern (SQL LIKE).</param>
        /// <param name="limit">Maximum number of results.</param>
        /// <returns>List of discovered sources.</returns>
        List<DiscoveredSource> GetSourcesByFilename(string filenamePattern, int limit = 100);

        /// <summary>
        ///     Gets all unique file sizes with their source counts.
        /// </summary>
        /// <param name="minSources">Minimum number of sources to include.</param>
        /// <returns>List of file size summaries.</returns>
        List<FileSizeSummary> GetFileSizeSummaries(int minSources = 2);

        /// <summary>
        ///     Gets the count of users flagged as not supporting partial downloads.
        /// </summary>
        /// <returns>Number of flagged users.</returns>
        int GetNoPartialSupportCount();

        /// <summary>
        ///     Resets all partial support flags.
        /// </summary>
        void ResetPartialSupportFlags();
    }

    /// <summary>
    ///     A discovered file source.
    /// </summary>
    public class DiscoveredSource
    {
        /// <summary>Gets or sets the username.</summary>
        public string Username { get; set; }

        /// <summary>Gets or sets the full file path on the user's share.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the file size in bytes.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the content hash (if verified).</summary>
        public string Hash { get; set; }

        /// <summary>Gets or sets the user's upload speed.</summary>
        public int UploadSpeed { get; set; }

        /// <summary>Gets or sets when this source was first seen.</summary>
        public long FirstSeenUnix { get; set; }

        /// <summary>Gets or sets when this source was last seen.</summary>
        public long LastSeenUnix { get; set; }
    }

    /// <summary>
    ///     Summary of sources for a specific file size.
    /// </summary>
    public class FileSizeSummary
    {
        /// <summary>Gets or sets the file size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the number of unique sources.</summary>
        public int SourceCount { get; set; }

        /// <summary>Gets or sets sample filename.</summary>
        public string SampleFilename { get; set; }
    }

    /// <summary>
    ///     Statistics about the discovery process.
    /// </summary>
    public class DiscoveryStats
    {
        /// <summary>Gets or sets total unique files discovered.</summary>
        public int TotalFiles { get; set; }

        /// <summary>Gets or sets total unique users discovered.</summary>
        public int TotalUsers { get; set; }

        /// <summary>Gets or sets total search cycles completed.</summary>
        public int SearchCycles { get; set; }

        /// <summary>Gets or sets files discovered in the current/last cycle.</summary>
        public int LastCycleNewFiles { get; set; }

        /// <summary>Gets or sets whether hash verification is enabled.</summary>
        public bool HashVerificationEnabled { get; set; }

        /// <summary>Gets or sets files with verified hashes.</summary>
        public int FilesWithHash { get; set; }
    }
}
