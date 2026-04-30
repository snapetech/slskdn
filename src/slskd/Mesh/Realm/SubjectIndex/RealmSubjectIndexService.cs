// <copyright file="RealmSubjectIndexService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh.Realm.SubjectIndex;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Governance;
using slskd.SocialFederation;

public sealed class RealmSubjectIndexService : IRealmSubjectIndexService
{
    private const string ProposalDocumentType = "realm-subject-index.proposal";
    private const string ReviewDocumentType = "realm-subject-index.review";
    private const int MaxProposalNoteLength = 512;

    private static readonly HashSet<string> AllowedEvidenceSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttps,
        Uri.UriSchemeHttp,
        "mbid",
        "workref",
        "songid",
    };

    private readonly IRealmService _realmService;
    private readonly ILogger<RealmSubjectIndexService> _logger;
    private readonly IRealmAwareGovernanceClient? _governanceClient;
    private readonly ConcurrentDictionary<string, RealmSubjectIndex> _indexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RealmSubjectIndexProposal> _proposals = new(StringComparer.OrdinalIgnoreCase);

    public RealmSubjectIndexService(
        IRealmService realmService,
        ILogger<RealmSubjectIndexService> logger,
        IRealmAwareGovernanceClient? governanceClient = null)
    {
        _realmService = realmService;
        _logger = logger;
        _governanceClient = governanceClient;
    }

    public Task<RealmSubjectIndexValidationResult> ValidateAsync(
        RealmSubjectIndex index,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Validate(index));
    }

    public Task<RealmSubjectIndexValidationResult> StoreAsync(
        RealmSubjectIndex index,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(index);
        if (!validation.IsValid)
        {
            return Task.FromResult(validation);
        }

        _indexes[BuildIndexKey(index.RealmId, index.Id)] = index;
        _logger.LogInformation(
            "[RealmSubjectIndex] Stored index {IndexId} revision {Revision} for realm {RealmId}",
            index.Id,
            index.Revision,
            index.RealmId);
        return Task.FromResult(validation);
    }

    public async Task<RealmSubjectIndexProposal> ProposeAsync(
        RealmSubjectIndex index,
        string proposedBy,
        string note = "",
        CancellationToken cancellationToken = default)
    {
        var proposal = BuildProposal(index, proposedBy, note);
        var validation = Validate(index);
        proposal.Errors.AddRange(validation.Errors);

        if (!IsSafeOpaqueReference(proposal.ProposedBy))
        {
            proposal.Errors.Add("Proposed-by identifier must be opaque and safe.");
        }

        if (proposal.Note.Length > MaxProposalNoteLength)
        {
            proposal.Errors.Add("Proposal note must be 512 characters or fewer.");
        }

        if (proposal.Errors.Count > 0)
        {
            proposal.Status = RealmSubjectIndexProposalStatus.Failed;
            _proposals[proposal.ProposalId] = proposal;
            return proposal;
        }

        var document = BuildProposalDocument(proposal);
        proposal.GovernanceDocumentId = document.Id;
        _proposals[proposal.ProposalId] = proposal;

        if (_governanceClient != null)
        {
            await _governanceClient.StoreDocumentForRealmAsync(document, proposal.RealmId, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "[RealmSubjectIndex] Proposed index {IndexId} revision {Revision} for realm {RealmId}",
            proposal.IndexId,
            proposal.Revision,
            proposal.RealmId);
        return proposal;
    }

    public async Task<RealmSubjectIndexProposalReview> ReviewProposalAsync(
        string proposalId,
        string reviewedBy,
        bool accept,
        string note = "",
        CancellationToken cancellationToken = default)
    {
        var normalizedProposalId = proposalId.Trim();
        var review = new RealmSubjectIndexProposalReview
        {
            ProposalId = normalizedProposalId,
            ReviewedBy = reviewedBy.Trim(),
            Accept = accept,
            Note = note.Trim(),
        };

        if (!_proposals.TryGetValue(normalizedProposalId, out var proposal))
        {
            review.Errors.Add("Proposal was not found.");
            return review;
        }

        if (proposal.Status != RealmSubjectIndexProposalStatus.Pending)
        {
            review.Errors.Add("Proposal has already been reviewed.");
        }

        if (!IsSafeOpaqueReference(review.ReviewedBy))
        {
            review.Errors.Add("Reviewed-by identifier must be opaque and safe.");
        }
        else if (!_realmService.IsTrustedGovernanceRoot(review.ReviewedBy))
        {
            review.Errors.Add("Reviewer is not trusted for this realm.");
        }

        if (review.Note.Length > MaxProposalNoteLength)
        {
            review.Errors.Add("Review note must be 512 characters or fewer.");
        }

        var validation = Validate(proposal.Index);
        review.Errors.AddRange(validation.Errors);

        if (review.Errors.Count > 0)
        {
            return review;
        }

        var document = BuildReviewDocument(proposal, review);
        review.GovernanceDocumentId = document.Id;
        proposal.Review = review;
        proposal.Status = accept ? RealmSubjectIndexProposalStatus.Accepted : RealmSubjectIndexProposalStatus.Rejected;
        _proposals[proposal.ProposalId] = proposal;

        if (_governanceClient != null)
        {
            await _governanceClient.StoreDocumentForRealmAsync(document, proposal.RealmId, cancellationToken).ConfigureAwait(false);
        }

        if (accept)
        {
            _indexes[BuildIndexKey(proposal.Index.RealmId, proposal.Index.Id)] = proposal.Index;
        }

        _logger.LogInformation(
            "[RealmSubjectIndex] {Action} proposal {ProposalId} for index {IndexId} revision {Revision}",
            accept ? "Accepted" : "Rejected",
            proposal.ProposalId,
            proposal.IndexId,
            proposal.Revision);
        return review;
    }

    public Task<IReadOnlyList<RealmSubjectIndexProposal>> GetProposalsForRealmAsync(
        string realmId,
        CancellationToken cancellationToken = default)
    {
        var proposals = _proposals.Values
            .Where(proposal => string.Equals(proposal.RealmId, realmId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(proposal => proposal.IndexId, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(proposal => proposal.Revision)
            .ThenBy(proposal => proposal.ProposalId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<RealmSubjectIndexProposal>>(proposals);
    }

    public Task<IReadOnlyList<RealmSubjectIndexResolution>> ResolveByRecordingIdAsync(
        string recordingId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordingId))
        {
            return Task.FromResult<IReadOnlyList<RealmSubjectIndexResolution>>(Array.Empty<RealmSubjectIndexResolution>());
        }

        var normalizedRecordingId = recordingId.Trim();
        var resolutions = _indexes.Values
            .SelectMany(index => index.Entries
                .Where(entry => MatchesRecordingId(entry, normalizedRecordingId))
                .Select(entry => new RealmSubjectIndexResolution
                {
                    Entry = entry,
                    RealmId = index.RealmId,
                    IndexId = index.Id,
                    Revision = index.Revision,
                    Provenance = $"realm:{index.RealmId}:subject-index:{index.Id}:r{index.Revision}",
                }))
            .OrderBy(result => result.RealmId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.IndexId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Entry.WorkRef.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<RealmSubjectIndexResolution>>(resolutions);
    }

    public Task<IReadOnlyList<RealmSubjectIndex>> GetIndexesForRealmAsync(
        string realmId,
        CancellationToken cancellationToken = default)
    {
        var indexes = _indexes.Values
            .Where(index => string.Equals(index.RealmId, realmId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(index => index.Id, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(index => index.Revision)
            .ToList();

        return Task.FromResult<IReadOnlyList<RealmSubjectIndex>>(indexes);
    }

    private RealmSubjectIndexValidationResult Validate(RealmSubjectIndex index)
    {
        var result = new RealmSubjectIndexValidationResult();

        if (string.IsNullOrWhiteSpace(index.Id))
        {
            result.Errors.Add("Index id is required.");
        }

        if (string.IsNullOrWhiteSpace(index.RealmId))
        {
            result.Errors.Add("Realm id is required.");
        }

        if (!_realmService.IsSameRealm(index.RealmId))
        {
            result.Errors.Add("Index realm does not match the local realm.");
        }

        if (string.IsNullOrWhiteSpace(index.SubjectNamespace))
        {
            result.Errors.Add("Subject namespace is required.");
        }

        if (index.Revision < 1)
        {
            result.Errors.Add("Revision must be positive.");
        }

        if (index.Entries.Count == 0)
        {
            result.Errors.Add("Index must contain at least one entry.");
        }

        ValidateSignature(index, result);

        foreach (var entry in index.Entries)
        {
            ValidateEntry(entry, result);
        }

        return result;
    }

    private void ValidateSignature(RealmSubjectIndex index, RealmSubjectIndexValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(index.Signature.Signer))
        {
            result.Errors.Add("Signature signer is required.");
        }
        else if (!_realmService.IsTrustedGovernanceRoot(index.Signature.Signer))
        {
            result.Errors.Add("Signature signer is not trusted for this realm.");
        }

        if (string.IsNullOrWhiteSpace(index.Signature.Value))
        {
            result.Errors.Add("Signature value is required.");
        }

        var expectedHash = index.ComputePayloadHash();
        if (!string.Equals(index.Signature.PayloadHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Signature payload hash does not match index contents.");
        }
    }

    private static void ValidateEntry(RealmSubjectIndexEntry entry, RealmSubjectIndexValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(entry.SubjectId))
        {
            result.Errors.Add("Entry subject id is required.");
        }

        if (!IsSafeWorkRef(entry.WorkRef))
        {
            result.Errors.Add($"Entry '{entry.SubjectId}' has an unsafe or incomplete WorkRef.");
        }

        foreach (var evidenceLink in entry.EvidenceLinks)
        {
            if (!IsSafeEvidenceLink(evidenceLink))
            {
                result.Errors.Add($"Entry '{entry.SubjectId}' has an unsafe evidence link.");
            }
        }
    }

    private static bool IsSafeWorkRef(WorkRef workRef)
    {
        return !string.IsNullOrWhiteSpace(workRef.Domain) &&
            !string.IsNullOrWhiteSpace(workRef.Title) &&
            workRef.ValidateSecurity();
    }

    private static bool IsSafeEvidenceLink(string evidenceLink)
    {
        if (string.IsNullOrWhiteSpace(evidenceLink))
        {
            return false;
        }

        if (!Uri.TryCreate(evidenceLink, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return AllowedEvidenceSchemes.Contains(uri.Scheme) &&
            string.IsNullOrWhiteSpace(uri.UserInfo) &&
            !evidenceLink.Contains('\\', StringComparison.Ordinal);
    }

    private static bool MatchesRecordingId(RealmSubjectIndexEntry entry, string recordingId)
    {
        var workRefMatches = TryGetRecordingId(entry.WorkRef, out var workRefRecordingId) &&
            string.Equals(workRefRecordingId, recordingId, StringComparison.OrdinalIgnoreCase);
        var externalIdMatches = entry.ExternalIds.TryGetValue("musicbrainz:recording", out var externalRecordingId) &&
            string.Equals(externalRecordingId, recordingId, StringComparison.OrdinalIgnoreCase);

        return workRefMatches || externalIdMatches;
    }

    private static bool TryGetRecordingId(WorkRef workRef, out string recordingId)
    {
        if (workRef.ExternalIds.TryGetValue("musicbrainz:recording", out recordingId!) ||
            workRef.ExternalIds.TryGetValue("musicbrainz", out recordingId!))
        {
            return !string.IsNullOrWhiteSpace(recordingId);
        }

        recordingId = string.Empty;
        return false;
    }

    private static string BuildIndexKey(string realmId, string indexId)
    {
        return $"{realmId.Trim().ToLowerInvariant()}:{indexId.Trim().ToLowerInvariant()}";
    }

    private static RealmSubjectIndexProposal BuildProposal(RealmSubjectIndex index, string proposedBy, string note)
    {
        return new RealmSubjectIndexProposal
        {
            ProposalId = BuildProposalId(index),
            RealmId = index.RealmId.Trim(),
            IndexId = index.Id.Trim(),
            Revision = index.Revision,
            ProposedBy = proposedBy.Trim(),
            Note = note.Trim(),
            Index = index,
        };
    }

    private static GovernanceDocument BuildProposalDocument(RealmSubjectIndexProposal proposal)
    {
        return new GovernanceDocument
        {
            Id = $"realm-subject-index-proposal:{proposal.ProposalId}",
            Type = ProposalDocumentType,
            Version = proposal.Revision,
            Created = proposal.ProposedAt,
            Modified = proposal.ProposedAt,
            RealmId = proposal.RealmId,
            Signer = proposal.Index.Signature.Signer,
            Signature = proposal.Index.Signature.Value,
            Content = proposal.Index,
            Metadata = new GovernanceMetadata
            {
                Description = $"Subject index proposal {proposal.IndexId} revision {proposal.Revision}",
                Properties =
                {
                    ["proposalId"] = proposal.ProposalId,
                    ["proposedBy"] = proposal.ProposedBy,
                    ["payloadHash"] = proposal.Index.Signature.PayloadHash,
                    ["note"] = proposal.Note,
                },
            },
        };
    }

    private static GovernanceDocument BuildReviewDocument(
        RealmSubjectIndexProposal proposal,
        RealmSubjectIndexProposalReview review)
    {
        return new GovernanceDocument
        {
            Id = $"realm-subject-index-review:{proposal.ProposalId}:{(review.Accept ? "accept" : "reject")}",
            Type = ReviewDocumentType,
            Version = proposal.Revision,
            Created = review.ReviewedAt,
            Modified = review.ReviewedAt,
            RealmId = proposal.RealmId,
            Signer = review.ReviewedBy,
            Signature = $"reviewed-by:{review.ReviewedBy}",
            Content = review,
            Metadata = new GovernanceMetadata
            {
                Description = $"{(review.Accept ? "Accept" : "Reject")} subject index proposal {proposal.ProposalId}",
                Properties =
                {
                    ["proposalId"] = proposal.ProposalId,
                    ["indexId"] = proposal.IndexId,
                    ["decision"] = review.Accept ? "accept" : "reject",
                    ["note"] = review.Note,
                },
            },
        };
    }

    private static string BuildProposalId(RealmSubjectIndex index)
    {
        return $"{index.RealmId.Trim().ToLowerInvariant()}:{index.Id.Trim().ToLowerInvariant()}:r{index.Revision}";
    }

    private static bool IsSafeOpaqueReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 128 &&
            !trimmed.Contains('/', StringComparison.Ordinal) &&
            !trimmed.Contains('\\', StringComparison.Ordinal) &&
            !trimmed.Contains(':', StringComparison.Ordinal) &&
            !trimmed.Contains("..", StringComparison.Ordinal);
    }
}
