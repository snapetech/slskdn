// <copyright file="QuarantineJuryModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.QuarantineJury;

using System.Security.Cryptography;
using System.Text;

public enum QuarantineJuryEvidenceType
{
    ContentId = 0,
    PerceptualHash = 1,
    SpectralSummary = 2,
    SongIdVerdict = 3,
}

public enum QuarantineJuryVerdict
{
    NeedsManualReview = 0,
    UpholdQuarantine = 1,
    ReleaseCandidate = 2,
}

public sealed class QuarantineJuryEvidence
{
    public QuarantineJuryEvidenceType Type { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}

public sealed class QuarantineJuryRequest
{
    public string Id { get; set; } = string.Empty;

    public string LocalReason { get; set; } = string.Empty;

    public List<QuarantineJuryEvidence> Evidence { get; set; } = new();

    public List<string> Jurors { get; set; } = new();

    public int MinJurorVotes { get; set; } = 2;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class QuarantineJuryVerdictRecord
{
    public string Id { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;

    public string Juror { get; set; } = string.Empty;

    public QuarantineJuryVerdict Verdict { get; set; } = QuarantineJuryVerdict.NeedsManualReview;

    public string Reason { get; set; } = string.Empty;

    public List<QuarantineJuryEvidence> Evidence { get; set; } = new();

    public QuarantineJurySignature Signature { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string ComputePayloadHash()
    {
        var payload = string.Join(
            "\n",
            RequestId.Trim(),
            Juror.Trim(),
            Verdict.ToString(),
            Reason.Trim(),
            string.Join(
                "|",
                Evidence
                    .OrderBy(evidence => evidence.Type)
                    .ThenBy(evidence => evidence.Reference, StringComparer.Ordinal)
                    .Select(evidence => $"{evidence.Type}:{evidence.Reference.Trim()}:{evidence.Summary.Trim()}")));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}

public sealed class QuarantineJurySignature
{
    public string Signer { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class QuarantineJuryValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; set; } = new();
}

public sealed class QuarantineJuryAggregate
{
    public string RequestId { get; set; } = string.Empty;

    public QuarantineJuryVerdict Recommendation { get; set; } = QuarantineJuryVerdict.NeedsManualReview;

    public int TotalVerdicts { get; set; }

    public int RequiredVotes { get; set; }

    public Dictionary<QuarantineJuryVerdict, int> VerdictCounts { get; set; } = new();

    public List<string> DissentingJurors { get; set; } = new();

    public bool QuorumReached { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public sealed class QuarantineJuryReview
{
    public QuarantineJuryRequest Request { get; set; } = new();

    public QuarantineJuryAggregate Aggregate { get; set; } = new();

    public List<QuarantineJuryVerdictRecord> Verdicts { get; set; } = new();

    public List<QuarantineJuryRouteAttempt> RouteAttempts { get; set; } = new();

    public QuarantineJuryReviewDecision? Acceptance { get; set; }

    public bool CanAcceptReleaseCandidate { get; set; }

    public string AcceptanceReason { get; set; } = string.Empty;
}

public sealed class QuarantineJuryAuditReport
{
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public int RequestCount { get; set; }

    public int AcceptedReleaseCandidateCount { get; set; }

    public int PendingReleaseCandidateCount { get; set; }

    public int PendingManualReviewCount { get; set; }

    public int UpholdQuarantineCount { get; set; }

    public int StaleRequestCount { get; set; }

    public List<QuarantineJuryAuditEntry> Entries { get; set; } = new();
}

public sealed class QuarantineJuryAuditEntry
{
    public string RequestId { get; set; } = string.Empty;

    public string LocalReason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public int EvidenceCount { get; set; }

    public int JurorCount { get; set; }

    public int VerdictCount { get; set; }

    public int RequiredVotes { get; set; }

    public QuarantineJuryVerdict Recommendation { get; set; } = QuarantineJuryVerdict.NeedsManualReview;

    public bool QuorumReached { get; set; }

    public bool HasAcceptance { get; set; }

    public bool CanAcceptReleaseCandidate { get; set; }

    public bool HasRouteAttempts { get; set; }

    public bool HasFailedRouteAttempts { get; set; }

    public bool IsStale { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public List<string> DissentingJurors { get; set; } = new();
}

public sealed class QuarantineJuryAcceptanceRequest
{
    public string AcceptedBy { get; set; } = "local-user";

    public string Note { get; set; } = string.Empty;
}

public sealed class QuarantineJuryReviewDecision
{
    public string Id { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;

    public string AcceptedBy { get; set; } = string.Empty;

    public QuarantineJuryVerdict AcceptedRecommendation { get; set; } = QuarantineJuryVerdict.NeedsManualReview;

    public string Note { get; set; } = string.Empty;

    public QuarantineJuryAggregate AggregateSnapshot { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class QuarantineJuryAcceptanceResult
{
    public bool IsAccepted => Errors.Count == 0 && Decision != null;

    public List<string> Errors { get; set; } = new();

    public QuarantineJuryReviewDecision? Decision { get; set; }
}

public sealed class QuarantineJuryReleasePackageResult
{
    public bool IsReady => Errors.Count == 0 && Package != null;

    public List<string> Errors { get; set; } = new();

    public QuarantineJuryReleasePackage? Package { get; set; }
}

public sealed class QuarantineJuryReleasePackage
{
    public string Type { get; set; } = "slskdn.quarantine-jury.release-package.v1";

    public string Version { get; set; } = "1.0";

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public string RequestId { get; set; } = string.Empty;

    public string LocalReason { get; set; } = string.Empty;

    public DateTimeOffset RequestCreatedAt { get; set; }

    public List<QuarantineJuryEvidence> RequestEvidence { get; set; } = new();

    public List<string> Jurors { get; set; } = new();

    public QuarantineJuryAggregate CurrentAggregate { get; set; } = new();

    public QuarantineJuryReviewDecision Acceptance { get; set; } = new();

    public List<QuarantineJuryVerdictRecord> Verdicts { get; set; } = new();

    public List<QuarantineJuryRouteAttempt> RouteAttempts { get; set; } = new();

    public List<string> Warnings { get; set; } = new();

    public bool MutatesLocalQuarantineState { get; set; }
}

public sealed class QuarantineJuryRouteRequest
{
    public string SenderPeerId { get; set; } = "local-quarantine-jury";

    public string PodId { get; set; } = "quarantine-jury";

    public string ChannelId { get; set; } = string.Empty;

    public List<string> TargetJurors { get; set; } = new();
}

public sealed class QuarantineJuryRouteAttempt
{
    public string Id { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;

    public string MessageId { get; set; } = string.Empty;

    public string PodId { get; set; } = string.Empty;

    public string ChannelId { get; set; } = string.Empty;

    public List<string> TargetJurors { get; set; } = new();

    public List<string> RoutedJurors { get; set; } = new();

    public List<string> FailedJurors { get; set; } = new();

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
