// <copyright file="SlskdnFullInstanceRunner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.Harness;

using System.Collections.Concurrent;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Full slskdn instance runner for tests that require TCP listeners (e.g., Bridge proxy server).
/// Starts an actual slskdn process instead of using TestServer.
/// </summary>
public class SlskdnFullInstanceRunner : IAsyncDisposable
{
    private static readonly TimeSpan ApiStartupTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ApiStartupProbeDelay = TimeSpan.FromMilliseconds(500);
    private const int CapturedLogLineLimit = 200;
    private readonly ILogger<SlskdnFullInstanceRunner> logger;
    private readonly string testId;
    private readonly string appDir;
    private readonly ConcurrentQueue<string> stdoutLines = new();
    private readonly ConcurrentQueue<string> stderrLines = new();
    private Process? slskdnProcess;
    private int apiPort;
    private int? bridgePort;
    private int? overlayPort;
    private int? dhtPort;
    private int? udpOverlayPort;
    private int? dataOverlayPort;
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
    public string DataDirectory => Path.Combine(appDir, "data");
    public string DownloadsDirectory => Path.Combine(appDir, "downloads");
    public string IncompleteDirectory => Path.Combine(appDir, "incomplete");
    public string SharesDirectory => Path.Combine(appDir, "shares");
    public int ApiPort => apiPort;
    public int? BridgePort => bridgePort;
    public int? OverlayPort => overlayPort;
    public int? DhtPort => dhtPort;
    public bool IsRunning => isRunning;
    public string ApiUrl => $"http://127.0.0.1:{apiPort}";

    /// <summary>
    /// Start full slskdn instance.
    /// </summary>
    public async Task StartAsync(
        bool enableBridge = false,
        int? bridgePortOverride = null,
        bool disableAuthentication = false,
        int? overlayPortOverride = null,
        int? dhtPortOverride = null,
        bool noConnect = true,
        string? soulseekUsername = null,
        string? soulseekPassword = null,
        CancellationToken ct = default)
    {
        logger.LogInformation("[TEST-SLSKDN-FULL] Starting full instance {TestId}", testId);

        // Allocate ephemeral ports
        apiPort = AllocateEphemeralPort();
        overlayPort = overlayPortOverride ?? AllocateEphemeralPort();
        dhtPort = dhtPortOverride ?? AllocateEphemeralPortUdp();
        udpOverlayPort = AllocateEphemeralPortUdp();
        dataOverlayPort = AllocateEphemeralPort();
        if (enableBridge)
        {
            bridgePort = bridgePortOverride ?? AllocateEphemeralPort();

            if (string.IsNullOrEmpty(DiscoverSoulfindBinary()))
            {
                throw new InvalidOperationException(
                    "Soulfind binary not found. Install soulfind or set SOULFIND_PATH before running bridge integration tests.");
            }
        }

        // Write configuration file
        var configPath = Path.Combine(appDir, "config", "slskd.yml");
        var configYaml = BuildConfigYaml(enableBridge, disableAuthentication, noConnect, soulseekUsername, soulseekPassword);
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
            Arguments = $"--config \"{configPath}\" --app-dir \"{appDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = appDir
        };

        if (enableBridge)
        {
            startInfo.Environment["SLSKDN_ENABLE_BRIDGE_PROXY"] = "1";
        }

        startInfo.Environment["APP_DIR"] = appDir;

        slskdnProcess = Process.Start(startInfo);
        if (slskdnProcess == null)
        {
            throw new InvalidOperationException($"Failed to start slskdn process: {binaryPath}");
        }

        slskdnProcess.OutputDataReceived += (_, args) => CaptureProcessLogLine(stdoutLines, args.Data);
        slskdnProcess.ErrorDataReceived += (_, args) => CaptureProcessLogLine(stderrLines, args.Data);
        slskdnProcess.BeginOutputReadLine();
        slskdnProcess.BeginErrorReadLine();

        // Wait for API to be ready
        await WaitForApiReadyAsync(ct);

        if (overlayPort.HasValue)
        {
            await WaitForTcpPortReadyAsync(overlayPort.Value, "overlay", ct);
        }

