namespace slskd.VirtualSoulfind.Capture;

/// <summary>
/// Observed Soulseek search result (pre-normalization).
/// </summary>
public class SearchObservation
{
    public string ObservationId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    
    // Search context
    public string Query { get; set; } = string.Empty;
    public string SoulseekUsername { get; set; } = string.Empty;
    
    // File details
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? BitRate { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Extension { get; set; }
    
    // Metadata extraction (best-effort from path)
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Title { get; set; }
}

/// <summary>
/// Observed completed transfer.
/// </summary>
public class TransferObservation
{
    public string TransferId { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
    
    public string SoulseekUsername { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? LocalPath { get; set; }  // Where we saved it
    
    public long SizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public double ThroughputBytesPerSec { get; set; }
    public bool Success { get; set; }
}















