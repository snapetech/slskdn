// <copyright file="SoulfindBridgeService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Bridge;

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using slskd;
using OptionsModel = slskd.Options;

/// <summary>
/// Interface for Soulfind bridge service lifecycle management.
/// </summary>
public interface ISoulfindBridgeService
{
    /// <summary>
    /// Is the bridge service running?
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Start the Soulfind bridge.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Stop the Soulfind bridge.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get bridge health status.
    /// </summary>
    Task<BridgeHealthStatus> GetHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Bridge health status.
/// </summary>
public class BridgeHealthStatus
{
    public bool IsHealthy { get; set; }
    public string? Version { get; set; }
    public int ActiveConnections { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// Soulfind bridge service - allows legacy clients to use VSF mesh.
/// </summary>
public class SoulfindBridgeService : ISoulfindBridgeService
{
    private readonly ILogger<SoulfindBridgeService> logger;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;
    private bool isRunning;
    private DateTimeOffset? startedAt;
    private Process? soulfindProcess;

    public SoulfindBridgeService(
        ILogger<SoulfindBridgeService> logger,
        IOptionsMonitor<OptionsModel> optionsMonitor)
    {
        this.logger = logger;
        this.optionsMonitor = optionsMonitor;
    }

    public bool IsRunning => isRunning;

    public async Task StartAsync(CancellationToken ct)
    {
        if (isRunning)
        {
            logger.LogWarning("[VSF-BRIDGE] Bridge already running");
            return;
        }

        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Bridge?.Enabled != true)
        {
            logger.LogInformation("[VSF-BRIDGE] Bridge disabled in configuration");
            return;
        }

        logger.LogInformation("[VSF-BRIDGE] Starting Soulfind bridge service");

        try
        {
            // Start Soulfind in proxy mode
            var soulfindPath = string.IsNullOrWhiteSpace(options.VirtualSoulfind.Bridge.SoulfindPath)
                ? "soulfind"
                : options.VirtualSoulfind.Bridge.SoulfindPath;
            var bridgePort = options.VirtualSoulfind.Bridge.Port > 0
                ? options.VirtualSoulfind.Bridge.Port
                : 2242;

            var startInfo = new ProcessStartInfo
            {
                FileName = soulfindPath,
                Arguments = $"--port {bridgePort}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Set PROXY_MODE environment variable
            startInfo.Environment["PROXY_MODE"] = "true";
            startInfo.Environment["SLSKDN_API_URL"] = $"http://localhost:{options.Web?.Port ?? 5030}";

            soulfindProcess = Process.Start(startInfo);

            if (soulfindProcess == null)
            {
                throw new Exception("Failed to start Soulfind process");
            }

            // Wait for startup
            await Task.Delay(TimeSpan.FromSeconds(2), ct);

            isRunning = true;
            startedAt = DateTimeOffset.UtcNow;

            logger.LogInformation("[VSF-BRIDGE] Soulfind bridge started on port {Port}", bridgePort);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE] Failed to start bridge: {Message}", ex.Message);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (!isRunning)
        {
            logger.LogDebug("[VSF-BRIDGE] Bridge not running");
            return;
        }

        logger.LogInformation("[VSF-BRIDGE] Stopping Soulfind bridge");

        try
        {
            if (soulfindProcess != null && !soulfindProcess.HasExited)
            {
                soulfindProcess.Kill();
                await soulfindProcess.WaitForExitAsync(ct);
            }

            isRunning = false;
            startedAt = null;

            logger.LogInformation("[VSF-BRIDGE] Soulfind bridge stopped");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE] Error stopping bridge: {Message}", ex.Message);
        }
    }

    public Task<BridgeHealthStatus> GetHealthAsync(CancellationToken ct)
    {
        var health = new BridgeHealthStatus
        {
            IsHealthy = isRunning && (soulfindProcess?.HasExited == false),
            Version = "1.0.0-proxy",
            ActiveConnections = 0, // TODO: Query Soulfind for active connections
            StartedAt = startedAt ?? DateTimeOffset.MinValue
        };

        return Task.FromResult(health);
    }
}

/// <summary>
/// Bridge configuration options.
/// </summary>
public class BridgeOptions
{
    /// <summary>
    /// Enable legacy client bridge.
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Path to Soulfind binary.
    /// </summary>
    public string? SoulfindPath { get; set; }
    
    /// <summary>
    /// Bridge listening port (Soulseek protocol).
    /// </summary>
    public int Port { get; set; } = 2242;
    
    /// <summary>
    /// Maximum concurrent legacy clients.
    /// </summary>
    public int MaxClients { get; set; } = 10;
    
    /// <summary>
    /// Require authentication for bridge connections.
    /// </summary>
    public bool RequireAuth { get; set; } = false;
}
