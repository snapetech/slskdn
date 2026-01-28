// <copyright file="SolidWebIdResolverTests.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Solid;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Solid;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;
using Xunit;

public class SolidWebIdResolverTests
{
    private readonly Mock<ILogger<SolidWebIdResolver>> _loggerMock = new();
    private readonly Mock<ISolidFetchPolicy> _policyMock = new();
    private IOptionsMonitor<slskd.Options> _options;
    private HttpClient _httpClient;
    private IHttpClientFactory _httpFactory;

    private SolidWebIdResolver CreateResolver()
    {
        _options ??= new TestOptionsMonitor(new slskd.Options
        {
            Solid = new slskd.Options.SolidOptions
            {
                AllowedHosts = new[] { "example.com" },
                TimeoutSeconds = 10,
                MaxFetchBytes = 1_000_000
            }
        });

        _httpClient ??= new HttpClient(new TestHttpMessageHandler());
        _httpFactory ??= new TestHttpClientFactory(_httpClient);

        return new SolidWebIdResolver(_httpFactory, _policyMock.Object, _options, _loggerMock.Object);
    }

    [Fact]
    public async Task ResolveAsync_CallsPolicyValidate()
    {
        var resolver = CreateResolver();
        var webId = new Uri("https://example.com/profile#me");
        _policyMock.Setup(x => x.ValidateAsync(webId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Will fail on HTTP call, but policy should be called first
        await Assert.ThrowsAnyAsync<Exception>(() => resolver.ResolveAsync(webId, CancellationToken.None));

        _policyMock.Verify(x => x.ValidateAsync(webId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_PolicyThrows_PropagatesException()
    {
        var resolver = CreateResolver();
        var webId = new Uri("https://example.com/profile#me");
        _policyMock.Setup(x => x.ValidateAsync(webId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blocked"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync(webId, CancellationToken.None));

        Assert.Equal("Blocked", ex.Message);
    }

    // Note: Full integration tests with real HTTP responses would require a test HTTP server
    // or more sophisticated mocking. These tests verify the policy integration and basic structure.
    // For CI-safe tests, we'd use a fake Solid server as mentioned in the implementation map.
}

// Simple test HTTP message handler for basic tests
internal class TestHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Return a basic response - actual tests would need more sophisticated mocking
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("@prefix solid: <http://www.w3.org/ns/solid/terms#> .\n<#me> solid:oidcIssuer <https://issuer.example> .", Encoding.UTF8, "text/turtle")
        };
        return Task.FromResult(response);
    }
}
