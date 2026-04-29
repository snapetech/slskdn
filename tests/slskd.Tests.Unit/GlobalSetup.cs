// <copyright file="GlobalSetup.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System.Runtime.CompilerServices;
using System.Threading;

/// <summary>
/// Pre-warms the thread pool when the test assembly loads to prevent saturation during parallel testing.
/// With 3000+ tests and numerous background tasks, the default minimum thread count is too low —
/// async continuations queue up for seconds waiting for threads to be created (500ms per new thread).
/// Setting a higher minimum ensures threads are available immediately.
/// </summary>
internal static class GlobalSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ThreadPool.SetMinThreads(64, 64);
    }
}
