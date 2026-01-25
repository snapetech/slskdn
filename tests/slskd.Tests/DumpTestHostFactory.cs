// <copyright file="DumpTestHostFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Asp.Versioning;
using slskd;
using slskd.Authentication;
using slskd.Core.API;
using slskd.Core.Security;

using SlskdOptions = slskd.Options;

/// <summary>
/// Test host for PR-06 dump endpoint: configurable AllowMemoryDump, AllowRemoteDump, and Passthrough role.
/// Includes ApplicationController; uses stubs for IApplication and IStateMonitor&lt;State&gt;.
/// </summary>
public class DumpTestHostFactory : WebApplicationFactory<ProgramStub>
{
    public DumpTestHostFactory(
        bool allowMemoryDump,
        bool allowRemoteDump,
        Role role)
    {
        AllowMemoryDump = allowMemoryDump;
        AllowRemoteDump = allowRemoteDump;
        Role = role;
    }

    private bool AllowMemoryDump { get; }
    private bool AllowRemoteDump { get; }
    private Role Role { get; }

    protected override IHostBuilder CreateHostBuilder()
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var contentRoot = Path.Combine(solutionRoot, "tests", "slskd.Tests");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(Path.Combine(solutionRoot, "slskd.Tests"));

        var options = new SlskdOptions
        {
            Diagnostics = new SlskdOptions.DiagnosticsOptions
            {
                AllowMemoryDump = AllowMemoryDump,
                AllowRemoteDump = AllowRemoteDump,
            },
        };

        var optionsAtStartup = new OptionsAtStartup
        {
            Web = new SlskdOptions.WebOptions
            {
                Authentication = new SlskdOptions.WebOptions.WebAuthenticationOptions { Disabled = true },
                AllowRemoteNoAuth = true,
            },
        };

        return new HostBuilder()
            .UseContentRoot(contentRoot)
            .UseEnvironment("Test")
            .ConfigureWebHostDefaults(web =>
            {
                web.UseTestServer();
                web.UseContentRoot(contentRoot);
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(optionsAtStartup);
                    services.AddSingleton<IOptions<SlskdOptions>>(new OptionsWrapper<SlskdOptions>(options));
                    services.AddSingleton<IOptionsMonitor<SlskdOptions>>(new TestOptionsMonitor<SlskdOptions>(options));
                    services.AddSingleton<IOptionsSnapshot<SlskdOptions>>(new StaticOptionsSnapshot(options));
                    services.AddSingleton<ISecurityService, StubSecurityService>();

                    services.AddSingleton<StubApplication>();
                    services.AddSingleton<IApplication>(sp => sp.GetRequiredService<StubApplication>());
                    services.AddHostedService<StubApplication>();

                    services.AddSingleton<IStateMonitor<State>>(new StubStateMonitor());

                    services.AddApiVersioning(o =>
                    {
                        o.DefaultApiVersion = new ApiVersion(0, 0);
                        o.AssumeDefaultVersionWhenUnspecified = true;
                        o.ReportApiVersions = true;
                    }).AddApiExplorer(o =>
                    {
                        o.GroupNameFormat = "'v'VVV";
                        o.SubstituteApiVersionInUrl = true;
                    });

                    services.AddAuthentication(PassthroughAuthentication.AuthenticationScheme)
                        .AddScheme<PassthroughAuthenticationOptions, PassthroughAuthenticationHandler>(
                            PassthroughAuthentication.AuthenticationScheme,
                            o =>
                            {
                                o.Username = "Test";
                                o.Role = Role;
                                o.AllowRemoteNoAuth = true;
                            });
                    services.AddAuthorization(o =>
                    {
                        o.AddPolicy(AuthPolicy.Any, p =>
                        {
                            p.AuthenticationSchemes.Add(PassthroughAuthentication.AuthenticationScheme);
                            p.RequireAuthenticatedUser();
                        });
                        o.AddPolicy(AuthPolicy.JwtOnly, p => p.RequireAuthenticatedUser());
                        o.AddPolicy(AuthPolicy.ApiKeyOnly, p => p.RequireAuthenticatedUser());
                    });
                    services.AddControllers(opt => opt.Filters.Add(new AuthorizeFilter(AuthPolicy.Any)))
                        .AddApplicationPart(typeof(ApplicationController).Assembly);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Headers.TryGetValue("X-Test-Remote-IP", out var v) &&
                            IPAddress.TryParse(v!, out var ip))
                            context.Connection.RemoteIpAddress = ip;
                        await next();
                    });
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e => e.MapControllers());
                });
            });
    }
}

internal sealed class StubApplication : IApplication
{
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    public Task CheckVersionAsync() => Task.CompletedTask;
    public void CollectGarbage() { }
}

internal sealed class StubStateMonitor : IStateMonitor<State>
{
    public State CurrentValue { get; } = new State();
    public IDisposable OnChange(Action<(State Previous, State Current)> listener) => new NullDisposable();
}

internal sealed class NullDisposable : IDisposable
{
    public void Dispose() { }
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string name) => CurrentValue;
    public IDisposable OnChange(Action<T, string> listener) => new NullDisposable();
}
