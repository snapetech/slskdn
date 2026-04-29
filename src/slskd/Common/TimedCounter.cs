// <copyright file="TimedCounter.cs" company="slskd Team">
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

// <copyright file="TimedCounter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System;
using System.Threading;
using System.Timers;
using Serilog;

namespace slskd
{
    /// <summary>
    ///     A counter that counts up over a set time period.
    /// </summary>
    public class TimedCounter : IDisposable
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TimedCounter"/> class.
        /// </summary>
        /// <param name="interval">The interval at which the OnElapsed delegate is called and the count resets.</param>
        /// <param name="onElapsed">The delagate to invoke with the current count when the timer elapses.</param>
        public TimedCounter(TimeSpan interval, Action<long> onElapsed)
        {
            OnElapsed = onElapsed;

            Timer = new System.Timers.Timer(interval.TotalMilliseconds);
            Timer.Elapsed += Elapsed;
            Timer.Start();
        }

        /// <summary>
        ///     Gets the current count.
        /// </summary>
        public long Count => Interlocked.Read(ref count);

        private System.Timers.Timer Timer { get; }
        private Action<long> OnElapsed { get; }
        private bool Disposed { get; set; }
        private long count;

        /// <summary>
        ///     Counts up by the specified <paramref name="count"/>.
        /// </summary>
        /// <param name="count">The number to add to the count.</param>
        public void CountUp(long count = 1)
        {
            Interlocked.Add(ref this.count, count);
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    Common.TimerDisposer.DisposeWithWait(Timer);
                }

                Disposed = true;
            }
        }

        private void Elapsed(object? sender, ElapsedEventArgs args)
        {
            var count = Interlocked.Exchange(ref this.count, 0);

            try
            {
                OnElapsed?.Invoke(count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TimedCounter elapsed callback failed");
            }
        }
    }
}
