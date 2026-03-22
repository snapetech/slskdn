// <copyright file="WebDavBackendTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Backends;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using slskd.VirtualSoulfind.Core;
using slskd.VirtualSoulfind.v2.Backends;
using slskd.VirtualSoulfind.v2.Sources;
using Xunit;

public class WebDavBackendTests
{
    [Fact]
    public async Task ValidateCandidate_WhenHttpClientThrows_ReturnsSanitizedError()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("sensitive detail"));
        var backend = CreateBackend(handler: handler);
        var candidate = new SourceCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ItemId = ContentItemId.NewId(),
            Backend = ContentBackendType.WebDav,
            BackendRef = "https://allowed.com/file.flac",
            TrustScore = 0.7f,
            ExpectedQuality = 0.8f,
        };

        var result = await backend.ValidateCandidateAsync(candidate, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("WebDAV validation failed", result.InvalidityReason);
        Assert.DoesNotContain("sensitive detail", result.InvalidityReason);
    }

    private static WebDavBackend CreateBackend(WebDavBackendOptions options = null, HttpMessageHandler handler = null)
    {
        options ??= new WebDavBackendOptions
        {
            Enabled = true,
            DomainAllowlist = new List<string> { "allowed.com" },
        };

        var httpClient = handler == null
            ? new HttpClient(new slskd.Tests.Unit.VirtualSoulfind.v2.Backends.StubHttpMessageHandler())
            : new HttpClient(handler);
        var httpFactory = new slskd.Tests.Unit.VirtualSoulfind.v2.Backends.TestHttpClientFactory(httpClient);
        var optionsMonitor = new Mock<IOptionsMonitor<WebDavBackendOptions>>();
        optionsMonitor.Setup(o => o.CurrentValue).Returns(options);

        return new WebDavBackend(httpFactory, optionsMonitor.Object);
    }
}
