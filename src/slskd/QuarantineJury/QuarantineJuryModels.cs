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
