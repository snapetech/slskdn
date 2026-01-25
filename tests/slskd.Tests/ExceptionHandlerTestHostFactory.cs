// <copyright file="ExceptionHandlerTestHostFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Asp.Versioning;
using slskd;
using slskd.Authentication;
using slskd.Core.Security;

/// <summary>
/// Test host with UseExceptionHandler (same logic as Program.cs) and ThrowController.
/// Uses UseEnvironment("Test") so IsDevelopment() is false — Production-like behavior for PR-05.
/// </summary>
public class ExceptionHandlerTestHostFactory : WebApplicationFactory<ProgramStub>
{
    protected override IHostBuilder CreateHostBuilder()
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var contentRoot = Path.Combine(solutionRoot, "tests", "slskd.Tests");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(Path.Combine(solutionRoot, "slskd.Tests"));

        return new HostBuilder()
            .UseContentRoot(contentRoot)
            .UseEnvironment("Test")
            .ConfigureWebHostDefaults(web =>
            {
                web.UseTestServer();
                web.UseContentRoot(contentRoot);
                web.ConfigureServices(services =>
                {
                    var optionsAtStartup = new OptionsAtStartup();
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
                        .AddApplicationPart(typeof(ThrowController).Assembly);
                });
                web.Configure(app =>
                {
                    // PR-05: same logic as Program.cs — ProblemDetails, traceId, generic detail when !IsDevelopment
                    // §11: FeatureNotImplementedException → 501 Not Implemented
                    app.UseExceptionHandler(a => a.Run(async context =>
                    {
                        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
                        if (feature?.Error != null)
                        {
                            var ex = feature.Error;
                            var traceId = context.TraceIdentifier;

                            if (!context.Response.HasStarted)
                            {
                                int status;
                                string title;
                                string detail;
                                if (ex is slskd.FeatureNotImplementedException fe)
                                {
                                    status = 501;
                                    title = "Not Implemented";
                                    detail = fe.Message;
                                }
                                else
                                {
                                    var env = context.RequestServices.GetService<IWebHostEnvironment>();
                                    var isDev = env?.IsDevelopment() == true;
                                    status = 500;
                                    title = "Internal Server Error";
                                    detail = isDev ? ex.ToString() : "An unexpected error occurred.";
                                }
                                var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
                                problem.Extensions["traceId"] = traceId;
                                context.Response.StatusCode = status;
                                context.Response.ContentType = "application/problem+json";
                                await context.Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(problem));
                            }
                        }
                    }));
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e => e.MapControllers());
                });
            });
    }
}
