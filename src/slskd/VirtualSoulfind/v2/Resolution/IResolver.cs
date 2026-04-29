// <copyright file="IResolver.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Resolution
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Execution;
    using slskd.VirtualSoulfind.v2.Planning;

    /// <summary>
    ///     Executes acquisition plans to obtain content.
    /// </summary>
    /// <remarks>
    ///     The Resolver is the execution engine that:
    ///     - Takes a plan from the Planner
    ///     - Executes plan steps in order
    ///     - Handles fallback between backends
    ///     - Returns acquired content or failure reason
    /// </remarks>
    public interface IResolver
    {
        /// <summary>
        ///     Execute an acquisition plan.
        /// </summary>
        /// <param name="plan">The plan to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Execution state with result or error.</returns>
        Task<PlanExecutionState> ExecutePlanAsync(
            TrackAcquisitionPlan plan,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get the current status of a plan execution.
        /// </summary>
        /// <param name="executionId">The execution ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Current execution state, or null if not found.</returns>
        Task<PlanExecutionState?> GetExecutionStatusAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}
