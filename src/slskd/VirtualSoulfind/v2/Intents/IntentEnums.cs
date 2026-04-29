// <copyright file="IntentEnums.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Intents
{
    /// <summary>
    ///     Priority level for acquisition intents.
    /// </summary>
    public enum IntentPriority
    {
        /// <summary>Low priority (backfill, nice-to-have).</summary>
        Low = 0,

        /// <summary>Normal priority (default).</summary>
        Normal = 1,

        /// <summary>High priority (user-requested).</summary>
        High = 2,

        /// <summary>Urgent (user waiting, interactive).</summary>
        Urgent = 3,
    }

    /// <summary>
    ///     Mode for acquisition intents (how aggressive to be).
    /// </summary>
    public enum IntentMode
    {
        /// <summary>User actively wants this (try hard).</summary>
        Wanted,

        /// <summary>Nice to have (try if convenient).</summary>
        NiceToHave,

        /// <summary>Backfill gaps (low priority, when idle).</summary>
        Backfill,
    }

    /// <summary>
    ///     Status of an acquisition intent.
    /// </summary>
    public enum IntentStatus
    {
        /// <summary>Not yet processed.</summary>
        Pending,

        /// <summary>Plan created, ready to execute.</summary>
        Planned,

        /// <summary>Currently being acquired.</summary>
        InProgress,

        /// <summary>Successfully completed.</summary>
        Completed,

        /// <summary>Failed (errors, no sources, etc.).</summary>
        Failed,

        /// <summary>On hold (paused by user or system).</summary>
        OnHold,

        /// <summary>Cancelled by user.</summary>
        Cancelled,
    }
}
