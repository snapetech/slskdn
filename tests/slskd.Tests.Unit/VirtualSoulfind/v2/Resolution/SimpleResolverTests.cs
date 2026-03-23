// <copyright file="SimpleResolverTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Resolution;

using Microsoft.Extensions.Options;
using Moq;
using slskd.VirtualSoulfind.Core;
using slskd.VirtualSoulfind.v2.Backends;
using slskd.VirtualSoulfind.v2.Execution;
using slskd.VirtualSoulfind.v2.Planning;
using slskd.VirtualSoulfind.v2.Resolution;
using slskd.VirtualSoulfind.v2.Sources;
using Xunit;

public class SimpleResolverTests
{
    [Fact]
    public async Task ExecutePlanAsync_WhenBackendThrows_ReturnsSanitizedExecutionError()
    {
        var backend = new Mock<IContentBackend>();
        backend.SetupGet(b => b.Type).Returns(ContentBackendType.Http);
        backend
            .Setup(b => b.ValidateCandidateAsync(It.IsAny<SourceCandidate>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var resolver = new SimpleResolver(
            CreateOptionsMonitor(),
            new[] { backend.Object });

        var plan = new TrackAcquisitionPlan
        {
            TrackId = Guid.NewGuid().ToString(),
            Steps = new[]
            {
                new PlanStep
                {
                    Backend = ContentBackendType.Http,
                    Candidates = new[]
                    {
                        new SourceCandidate
                        {
                            Id = Guid.NewGuid().ToString(),
                            ItemId = ContentItemId.NewId(),
                            Backend = ContentBackendType.Http,
                            BackendRef = "https://allowed.com/file.flac",
                        },
                    },
                },
            },
        };

        var result = await resolver.ExecutePlanAsync(plan, CancellationToken.None);

        Assert.Equal(PlanExecutionStatus.Failed, result.Status);
        Assert.DoesNotContain("sensitive detail", result.ErrorMessage ?? string.Empty);
        Assert.Equal("All candidates in step failed", result.ErrorMessage);
    }

    private static IOptionsMonitor<ResolverOptions> CreateOptionsMonitor()
    {
        return Mock.Of<IOptionsMonitor<ResolverOptions>>(options =>
            options.CurrentValue == new ResolverOptions());
    }
}
