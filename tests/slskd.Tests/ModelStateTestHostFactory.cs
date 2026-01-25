// <copyright file="ModelStateTestHostFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using System.IO;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
/// Test host with EnforceSecurity=true and SuppressModelStateInvalidFilter=false (PR-07).
/// Invalid model → 400 with ValidationProblemDetails.
/// </summary>
public class ModelStateTestHostFactory : WebApplicationFactory<ProgramStub>
{
    protected override IHostBuilder CreateHostBuilder()
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var contentRoot = Path.Combine(solutionRoot, "tests", "slskd.Tests");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(Path.Combine(solutionRoot, "slskd.Tests"));

        var optionsAtStartup = new OptionsAtStartup
        {
            Web = new slskd.Options.WebOptions { EnforceSecurity = true },
            Headless = false,
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
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e => e.MapControllers());
                });
            });
    }
}
