// <copyright file="MeshGatewayTestHostFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using slskd;
using slskd.API.Mesh;
using slskd.Mesh.ServiceFabric;

/// <summary>
/// Test host for PR-08 MeshGatewayController body handling: bounded read, 413 on over limit.
/// MeshGatewayOptions.Enabled=true, AllowedServices=["pods"], CsrfToken=null (localhost, no CSRF).
/// Stubs: IMeshServiceDirectory (returns one descriptor for "pods"), IMeshServiceClient (returns OK).
/// </summary>
public class MeshGatewayTestHostFactory : WebApplicationFactory<ProgramStub>
{
    public MeshGatewayTestHostFactory(int maxRequestBodyBytes = 100)
    {
        MaxRequestBodyBytes = maxRequestBodyBytes;
    }

    private int MaxRequestBodyBytes { get; }

    protected override IHostBuilder CreateHostBuilder()
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var contentRoot = Path.Combine(solutionRoot, "tests", "slskd.Tests");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(Path.Combine(solutionRoot, "slskd.Tests"));

        var directoryStub = new StubMeshServiceDirectory();
        var clientStub = new StubMeshServiceClient();

        return new HostBuilder()
            .UseContentRoot(contentRoot)
            .UseEnvironment("Test")
            .ConfigureWebHostDefaults(web =>
            {
                web.UseTestServer();
                web.UseContentRoot(contentRoot);
                web.ConfigureServices(services =>
                {
                    services.Configure<MeshGatewayOptions>(o =>
                    {
                        o.Enabled = true;
                        o.AllowedServices = new List<string> { "pods" };
                        o.MaxRequestBodyBytes = MaxRequestBodyBytes;
                        o.CsrfToken = null; // localhost: no CSRF required
                        o.BindAddress = "127.0.0.1";
                    });

                    services.AddSingleton<IMeshServiceDirectory>(directoryStub);
                    services.AddSingleton<StubMeshServiceClient>(clientStub);
                    services.AddSingleton<IMeshServiceClient>(sp => sp.GetRequiredService<StubMeshServiceClient>());

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

                    services.AddControllers()
                        .AddApplicationPart(typeof(MeshGatewayController).Assembly);
                });
                web.Configure(app =>
                {
                    app.UseMiddleware<MeshGatewayAuthMiddleware>();
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapControllers());
                });
            });
    }
}

internal sealed class StubMeshServiceDirectory : IMeshServiceDirectory
{
    private static readonly MeshServiceDescriptor PodsDescriptor = new()
    {
        ServiceId = MeshServiceDescriptor.DeriveServiceId("pods", "test-peer"),
        ServiceName = "pods",
        Version = "1.0.0",
        OwnerPeerId = "test-peer",
        Endpoint = new MeshServiceEndpoint { Protocol = "quic", Host = "test", Port = 0 },
        CreatedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
    };

    public Task<IReadOnlyList<MeshServiceDescriptor>> FindByNameAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        if (serviceName == "pods")
            return Task.FromResult<IReadOnlyList<MeshServiceDescriptor>>(new[] { PodsDescriptor });
        return Task.FromResult<IReadOnlyList<MeshServiceDescriptor>>(Array.Empty<MeshServiceDescriptor>());
    }

    public Task<IReadOnlyList<MeshServiceDescriptor>> FindByIdAsync(
        string serviceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MeshServiceDescriptor>>(Array.Empty<MeshServiceDescriptor>());
}

internal sealed class StubMeshServiceClient : IMeshServiceClient
{
    public byte[] LastPayload { get; private set; } = Array.Empty<byte>();
    public string LastMethod { get; private set; } = "";
    public string LastServiceName { get; private set; } = "";

    public Task<ServiceReply> CallAsync(
        string targetPeerId,
        ServiceCall call,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ServiceReply { StatusCode = ServiceStatusCodes.OK, Payload = Array.Empty<byte>() });

    public Task<ServiceReply> CallServiceAsync(
        string serviceName,
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        LastServiceName = serviceName;
        LastMethod = method;
        LastPayload = payload.ToArray();
        return Task.FromResult(new ServiceReply { StatusCode = ServiceStatusCodes.OK, Payload = Array.Empty<byte>() });
    }
}
