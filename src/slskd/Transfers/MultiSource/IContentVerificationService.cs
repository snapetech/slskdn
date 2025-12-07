// <copyright file="IContentVerificationService.cs" company="slskd Team">
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

namespace slskd.Transfers.MultiSource
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for verifying file content identity across multiple sources.
    /// </summary>
    public interface IContentVerificationService
    {
        /// <summary>
        ///     Verifies multiple sources for a file and groups them by content hash.
        /// </summary>
        /// <param name="request">The verification request containing file details and candidate sources.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The verification result with sources grouped by content hash.</returns>
        Task<ContentVerificationResult> VerifySourcesAsync(
            ContentVerificationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the content hash for a single source.
        /// </summary>
        /// <param name="username">The username of the source.</param>
        /// <param name="filename">The remote filename.</param>
        /// <param name="fileSize">The file size in bytes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The content hash, or null if verification failed.</returns>
        Task<string> GetContentHashAsync(
            string username,
            string filename,
            long fileSize,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Request for content verification across multiple sources.
    /// </summary>
    public class ContentVerificationRequest
    {
        /// <summary>
        ///     Gets or sets the filename to verify (used for logging only).
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the expected file size in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        ///     Gets or sets the list of candidate source usernames (deprecated - use CandidateSources).
        /// </summary>
        public List<string> CandidateUsernames { get; set; } = new();

        /// <summary>
        ///     Gets or sets the list of candidate sources with their filenames.
        ///     Each entry maps username to their specific file path.
        /// </summary>
        public Dictionary<string, string> CandidateSources { get; set; } = new();

        /// <summary>
        ///     Gets or sets the timeout for each verification attempt in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;
    }

    /// <summary>
    ///     Result of content verification across multiple sources.
    /// </summary>
    public class ContentVerificationResult
    {
        /// <summary>
        ///     Gets or sets the filename that was verified.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the file size in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        ///     Gets or sets the groups of verified sources, keyed by content hash.
        /// </summary>
        public Dictionary<string, List<VerifiedSource>> SourcesByHash { get; set; } = new();

        /// <summary>
        ///     Gets or sets the list of sources that failed verification.
        /// </summary>
        public List<FailedSource> FailedSources { get; set; } = new();

        /// <summary>
        ///     Gets or sets the expected hash from the hash database (if found).
        /// </summary>
        public string ExpectedHash { get; set; }

        /// <summary>
        ///     Gets a value indicating whether the hash was found in the database.
        /// </summary>
        public bool WasCached => !string.IsNullOrEmpty(ExpectedHash);

        /// <summary>
        ///     Gets the hash with the most verified sources (best candidate for multi-source).
        /// </summary>
        public string BestHash
        {
            get
            {
                string bestHash = null;
                int maxCount = 0;

                foreach (var (hash, sources) in SourcesByHash)
                {
                    if (sources.Count > maxCount)
                    {
                        maxCount = sources.Count;
                        bestHash = hash;
                    }
                }

                return bestHash;
            }
        }

        /// <summary>
        ///     Gets the sources with the best (most common) hash.
        /// </summary>
        public List<VerifiedSource> BestSources => BestHash != null && SourcesByHash.TryGetValue(BestHash, out var sources) ? sources : new List<VerifiedSource>();
    }

    /// <summary>
    ///     A verified source with content hash.
    /// </summary>
    public class VerifiedSource
    {
        /// <summary>
        ///     Gets or sets the username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the full file path on the user's share.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        ///     Gets or sets the content hash.
        /// </summary>
        public string ContentHash { get; set; }

        /// <summary>
        ///     Gets or sets the verification method used.
        /// </summary>
        public VerificationMethod Method { get; set; }

        /// <summary>
        ///     Gets or sets the time taken to verify in milliseconds.
        /// </summary>
        public long VerificationTimeMs { get; set; }
    }

    /// <summary>
    ///     A source that failed verification.
    /// </summary>
    public class FailedSource
    {
        /// <summary>
        ///     Gets or sets the username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the reason for failure.
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    ///     Method used for content verification.
    /// </summary>
    public enum VerificationMethod
    {
        /// <summary>
        ///     No verification - size match only.
        /// </summary>
        None,

        /// <summary>
        ///     FLAC STREAMINFO MD5 (42 bytes, most reliable).
        /// </summary>
        FlacStreamInfoMd5,

        /// <summary>
        ///     SHA256 of first 32KB of file content.
        /// </summary>
        ContentSha256,
    }
}
