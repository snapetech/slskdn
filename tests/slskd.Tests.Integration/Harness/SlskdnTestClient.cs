namespace slskd.Tests.Integration.Harness;

using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

/// <summary>
/// slskdn test client harness for isolated test instances.
/// </summary>
public class SlskdnTestClient : IAsyncDisposable
{
    private readonly ILogger<SlskdnTestClient> logger;
    private readonly string testId;
    private readonly string configDir;
    private readonly string shareDir;
    private WebApplication? app;
    private HttpClient? httpClient;
    private int apiPort;

    public SlskdnTestClient(ILogger<SlskdnTestClient> logger, string testId)
    {
        this.logger = logger;
        this.testId = testId;
        this.configDir = Path.Combine(Path.GetTempPath(), "slskdn-test", testId, "config");
        this.shareDir = Path.Combine(Path.GetTempPath(), "slskdn-test", testId, "shares");
        
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(shareDir);
    }

    public string ConfigDirectory => configDir;
    public string ShareDirectory => shareDir;
    public int ApiPort => apiPort;
    public HttpClient HttpClient => httpClient ?? throw new InvalidOperationException("Client not started");

    /// <summary>
    /// Start slskdn test instance.
    /// </summary>
    public async Task StartAsync(
        string soulfindHost = "127.0.0.1",
        int soulfindPort = 2242,
        CancellationToken ct = default)
    {
        logger.LogInformation("[TEST-SLSKDN] Starting test instance {TestId}", testId);

        // Allocate ephemeral port for API
        apiPort = AllocateEphemeralPort();

        // Build test configuration
        var config = new Dictionary<string, string>
        {
            ["Soulseek:Address"] = soulfindHost,
            ["Soulseek:Port"] = soulfindPort.ToString(),
            ["Soulseek:Username"] = $"test-{testId}",
            ["Soulseek:Password"] = "test-password",
            ["Web:Port"] = apiPort.ToString(),
            ["Web:Host"] = "127.0.0.1",
            ["Directories:Incomplete"] = Path.Combine(configDir, "incomplete"),
            ["Directories:Downloads"] = Path.Combine(configDir, "downloads"),
            ["Shares:Directories:0"] = shareDir,
            ["Database:Path"] = Path.Combine(configDir, "test.db"),
            ["VirtualSoulfind:Capture:Enabled"] = "true",
            ["VirtualSoulfind:ShadowIndex:Enabled"] = "true",
            ["VirtualSoulfind:Scenes:Enabled"] = "true",
            ["VirtualSoulfind:DisasterMode:Auto"] = "true"
        };

        // Build minimal WebApplication
        var builder = WebApplication.CreateBuilder();
        
        foreach (var (key, value) in config)
        {
            builder.Configuration[key] = value;
        }

        // Register test services (simplified)
        builder.Services.AddControllers();
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // TODO: Register full slskdn services here
        // For now, this is a minimal stub

        app = builder.Build();
        app.MapControllers();

        // Start in background
        _ = Task.Run(() => app.RunAsync(ct), ct);

        // Wait for readiness
        await WaitForReadinessAsync(ct);

        // Create HTTP client
        httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{apiPort}")
        };

        logger.LogInformation("[TEST-SLSKDN] Test instance {TestId} ready on port {Port}", testId, apiPort);
    }

    /// <summary>
    /// Stop slskdn test instance.
    /// </summary>
    public async Task StopAsync()
    {
        if (app != null)
        {
            logger.LogInformation("[TEST-SLSKDN] Stopping test instance {TestId}", testId);
            await app.StopAsync();
            await app.DisposeAsync();
        }

        httpClient?.Dispose();

        logger.LogInformation("[TEST-SLSKDN] Test instance {TestId} stopped", testId);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        // Clean up test directories
        try
        {
            if (Directory.Exists(configDir))
            {
                Directory.Delete(Path.GetDirectoryName(configDir)!, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TEST-SLSKDN] Failed to clean up test directory: {Dir}", configDir);
        }
    }

    /// <summary>
    /// Add test file to share directory.
    /// </summary>
    public async Task AddSharedFileAsync(string filename, byte[] content)
    {
        var path = Path.Combine(shareDir, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content);
        
        logger.LogDebug("[TEST-SLSKDN] Added shared file: {File}", filename);
    }

    /// <summary>
    /// API wrapper: Search.
    /// </summary>
    public async Task<HttpResponseMessage> SearchAsync(string query, CancellationToken ct = default)
    {
        logger.LogDebug("[TEST-SLSKDN-API] Search: {Query}", query);
        return await HttpClient.PostAsJsonAsync("/api/search", new { query }, ct);
    }

    /// <summary>
    /// API wrapper: Download.
    /// </summary>
    public async Task<HttpResponseMessage> DownloadAsync(string username, string filename, CancellationToken ct = default)
    {
        logger.LogDebug("[TEST-SLSKDN-API] Download: {Username}/{Filename}", username, filename);
        return await HttpClient.PostAsJsonAsync("/api/downloads", new { username, filename }, ct);
    }

    /// <summary>
    /// API wrapper: Get job status.
    /// </summary>
    public async Task<HttpResponseMessage> GetJobStatusAsync(string jobId, CancellationToken ct = default)
    {
        logger.LogDebug("[TEST-SLSKDN-API] Get job: {JobId}", jobId);
        return await HttpClient.GetAsync($"/api/jobs/{jobId}", ct);
    }

    private int AllocateEphemeralPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task WaitForReadinessAsync(CancellationToken ct)
    {
        var maxAttempts = 50;
        var attempt = 0;

        while (attempt < maxAttempts && !ct.IsCancellationRequested)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", apiPort, ct);
                return;
            }
            catch
            {
                await Task.Delay(100, ct);
                attempt++;
            }
        }

        throw new TimeoutException($"slskdn test instance did not become ready after {maxAttempts * 100}ms");
    }
}
