// <copyright file="FedMeshRateLimitTestHostFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using System.IO;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Asp.Versioning;
using slskd;
using slskd.Authentication;
using slskd.Core.API;
using slskd.Core.Security;

/// <summary>
/// Test host for PR-09 federation inbox and mesh gateway rate limiting.
/// FederationPermitLimit=2, MeshGatewayPermitLimit=2; stub routes /api/v0/actors/{id}/inbox and /mesh/ok.
/// Burst of 3 to each → 3rd returns 429.
/// </summary>
public class FedMeshRateLimitTestHostFactory : WebApplicationFactory<ProgramStub>
{
    protected override IHostBuilder CreateHostBuilder()
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var contentRoot = Path.Combine(solutionRoot, "tests", "slskd.Tests");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(Path.Combine(solutionRoot, "slskd.Tests"));

        var rateLimiting = new slskd.Options.WebOptions.RateLimitingOptions
        {
            Enabled = true,
            ApiPermitLimit = 100,
            ApiWindowSeconds = 60,
            FederationPermitLimit = 2,
            FederationWindowSeconds = 60,
            MeshGatewayPermitLimit = 2,
            MeshGatewayWindowSeconds = 60,
        };
        var optionsAtStartup = new OptionsAtStartup
        {
            Web = new slskd.Options.WebOptions { EnforceSecurity = true, RateLimiting = rateLimiting },
            Headless = false,
        };

        var apiPermit = rateLimiting.ApiPermitLimit;
        var apiWindow = TimeSpan.FromSeconds(60);
        var fedPermit = rateLimiting.FederationPermitLimit;
        var fedWindow = TimeSpan.FromSeconds(60);
        var meshPermit = rateLimiting.MeshGatewayPermitLimit;
        var meshWindow = TimeSpan.FromSeconds(60);

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
                    services.AddOptions<slskd.Options>();
                    services.AddSingleton<IOptionsSnapshot<slskd.Options>>(sp =>
                        new StaticOptionsSnapshot(sp.GetRequiredService<IOptions<slskd.Options>>().Value));
                    services.AddSingleton<ISecurityService, StubSecurityService>();

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

                    services.AddRateLimiter(o =>
                    {
                        o.RejectionStatusCode = 429;
                        o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        {
                            var path = context.Request.Path.Value ?? "";
                            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                            if (path.StartsWith("/mesh/", StringComparison.OrdinalIgnoreCase))
                                return RateLimitPartition.GetFixedWindowLimiter("mesh:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = meshPermit, Window = meshWindow });
                            if (path.Contains("/inbox", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                                return RateLimitPartition.GetFixedWindowLimiter("fed:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = fedPermit, Window = fedWindow });
                            return RateLimitPartition.GetFixedWindowLimiter("api:" + ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = apiPermit, Window = apiWindow });
                        });
                    });

                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
                    services.AddAuthorization(o =>
                        o.AddPolicy(AuthPolicy.Any, p => p.RequireAuthenticatedUser()));

                    services.AddControllers(o => o.Filters.Add(new AuthorizeFilter(AuthPolicy.Any)))
                        .ConfigureApiBehaviorOptions(o =>
                            o.SuppressModelStateInvalidFilter = !optionsAtStartup.Web.EnforceSecurity)
                        .AddApplicationPart(typeof(SessionController).Assembly);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e =>
                    {
                        // Stub routes for PR-09 fed/mesh rate limit tests (path matches partition logic)
                        e.MapPost("/api/v0/actors/{id}/inbox", (string id) => Results.Ok()).AllowAnonymous();
                        e.MapGet("/mesh/ok", () => Results.Ok()).AllowAnonymous();
                        e.MapControllers();
                    });
                });
            });
    }
}
