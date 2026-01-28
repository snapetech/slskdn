// <copyright file="SolidClientIdDocumentServiceTests.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Solid;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Solid;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;
using Xunit;

public class SolidClientIdDocumentServiceTests
{
    private IOptionsMonitor<slskd.Options> _options;

    private SolidClientIdDocumentService CreateService()
    {
        return new SolidClientIdDocumentService(_options);
    }

    private HttpContext CreateHttpContext(string scheme = "https", string host = "example.com")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString(host);
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task WriteClientIdDocumentAsync_UsesRequestBaseUrl()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Solid = new slskd.Options.SolidOptions
            {
                ClientIdUrl = null,
                RedirectPath = "/solid/callback"
            }
        });
        var service = CreateService();
        var context = CreateHttpContext("https", "slskdn.example.com");

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        Assert.Contains("https://slskdn.example.com/solid/clientid.jsonld", json);
        Assert.Contains("https://slskdn.example.com/solid/callback", json);
        Assert.Equal("application/ld+json", context.Response.ContentType);
    }

    [Fact]
    public async Task WriteClientIdDocumentAsync_UsesConfiguredClientIdUrl()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Solid = new slskd.Options.SolidOptions
            {
                ClientIdUrl = "https://custom.example/clientid.jsonld",
                RedirectPath = "/solid/callback"
            }
        });
        var service = CreateService();
        var context = CreateHttpContext("https", "slskdn.example.com");

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        Assert.Contains("https://custom.example/clientid.jsonld", json);
        Assert.Contains("https://slskdn.example.com/solid/callback", json);
    }

    [Fact]
    public async Task WriteClientIdDocumentAsync_IncludesRequiredFields()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Solid = new slskd.Options.SolidOptions
            {
                RedirectPath = "/solid/callback"
            }
        });
        var service = CreateService();
        var context = CreateHttpContext();

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        // JSON serialization converts @context to "context" in the output
        Assert.Contains("context", json);
        Assert.Contains("client_id", json);
        Assert.Contains("client_name", json);
        Assert.Contains("application_type", json);
        Assert.Contains("redirect_uris", json);
        Assert.Contains("scope", json);
        Assert.Contains("openid webid", json);
    }

    [Fact]
    public async Task WriteClientIdDocumentAsync_SetsContentType()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Solid = new slskd.Options.SolidOptions()
        });
        var service = CreateService();
        var context = CreateHttpContext();

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        Assert.Equal("application/ld+json", context.Response.ContentType);
    }
}
