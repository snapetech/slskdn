// <copyright file="AcceleratedDownloadService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.Downloads;

using Microsoft.Extensions.Options;

/// <summary>
///     Runtime state for accelerated downloads.
/// </summary>
public interface IAcceleratedDownloadService
{
    /// <summary>
    ///     Gets a value indicating whether accelerated downloads are enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Gets the current accelerated download state.
    /// </summary>
    AcceleratedDownloadState GetState();

    /// <summary>
    ///     Enables or disables accelerated downloads.
    /// </summary>
    AcceleratedDownloadState SetEnabled(bool enabled);
}

/// <summary>
///     Runtime accelerated download state.
/// </summary>
public sealed class AcceleratedDownloadState
{
    /// <summary>
    ///     Gets or sets a value indicating whether accelerated downloads are enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    ///     Gets or sets when the state was last changed.
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    ///     Gets or sets the active network-health policy summary.
    /// </summary>
    public string Policy { get; init; } = string.Empty;
}

/// <summary>
///     In-memory runtime switch for accelerated downloads.
/// </summary>
public sealed class AcceleratedDownloadService : IAcceleratedDownloadService
{
    private const string ConservativePolicy =
        "Normal downloads remain single-source. Underperforming downloads may use verified alternate sources; raw Soulseek peers use sequential failover, while true multipart chunking is reserved for trusted mesh-overlay peers.";

    private readonly object syncRoot = new();
    private bool enabled;
    private DateTime updatedAt;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AcceleratedDownloadService"/> class.
    /// </summary>
    public AcceleratedDownloadService(IOptionsMonitor<slskd.Options> options)
    {
        enabled = options.CurrentValue.RescueMode.Enabled;
        updatedAt = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            lock (syncRoot)
            {
                return enabled;
            }
        }
    }

    /// <inheritdoc />
    public AcceleratedDownloadState GetState()
    {
        lock (syncRoot)
        {
            return CreateState();
        }
    }

    /// <inheritdoc />
    public AcceleratedDownloadState SetEnabled(bool enabled)
    {
        lock (syncRoot)
        {
            this.enabled = enabled;
            updatedAt = DateTime.UtcNow;
            return CreateState();
        }
    }

    private AcceleratedDownloadState CreateState()
        => new()
        {
            Enabled = enabled,
            UpdatedAt = updatedAt,
            Policy = ConservativePolicy,
        };
}
