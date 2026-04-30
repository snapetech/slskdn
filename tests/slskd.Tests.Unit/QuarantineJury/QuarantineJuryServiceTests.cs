// <copyright file="QuarantineJuryServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.QuarantineJury;

using Microsoft.Extensions.Logging.Abstractions;
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
