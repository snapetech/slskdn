// <copyright file="QuarantineJuryServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.QuarantineJury;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using slskd.QuarantineJury;

public sealed class QuarantineJuryServiceTests
{
    [Fact]
    public async Task CreateRequestAsync_StoresValidMinimalEvidenceRequest()
    {
        var service = CreateService();

        var result = await service.CreateRequestAsync(CreateRequest());
        var requests = await service.GetRequestsAsync();

        Assert.True(result.IsValid);
        var request = Assert.Single(requests);
        Assert.Equal("suspected_mismatch", request.LocalReason);
        Assert.Equal(2, request.MinJurorVotes);
    }

    [Fact]
    public async Task CreateRequestAsync_RejectsUnsafeEvidenceReference()
    {
        var service = CreateService();
        var request = CreateRequest();
        request.Evidence[0].Reference = "/home/user/Music/private-track.flac";

        var result = await service.CreateRequestAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains("Evidence references must not include paths, raw hashes, endpoints, or private identifiers.", result.Errors);
    }

    [Fact]
    public async Task SubmitVerdictAsync_RejectsUnselectedJuror()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        var verdict = CreateVerdict(request.Id, "actor:mallory", QuarantineJuryVerdict.ReleaseCandidate);
        Sign(verdict);

        var result = await service.SubmitVerdictAsync(verdict);

