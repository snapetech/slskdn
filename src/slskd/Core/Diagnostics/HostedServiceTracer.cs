// <copyright file="HostedServiceTracer.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace slskd.Core.Diagnostics;

internal sealed class HostedServiceTracer : IHostedService
{
    private readonly IReadOnlyList<IHostedService> _inner;
    private readonly string _name = nameof(HostedServiceTracer);

    public HostedServiceTracer(IEnumerable<IHostedService> inner)
    {
        _inner = inner?.ToList() ?? new List<IHostedService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var swAll = Stopwatch.StartNew();
        Console.Error.WriteLine($"[{_name}] StartAsync begin; services={_inner.Count}");

        foreach (var svc in _inner)
        {
            var svcName = svc.GetType().FullName ?? svc.GetType().Name;
            var sw = Stopwatch.StartNew();
            Console.Error.WriteLine($"[{_name}] -> START {svcName} t={DateTimeOffset.UtcNow:O}");

            var startTask = svc.StartAsync(cancellationToken);
            var completed = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            if (completed != startTask)
            {
                Console.Error.WriteLine($"[{_name}] !! TIMEOUT waiting StartAsync {svcName} after {sw.Elapsed}");
            }

            await startTask;
            Console.Error.WriteLine($"[{_name}] <- DONE  {svcName} dt={sw.Elapsed}");
        }

        Console.Error.WriteLine($"[{_name}] StartAsync end dt={swAll.Elapsed}");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        for (var i = _inner.Count - 1; i >= 0; i--)
        {
            var svc = _inner[i];
            var svcName = svc.GetType().FullName ?? svc.GetType().Name;
            Console.Error.WriteLine($"[{_name}] -> STOP {svcName} t={DateTimeOffset.UtcNow:O}");
            await svc.StopAsync(cancellationToken);
            Console.Error.WriteLine($"[{_name}] <- STOP {svcName}");
        }
    }
}
