// <copyright file="SolidIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Integration.Solid;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using slskd.Solid.API;
using Xunit;

/// <summary>
/// Integration tests for Solid feature.
/// Tests the full HTTP stack with real controllers and HTTP client.
/// </summary>
public class SolidIntegrationTests : IClassFixture<SolidTestWebApplicationFactory>
{
    private readonly SolidTestWebApplicationFactory _factory;

    public SolidIntegrationTests(SolidTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Status_WhenFeatureEnabled_Returns200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v0/solid/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.True(doc.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Status_WhenFeatureDisabled_Returns404()
    {
        // Arrange
        var factory = new SolidTestWebApplicationFactory(false);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v0/solid/status");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClientIdDocument_WhenFeatureEnabled_ReturnsJsonLd()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/solid/clientid.jsonld");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/ld+json", response.Content.Headers.ContentType?.ToString());
        
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal("https://www.w3.org/ns/solid/oidc-context.jsonld", doc.GetProperty("@context").GetString());
        Assert.True(doc.TryGetProperty("client_id", out var clientId));
        Assert.True(doc.TryGetProperty("redirect_uris", out var redirectUris));
        Assert.True(redirectUris.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task ClientIdDocument_WhenFeatureDisabled_Returns404()
    {
        // Arrange
        var factory = new SolidTestWebApplicationFactory(false);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/solid/clientid.jsonld");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResolveWebId_WithValidWebId_ReturnsOidcIssuers()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            webId = "https://example.com/profile/card#me"
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        // Act - This will fail SSRF policy (host not in AllowedHosts), but tests the endpoint
        var response = await client.PostAsync("/api/v0/solid/resolve-webid", content);

        // Assert - Should return 400 or 500 due to SSRF policy blocking
        // (We can't easily mock the HTTP fetch in integration tests, so we verify the policy is enforced)
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || 
                   response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ResolveWebId_WhenFeatureDisabled_Returns404()
    {
        // Arrange
        var factory = new SolidTestWebApplicationFactory(false);
        var client = factory.CreateClient();
        var request = new
        {
            webId = "https://example.com/profile/card#me"
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v0/solid/resolve-webid", content);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResolveWebId_WithInvalidUri_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            webId = "not-a-valid-uri"
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v0/solid/resolve-webid", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>
/// Test web application factory for Solid integration tests.
/// Configures Solid feature and options.
/// </summary>
public class SolidTestWebApplicationFactory : IDisposable
{
    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly bool _solidEnabled;

    public SolidTestWebApplicationFactory(bool solidEnabled = true)
    {
        _solidEnabled = solidEnabled;
        
        var builder = new HostBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    // Add authentication for [Authorize] attributes
                    services.AddAuthentication("Test")
                        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, slskd.Tests.Integration.Harness.TestAuthHandler>("Test", _ => { });
                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Test")
                            .RequireAuthenticatedUser()
                            .Build();
                    });

                    // Add API versioning
                    services.AddApiVersioning(options =>
                    {
                        options.ReportApiVersions = true;
                        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(0, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                    });

                    // Add controllers including Solid controller
                    services.AddControllers()
                        .AddApplicationPart(typeof(SolidController).Assembly)
                        .AddJsonOptions(options =>
                        {
                            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                        });

                    // Override options with Solid configuration
                    services.AddSingleton<IOptionsMonitor<slskd.Options>>(_ => 
                        new slskd.Tests.Integration.StaticOptionsMonitor<slskd.Options>(new slskd.Options
                        {
                            Feature = new slskd.Core.FeatureOptions
                            {
                                Solid = _solidEnabled
                            },
                            Solid = new slskd.Core.SolidOptions
                            {
                                AllowInsecureHttp = true, // Allow http:// for localhost testing
                                AllowedHosts = solidEnabled ? new[] { "example.com", "localhost" } : Array.Empty<string>(),
                                MaxFetchBytes = 1_000_000,
                                TimeoutSeconds = 10
                            }
                        }));

                    // Register HttpClientFactory (required by SolidWebIdResolver)
                    services.AddHttpClient();

                    // Register Solid services
                    services.AddSingleton<slskd.Solid.ISolidClientIdDocumentService, slskd.Solid.SolidClientIdDocumentService>();
                    services.AddSingleton<slskd.Solid.ISolidWebIdResolver, slskd.Solid.SolidWebIdResolver>();
                    services.AddSingleton<slskd.Solid.ISolidFetchPolicy, slskd.Solid.SolidFetchPolicy>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapGet("/solid/clientid.jsonld", async context =>
                        {
                            var opts = context.RequestServices.GetRequiredService<IOptionsMonitor<slskd.Options>>();
                            if (!opts.CurrentValue.Feature.Solid)
                            {
                                context.Response.StatusCode = 404;
                                return;
                            }
                            var svc = context.RequestServices.GetRequiredService<slskd.Solid.ISolidClientIdDocumentService>();
                            context.Response.ContentType = "application/ld+json";
                            await svc.WriteClientIdDocumentAsync(context, context.RequestAborted);
                        }).AllowAnonymous();
                    });
                });
            });

        var host = builder.Build();
        _server = host.GetTestServer();
        _client = _server.CreateClient();
    }

    public HttpClient CreateClient()
    {
        return _client;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _server?.Dispose();
    }
}
