namespace slskd.VirtualSoulfind.DisasterMode;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public enum SoulseekHealth
{
    Healthy,
    Degraded,
    Unavailable
}

public class SoulseekHealthChangedEventArgs : EventArgs
{
    public SoulseekHealth OldHealth { get; set; }
    public SoulseekHealth NewHealth { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Reason { get; set; }
}

public interface ISoulseekClient { }

public interface ISoulseekHealthMonitor
{
    SoulseekHealth CurrentHealth { get; }
    event EventHandler<SoulseekHealthChangedEventArgs>? HealthChanged;
    Task StartMonitoringAsync(CancellationToken ct = default);
}

public class SoulseekHealthMonitor : ISoulseekHealthMonitor
{
    private readonly ILogger<SoulseekHealthMonitor> logger;

    public SoulseekHealthMonitor(ILogger<SoulseekHealthMonitor> logger, ISoulseekClient soulseek)
    {
        this.logger = logger;
    }

    public SoulseekHealth CurrentHealth { get; private set; } = SoulseekHealth.Healthy;

    public event EventHandler<SoulseekHealthChangedEventArgs>? HealthChanged;

    public Task StartMonitoringAsync(CancellationToken ct = default)
    {
        logger.LogInformation("[VSF-HEALTH] Stub monitor started");
        return Task.CompletedTask;
    }
}
