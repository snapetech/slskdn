// <copyright file="NoAuthTestHostFactory.cs" company="slskdN Team">
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

/// <summary>
/// Test host with authentication disabled (Passthrough). Used for PR-03: no-auth loopback vs remote.
/// </summary>
public class NoAuthTestHostFactory : WebApplicationFactory<ProgramStub>
{
    protected override IHostBuilder CreateHostBuilder()
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var contentRoot = Path.Combine(solutionRoot, "tests", "slskd.Tests");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(Path.Combine(solutionRoot, "slskd.Tests"));

        var optionsAtStartup = new OptionsAtStartup
        {
            Web = new slskd.Options.WebOptions
            {
                Authentication = new slskd.Options.WebOptions.WebAuthenticationOptions { Disabled = true },
                AllowRemoteNoAuth = false,
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
                    services.AddSingleton<OptionsAtStartup>(optionsAtStartup);
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

                    services.AddAuthentication(PassthroughAuthentication.AuthenticationScheme)
                        .AddScheme<PassthroughAuthenticationOptions, PassthroughAuthenticationHandler>(
                            PassthroughAuthentication.AuthenticationScheme,
                            o =>
                            {
                                o.Username = "Anonymous";
                                o.Role = Role.Administrator;
                                o.AllowRemoteNoAuth = optionsAtStartup.Web.AllowRemoteNoAuth;
                            });
                    services.AddAuthorization(o =>
                    {
                        o.AddPolicy(AuthPolicy.Any, p =>
                        {
                            p.AuthenticationSchemes.Add(PassthroughAuthentication.AuthenticationScheme);
                            p.RequireAuthenticatedUser();
                        });
                    });
                    services.AddControllers(opt => opt.Filters.Add(new AuthorizeFilter(AuthPolicy.Any)))
                        .AddApplicationPart(typeof(SessionController).Assembly);
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
