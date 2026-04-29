// <copyright file="SolidClientIdDocumentServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Solid;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        return new SolidClientIdDocumentService(_options, NullLogger<SolidClientIdDocumentService>.Instance);
    }

    private (HttpContext Context, MemoryStream ResponseBody) CreateHttpContext(string scheme = "https", string host = "example.com")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString(host);
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        return (context, responseBody);
    }

    [Fact]
    public async Task WriteClientIdDocumentAsync_Returns404WhenClientIdUrlNotConfigured()
    {
        // HARDENING-2026-04-20 H2: deriving the client_id from Request.Host leaked whichever
        // hostname the caller reached us on. When ClientIdUrl is unset we now refuse the request.
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Solid = new slskd.Options.SolidOptions
            {
                ClientIdUrl = null,
                RedirectPath = "/solid/callback"
            }
        });
        var service = CreateService();
        var (context, responseBody) = CreateHttpContext("https", "slskdn.example.com");
        using var _ = responseBody;

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
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
        var (context, responseBody) = CreateHttpContext("https", "slskdn.example.com");
        using var _ = responseBody;

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        // HARDENING-2026-04-20 H2: redirect_uri must be derived from the configured client_id's origin,
        // not from the request host. The request reached us at slskdn.example.com but the document must
        // not mention that host.
        Assert.Contains("https://custom.example/clientid.jsonld", json);
        Assert.Contains("https://custom.example/solid/callback", json);
        Assert.DoesNotContain("slskdn.example.com", json);
    }

    [Fact]
    public async Task WriteClientIdDocumentAsync_IncludesRequiredFields()
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
        var (context, responseBody) = CreateHttpContext();
        using var _ = responseBody;

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        Assert.Contains("\"@context\"", json);
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
            Solid = new slskd.Options.SolidOptions
            {
                ClientIdUrl = "https://custom.example/clientid.jsonld"
            }
        });
        var service = CreateService();
        var (context, responseBody) = CreateHttpContext();
        using var _ = responseBody;

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        Assert.Equal("application/ld+json", context.Response.ContentType);
    }

    [Fact]
    public async Task WriteClientIdDocumentAsync_Returns500WhenClientIdUrlIsMalformed()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Solid = new slskd.Options.SolidOptions
            {
                ClientIdUrl = "not a url"
            }
        });
        var service = CreateService();
        var (context, responseBody) = CreateHttpContext();
        using var _ = responseBody;

        await service.WriteClientIdDocumentAsync(context, CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }
}
