// <copyright file="TestHostFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Text;
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
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using slskd;
using slskd.Authentication;
using slskd.Core.API;
using slskd.Core.Security;

/// <summary>
/// Minimal test host factory that boots the API with test settings.
/// Used for smoke and regression tests (e.g. GET /api/v0/session/enabled).
/// </summary>
public class TestHostFactory : WebApplicationFactory<ProgramStub>
{
    protected override IHostBuilder CreateHostBuilder()
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var contentRoot = Path.Combine(solutionRoot, "tests", "slskd.Tests");
        Directory.CreateDirectory(contentRoot);
        // Base WebApplicationFactory resolves content root to solutionRoot/slskd.Tests; ensure it exists.
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
                    {
                        var opts = sp.GetRequiredService<IOptions<slskd.Options>>();
                        return new StaticOptionsSnapshot(opts.Value);
                    });
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

                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer();
                    services.AddAuthorization(o =>
                        o.AddPolicy(AuthPolicy.Any, p => p.RequireAuthenticatedUser()));
                    services.AddControllers(o =>
                        o.Filters.Add(new AuthorizeFilter(AuthPolicy.Any)))
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

/// <summary>Marker for WebApplicationFactory; host is built in CreateHostBuilder.</summary>
public class ProgramStub
{
    public static IHostBuilder CreateHostBuilder(string[] args) => new HostBuilder();
}

internal class StaticOptionsSnapshot : IOptionsSnapshot<slskd.Options>
{
    private readonly slskd.Options _value;
    public StaticOptionsSnapshot(slskd.Options value) => _value = value;
    public slskd.Options Value => _value;
    public slskd.Options Get(string? name) => _value;
}

internal class StubSecurityService : ISecurityService
{
    public JwtSecurityToken GenerateJwt(string username, Role role, int? ttl = null) =>
        new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: Array.Empty<Claim>(),
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-key-must-be-32-bytes!!!!!!!")),
                "HS256"));

    public (string Name, Role Role) AuthenticateWithApiKey(string key, IPAddress callerIpAddress) =>
        ("test", Role.Administrator);
}
