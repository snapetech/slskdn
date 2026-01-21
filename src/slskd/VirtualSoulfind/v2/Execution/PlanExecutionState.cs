// <copyright file="PlanExecutionState.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Execution
{
    using System;

    /// <summary>
    ///     Status of a plan execution.
    /// </summary>
    public enum PlanExecutionStatus
    {
        Pending,
        Running,
        Succeeded,
        Failed,
        Cancelled,
    }

    /// <summary>
    ///     State tracking for plan execution (future: resolver phase).
    /// </summary>
    public sealed class PlanExecutionState
    {
        public string ExecutionId { get; init; }
        public string TrackId { get; init; }
        public PlanExecutionStatus Status { get; init; }
        public int CurrentStepIndex { get; init; }
        public int TotalSteps { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
