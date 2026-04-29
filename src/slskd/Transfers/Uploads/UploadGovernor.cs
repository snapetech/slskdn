// <copyright file="UploadGovernor.cs" company="slskd Team">
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

// <copyright file="UploadGovernor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Options;

namespace slskd.Transfers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Users;

    /// <summary>
    ///     Governs upload transfer speed.
    /// </summary>
    public interface IUploadGovernor : IDisposable
    {
        /// <summary>
        ///     Asynchronously obtains a grant of <paramref name="requestedBytes"/> for the requesting <paramref name="username"/>.
        /// </summary>
        /// <remarks>
        ///     This operation completes when any number of bytes can be granted. The amount returned may be smaller than the
        ///     requested amount.
        /// </remarks>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="requestedBytes">The number of requested bytes.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context, including the number of bytes granted.</returns>
        Task<int> GetBytesAsync(string username, int requestedBytes, CancellationToken cancellationToken);

        /// <summary>
        ///     Returns wasted bytes for redistribution.
        /// </summary>
        /// <param name="username">The username of the user that generated the waste.</param>
        /// <param name="attemptedBytes">The number of bytes that were attempted to be transferred.</param>
        /// <param name="grantedBytes">The number of bytes granted by all governors in the system.</param>
        /// <param name="actualBytes">The actual number of bytes transferred.</param>
        public void ReturnBytes(string username, int attemptedBytes, int grantedBytes, int actualBytes);
    }

    /// <summary>
    ///     Governs upload transfer speed.
    /// </summary>
    public class UploadGovernor : IUploadGovernor, IDisposable
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UploadGovernor"/> class.
        /// </summary>
        /// <param name="userService">The UserService instance to use.</param>
        /// <param name="optionsMonitor">The OptionsMonitor instance to use.</param>
        /// <param name="scheduledRateLimitService">The scheduled rate limit service to use.</param>
        /// <param name="bucketDisposalDelayMs">Milliseconds to delay disposal of replaced buckets. Defaults to 5000. Pass 0 in unit tests.</param>
        public UploadGovernor(
            IUserService userService,
            IOptionsMonitor<Options> optionsMonitor,
            IScheduledRateLimitService? scheduledRateLimitService = null,
            int bucketDisposalDelayMs = 5000)
        {
            Users = userService;
            ScheduledRateLimitService = scheduledRateLimitService;
            BucketDisposalDelayMs = bucketDisposalDelayMs;

            OptionsMonitor = optionsMonitor;
            OptionsMonitorRegistration = OptionsMonitor.OnChange(Configure);

            Configure(OptionsMonitor.CurrentValue);
        }

        private bool Disposed { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IDisposable? OptionsMonitorRegistration { get; set; }
        private string LastOptionsHash { get; set; } = string.Empty;
        private int LastGlobalSpeedLimit { get; set; }
        private int BucketDisposalDelayMs { get; }
        private Dictionary<string, ITokenBucket> TokenBuckets { get; set; } = new Dictionary<string, ITokenBucket>();
        private IUserService Users { get; }
        private IScheduledRateLimitService? ScheduledRateLimitService { get; }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Asynchronously obtains a grant of <paramref name="requestedBytes"/> for the requesting <paramref name="username"/>.
        /// </summary>
        /// <remarks>
        ///     This operation completes when any number of bytes can be granted. The amount returned may be smaller than the
        ///     requested amount.
        /// </remarks>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="requestedBytes">The number of requested bytes.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context, including the number of bytes granted.</returns>
        public Task<int> GetBytesAsync(string username, int requestedBytes, CancellationToken cancellationToken)
        {
            var group = Users.GetGroup(username);
            var bucket = TokenBuckets.GetValueOrDefault(group ?? string.Empty, TokenBuckets[Application.DefaultGroup]);

            return bucket.GetAsync(requestedBytes, cancellationToken);
        }

        /// <summary>
        ///     Returns wasted bytes for redistribution.
        /// </summary>
        /// <param name="username">The username of the user that generated the waste.</param>
        /// <param name="attemptedBytes">The number of bytes that were attempted to be transferred.</param>
        /// <param name="grantedBytes">The number of bytes granted by all governors in the system.</param>
        /// <param name="actualBytes">The actual number of bytes transferred.</param>
        public void ReturnBytes(string username, int attemptedBytes, int grantedBytes, int actualBytes)
        {
            var waste = Math.Max(0, grantedBytes - actualBytes);

            if (waste == 0)
            {
                return;
            }

            var group = Users.GetGroup(username);
            var bucket = TokenBuckets.GetValueOrDefault(group ?? string.Empty, TokenBuckets[Application.DefaultGroup]);

            // we don't have enough information to tell whether grantedBytes was reduced by the global limiter within
            // Soulseek.NET, so we just return the bytes that we know for sure that were wasted, which is grantedBytes - actualBytes.
            // example: we grant 1000 bytes. Soulseek.NET grants only 500. 250 bytes are written. ideally we would return 750
            // bytes, but instead we return 250. this discrepancy doesn't really matter because Soulseek.NET is the constraint in
            // this scenario and the additional tokens we would return would never be used.
            bucket.Return(waste);
        }

        private void Configure(Options options)
        {
            static TokenBucket CreateBucket(int speedInKiB)
            {
                var speedInBytes = speedInKiB * 1024L;
                var intervalInMs = 100;
                var bucketRefreshesPerSecond = 1000 / intervalInMs;
                var capacity = speedInBytes / bucketRefreshesPerSecond;

                return new(capacity, interval: intervalInMs);
            }

            var optionsHash = Compute.Sha1Hash(options.Groups.ToJson());

            // Get the effective global upload speed limit (considering scheduled limits)
            var effectiveGlobalUploadSpeedLimit = ScheduledRateLimitService?.GetEffectiveUploadSpeedLimit() ?? options.Global.Upload.SpeedLimit;

            if (optionsHash == LastOptionsHash && effectiveGlobalUploadSpeedLimit == LastGlobalSpeedLimit)
            {
                return;
            }

            // build a new dictionary of token buckets based on the current groups, then
            // swap it in for the existing dictionary.  there's risk of inaccuracy here if
            // groups are deleted or users are moved around, as bytes may be taken from or returned
            // to the wrong bucket.  this is acceptable.  reconfiguring buckets replenishes them,
            // also, so transfers in progress will briefly exceed the intended speeds.
            var tokenBuckets = new Dictionary<string, ITokenBucket>()
            {
                { Application.PrivilegedGroup, CreateBucket(speedInKiB: effectiveGlobalUploadSpeedLimit) },
                { Application.DefaultGroup, CreateBucket(speedInKiB: options.Groups.Default.Upload.SpeedLimit) },
                { Application.LeecherGroup, CreateBucket(speedInKiB: options.Groups.Leechers.Upload.SpeedLimit) },
            };

            foreach (var group in options.Groups.UserDefined)
            {
                tokenBuckets.Add(group.Key, CreateBucket(group.Value.Upload.SpeedLimit));
            }

            var previousBuckets = TokenBuckets;
            TokenBuckets = tokenBuckets;

            // Delay disposal so any in-flight GetBytesAsync calls that already captured a reference
            // to the old bucket can complete safely before SyncRoot is disposed.  The timer interval
            // is 100 ms, so 5 s is many multiples of the worst-case hold time.
            // When delay is 0 (test mode), dispose synchronously to avoid thread-pool scheduling
            // delays that make timing-sensitive tests unreliable.
            if (BucketDisposalDelayMs == 0)
            {
                DisposeBuckets(previousBuckets);
            }
            else
            {
                _ = Task.Delay(BucketDisposalDelayMs).ContinueWith(_ => DisposeBuckets(previousBuckets), TaskScheduler.Default);
            }

            LastGlobalSpeedLimit = effectiveGlobalUploadSpeedLimit;
            LastOptionsHash = optionsHash;
        }

        private static void DisposeBuckets(Dictionary<string, ITokenBucket> buckets)
        {
            foreach (var bucket in buckets.Values)
            {
                if (bucket is IDisposable disposableBucket)
                {
                    disposableBucket.Dispose();
                }
            }
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        /// <param name="disposing">A value indicating whether disposal is in progress.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    OptionsMonitorRegistration?.Dispose();
                    OptionsMonitorRegistration = null;
                    DisposeBuckets(TokenBuckets);
                    TokenBuckets = new Dictionary<string, ITokenBucket>();
                }

                Disposed = true;
            }
        }
    }
}
