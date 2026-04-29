// <copyright file="Retry.cs" company="slskd Team">
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

// <copyright file="Retry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Retry logic.
    /// </summary>
    public static class Retry
    {
        /// <summary>
        ///     Executes logic with the specified retry parameters.
        /// </summary>
        /// <param name="task">The logic to execute.</param>
        /// <param name="isRetryable">A function returning a value indicating whether the last Exception is retryable.</param>
        /// <param name="onRetry">An action to execute before each retry delay.</param>
        /// <param name="onFailure">An action to execute on failure.</param>
        /// <param name="maxAttempts">The maximum number of retry attempts.</param>
        /// <param name="baseDelayInMilliseconds">The initial delay in milliseconds.</param>
        /// <param name="maxDelayInMilliseconds">The maximum delay in milliseconds.</param>
        /// <param name="exceptionHistoryLimit">The maximum number of exceptions to keep for the final aggregate exception.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The execution context.</returns>
        public static async Task Do(
            Func<Task> task,
            Func<int, Exception, bool>? isRetryable = null,
            Action<int, int>? onRetry = null,
            Action<int, Exception>? onFailure = null,
            int maxAttempts = 3,
            int baseDelayInMilliseconds = 1000,
            int maxDelayInMilliseconds = int.MaxValue,
            int exceptionHistoryLimit = 5,
            CancellationToken cancellationToken = default)
        {
            await Do<object>(async () =>
            {
                await task();
                return null!;
            }, isRetryable, onRetry, onFailure, maxAttempts, baseDelayInMilliseconds, maxDelayInMilliseconds, exceptionHistoryLimit, cancellationToken);
        }

        /// <summary>
        ///     Executes logic with the specified retry parameters.
        /// </summary>
        /// <param name="task">The logic to execute.</param>
        /// <param name="isRetryable">A function returning a value indicating whether the last Exception is retryable.</param>
        /// <param name="onRetry">An action to execute before each retry delay.</param>
        /// <param name="onFailure">An action to execute on failure.</param>
        /// <param name="maxAttempts">The maximum number of retry attempts.</param>
        /// <param name="baseDelayInMilliseconds">The initial delay in milliseconds.</param>
        /// <param name="maxDelayInMilliseconds">The maximum delay in milliseconds.</param>
        /// <param name="exceptionHistoryLimit">The maximum number of exceptions to keep for the final aggregate exception.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <typeparam name="T">The Type of the logic return value.</typeparam>
        /// <returns>The execution context.</returns>
        public static async Task<T> Do<T>(
            Func<Task<T>> task,
            Func<int, Exception, bool>? isRetryable = null,
            Action<int, int>? onRetry = null,
            Action<int, Exception>? onFailure = null,
            int maxAttempts = 3,
            int baseDelayInMilliseconds = 1000,
            int maxDelayInMilliseconds = int.MaxValue,
            int exceptionHistoryLimit = 5,
            CancellationToken cancellationToken = default)
        {
            maxAttempts = Math.Max(1, maxAttempts);
            baseDelayInMilliseconds = Math.Max(0, baseDelayInMilliseconds);
            exceptionHistoryLimit = Math.Max(1, exceptionHistoryLimit);

            var attempt = 1;
            var history = new Queue<Exception>();
            var canRetry = isRetryable ?? ((_, _) => true);

            while (attempt <= maxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (attempt > 1)
                    {
                        var pause = GetRetryDelay(attempt - 1, baseDelayInMilliseconds, maxDelayInMilliseconds);
                        onRetry?.Invoke(attempt, pause);

                        if (pause > 0)
                        {
                            await Task.Delay(pause, cancellationToken);
                        }
                    }

                    return await task();
                }
                catch (Exception ex)
                {
                    Remember(history, ex, exceptionHistoryLimit);

                    try
                    {
                        onFailure?.Invoke(attempt, ex);

                        if (!canRetry(attempt, ex))
                        {
                            break;
                        }
                    }
                    catch (Exception retryCallbackException)
                    {
                        throw new RetryException(
                            "Retry callback failed while handling an operation failure.",
                            new AggregateException(ex, retryCallbackException));
                    }
                }

                attempt++;
            }

            throw new AggregateException(history);
        }

        private static int GetRetryDelay(int completedFailures, int baseDelayInMilliseconds, int maxDelayInMilliseconds)
        {
            var (delay, jitter) = Compute.ExponentialBackoffDelay(completedFailures, baseDelayInMilliseconds, maxDelayInMilliseconds);
            return delay + jitter;
        }

        private static void Remember(Queue<Exception> history, Exception exception, int limit)
        {
            history.Enqueue(exception);

            while (history.Count > limit)
            {
                history.Dequeue();
            }
        }
    }
}
