// <copyright file="IResolver.cs" company="slskd Team">
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
