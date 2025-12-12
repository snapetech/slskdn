// <copyright file="IntentPriority.cs" company="slskd Team">
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
