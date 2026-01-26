// <copyright file="MdnsAdvertiserTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Identity;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Identity;
using Xunit;

public class MdnsAdvertiserTests : IDisposable
{
    private readonly Mock<ILogger<MdnsAdvertiser>> _logMock = new();
    private MdnsAdvertiser? _advertiser;

    public void Dispose()
    {
        _advertiser?.Dispose();
    }

    [Fact]
    public async Task StartAsync_WithValidParams_DoesNotThrow()
    {
        _advertiser = new MdnsAdvertiser(_logMock.Object);
        var properties = new Dictionary<string, string> { ["test"] = "value" };

        // This may fail on systems without network or permissions, so we catch and verify it at least attempts
        try
        {
            await _advertiser.StartAsync("TestService", "_slskdn._tcp", 8080, properties, CancellationToken.None);
            // If it succeeds, verify it can be stopped
            _advertiser.Stop();
        }
        catch (System.Net.Sockets.SocketException)
        {
            // Expected on systems without network access or permissions
        }
        catch (Exception ex)
        {
            // Other exceptions are unexpected
            Assert.True(false, $"Unexpected exception: {ex}");
        }
    }

    [Fact]
    public void Stop_WhenNotStarted_DoesNotThrow()
    {
        _advertiser = new MdnsAdvertiser(_logMock.Object);
        _advertiser.Stop(); // Should not throw
    }

    [Fact]
    public void Dispose_WhenNotStarted_DoesNotThrow()
    {
        _advertiser = new MdnsAdvertiser(_logMock.Object);
        _advertiser.Dispose(); // Should not throw
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _advertiser = new MdnsAdvertiser(_logMock.Object);
        _advertiser.Dispose();
        _advertiser.Dispose(); // Should not throw
    }
}
