// <copyright file="TimerDisposer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common;

using System;
using System.Threading;

/// <summary>
/// Utilities for deterministic timer teardown.
/// </summary>
internal static class TimerDisposer
{
    /// <summary>
    /// Dispose a <see cref="Timer"/> and wait until active callbacks are complete.
    /// </summary>
    /// <param name="timer">Timer to dispose.</param>
    public static void DisposeWithWait(Timer timer)
    {
        using var disposed = new ManualResetEvent(false);

        try
        {
            timer.Dispose(disposed);
            disposed.WaitOne();
        }
        catch (ObjectDisposedException)
        {
            // Idempotent dispose already happened.
        }
    }

    /// <summary>
    /// Dispose a <see cref="System.Timers.Timer"/> and wait until callbacks are drained.
    /// </summary>
    /// <param name="timer">Timer to dispose.</param>
    public static void DisposeWithWait(System.Timers.Timer timer)
    {
        try
        {
            timer.Stop();
            timer.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Idempotent dispose already happened.
        }
    }
}
