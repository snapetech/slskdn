namespace slskd.Tests.Integration;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using global::slskd.API.Compatibility;
using global::slskd.API.Native;
using global::slskd.Jobs;
using global::slskd.LibraryHealth;
using global::slskd.Search;
using global::slskd.Transfers;
using global::slskd.Transfers.MultiSource.Caching;
// using Soulseek; // Deferred - requires proper package reference
using OptionsModel = global::slskd.Options;

/// <summary>
/// Integration test host using real controllers with lightweight stubs.
/// </summary>
public class StubWebApplicationFactory : WebApplicationFactory<ProgramStub>
{
    protected override IHostBuilder CreateHostBuilder()
    {
        // Use the solution root as content root - simpler and avoids path issues
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var testContentRoot = Path.Combine(solutionRoot, "tests", "slskd.Tests.Integration");
        
        // Ensure the content root directory exists
        Directory.CreateDirectory(testContentRoot);
        
        // Manually create host builder to avoid default path inference issues
        return new HostBuilder()
            .UseContentRoot(testContentRoot)
            .UseEnvironment("Test")
            .ConfigureHostConfiguration(config =>
            {
                // Set content root via configuration to ensure it's used
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [HostDefaults.ContentRootKey] = testContentRoot
                });
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.UseContentRoot(testContentRoot);
                
                webBuilder.ConfigureAppConfiguration(config =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Logging:LogLevel:Default"] = "Warning"
                    });
                });
                
                webBuilder.ConfigureServices(services =>
                {
                    services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                            .RequireAuthenticatedUser()
                            .Build();
                    });

                    // Add API versioning (required for routes with apiVersion constraint)
                    services.AddApiVersioning(options =>
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

                    services.AddControllers()
                        .AddJsonOptions(options =>
                        {
                            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                            options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                        })
                        .AddApplicationPart(typeof(CapabilitiesController).Assembly)
                        // CompatibilityController requires ISoulseekClient - skip for now
                        // .AddApplicationPart(typeof(CompatibilityController).Assembly)
                        .AddApplicationPart(typeof(DownloadsCompatibilityController).Assembly)
                        .AddApplicationPart(typeof(LibraryHealthController).Assembly)
                        .AddApplicationPart(typeof(global::slskd.API.Compatibility.LibraryCompatibilityController).Assembly)
                        .AddApplicationPart(typeof(global::slskd.API.Compatibility.ServerCompatibilityController).Assembly)
                        .AddApplicationPart(typeof(global::slskd.API.Compatibility.RoomsCompatibilityController).Assembly)
                        .AddApplicationPart(typeof(global::slskd.API.Compatibility.UsersCompatibilityController).Assembly)
                        .AddApplicationPart(typeof(JobsController).Assembly)
                        .AddApplicationPart(typeof(SearchCompatibilityController).Assembly)
                        .AddApplicationPart(typeof(global::slskd.API.VirtualSoulfind.DisasterModeController).Assembly)
                        .ConfigureApplicationPartManager(manager =>
                        {
                            // Exclude conflicting controllers - use JobsController instead
                            var existingProvider = manager.FeatureProviders.OfType<ControllerFeatureProvider>().FirstOrDefault();
                            if (existingProvider != null)
                            {
                                manager.FeatureProviders.Remove(existingProvider);
                            }
                            manager.FeatureProviders.Add(new ExcludeControllerFeatureProvider(
                                typeof(global::slskd.Jobs.API.DiscographyJobsController),
                                typeof(global::slskd.Jobs.API.LabelCrateJobsController)));
                        })
                        .AddApplicationPart(typeof(SearchCompatibilityController).Assembly)
                        .AddApplicationPart(typeof(global::slskd.API.VirtualSoulfind.DisasterModeController).Assembly);

                    var discographyService = new StubDiscographyJobService();
                    var labelCrateService = new StubLabelCrateJobService();
                    
                    services.AddSingleton<IOptionsMonitor<OptionsModel>>(_ => new StaticOptionsMonitor<OptionsModel>(new OptionsModel
                    {
                        WarmCache = new global::slskd.WarmCacheOptions
                        {
                            Enabled = true
                        }
                    }))
                        .AddSingleton<IDiscographyJobService>(discographyService)
                        .AddSingleton<ILabelCrateJobService>(labelCrateService)
                        .AddSingleton<IJobServiceWithList>(new JobServiceListAdapter(discographyService, labelCrateService))
                        .AddSingleton<ILibraryHealthService, StubLibraryHealthService>()
                        .AddSingleton<ITransferService>(_ => NullProxy<ITransferService>.Create())
                        .AddSingleton<ISearchService>(_ => NullProxy<ISearchService>.Create())
                        .AddSingleton<IWarmCachePopularityService>(_ => NullProxy<IWarmCachePopularityService>.Create());
                        // ISoulseekClient stub deferred - requires Soulseek package types
                });
                
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
                });
            });
    }
}

