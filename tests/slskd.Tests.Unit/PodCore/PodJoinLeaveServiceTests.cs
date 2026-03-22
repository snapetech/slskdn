// <copyright file="PodJoinLeaveServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.PodCore;
using Xunit;

public class PodJoinLeaveServiceTests
{
    [Fact]
    public async Task RequestJoinAsync_WhenDependencyThrows_ReturnsSanitizedError()
    {
        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.GetPodAsync("pod-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var service = CreateService(podService: podService);

        var result = await service.RequestJoinAsync(
            new PodJoinRequest("pod-1", "peer-1", "member", "pub", 1, "long-signature-value"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Failed to process join request", result.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessJoinAcceptanceAsync_WhenDependencyThrows_ReturnsSanitizedError()
    {
        var membershipVerifier = new Mock<IPodMembershipVerifier>();
        membershipVerifier
            .Setup(service => service.HasRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var service = CreateService(membershipVerifier: membershipVerifier);

        var result = await service.ProcessJoinAcceptanceAsync(
            new PodJoinAcceptance("pod-1", "peer-1", "member", "owner-1", "pub", 1, "long-signature-value"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Failed to process join acceptance", result.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", result.ErrorMessage);
    }

    [Fact]
    public async Task RequestLeaveAsync_WhenDependencyThrows_ReturnsSanitizedError()
    {
        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.GetMembersAsync("pod-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var service = CreateService(podService: podService);

        var result = await service.RequestLeaveAsync(
            new PodLeaveRequest("pod-1", "peer-1", "pub", 1, "long-signature-value"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Failed to process leave request", result.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessLeaveAcceptanceAsync_WhenDependencyThrows_ReturnsSanitizedError()
    {
        var membershipVerifier = new Mock<IPodMembershipVerifier>();
        membershipVerifier
            .Setup(service => service.HasRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var service = CreateService(membershipVerifier: membershipVerifier);

        var result = await service.ProcessLeaveAcceptanceAsync(
            new PodLeaveAcceptance("pod-1", "peer-1", "owner-1", "pub", 1, "long-signature-value"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Failed to process leave acceptance", result.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", result.ErrorMessage);
    }

    [Fact]
    public async Task GetPendingJoinRequestsAsync_WhenPodHasNoRequests_DoesNotCreateBucket()
    {
        var service = CreateService();

        var result = await service.GetPendingJoinRequestsAsync("pod-1", CancellationToken.None);

        Assert.Empty(result);

        var field = typeof(PodJoinLeaveService).GetField("_pendingJoinRequests", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var pending = Assert.IsType<ConcurrentDictionary<string, ConcurrentBag<PodJoinRequest>>>(field!.GetValue(service));
        Assert.False(pending.ContainsKey("pod-1"));
    }

    [Fact]
    public async Task CancelJoinRequestAsync_MatchesPeerIdCaseInsensitively()
    {
        var service = CreateService();
        var field = typeof(PodJoinLeaveService).GetField("_pendingJoinRequests", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var pending = Assert.IsType<ConcurrentDictionary<string, ConcurrentBag<PodJoinRequest>>>(field!.GetValue(service));
        pending["pod-1"] = new ConcurrentBag<PodJoinRequest>(new[]
        {
            new PodJoinRequest("pod-1", "Peer-1", "member", "pub", 1, "sig"),
        });

        var cancelled = await service.CancelJoinRequestAsync("pod-1", "peer-1", CancellationToken.None);

        Assert.True(cancelled);
        var remaining = await service.GetPendingJoinRequestsAsync("pod-1", CancellationToken.None);
        Assert.Empty(remaining);
    }

    private static PodJoinLeaveService CreateService(
        Mock<IPodService>? podService = null,
        Mock<IPodMembershipService>? membershipService = null,
        Mock<IPodMembershipVerifier>? membershipVerifier = null)
    {
        return new PodJoinLeaveService(
            Mock.Of<ILogger<PodJoinLeaveService>>(),
            (podService ?? new Mock<IPodService>()).Object,
            (membershipService ?? new Mock<IPodMembershipService>()).Object,
            (membershipVerifier ?? new Mock<IPodMembershipVerifier>()).Object,
            Mock.Of<IOptionsMonitor<PodJoinOptions>>(options => options.CurrentValue == new PodJoinOptions()));
    }
}