        Assert.False(result.IsValid);
        Assert.Contains("Juror is not selected for this request.", result.Errors);
    }

    [Fact]
    public async Task SubmitVerdictAsync_RejectsTamperedSignatureHash()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        var verdict = CreateVerdict(request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        Sign(verdict);
        verdict.Reason = "tampered";

        var result = await service.SubmitVerdictAsync(verdict);

        Assert.False(result.IsValid);
        Assert.Contains("Signature payload hash does not match verdict contents.", result.Errors);
    }

    [Fact]
    public async Task GetAggregateAsync_WaitsForJurorQuorum()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        var verdict = CreateVerdict(request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        Sign(verdict);
        await service.SubmitVerdictAsync(verdict);

        var aggregate = await service.GetAggregateAsync(request.Id);

        Assert.False(aggregate.QuorumReached);
        Assert.Equal(QuarantineJuryVerdict.NeedsManualReview, aggregate.Recommendation);
        Assert.Equal("Waiting for trusted juror quorum: 1/2.", aggregate.Reason);
    }

    [Fact]
    public async Task GetAggregateAsync_RecommendsReleaseCandidateAfterSupermajority()
    {
        var service = CreateService();
        var request = CreateRequest();
        request.MinJurorVotes = 3;
        request.Jurors.Add("actor:carol");
        await service.CreateRequestAsync(request);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        await SubmitSignedVerdict(service, request.Id, "actor:bob", QuarantineJuryVerdict.ReleaseCandidate);
        await SubmitSignedVerdict(service, request.Id, "actor:carol", QuarantineJuryVerdict.UpholdQuarantine);

        var aggregate = await service.GetAggregateAsync(request.Id);

        Assert.True(aggregate.QuorumReached);
        Assert.Equal(QuarantineJuryVerdict.ReleaseCandidate, aggregate.Recommendation);
        Assert.Equal(2, aggregate.VerdictCounts[QuarantineJuryVerdict.ReleaseCandidate]);
        Assert.Equal(new[] { "actor:carol" }, aggregate.DissentingJurors);
    }

    [Fact]
    public async Task SubmitVerdictAsync_ReplacesSameJurorVerdict()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.UpholdQuarantine);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        await SubmitSignedVerdict(service, request.Id, "actor:bob", QuarantineJuryVerdict.ReleaseCandidate);

        var aggregate = await service.GetAggregateAsync(request.Id);

        Assert.Equal(2, aggregate.TotalVerdicts);
        Assert.Equal(QuarantineJuryVerdict.ReleaseCandidate, aggregate.Recommendation);
    }

    [Fact]
    public async Task Constructor_LoadsPersistedRequestsAndVerdicts()
    {
        var storagePath = CreateStoragePath();
        var service = CreateService(storagePath);
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        await SubmitSignedVerdict(service, request.Id, "actor:bob", QuarantineJuryVerdict.ReleaseCandidate);

        var reloaded = CreateService(storagePath);
        var requests = await reloaded.GetRequestsAsync();
        var aggregate = await reloaded.GetAggregateAsync(request.Id);

        Assert.Single(requests);
        Assert.Equal(2, aggregate.TotalVerdicts);
        Assert.Equal(QuarantineJuryVerdict.ReleaseCandidate, aggregate.Recommendation);
    }

    [Fact]
    public async Task RouteRequestAsync_RoutesOnlySelectedSafeJurors()
    {
        var router = new Mock<IPodMessageRouter>();
        router
            .Setup(service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMessageRoutingResult(
                Success: true,
                MessageId: "message-1",
                PodId: "quarantine-jury",
                TargetPeerCount: 1,
                SuccessfullyRoutedCount: 1,
                FailedRoutingCount: 0,
                RoutingDuration: TimeSpan.FromMilliseconds(1)));
        var service = CreateService(CreateStoragePath(), router.Object);
        var request = CreateRequest();
        await service.CreateRequestAsync(request);

        var attempt = await service.RouteRequestAsync(
            request.Id,
            new QuarantineJuryRouteRequest
            {
                TargetJurors = new List<string> { " actor:bob ", "actor:bob" },
            });

        Assert.True(attempt.Success);
        Assert.Equal(new[] { "actor:bob" }, attempt.TargetJurors);
        Assert.Equal(new[] { "actor:bob" }, attempt.RoutedJurors);
        router.Verify(service => service.RouteMessageToPeersAsync(
            It.Is<PodMessage>(message =>
                message.PodId == "quarantine-jury" &&
                message.ChannelId == $"request:{request.Id}" &&
                message.Body.Contains("request-1")),
            It.Is<IEnumerable<string>>(targets => targets.SequenceEqual(new[] { "actor:bob" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RouteRequestAsync_RejectsUnselectedJurorWithoutRouting()
    {
        var router = new Mock<IPodMessageRouter>();
        var service = CreateService(CreateStoragePath(), router.Object);
        var request = CreateRequest();
        await service.CreateRequestAsync(request);

        var attempt = await service.RouteRequestAsync(
            request.Id,
            new QuarantineJuryRouteRequest
            {
                TargetJurors = new List<string> { "actor:mallory" },
            });

        Assert.False(attempt.Success);
        Assert.Equal("Route targets must be selected safe jurors.", attempt.ErrorMessage);
        router.Verify(service => service.RouteMessageToPeersAsync(
            It.IsAny<PodMessage>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RouteRequestAsync_RejectsUnsafeRouteMetadataWithoutRouting()
    {
        var router = new Mock<IPodMessageRouter>();
        var service = CreateService(CreateStoragePath(), router.Object);
        var request = CreateRequest();
        await service.CreateRequestAsync(request);

        var attempt = await service.RouteRequestAsync(
            request.Id,
            new QuarantineJuryRouteRequest
            {
                ChannelId = "/home/user/private-jury-channel",
                TargetJurors = new List<string> { "actor:alice" },
            });

        Assert.False(attempt.Success);
        Assert.Equal("Route metadata must be opaque and safe.", attempt.ErrorMessage);
        router.Verify(service => service.RouteMessageToPeersAsync(
            It.IsAny<PodMessage>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RouteRequestAsync_PersistsRouteAttempts()
    {
        var storagePath = CreateStoragePath();
        var router = new Mock<IPodMessageRouter>();
        router
            .Setup(service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMessageRoutingResult(
                Success: false,
                MessageId: "message-1",
                PodId: "quarantine-jury",
                TargetPeerCount: 2,
                SuccessfullyRoutedCount: 1,
                FailedRoutingCount: 1,
                RoutingDuration: TimeSpan.FromMilliseconds(1),
                ErrorMessage: "partial route failure",
                FailedPeerIds: new[] { "actor:bob" }));
        var service = CreateService(storagePath, router.Object);
        var request = CreateRequest();
        await service.CreateRequestAsync(request);

        await service.RouteRequestAsync(request.Id, new QuarantineJuryRouteRequest());

        var reloaded = CreateService(storagePath);
        var attempts = await reloaded.GetRouteAttemptsAsync(request.Id);
        var attempt = Assert.Single(attempts);
        Assert.False(attempt.Success);
        Assert.Equal(new[] { "actor:bob" }, attempt.FailedJurors);
        Assert.Equal(new[] { "actor:alice" }, attempt.RoutedJurors);
    }

    [Fact]
    public async Task GetReviewAsync_ReturnsManualReviewSurface()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        await SubmitSignedVerdict(service, request.Id, "actor:bob", QuarantineJuryVerdict.ReleaseCandidate);

        var review = await service.GetReviewAsync(request.Id);

        Assert.NotNull(review);
        Assert.Equal(request.Id, review.Request.Id);
        Assert.Equal(QuarantineJuryVerdict.ReleaseCandidate, review.Aggregate.Recommendation);
        Assert.Equal(2, review.Verdicts.Count);
        Assert.True(review.CanAcceptReleaseCandidate);
        Assert.Equal("Release-candidate recommendation can be accepted manually.", review.AcceptanceReason);
    }

    [Fact]
    public async Task AcceptReleaseCandidateAsync_RejectsWhenRecommendationIsNotReleaseCandidate()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.UpholdQuarantine);
        await SubmitSignedVerdict(service, request.Id, "actor:bob", QuarantineJuryVerdict.UpholdQuarantine);

        var result = await service.AcceptReleaseCandidateAsync(
            request.Id,
            new QuarantineJuryAcceptanceRequest
            {
                AcceptedBy = "actor:operator",
            });

        Assert.False(result.IsAccepted);
        Assert.Contains("Only a release-candidate supermajority can be accepted.", result.Errors);
    }

    [Fact]
    public async Task AcceptReleaseCandidateAsync_StoresManualAcceptanceWithoutChangingAggregate()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        await SubmitSignedVerdict(service, request.Id, "actor:bob", QuarantineJuryVerdict.ReleaseCandidate);

        var result = await service.AcceptReleaseCandidateAsync(
            request.Id,
            new QuarantineJuryAcceptanceRequest
            {
                AcceptedBy = "actor:operator",
                Note = "manual local acceptance",
            });
        var aggregate = await service.GetAggregateAsync(request.Id);
        var review = await service.GetReviewAsync(request.Id);

        Assert.True(result.IsAccepted);
        Assert.NotNull(result.Decision);
        Assert.Equal("actor:operator", result.Decision.AcceptedBy);
        Assert.Equal(QuarantineJuryVerdict.ReleaseCandidate, result.Decision.AcceptedRecommendation);
        Assert.Equal(QuarantineJuryVerdict.ReleaseCandidate, aggregate.Recommendation);
        Assert.NotNull(review);
        Assert.NotNull(review.Acceptance);
        Assert.False(review.CanAcceptReleaseCandidate);
        Assert.Equal("Release-candidate recommendation has already been accepted.", review.AcceptanceReason);
    }

    [Fact]
    public async Task AcceptReleaseCandidateAsync_RejectsUnsafeAcceptedByIdentifier()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        await SubmitSignedVerdict(service, request.Id, "actor:bob", QuarantineJuryVerdict.ReleaseCandidate);

        var result = await service.AcceptReleaseCandidateAsync(
            request.Id,
            new QuarantineJuryAcceptanceRequest
            {
                AcceptedBy = "/home/user/private-operator",
            });

        Assert.False(result.IsAccepted);
        Assert.Contains("Accepted-by identifier must be opaque and safe.", result.Errors);
    }

    [Fact]
    public async Task Constructor_LoadsPersistedReviewDecision()
    {
        var storagePath = CreateStoragePath();
        var service = CreateService(storagePath);
        var request = CreateRequest();
        await service.CreateRequestAsync(request);
        await SubmitSignedVerdict(service, request.Id, "actor:alice", QuarantineJuryVerdict.ReleaseCandidate);
        await SubmitSignedVerdict(service, request.Id, "actor:bob", QuarantineJuryVerdict.ReleaseCandidate);
        await service.AcceptReleaseCandidateAsync(
            request.Id,
            new QuarantineJuryAcceptanceRequest
            {
                AcceptedBy = "actor:operator",
            });

        var reloaded = CreateService(storagePath);
        var review = await reloaded.GetReviewAsync(request.Id);

        Assert.NotNull(review);
        Assert.NotNull(review.Acceptance);
        Assert.Equal("actor:operator", review.Acceptance.AcceptedBy);
        Assert.Equal(QuarantineJuryVerdict.ReleaseCandidate, review.Acceptance.AggregateSnapshot.Recommendation);
    }

    private static QuarantineJuryService CreateService()
    {
        return CreateService(CreateStoragePath());
    }

    private static QuarantineJuryService CreateService(string storagePath)
    {
        return new QuarantineJuryService(
            NullLogger<QuarantineJuryService>.Instance,
            storagePath);
    }

    private static QuarantineJuryService CreateService(string storagePath, IPodMessageRouter messageRouter)
    {
        return new QuarantineJuryService(
            NullLogger<QuarantineJuryService>.Instance,
            storagePath,
            messageRouter);
    }

    private static string CreateStoragePath()
    {
        return Path.Combine(Path.GetTempPath(), "slskdn-jury-tests", $"{Guid.NewGuid():N}.json");
    }

    private static QuarantineJuryRequest CreateRequest()
    {
        return new QuarantineJuryRequest
        {
            Id = "request-1",
            LocalReason = "suspected_mismatch",
            Jurors = new List<string> { "actor:alice", "actor:bob" },
            Evidence = new List<QuarantineJuryEvidence>
            {
                new()
                {
                    Type = QuarantineJuryEvidenceType.ContentId,
                    Reference = "content:rare-track",
                    Summary = "Quarantined after local signature mismatch.",
                },
                new()
                {
                    Type = QuarantineJuryEvidenceType.SongIdVerdict,
                    Reference = "songid:run-1",
                    Summary = "needs_manual_review",
                },
            },
        };
    }

    private static async Task SubmitSignedVerdict(
        QuarantineJuryService service,
        string requestId,
        string juror,
        QuarantineJuryVerdict verdictValue)
    {
        var verdict = CreateVerdict(requestId, juror, verdictValue);
        Sign(verdict);
        var result = await service.SubmitVerdictAsync(verdict);
        Assert.True(result.IsValid);
    }

    private static QuarantineJuryVerdictRecord CreateVerdict(
        string requestId,
        string juror,
        QuarantineJuryVerdict verdict)
    {
        return new QuarantineJuryVerdictRecord
        {
            RequestId = requestId,
            Juror = juror,
            Verdict = verdict,
            Reason = "local evidence reviewed",
            Evidence = new List<QuarantineJuryEvidence>
            {
                new()
                {
                    Type = QuarantineJuryEvidenceType.SpectralSummary,
                    Reference = "spectral:lane-summary-1",
                    Summary = "Juror has matching spectral summary.",
                },
            },
        };
    }

    private static void Sign(QuarantineJuryVerdictRecord verdict)
    {
        verdict.Signature = new QuarantineJurySignature
        {
            Signer = verdict.Juror,
            PayloadHash = verdict.ComputePayloadHash(),
            Value = "signed-juror-verdict",
        };
    }
}
