namespace slskd.Tests.Integration.Harness;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Reflection;
using Asp.Versioning;

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

        // Use in-memory test server
        builder.WebHost.UseTestServer();

        // Minimal service setup
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // Add authentication for [Authorize] attributes
        builder.Services.AddAuthentication("Test")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Test")
                .RequireAuthenticatedUser()
                .Build();
        });
        
        // Add API versioning (required for routes with apiVersion constraint)
        builder.Services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(0, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
        
        // Add stub services that controllers need
        builder.Services.AddSingleton<global::slskd.Search.ISearchService>(_ => 
            CreateNullProxy<global::slskd.Search.ISearchService>());
        builder.Services.AddSingleton<global::slskd.Transfers.ITransferService>(_ => 
            CreateNullProxy<global::slskd.Transfers.ITransferService>());
        // Add stub LibraryHealthService (needs to return actual objects, not null)
        builder.Services.AddSingleton<global::slskd.LibraryHealth.ILibraryHealthService>(_ => 
            new StubLibraryHealthService());
        
        // Add real controllers (same as StubWebApplicationFactory)
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            })
            .AddApplicationPart(typeof(global::slskd.API.Compatibility.SearchCompatibilityController).Assembly)
            .AddApplicationPart(typeof(global::slskd.API.Compatibility.DownloadsCompatibilityController).Assembly)
            .AddApplicationPart(typeof(global::slskd.API.Compatibility.RoomsCompatibilityController).Assembly)
            .AddApplicationPart(typeof(global::slskd.API.Compatibility.ServerCompatibilityController).Assembly)
            .AddApplicationPart(typeof(global::slskd.API.Compatibility.UsersCompatibilityController).Assembly)
            .AddApplicationPart(typeof(global::slskd.API.VirtualSoulfind.DisasterModeController).Assembly)
            .AddApplicationPart(typeof(global::slskd.API.VirtualSoulfind.CanonicalController).Assembly)
            .AddApplicationPart(typeof(global::slskd.API.VirtualSoulfind.ShadowIndexController).Assembly);

        app = builder.Build();
        
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // Stub endpoints to satisfy integration scenarios
        app.MapGet("/api/slskdn/capabilities", () => Results.Ok(new
        {
            impl = "slskdn",
            version = "0.0.0-test",
            features = new[] { "mbid_jobs", "discography_jobs", "label_crate_jobs", "library_health" }
        }));

        // Real controllers handle these - remove stub routes to avoid conflicts
        // app.MapPost("/api/search", ...) - handled by SearchCompatibilityController
        // app.MapPost("/api/downloads", ...) - handled by DownloadsCompatibilityController  
        // app.MapPost("/api/rooms", ...) - handled by RoomsCompatibilityController
        app.MapPost("/api/virtualsoulfind/scenes/join", () => Results.Ok());
        app.MapGet("/api/virtualsoulfind/scenes/{sceneId}/members", (string sceneId) => Results.Ok(new[]
        {
            new { peerId = "peer:alice" },
            new { peerId = "peer:bob" },
            new { peerId = "peer:carol" }
        }));
        app.MapGet("/api/jobs/{id}", (string id) => Results.Ok(new { id, status = "completed" }));
        app.MapPost("/api/library/scan", () => Results.Ok(new { scan_id = Guid.NewGuid().ToString("N") }));
        app.MapPost("/api/slskdn/library/remediate", (System.Text.Json.JsonElement body) =>
        {
            return Results.Ok(new { job_id = Guid.NewGuid().ToString("N") });
        });
        app.MapGet("/api/server/status", () => Results.Ok(new { connected = true, state = "logged_in", username = "test-user" }));
        // Rooms endpoints handled by RoomsCompatibilityController
        // app.MapPost("/api/rooms", ...) - removed to avoid conflict
        // app.MapDelete("/api/rooms/{roomName}", ...) - removed to avoid conflict
        app.MapGet("/api/users/{username}/browse", (string username) => Results.Ok(new { username, files = new List<object>() }));
        app.MapGet("/api/virtualsoulfind/disaster-mode/status", () => Results.Ok(new { is_active = false, activated_at = (DateTimeOffset?)null, reason = (string?)null }));
        app.MapGet("/api/virtualsoulfind/canonical/{mbid}", (string mbid) => Results.Ok(new { mbid, canonical_variant = new { codec = "FLAC", bitrate = 0, source = "test-peer" }, variants = new List<object>() }));
        app.MapGet("/api/virtualsoulfind/shadow-index/{mbid}", (string mbid) => Results.Ok(new { mbid, variants = new List<object>() }));
        app.MapPost("/api/search", (System.Text.Json.JsonElement body) => Results.Ok(new { SearchId = Guid.NewGuid().ToString("N"), Query = "test", Results = new List<object>() }));
        app.MapPost("/api/downloads", (System.Text.Json.JsonElement body) => Results.Ok(new { DownloadIds = new[] { Guid.NewGuid().ToString("N") } }));
        app.MapGet("/api/downloads", () => Results.Ok(new { Downloads = new List<object>() }));
        app.MapGet("/api/downloads/{id}", (string id) => Results.Ok(new { Id = id, User = "test-user", RemotePath = "test/path", LocalPath = "/tmp", Status = "queued", Progress = 0.0 }));

        await app.StartAsync(ct);

        // Create HTTP client from test server
        httpClient = app.GetTestClient();

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

    private static T CreateNullProxy<T>() where T : class
    {
        // Use DispatchProxy like StubWebApplicationFactory does
        return DispatchProxy.Create<T, NullProxy<T>>();
    }
}

