namespace slskd.Tests.Integration.Harness;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        logger.LogInformation("[TEST-SOULFIND] Stub Soulfind start");
        port = AllocateEphemeralPort();
        isRunning = true;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stop Soulfind.
    /// </summary>
    public async Task StopAsync()
    {
        isRunning = false;
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
