namespace slskd.Tests.Integration.Harness;

using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;

/// <summary>
/// Soulfind test harness for integration testing.
/// </summary>
public class SoulfindRunner : IAsyncDisposable
{
    private readonly ILogger<SoulfindRunner> logger;
    private Process? soulfindProcess;
    private int port;
    private bool isRunning;

    public SoulfindRunner(ILogger<SoulfindRunner> logger)
    {
        this.logger = logger;
    }

    public int Port => port;
    public bool IsRunning => isRunning;

    /// <summary>
    /// Start Soulfind with ephemeral port.
    /// Tries multiple strategies:
    /// 1. Start Soulfind binary if available (SOULFIND_PATH or discovered)
    /// 2. Start Docker container if available
    /// 3. Fall back to stub mode (tests will need to mock ISoulseekClient)
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        port = AllocateEphemeralPort();
        
        // Strategy 1: Try to start Soulfind binary
        var binaryPath = DiscoverSoulfindBinary();
        if (!string.IsNullOrEmpty(binaryPath))
        {
            logger.LogInformation("[TEST-SOULFIND] Starting Soulfind binary: {Path} on port {Port}", binaryPath, port);
            try
            {
                await StartSoulfindProcessAsync(binaryPath, ct);
                isRunning = true;
                logger.LogInformation("[TEST-SOULFIND] Soulfind started successfully on port {Port}", port);
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[TEST-SOULFIND] Failed to start Soulfind binary, trying alternatives");
            }
        }
        
        // Strategy 2: Try Docker (if available)
        if (await TryStartDockerContainerAsync(ct))
        {
            isRunning = true;
            logger.LogInformation("[TEST-SOULFIND] Soulfind running in Docker on port {Port}", port);
            return;
        }
        
        // Strategy 3: Stub mode - tests should use mocked ISoulseekClient
        logger.LogWarning("[TEST-SOULFIND] Soulfind binary and Docker not available - using stub mode. Tests should mock ISoulseekClient.");
        isRunning = false; // Indicates stub mode
    }
    
    private async Task StartSoulfindProcessAsync(string binaryPath, CancellationToken ct)
    {
        var tempDataDir = Path.Combine(Path.GetTempPath(), "slskdn-test", $"soulfind-{port}");
        Directory.CreateDirectory(tempDataDir);
        
        var psi = new ProcessStartInfo
        {
            FileName = binaryPath,
            Arguments = $"--port {port} --data-dir \"{tempDataDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        soulfindProcess = Process.Start(psi);
        if (soulfindProcess == null)
        {
            throw new InvalidOperationException($"Failed to start Soulfind process: {binaryPath}");
        }
        
        // Wait for process to be ready
        await WaitForReadinessAsync(ct);
    }
    
    private async Task<bool> TryStartDockerContainerAsync(CancellationToken ct)
    {
        // Check if Docker is available
        try
        {
            var dockerCheck = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            
            using var checkProcess = Process.Start(dockerCheck);
            if (checkProcess == null)
            {
                return false;
            }
            
            await checkProcess.WaitForExitAsync(ct);
            if (checkProcess.ExitCode != 0)
            {
                return false;
            }
        }
        catch
        {
            // Docker not available
            return false;
        }
        
        // Try to start Soulfind container
        // Uses official Soulfind image from GitHub Container Registry
        // ghcr.io/soulfind-dev/soulfind (see https://github.com/soulfind-dev/soulfind)
        try
        {
            var containerName = $"soulfind-test-{port}";
            var startArgs = $"run -d --name {containerName} -p {port}:2242 ghcr.io/soulfind-dev/soulfind:latest";
            
            var startProcess = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = startArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            
            using var process = Process.Start(startProcess);
            if (process == null)
            {
                return false;
            }
            
            await process.WaitForExitAsync(ct);
            if (process.ExitCode == 0)
            {
                // Wait for container to be ready
                await Task.Delay(2000, ct); // Give container time to start
                await WaitForReadinessAsync(ct);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[TEST-SOULFIND] Docker container start failed (image may not exist)");
        }
        
        return false;
    }

    /// <summary>
    /// Stop Soulfind.
    /// </summary>
    public async Task StopAsync()
    {
        isRunning = false;
        
        // Stop process if running
        if (soulfindProcess != null && !soulfindProcess.HasExited)
        {
            try
            {
                soulfindProcess.Kill();
                await soulfindProcess.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[TEST-SOULFIND] Error stopping Soulfind process");
            }
            finally
            {
                soulfindProcess?.Dispose();
                soulfindProcess = null;
            }
        }
        
        // Stop Docker container if running
        try
        {
            var containerName = $"soulfind-test-{port}";
            var stopArgs = $"stop {containerName}";
            var removeArgs = $"rm -f {containerName}";
            
            using var stopProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = stopArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (stopProcess != null)
            {
                await stopProcess.WaitForExitAsync();
            }
            
            using var removeProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = removeArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (removeProcess != null)
            {
                await removeProcess.WaitForExitAsync();
            }
        }
        catch
        {
            // Ignore Docker cleanup errors
        }
        
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        soulfindProcess?.Dispose();
    }

    private string? DiscoverSoulfindBinary()
    {
        // Check common locations
        var locations = new[]
        {
            "/usr/local/bin/soulfind",
            "/usr/bin/soulfind",
            "./soulfind",
            "../soulfind/soulfind",
            Environment.GetEnvironmentVariable("SOULFIND_PATH")
        };

        foreach (var location in locations.Where(l => l != null))
        {
            if (File.Exists(location))
            {
                logger.LogDebug("[TEST-SOULFIND] Found Soulfind at {Path}", location);
                return location;
            }
        }

        // Try which/where command
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
            // Ignore errors
        }

        return null;
    }

    private int AllocateEphemeralPort()
    {
        // Bind to port 0 to get OS-assigned ephemeral port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task WaitForReadinessAsync(CancellationToken ct)
    {
        var maxAttempts = 30;
        var attempt = 0;

        while (attempt < maxAttempts && !ct.IsCancellationRequested)
        {
            try
            {
                // Try to connect to Soulfind port
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", port, ct);
                
                // Connection successful - Soulfind is ready
                return;
            }
            catch
            {
                // Not ready yet
                await Task.Delay(100, ct);
                attempt++;
            }
        }

        throw new TimeoutException($"Soulfind did not become ready after {maxAttempts * 100}ms");
    }
}
