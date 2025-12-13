namespace slskd.VirtualSoulfind.DisasterMode;

/// <summary>
/// Disaster mode telemetry.
/// </summary>
public class DisasterModeTelemetry
{
    public DateTimeOffset? LastActivation { get; set; }
    public DateTimeOffset? LastDeactivation { get; set; }
    public TimeSpan TotalDisasterModeTime { get; set; }
    public int ActivationCount { get; set; }
    public int MeshSearchCount { get; set; }
    public int MeshTransferCount { get; set; }
    public List<DisasterModeEvent> RecentEvents { get; set; } = new();
}

/// <summary>
/// Disaster mode event.
/// </summary>
public class DisasterModeEvent
{
    public DateTimeOffset Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public SoulseekHealth? HealthBefore { get; set; }
    public SoulseekHealth? HealthAfter { get; set; }
}

/// <summary>
/// Disaster mode telemetry service.
/// </summary>
public interface IDisasterModeTelemetry
{
    /// <summary>
    /// Record disaster mode activation.
    /// </summary>
    void RecordActivation(string reason);
    
    /// <summary>
    /// Record disaster mode deactivation.
    /// </summary>
    void RecordDeactivation();
    
    /// <summary>
    /// Record mesh search.
    /// </summary>
    void RecordMeshSearch();
    
    /// <summary>
    /// Record mesh transfer.
    /// </summary>
    void RecordMeshTransfer();
    
    /// <summary>
    /// Get telemetry data.
    /// </summary>
    DisasterModeTelemetry GetTelemetry();
}

/// <summary>
/// Disaster mode telemetry implementation.
/// </summary>
public class DisasterModeTelemetryService : IDisasterModeTelemetry
{
    private readonly ILogger<DisasterModeTelemetryService> logger;
    private readonly DisasterModeTelemetry telemetry = new();
    private readonly object lockObj = new();

    public DisasterModeTelemetryService(ILogger<DisasterModeTelemetryService> logger)
    {
        this.logger = logger;
    }

    public void RecordActivation(string reason)
    {
        lock (lockObj)
        {
            telemetry.LastActivation = DateTimeOffset.UtcNow;
            telemetry.ActivationCount++;

            telemetry.RecentEvents.Add(new DisasterModeEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = "Activation",
                Reason = reason
            });

            TrimEvents();
        }

        logger.LogInformation("[VSF-TELEMETRY] Disaster mode activation recorded: {Reason}", reason);
    }

    public void RecordDeactivation()
    {
        lock (lockObj)
        {
            var now = DateTimeOffset.UtcNow;
            telemetry.LastDeactivation = now;

            if (telemetry.LastActivation.HasValue)
            {
                var duration = now - telemetry.LastActivation.Value;
                telemetry.TotalDisasterModeTime += duration;
            }

            telemetry.RecentEvents.Add(new DisasterModeEvent
            {
                Timestamp = now,
                EventType = "Deactivation"
            });

            TrimEvents();
        }

        logger.LogInformation("[VSF-TELEMETRY] Disaster mode deactivation recorded");
    }

    public void RecordMeshSearch()
    {
        lock (lockObj)
        {
            telemetry.MeshSearchCount++;
        }
    }

    public void RecordMeshTransfer()
    {
        lock (lockObj)
        {
            telemetry.MeshTransferCount++;
        }
    }

    public DisasterModeTelemetry GetTelemetry()
    {
        lock (lockObj)
        {
            return new DisasterModeTelemetry
            {
                LastActivation = telemetry.LastActivation,
                LastDeactivation = telemetry.LastDeactivation,
                TotalDisasterModeTime = telemetry.TotalDisasterModeTime,
                ActivationCount = telemetry.ActivationCount,
                MeshSearchCount = telemetry.MeshSearchCount,
                MeshTransferCount = telemetry.MeshTransferCount,
                RecentEvents = telemetry.RecentEvents.ToList()
            };
        }
    }

    private void TrimEvents()
    {
        // Keep only last 100 events
        while (telemetry.RecentEvents.Count > 100)
        {
            telemetry.RecentEvents.RemoveAt(0);
        }
    }
}
















