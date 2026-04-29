// <copyright file="IIntentQueueProcessor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Processing
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Processes pending intents from the intent queue.
    /// </summary>
    /// <remarks>
    ///     The Intent Queue Processor is the automation engine that:
    ///     1. Polls the intent queue for pending items
    ///     2. Creates acquisition plans via IPlanner
    ///     3. Executes plans via IResolver
    ///     4. Updates intent status based on results
    ///
    ///     This is what makes VirtualSoulfind v2 autonomous - users add intents,
    ///     and the processor takes care of the rest.
    /// </remarks>
    public interface IIntentQueueProcessor
    {
        /// <summary>
        ///     Processes a batch of pending intents.
        /// </summary>
        /// <param name="maxIntents">Maximum number of intents to process in this batch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of intents processed.</returns>
        Task<int> ProcessBatchAsync(int maxIntents = 10, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Processes a specific intent by ID.
        /// </summary>
        /// <param name="desiredTrackId">The desired track ID to process.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if processed successfully, false otherwise.</returns>
        Task<bool> ProcessIntentAsync(string desiredTrackId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the current processing statistics.
        /// </summary>
        Task<IntentProcessorStats> GetStatsAsync();
    }

    /// <summary>
    ///     Statistics for the intent queue processor.
    /// </summary>
    public sealed class IntentProcessorStats
    {
        /// <summary>
        ///     Gets or initializes the total number of intents processed.
        /// </summary>
        public int TotalProcessed { get; init; }

        /// <summary>
        ///     Gets or initializes the number of successful completions.
        /// </summary>
        public int SuccessCount { get; init; }

        /// <summary>
        ///     Gets or initializes the number of failures.
        /// </summary>
        public int FailureCount { get; init; }

        /// <summary>
        ///     Gets or initializes the number of intents currently in progress.
        /// </summary>
        public int InProgressCount { get; init; }

        /// <summary>
        ///     Gets or initializes the number of pending intents.
        /// </summary>
        public int PendingCount { get; init; }
    }
}