internal class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    private readonly T value;
    public StaticOptionsMonitor(T value) => this.value = value;
    public T CurrentValue => value;
    public T Get(string name) => value;
    public IDisposable OnChange(Action<T, string> listener) => NullDisposable.Instance;

    private class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}

internal class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.Name, "test-user"));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal class StubDiscographyJobService : IDiscographyJobService
{
    private readonly ConcurrentDictionary<string, DiscographyJob> jobs = new();

    public Task<string> CreateJobAsync(DiscographyJobRequest request, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var job = new DiscographyJob
        {
            JobId = id,
            ArtistId = request.ArtistId ?? "",
            ArtistName = request.ArtistId ?? "",
            Profile = request.Profile,
            TargetDirectory = request.TargetDirectory ?? string.Empty,
            TotalReleases = 0,
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        jobs[id] = job;
        return Task.FromResult(id);
    }

    public Task<DiscographyJob?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task SetReleaseStatusAsync(string jobId, string releaseId, JobStatus status, CancellationToken ct = default) => Task.CompletedTask;

    // Helper method for test host
    public IReadOnlyList<DiscographyJob> GetAllJobs() => jobs.Values.ToList();
}

internal class StubLabelCrateJobService : ILabelCrateJobService
{
    private readonly ConcurrentDictionary<string, LabelCrateJob> jobs = new();

    public Task<string> CreateJobAsync(LabelCrateJobRequest request, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        jobs[id] = new LabelCrateJob
        {
            JobId = id,
            LabelId = request.LabelId ?? string.Empty,
            LabelName = request.LabelName ?? request.LabelId ?? string.Empty,
            Limit = request.Limit > 0 ? request.Limit : 10,
            TotalReleases = request.Limit > 0 ? request.Limit : 0,
            Status = JobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        return Task.FromResult(id);
    }

    public Task<LabelCrateJob?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task SetReleaseStatusAsync(string jobId, string releaseId, JobStatus status, CancellationToken ct = default) => Task.CompletedTask;

    // Helper method for test host
    public IReadOnlyList<LabelCrateJob> GetAllJobs() => jobs.Values.ToList();
}

internal class ExcludeControllerFeatureProvider : ControllerFeatureProvider
{
    private readonly HashSet<Type> excludeTypes;

    public ExcludeControllerFeatureProvider(params Type[] excludeTypes)
    {
        this.excludeTypes = new HashSet<Type>(excludeTypes);
    }

    protected override bool IsController(TypeInfo typeInfo)
    {
        if (excludeTypes.Contains(typeInfo.AsType()))
            return false;
        return base.IsController(typeInfo);
    }
}

internal class JobServiceListAdapter : global::slskd.API.Native.IJobServiceWithList
{
    private readonly StubDiscographyJobService discographyService;
    private readonly StubLabelCrateJobService labelCrateService;

    public JobServiceListAdapter(StubDiscographyJobService discographyService, StubLabelCrateJobService labelCrateService)
    {
        this.discographyService = discographyService;
        this.labelCrateService = labelCrateService;
    }

    public IReadOnlyList<global::slskd.Jobs.DiscographyJob> GetAllDiscographyJobs() => discographyService.GetAllJobs();
    public IReadOnlyList<global::slskd.Jobs.LabelCrateJob> GetAllLabelCrateJobs() => labelCrateService.GetAllJobs();
}

internal class StubLibraryHealthService : ILibraryHealthService
{
    public Task<string> StartScanAsync(LibraryHealthScanRequest request, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task<LibraryHealthScan> GetScanStatusAsync(string scanId, CancellationToken ct = default) => Task.FromResult(new LibraryHealthScan
    {
        ScanId = scanId,
        LibraryPath = "(all)",
        StartedAt = DateTimeOffset.UtcNow,
        CompletedAt = DateTimeOffset.UtcNow,
        Status = ScanStatus.Completed
    });

    public Task<List<LibraryIssue>> GetIssuesAsync(LibraryHealthIssueFilter filter, CancellationToken ct = default) => Task.FromResult(new List<LibraryIssue>());

    public Task UpdateIssueStatusAsync(string issueId, LibraryIssueStatus newStatus, CancellationToken ct = default) => Task.CompletedTask;

    public Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task<LibraryHealthSummary> GetSummaryAsync(string libraryPath, CancellationToken ct = default) => Task.FromResult(new LibraryHealthSummary { LibraryPath = libraryPath ?? "(all)", TotalIssues = 0, IssuesOpen = 0, IssuesResolved = 0 });
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















