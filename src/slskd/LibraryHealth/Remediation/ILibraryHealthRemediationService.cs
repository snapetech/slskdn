// <copyright file="ILibraryHealthRemediationService.cs" company="slskdn Team">
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

namespace slskd.LibraryHealth.Remediation
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for creating remediation jobs from library health issues.
    /// </summary>
    public interface ILibraryHealthRemediationService
    {
        /// <summary>
        ///     Creates a remediation job to fix the specified issues.
        /// </summary>
        /// <param name="issueIds">List of issue IDs to fix.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created job ID.</returns>
        Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Links a download job to the issues it's attempting to fix.
        /// </summary>
        /// <param name="jobId">The download job ID.</param>
        /// <param name="issueIds">The issue IDs being fixed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LinkJobToIssuesAsync(string jobId, List<string> issueIds, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Checks the status of a remediation job and updates linked issues if complete.
        /// </summary>
        /// <param name="jobId">The job ID to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CheckJobStatusAndResolveIssuesAsync(string jobId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Strategy for fixing issues.
    /// </summary>
    public enum RemediationStrategy
    {
        /// <summary>
        ///     Re-download individual tracks (transcodes, corrupted files).
        /// </summary>
        RedownloadTracks,

        /// <summary>
        ///     Download missing tracks to complete an album.
        /// </summary>
        CompleteAlbum,

        /// <summary>
        ///     Replace files with canonical variants.
        /// </summary>
        ReplaceWithCanonical,
    }
}
















