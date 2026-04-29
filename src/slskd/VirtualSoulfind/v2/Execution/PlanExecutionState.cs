// <copyright file="PlanExecutionState.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
        public string ExecutionId { get; init; } = string.Empty;
        public string TrackId { get; init; } = string.Empty;
        public PlanExecutionStatus Status { get; init; }
        public int CurrentStepIndex { get; init; }
        public int TotalSteps { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public string? ErrorMessage { get; init; }

        /// <summary>
        ///     Path to the fetched file when Status is Succeeded and the resolver performed a fetch.
        ///     Null when the backend does not perform a fetch (e.g. LocalLibrary) or when no fetch was done.
        /// </summary>
        public string? FetchedFilePath { get; init; }
    }
}