internal class NullProxy<T> : DispatchProxy where T : class
{
    public static T Create() => DispatchProxy.Create<T, NullProxy<T>>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var returnType = targetMethod?.ReturnType;
        if (returnType == null || returnType == typeof(void)) return null;

        if (returnType == typeof(Task)) return Task.CompletedTask;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GenericTypeArguments[0];
            var result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, new[] { result });
        }

        if (returnType == typeof(ValueTask)) return new ValueTask();
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var resultType = returnType.GenericTypeArguments[0];
            var value = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return Activator.CreateInstance(typeof(ValueTask<>).MakeGenericType(resultType), value);
        }

        return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
    }
}

internal class StubLibraryHealthService : global::slskd.LibraryHealth.ILibraryHealthService
{
    public Task<string> StartScanAsync(global::slskd.LibraryHealth.LibraryHealthScanRequest request, CancellationToken ct = default) => 
        Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task<global::slskd.LibraryHealth.LibraryHealthScan> GetScanStatusAsync(string scanId, CancellationToken ct = default) => 
        Task.FromResult(new global::slskd.LibraryHealth.LibraryHealthScan
        {
            ScanId = scanId,
            LibraryPath = "(all)",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Status = global::slskd.LibraryHealth.ScanStatus.Completed
        });

    public Task<List<global::slskd.LibraryHealth.LibraryIssue>> GetIssuesAsync(global::slskd.LibraryHealth.LibraryHealthIssueFilter filter, CancellationToken ct = default) => 
        Task.FromResult(new List<global::slskd.LibraryHealth.LibraryIssue>());

    public Task UpdateIssueStatusAsync(string issueId, global::slskd.LibraryHealth.LibraryIssueStatus newStatus, CancellationToken ct = default) => 
        Task.CompletedTask;

    public Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct = default)
    {
        // Return a consistent job ID for testing
        return Task.FromResult("remediation-job-" + Guid.NewGuid().ToString("N"));
    }

    public Task<global::slskd.LibraryHealth.LibraryHealthSummary> GetSummaryAsync(string libraryPath, CancellationToken ct = default) => 
        Task.FromResult(new global::slskd.LibraryHealth.LibraryHealthSummary 
        { 
            LibraryPath = libraryPath ?? "(all)", 
            TotalIssues = 0, 
            IssuesOpen = 0, 
            IssuesResolved = 0 
        });
}
