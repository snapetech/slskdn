// <copyright file="DisasterModeRecovery.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.DisasterMode;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

public interface IDisasterModeRecovery
{
    Task AttemptRecoveryAsync(CancellationToken ct = default);
    bool ShouldAttemptRecovery();
}

public class DisasterModeRecovery : IDisasterModeRecovery
{
    private readonly ILogger<DisasterModeRecovery> logger;

    public DisasterModeRecovery(
        ILogger<DisasterModeRecovery> logger,
        ISoulseekHealthMonitor healthMonitor,
        IDisasterModeCoordinator disasterMode,
        ISoulseekClient soulseek,
        IOptionsMonitor<slskd.Options> optionsMonitor)
    {
        this.logger = logger;
    }

    public Task AttemptRecoveryAsync(CancellationToken ct = default)
    {
        logger.LogInformation("[VSF-RECOVERY] Stub recovery invoked");
        return Task.CompletedTask;
    }

    public bool ShouldAttemptRecovery() => false;
}