        // If bridge is enabled, wait for bridge port to be listening
        if (enableBridge && bridgePort.HasValue)
        {
            await WaitForTcpPortReadyAsync(bridgePort.Value, "bridge", ct);
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
                slskdnProcess.CancelOutputRead();
                slskdnProcess.CancelErrorRead();
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

    private string BuildConfigYaml(
        bool enableBridge,
        bool disableAuthentication,
        bool noConnect,
        string? soulseekUsername,
        string? soulseekPassword)
    {
        var sb = new StringBuilder();
        sb.AppendLine("web:");
        sb.AppendLine($"  port: {apiPort}");
        sb.AppendLine("  host: 127.0.0.1");
        sb.AppendLine("  https:");
        sb.AppendLine("    disabled: true");
        sb.AppendLine("    force: false");
        sb.AppendLine("  authentication:");
        sb.AppendLine($"    disabled: {disableAuthentication.ToString().ToLowerInvariant()}");
        sb.AppendLine("    username: admin");
        sb.AppendLine("    password: admin");
        sb.AppendLine("directories:");
        sb.AppendLine($"  downloads: {Path.Combine(appDir, "downloads")}");
        sb.AppendLine($"  incomplete: {Path.Combine(appDir, "incomplete")}");
        sb.AppendLine("shares:");
        sb.AppendLine("  directories:");
        sb.AppendLine($"    - {Path.Combine(appDir, "shares")}");
        sb.AppendLine("  cache:");
        sb.AppendLine("    storage_mode: disk");
        sb.AppendLine("feature:");
        sb.AppendLine("  identityFriends: true");
        sb.AppendLine("  collectionsSharing: true");
        sb.AppendLine("  scenePodBridge: true");
        sb.AppendLine("soulseek:");
        sb.AppendLine($"  username: {YamlEscape(soulseekUsername ?? testId)}");
        sb.AppendLine($"  password: {YamlEscape(soulseekPassword ?? "test-password")}");
        sb.AppendLine("dhtRendezvous:");
        sb.AppendLine("  enabled: true");
        sb.AppendLine($"  overlay_port: {overlayPort ?? 50305}");
        sb.AppendLine($"  dht_port: {dhtPort ?? 50306}");
        sb.AppendLine("  bootstrap_routers:");
        sb.AppendLine("    - router.bittorrent.com");
        sb.AppendLine("    - router.utorrent.com");
        sb.AppendLine("    - dht.transmissionbt.com");
        sb.AppendLine("  announce_interval_seconds: 900");
        sb.AppendLine("  discovery_interval_seconds: 600");
        sb.AppendLine("  min_neighbors: 1");
        sb.AppendLine("overlay:");
        sb.AppendLine("  enable: false");
        sb.AppendLine($"  listen_port: {udpOverlayPort ?? 50400}");
        sb.AppendLine("overlayData:");
        sb.AppendLine("  enable: false");
        sb.AppendLine($"  listen_port: {dataOverlayPort ?? 50401}");

        if (enableBridge && bridgePort.HasValue)
        {
            sb.AppendLine("virtualSoulfind:");
            sb.AppendLine("  bridge:");
            sb.AppendLine("    enabled: true");
            sb.AppendLine($"    port: {bridgePort.Value}");
            sb.AppendLine("    requireAuth: false");
        }

        sb.AppendLine("flags:");
        sb.AppendLine($"  no_connect: {noConnect.ToString().ToLowerInvariant()}");

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
                Path.Combine(solutionRoot, "src", "slskd", "bin", "Debug", "net10.0", "slskd"),
                Path.Combine(solutionRoot, "src", "slskd", "bin", "Release", "net10.0", "slskd"),
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

    private string? DiscoverSoulfindBinary()
    {
        var locations = new[]
        {
            "/usr/local/bin/soulfind",
            "/usr/bin/soulfind",
            "./soulfind",
            "../soulfind/soulfind",
            Environment.GetEnvironmentVariable("SOULFIND_PATH")
        };

        foreach (var location in locations.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            if (File.Exists(location))
            {
                return location;
            }
        }

        try
        {
            var whichCommand = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = new ProcessStartInfo
            {
                FileName = whichCommand,
                Arguments = "soulfind",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd()?.Trim();

            if (!string.IsNullOrEmpty(output) && File.Exists(output))
            {
                return output;
            }
        }
        catch
        {
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
        var maxAttempts = (int)Math.Ceiling(ApiStartupTimeout.TotalMilliseconds / ApiStartupProbeDelay.TotalMilliseconds);
        var attempt = 0;

        while (attempt < maxAttempts && !ct.IsCancellationRequested)
        {
            if (slskdnProcess?.HasExited == true)
            {
                throw BuildProcessExitException();
            }

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

            await Task.Delay(ApiStartupProbeDelay, ct);
            attempt++;
        }

        throw new TimeoutException(
            $"slskdn instance did not become ready after {ApiStartupTimeout.TotalMilliseconds}ms" +
            $"{Environment.NewLine}STDOUT:{Environment.NewLine}{FormatCapturedLogs(stdoutLines)}" +
            $"{Environment.NewLine}STDERR:{Environment.NewLine}{FormatCapturedLogs(stderrLines)}");
    }

    private InvalidOperationException BuildProcessExitException()
    {
        if (slskdnProcess == null)
        {
            return new InvalidOperationException("slskdn process exited before startup completed");
        }

        return new InvalidOperationException(
            $"slskdn process exited before startup completed (exit code {slskdnProcess.ExitCode})" +
            $"{Environment.NewLine}STDOUT:{Environment.NewLine}{FormatCapturedLogs(stdoutLines)}" +
            $"{Environment.NewLine}STDERR:{Environment.NewLine}{FormatCapturedLogs(stderrLines)}");
    }

    private async Task WaitForTcpPortReadyAsync(int port, string serviceName, CancellationToken ct)
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
                logger.LogInformation("[TEST-SLSKDN-FULL] {ServiceName} ready after {Attempts} attempts", serviceName, attempt + 1);
                return;
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(500, ct);
            attempt++;
        }

        throw new TimeoutException($"{serviceName} did not become ready on port {port} after {maxAttempts * 500}ms");
    }

    private int AllocateEphemeralPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private int AllocateEphemeralPortUdp()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }

    private static string YamlEscape(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
    private static void CaptureProcessLogLine(ConcurrentQueue<string> lines, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lines.Enqueue(line);
        while (lines.Count > CapturedLogLineLimit && lines.TryDequeue(out _))
        {
        }
    }

    private static string FormatCapturedLogs(ConcurrentQueue<string> lines)
    {
        var snapshot = lines.ToArray();
        return snapshot.Length == 0
            ? "<no output captured>"
            : string.Join(Environment.NewLine, snapshot.TakeLast(CapturedLogLineLimit));
    }
}
