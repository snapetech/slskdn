// <copyright file="SlskdnFullInstanceRunner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.Harness;

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Full slskdn instance runner for tests that require TCP listeners (e.g., Bridge proxy server).
/// Starts an actual slskdn process instead of using TestServer.
/// </summary>
public class SlskdnFullInstanceRunner : IAsyncDisposable
{
    private readonly ILogger<SlskdnFullInstanceRunner> logger;
    private readonly string testId;
    private readonly string appDir;
    private Process? slskdnProcess;
    private int apiPort;
    private int? bridgePort;
    private bool isRunning;

    public SlskdnFullInstanceRunner(ILogger<SlskdnFullInstanceRunner> logger, string testId)
    {
        this.logger = logger;
        this.testId = testId;
        this.appDir = Path.Combine(Path.GetTempPath(), "slskdn-test", testId);
        
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(Path.Combine(appDir, "config"));
        Directory.CreateDirectory(Path.Combine(appDir, "downloads"));
        Directory.CreateDirectory(Path.Combine(appDir, "incomplete"));
        Directory.CreateDirectory(Path.Combine(appDir, "shares"));
    }

    public string AppDirectory => appDir;
    public int ApiPort => apiPort;
    public int? BridgePort => bridgePort;
    public bool IsRunning => isRunning;
    public string ApiUrl => $"http://127.0.0.1:{apiPort}";

    /// <summary>
    /// Start full slskdn instance.
    /// </summary>
    public async Task StartAsync(
        bool enableBridge = false,
        int? bridgePortOverride = null,
        CancellationToken ct = default)
    {
        logger.LogInformation("[TEST-SLSKDN-FULL] Starting full instance {TestId}", testId);

        // Allocate ephemeral ports
        apiPort = AllocateEphemeralPort();
        if (enableBridge)
        {
            bridgePort = bridgePortOverride ?? AllocateEphemeralPort();
        }

        // Write configuration file
        var configPath = Path.Combine(appDir, "config", "slskd.yml");
        var configYaml = BuildConfigYaml(enableBridge);
        await File.WriteAllTextAsync(configPath, configYaml, ct);

        // Find slskdn binary
        var binaryPath = DiscoverSlskdnBinary();
        if (string.IsNullOrEmpty(binaryPath))
        {
            throw new InvalidOperationException(
                "slskdn binary not found. Build the project first: dotnet build -c Release");
        }

        // Start process
        var startInfo = new ProcessStartInfo
        {
            FileName = binaryPath,
            Arguments = $"--config \"{configPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = appDir
        };

        slskdnProcess = Process.Start(startInfo);
        if (slskdnProcess == null)
        {
            throw new InvalidOperationException($"Failed to start slskdn process: {binaryPath}");
        }

        // Wait for API to be ready
        await WaitForApiReadyAsync(ct);
        
        // If bridge is enabled, wait for bridge port to be listening
        if (enableBridge && bridgePort.HasValue)
        {
            await WaitForBridgeReadyAsync(bridgePort.Value, ct);
        }
        
        isRunning = true;
        logger.LogInformation("[TEST-SLSKDN-FULL] Instance {TestId} ready on API port {ApiPort}", testId, apiPort);
        if (enableBridge && bridgePort.HasValue)
        {
            logger.LogInformation("[TEST-SLSKDN-FULL] Bridge ready on port {BridgePort}", bridgePort.Value);
        }
    }

    /// <summary>
    /// Stop slskdn instance.
    /// </summary>
    public async Task StopAsync()
    {
        if (slskdnProcess != null && !slskdnProcess.HasExited)
        {
            logger.LogInformation("[TEST-SLSKDN-FULL] Stopping instance {TestId}", testId);
            
            try
            {
                slskdnProcess.Kill();
                await slskdnProcess.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[TEST-SLSKDN-FULL] Error stopping process");
            }
            
            slskdnProcess.Dispose();
            slskdnProcess = null;
        }

        isRunning = false;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        // Cleanup
        try
        {
            if (Directory.Exists(appDir))
            {
                Directory.Delete(appDir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TEST-SLSKDN-FULL] Failed to clean up directory: {Dir}", appDir);
        }
    }

    private string BuildConfigYaml(bool enableBridge)
    {
        var sb = new StringBuilder();
        sb.AppendLine("web:");
        sb.AppendLine($"  port: {apiPort}");
        sb.AppendLine("  host: 127.0.0.1");
        sb.AppendLine("  authentication:");
        sb.AppendLine("    username: admin");
        sb.AppendLine("    password: admin");
        sb.AppendLine("directories:");
        sb.AppendLine($"  downloads: {Path.Combine(appDir, "downloads")}");
        sb.AppendLine($"  incomplete: {Path.Combine(appDir, "incomplete")}");
        sb.AppendLine("shares:");
        sb.AppendLine("  directories:");
        sb.AppendLine($"    - {Path.Combine(appDir, "shares")}");
        sb.AppendLine("feature:");
        sb.AppendLine("  identityFriends: true");
        sb.AppendLine("  collectionsSharing: true");
        sb.AppendLine("  scenePodBridge: true");
        
        if (enableBridge && bridgePort.HasValue)
        {
            sb.AppendLine("virtualSoulfind:");
            sb.AppendLine("  bridge:");
            sb.AppendLine("    enabled: true");
            sb.AppendLine($"    port: {bridgePort.Value}");
            sb.AppendLine("    requireAuth: false");
        }
        
        sb.AppendLine("flags:");
        sb.AppendLine("  no_connect: true"); // Don't connect to real Soulseek for tests

        return sb.ToString();
    }

    private string? DiscoverSlskdnBinary()
    {
        // Check environment variable
        var envPath = Environment.GetEnvironmentVariable("SLSKDN_BINARY_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        // Check common build output locations
        var solutionRoot = FindSolutionRoot();
        if (solutionRoot != null)
        {
            var candidates = new[]
            {
                Path.Combine(solutionRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd"),
                Path.Combine(solutionRoot, "src", "slskd", "bin", "Debug", "net8.0", "slskd"),
                Path.Combine(solutionRoot, "publish", "slskd")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private string? FindSolutionRoot()
    {
        var current = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(current);
        
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "slskd.sln")) ||
                File.Exists(Path.Combine(dir.FullName, "src", "slskd", "slskd.csproj")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return null;
    }

    private async Task WaitForApiReadyAsync(CancellationToken ct)
    {
        const int maxAttempts = 50;
        var attempt = 0;

        while (attempt < maxAttempts && !ct.IsCancellationRequested)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = await client.GetAsync($"{ApiUrl}/api/v0/session/enabled", ct);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("[TEST-SLSKDN-FULL] API ready after {Attempts} attempts", attempt + 1);
                    return;
                }
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(500, ct);
            attempt++;
        }

        throw new TimeoutException($"slskdn instance did not become ready after {maxAttempts * 500}ms");
    }

    private async Task WaitForBridgeReadyAsync(int port, CancellationToken ct)
    {
        const int maxAttempts = 30;
        var attempt = 0;

        while (attempt < maxAttempts && !ct.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                client.Close();
                logger.LogInformation("[TEST-SLSKDN-FULL] Bridge ready after {Attempts} attempts", attempt + 1);
                return;
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(500, ct);
            attempt++;
        }

        throw new TimeoutException($"Bridge did not become ready on port {port} after {maxAttempts * 500}ms");
    }

    private int AllocateEphemeralPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
