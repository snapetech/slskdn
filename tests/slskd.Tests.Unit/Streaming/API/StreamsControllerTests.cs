// <copyright file="StreamsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Streaming.API;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd;
using slskd.Sharing;
using slskd.Streaming;
using Xunit;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;

public class StreamsControllerTests
{
    private readonly Mock<IContentLocator> _locatorMock = new();
    private readonly Mock<IShareTokenService> _tokensMock = new();
    private readonly Mock<ISharingService> _sharingMock = new();
    private readonly Mock<IStreamSessionLimiter> _limiterMock = new();
    private IOptionsMonitor<slskd.Options> _options = new TestOptionsMonitor(new slskd.Options
    {
        Feature = new slskd.Options.FeatureOptions { Streaming = true },
        Soulseek = new slskd.Options.SoulseekOptions { Username = "alice" }
    });

    private StreamsController CreateController()
    {
        return new StreamsController(
            _locatorMock.Object,
            _tokensMock.Object,
            _sharingMock.Object,
            _limiterMock.Object,
            _options);
    }

    private void SetContext(StreamsController c, string? range = null, string? authBearer = null, bool authenticated = false)
    {
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.HttpContext.Request.Method = "GET";
        if (range != null) c.HttpContext.Request.Headers.Range = range;
        if (authBearer != null) c.HttpContext.Request.Headers.Authorization = "Bearer " + authBearer;
        if (authenticated)
            c.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "u") }, "Test"));
    }

    [Fact]
    public async Task Get_FeatureStreamingDisabled_ReturnsNotFound()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Feature = new slskd.Options.FeatureOptions { Streaming = false },
            Soulseek = new slskd.Options.SoulseekOptions { Username = "alice" }
        });
        var c = CreateController();
        SetContext(c, authenticated: true);

        var r = await c.Get("c1", null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
        _locatorMock.Verify(x => x.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_MultiRange_ReturnsBadRequest()
    {
        var controller = CreateController();
        SetContext(controller, range: "bytes=0-499,500-999", authenticated: true);

        var r = await controller.Get("c1", null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("Multiple byte ranges are not supported.", bad.Value);
        _locatorMock.Verify(x => x.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_NoAuth_ReturnsUnauthorized()
    {
        var controller = CreateController();
        SetContext(controller);

        var r = await controller.Get("c1", null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(r);
    }

    [Fact]
    public async Task Get_InvalidToken_ReturnsUnauthorized()
    {
        var controller = CreateController();
        SetContext(controller, authBearer: "share:bad-token");
        _tokensMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ShareTokenClaims?)null);

        var r = await controller.Get("c1", null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(r);
    }

    [Fact]
    public async Task Get_ValidToken_AllowStreamFalse_ReturnsUnauthorized()
    {
        var controller = CreateController();
        SetContext(controller, authBearer: "share:tok");
        _tokensMock.Setup(x => x.ValidateAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareTokenClaims("s1", "c1", null, false, true, 1, DateTimeOffset.UtcNow.AddHours(1)));

        var r = await controller.Get("c1", null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(r);
    }

    [Fact]
    public async Task Get_ValidToken_CollectionDoesNotContainContentId_ReturnsNotFound()
    {
        var controller = CreateController();
        SetContext(controller, authBearer: "tok");
        _tokensMock.Setup(x => x.ValidateAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareTokenClaims("s1", "c1", null, true, true, 1, DateTimeOffset.UtcNow.AddHours(1)));
        _sharingMock.Setup(x => x.GetCollectionItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CollectionItem> { new() { ContentId = "other" } });

        var r = await controller.Get("c1", null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Get_ResolveReturnsNull_ReturnsNotFound()
    {
        var controller = CreateController();
        SetContext(controller, authenticated: true);
        _locatorMock.Setup(x => x.Resolve("c1", It.IsAny<CancellationToken>())).Returns((ResolvedContent?)null);

        var r = await controller.Get("c1", null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Get_LimiterRejects_Returns429()
    {
        var controller = CreateController();
        SetContext(controller, authenticated: true);
        _locatorMock.Setup(x => x.Resolve("c1", It.IsAny<CancellationToken>()))
            .Returns(new ResolvedContent("/tmp/x", 100, "audio/mpeg"));
        _limiterMock.Setup(x => x.TryAcquire(It.IsAny<string>(), It.IsAny<int>())).Returns(false);

        var r = await controller.Get("c1", null, CancellationToken.None);

        var sc = Assert.IsType<ObjectResult>(r);
        Assert.Equal(429, sc.StatusCode);
        Assert.Equal("Too many concurrent streams.", sc.Value);
    }

    [Fact]
    public async Task Get_NormalAuth_Success_ReturnsFileWithRange()
    {
        var path = Path.Combine(Path.GetTempPath(), "StreamsCtrl_" + Guid.NewGuid().ToString("N")[..8] + ".bin");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 });
            var controller = CreateController();
            SetContext(controller, authenticated: true);
            _locatorMock.Setup(x => x.Resolve("c1", It.IsAny<CancellationToken>()))
                .Returns(new ResolvedContent(path, 3, "application/octet-stream"));
            _limiterMock.Setup(x => x.TryAcquire("user:alice", 5)).Returns(true);

            var r = await controller.Get("c1", null, CancellationToken.None);

            var file = Assert.IsType<FileStreamResult>(r);
            Assert.Equal("application/octet-stream", file.ContentType);
            Assert.True(file.EnableRangeProcessing);
            var buf = new byte[4];
            var n = file.FileStream.Read(buf, 0, 4);
            Assert.Equal(3, n);
            Assert.Equal(1, buf[0]);
            Assert.Equal(2, buf[1]);
            Assert.Equal(3, buf[2]);
            file.FileStream.Dispose();
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task Get_TokenAuth_StripsSharePrefix_AndSucceeds()
    {
        var path = Path.Combine(Path.GetTempPath(), "StreamsCtrl_" + Guid.NewGuid().ToString("N")[..8] + ".bin");
        var collectionId = Guid.NewGuid();
        try
        {
            await File.WriteAllBytesAsync(path, new byte[] { 4, 5 });
            var controller = CreateController();
            SetContext(controller, authBearer: "share:the-jwt");
            _tokensMock.Setup(x => x.ValidateAsync("the-jwt", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShareTokenClaims("s1", collectionId.ToString(), null, true, true, 2, DateTimeOffset.UtcNow.AddHours(1)));
            _sharingMock.Setup(x => x.GetCollectionItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CollectionItem> { new() { ContentId = "c1" } });
            _locatorMock.Setup(x => x.Resolve("c1", It.IsAny<CancellationToken>()))
                .Returns(new ResolvedContent(path, 2, "audio/flac"));
            _limiterMock.Setup(x => x.TryAcquire("s1", 2)).Returns(true);

            var r = await controller.Get("c1", null, CancellationToken.None);

            var file = Assert.IsType<FileStreamResult>(r);
            Assert.Equal("audio/flac", file.ContentType);
            _tokensMock.Verify(x => x.ValidateAsync("the-jwt", It.IsAny<CancellationToken>()), Times.Once);
            file.FileStream.Dispose();
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
