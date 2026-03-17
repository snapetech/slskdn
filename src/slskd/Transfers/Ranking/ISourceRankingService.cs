// <copyright file="ISourceRankingService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the GNU Affero General Public License v3.0.
// </copyright>

namespace slskd.Transfers.Ranking
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for ranking download sources using smart scoring.
    /// </summary>
    public interface ISourceRankingService
    {
        /// <summary>
        ///     Ranks a list of source candidates using smart scoring algorithm.
        /// </summary>
        /// <param name="candidates">The candidates to rank.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The ranked candidates, best first.</returns>
        Task<IEnumerable<RankedSource>> RankSourcesAsync(
            IEnumerable<SourceCandidate> candidates,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Records a successful download from a user.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        Task RecordSuccessAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Records a failed download from a user.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        Task RecordFailureAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the download history for a user.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The user's download history.</returns>
        Task<UserDownloadHistory> GetHistoryAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the download history for multiple users.
        /// </summary>
        /// <param name="usernames">The usernames.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A dictionary of username to download history.</returns>
        Task<IDictionary<string, UserDownloadHistory>> GetHistoriesAsync(
            IEnumerable<string> usernames,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     A candidate source for download.
    /// </summary>
    public class SourceCandidate
    {
        /// <summary>
        ///     Gets or sets the username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the filename.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the file size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the user has a free upload slot.
        /// </summary>
        public bool HasFreeUploadSlot { get; set; }

        /// <summary>
        ///     Gets or sets the queue length.
        /// </summary>
        public int QueueLength { get; set; }

        /// <summary>
        ///     Gets or sets the upload speed in bytes/sec.
        /// </summary>
        public int UploadSpeed { get; set; }

        /// <summary>
        ///     Gets or sets the size difference percentage from expected (for replacement scenarios).
        /// </summary>
        public double? SizeDiffPercent { get; set; }
    }

    /// <summary>
    ///     A ranked source with computed score.
    /// </summary>
    public class RankedSource : SourceCandidate
    {
        /// <summary>
        ///     Gets or sets the smart score (higher is better).
        /// </summary>
        public double SmartScore { get; set; }

        /// <summary>
        ///     Gets or sets the speed score component.
        /// </summary>
        public double SpeedScore { get; set; }

        /// <summary>
        ///     Gets or sets the queue score component.
        /// </summary>
        public double QueueScore { get; set; }

        /// <summary>
        ///     Gets or sets the free slot score component.
        /// </summary>
        public double FreeSlotScore { get; set; }

        /// <summary>
        ///     Gets or sets the history score component.
        /// </summary>
        public double HistoryScore { get; set; }

        /// <summary>
        ///     Gets or sets the size match score component (for replacement scenarios).
        /// </summary>
        public double SizeMatchScore { get; set; }
    }

    /// <summary>
    ///     Download history for a user.
    /// </summary>
    public class UserDownloadHistory
    {
        /// <summary>
        ///     Gets or sets the username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the number of successful downloads.
        /// </summary>
        public int Successes { get; set; }

        /// <summary>
        ///     Gets or sets the number of failed downloads.
        /// </summary>
        public int Failures { get; set; }

        /// <summary>
        ///     Gets the success rate (0-1).
        /// </summary>
        public double SuccessRate => Successes + Failures > 0
            ? (double)Successes / (Successes + Failures)
            : 0.5; // Neutral if no history
    }
}
