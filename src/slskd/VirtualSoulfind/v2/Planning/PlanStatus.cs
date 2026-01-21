// <copyright file="PlanStatus.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.v2.Planning
{
    /// <summary>
    /// Status of a multi-source download plan.
    /// </summary>
    public enum PlanStatus
    {
        /// <summary>
        /// Plan is being created.
        /// </summary>
        Planning,

        /// <summary>
        /// Plan is ready and waiting to execute.
        /// </summary>
        Ready,

        /// <summary>
        /// Plan is currently executing.
        /// </summary>
        InProgress,

        /// <summary>
        /// Plan completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Plan failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Plan was cancelled.
        /// </summary>
        Cancelled,
    }
}



