// <copyright file="ResolverOptions.cs" company="slskd Team">
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
    /// <summary>
    ///     Configuration for the Resolver.
    /// </summary>
    public sealed class ResolverOptions
    {
        /// <summary>
        ///     Maximum concurrent plan executions.
        /// </summary>
        public int MaxConcurrentExecutions { get; init; } = 5;

        /// <summary>
        ///     Default timeout per step (seconds).
        /// </summary>
        public int DefaultStepTimeoutSeconds { get; init; } = 60;

        /// <summary>
        ///     Maximum retries per step on transient failures.
        /// </summary>
        public int MaxRetriesPerStep { get; init; } = 2;

        /// <summary>
        ///     Enable automatic fallback to next candidate on failure.
        /// </summary>
        public bool EnableAutoFallback { get; init; } = true;

        /// <summary>
        ///     Directory where the resolver writes fetched files. If null or empty, uses the system temp path.
        /// </summary>
        public string? DownloadDirectory { get; init; }
    }
}
