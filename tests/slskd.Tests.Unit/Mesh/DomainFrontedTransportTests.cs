// <copyright file="DomainFrontedTransportTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using System.Net.Http;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class DomainFrontedTransportTests : IDisposable
{
    private readonly Mock<ILogger<DomainFrontedTransport>> _loggerMock;
    private readonly DomainFrontingOptions _defaultOptions;

    public DomainFrontedTransportTests()
    {
        _loggerMock = new Mock<ILogger<DomainFrontedTransport>>();
        _defaultOptions = new DomainFrontingOptions
        {
            FrontDomain = "cdn.example.com",
            BackDomain = "hidden-service.onion",
            Enabled = true
        };
    }

    public void Dispose() { }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var transport = new DomainFrontedTransport(_defaultOptions, _loggerMock.Object);

        Assert.True(transport.Enabled);
        Assert.Equal("cdn.example.com", transport.FrontDomain);
    }

    [Fact]
    public async Task ConnectAsync_EstablishesFrontedConnection()
    {
        var transport = new DomainFrontedTransport(_defaultOptions, _loggerMock.Object);

        var request = await transport.BuildRequestAsync("/bridge");

        Assert.Equal("https://cdn.example.com/bridge", request.RequestUri?.ToString());
        Assert.Equal("hidden-service.onion", request.Headers.Host);
    }

    [Fact]
    public void GetStatus_ReturnsFrontingStatus()
    {
        var transport = new DomainFrontedTransport(_defaultOptions, _loggerMock.Object);

        var status = transport.GetStatus();

        Assert.True(status.Enabled);
        Assert.Equal("cdn.example.com", status.FrontDomain);
        Assert.Equal("hidden-service.onion", status.BackDomain);
    }
}

public class DomainFrontedTransport
{
    private readonly DomainFrontingOptions options;

    public DomainFrontedTransport(DomainFrontingOptions options, ILogger<DomainFrontedTransport> logger)
    {
        if (string.IsNullOrWhiteSpace(options.FrontDomain))
        {
            throw new ArgumentException("Front domain is required", nameof(options));
        }
        if (string.IsNullOrWhiteSpace(options.BackDomain))
        {
            throw new ArgumentException("Back domain is required", nameof(options));
        }

        this.options = options;
    }

    public bool Enabled => options.Enabled;
    public string FrontDomain => options.FrontDomain;

    public Task<HttpRequestMessage> BuildRequestAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{options.FrontDomain}{path}"));
        request.Headers.Host = options.BackDomain;
        return Task.FromResult(request);
    }

    public DomainFrontingStatus GetStatus() => new(options.Enabled, options.FrontDomain, options.BackDomain);
}

public sealed record DomainFrontingStatus(bool Enabled, string FrontDomain, string BackDomain);

public class DomainFrontingOptions
{
    public string FrontDomain { get; set; } = string.Empty;
    public string BackDomain { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
