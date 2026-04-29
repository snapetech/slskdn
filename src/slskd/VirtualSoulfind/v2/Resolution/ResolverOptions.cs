// <copyright file="ResolverOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
