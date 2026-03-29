// <copyright file="PeerVerificationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous.Security;

using Microsoft.Extensions.Logging;
using Moq;
using slskd.DhtRendezvous.Security;
using Soulseek;
using Xunit;

public class PeerVerificationServiceTests
{
    [Fact]
    public async Task VerifyPeerAsync_WhenSoulseekThrows_ReturnsSanitizedFailure()
    {
        var client = new Mock<ISoulseekClient>();
        client
            .Setup(c => c.GetUserInfoAsync("alice", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var service = new PeerVerificationService(
            Mock.Of<ILogger<PeerVerificationService>>(),
            client.Object);

        var result = await service.VerifyPeerAsync("alice", "challenge", CancellationToken.None);

        Assert.False(result.IsVerified);
        Assert.False(result.IsPartial);
        Assert.Equal("Verification failed", result.FailureReason);
        Assert.DoesNotContain("sensitive detail", result.FailureReason);
    }
}
