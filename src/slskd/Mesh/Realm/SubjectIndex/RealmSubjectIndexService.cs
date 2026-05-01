// <copyright file="RealmSubjectIndexService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh.Realm.SubjectIndex;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Governance;
using slskd.SocialFederation;

public sealed class RealmSubjectIndexService : IRealmSubjectIndexService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private const string ProposalDocumentType = "realm-subject-index.proposal";
    private const string ReviewDocumentType = "realm-subject-index.review";
    private const int MaxProposalNoteLength = 512;
    private const int MaxAuthorityDecisionNoteLength = 512;

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
    private readonly object _storageSync = new();
    private readonly string _storagePath;
    private readonly ConcurrentDictionary<string, RealmSubjectIndex> _indexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RealmSubjectIndexProposal> _proposals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RealmSubjectIndexAuthorityDecision> _authorityDecisions = new(StringComparer.OrdinalIgnoreCase);

    private sealed record IndexedSubjectEntry(
        RealmSubjectIndex Index,
        RealmSubjectIndexEntry Entry,
        IReadOnlyDictionary<string, string> ExternalIds);

    public RealmSubjectIndexService(
        IRealmService realmService,
        ILogger<RealmSubjectIndexService> logger,
        IRealmAwareGovernanceClient? governanceClient = null)
        : this(
            realmService,
            logger,
            Path.Combine(
                string.IsNullOrWhiteSpace(global::slskd.Program.AppDirectory)
                    ? global::slskd.Program.DefaultAppDirectory
                    : global::slskd.Program.AppDirectory,
                "realm-subject-indexes.json"),
            governanceClient)
    {
    }

    public RealmSubjectIndexService(
        IRealmService realmService,
        ILogger<RealmSubjectIndexService> logger,
        string storagePath,
        IRealmAwareGovernanceClient? governanceClient = null)
    {
        _realmService = realmService;
        _logger = logger;
        _storagePath = storagePath;
        _governanceClient = governanceClient;
        LoadState();
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
        PersistState();
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
            PersistState();
            return proposal;
        }

        var document = BuildProposalDocument(proposal);
        proposal.GovernanceDocumentId = document.Id;
        _proposals[proposal.ProposalId] = proposal;
        PersistState();

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

        PersistState();

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
            .Where(IsAuthorityEnabled)
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

    public Task<RealmSubjectIndexConflictReport> GetConflictReportAsync(
        string realmId,
        CancellationToken cancellationToken = default)
    {
        var normalizedRealmId = realmId.Trim();
        var allIndexes = _indexes.Values
            .Where(index => string.Equals(index.RealmId, normalizedRealmId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(index => index.Id, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(index => index.Revision)
            .ToList();
        var indexes = allIndexes
            .Where(IsAuthorityEnabled)
            .ToList();

        var entries = indexes
            .SelectMany(index => index.Entries.Select(entry => new IndexedSubjectEntry(index, entry, BuildExternalIdMap(entry))))
            .ToList();

        var report = new RealmSubjectIndexConflictReport
        {
            RealmId = normalizedRealmId,
            IndexCount = indexes.Count,
            DisabledAuthorityCount = allIndexes.Count - indexes.Count,
            EntryCount = entries.Count,
        };

        report.Conflicts.AddRange(BuildExternalIdConflicts(entries));
        report.Conflicts.AddRange(BuildRecordingSubjectConflicts(entries));
        report.Conflicts.AddRange(BuildWorkIdentityConflicts(entries));
        report.Conflicts.AddRange(BuildAliasSubjectConflicts(entries));

        report.Conflicts = report.Conflicts
            .OrderBy(conflict => conflict.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(conflict => conflict.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(conflict => conflict.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<RealmSubjectIndexAuthorityDecision>> GetAuthorityDecisionsForRealmAsync(
        string realmId,
        CancellationToken cancellationToken = default)
    {
        var normalizedRealmId = realmId.Trim();
        var decisions = _authorityDecisions.Values
            .Where(decision => string.Equals(decision.RealmId, normalizedRealmId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(decision => decision.IndexId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<RealmSubjectIndexAuthorityDecision>>(decisions);
    }

    public Task<RealmSubjectIndexAuthorityDecision> SetAuthorityEnabledAsync(
        string realmId,
        string indexId,
        RealmSubjectIndexAuthorityDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new RealmSubjectIndexAuthorityDecisionRequest();
        var decision = new RealmSubjectIndexAuthorityDecision
        {
            RealmId = realmId.Trim(),
            IndexId = indexId.Trim(),
            Enabled = request.Enabled,
            DecidedBy = string.IsNullOrWhiteSpace(request.DecidedBy) ? "local-user" : request.DecidedBy.Trim(),
            Note = request.Note?.Trim() ?? string.Empty,
            DecidedAt = DateTimeOffset.UtcNow,
        };

        if (string.IsNullOrWhiteSpace(decision.RealmId))
        {
            decision.Errors.Add("Realm id is required.");
        }
        else if (!_realmService.IsSameRealm(decision.RealmId))
        {
            decision.Errors.Add("Realm id does not match the local realm.");
        }

        if (string.IsNullOrWhiteSpace(decision.IndexId))
        {
            decision.Errors.Add("Index id is required.");
        }
        else if (!_indexes.ContainsKey(BuildIndexKey(decision.RealmId, decision.IndexId)))
        {
            decision.Errors.Add("Index authority was not found.");
        }

        if (!IsSafeOpaqueReference(decision.DecidedBy))
        {
            decision.Errors.Add("Decided-by identifier must be opaque and safe.");
        }

        if (decision.Note.Length > MaxAuthorityDecisionNoteLength)
        {
            decision.Errors.Add("Authority decision note must be 512 characters or fewer.");
        }

        if (decision.Errors.Count == 0)
        {
            _authorityDecisions[BuildIndexKey(decision.RealmId, decision.IndexId)] = decision;
            PersistState();
        }

        return Task.FromResult(decision);
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

    private static IReadOnlyList<RealmSubjectIndexConflict> BuildExternalIdConflicts(
        IReadOnlyCollection<IndexedSubjectEntry> entries)
    {
        return entries
            .SelectMany(indexed => indexed.ExternalIds.Select(externalId => new
            {
                Indexed = indexed,
                Key = externalId.Key,
                Value = externalId.Value,
            }))
            .GroupBy(
                item => $"{Normalize(item.Indexed.Index.SubjectNamespace)}:{Normalize(item.Indexed.Entry.SubjectId)}:{Normalize(item.Key)}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group
                .Select(item => Normalize(item.Value))
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1)
            .Select(group =>
            {
                var first = group.First();
                return BuildConflict(
                    "external-id",
                    first.Indexed.Index.SubjectNamespace,
                    first.Indexed.Entry.SubjectId,
                    first.Key,
                    $"Subject '{first.Indexed.Entry.SubjectId}' has conflicting external id values for '{first.Key}'.",
                    group.Select(item => BuildConflictValue(item.Indexed, item.Value)));
            })
            .ToList();
    }

    private static IReadOnlyList<RealmSubjectIndexConflict> BuildRecordingSubjectConflicts(
        IReadOnlyCollection<IndexedSubjectEntry> entries)
    {
        return entries
            .SelectMany(indexed => GetRecordingIds(indexed).Select(recordingId => new
            {
                Indexed = indexed,
                RecordingId = recordingId,
            }))
            .GroupBy(item => Normalize(item.RecordingId), StringComparer.OrdinalIgnoreCase)
            .Where(group => group
                .Select(item => Normalize(item.Indexed.Entry.SubjectId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1)
            .Select(group =>
            {
                var first = group.First();
                return BuildConflict(
                    "recording-subject",
                    first.Indexed.Index.SubjectNamespace,
                    string.Empty,
                    first.RecordingId,
                    $"Recording '{first.RecordingId}' maps to multiple realm subjects.",
                    group.Select(item => BuildConflictValue(item.Indexed, item.Indexed.Entry.SubjectId)));
            })
            .ToList();
    }

    private static IReadOnlyList<RealmSubjectIndexConflict> BuildWorkIdentityConflicts(
        IReadOnlyCollection<IndexedSubjectEntry> entries)
    {
        return entries
            .GroupBy(
                indexed => $"{Normalize(indexed.Index.SubjectNamespace)}:{Normalize(indexed.Entry.SubjectId)}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group
                .Select(indexed => BuildWorkIdentity(indexed.Entry))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1)
            .Select(group =>
            {
                var first = group.First();
                return BuildConflict(
                    "workref-identity",
                    first.Index.SubjectNamespace,
                    first.Entry.SubjectId,
                    "workref",
                    $"Subject '{first.Entry.SubjectId}' has conflicting title or creator values.",
                    group.Select(indexed => BuildConflictValue(indexed, BuildWorkIdentity(indexed.Entry))));
            })
            .ToList();
    }

    private static IReadOnlyList<RealmSubjectIndexConflict> BuildAliasSubjectConflicts(
        IReadOnlyCollection<IndexedSubjectEntry> entries)
    {
        return entries
            .SelectMany(indexed => indexed.Entry.Aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(alias => new
                {
                    Indexed = indexed,
                    Alias = alias.Trim(),
                }))
            .GroupBy(
                item => $"{Normalize(item.Indexed.Index.SubjectNamespace)}:{Normalize(item.Alias)}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group
                .Select(item => Normalize(item.Indexed.Entry.SubjectId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1)
            .Select(group =>
            {
                var first = group.First();
                return BuildConflict(
                    "alias-subject",
                    first.Indexed.Index.SubjectNamespace,
                    string.Empty,
                    first.Alias,
                    $"Alias '{first.Alias}' maps to multiple realm subjects.",
                    group.Select(item => BuildConflictValue(item.Indexed, item.Indexed.Entry.SubjectId)));
            })
            .ToList();
    }

    private static RealmSubjectIndexConflict BuildConflict(
        string type,
        string subjectNamespace,
        string subjectId,
        string key,
        string description,
        IEnumerable<RealmSubjectIndexConflictValue> values)
    {
        var sortedValues = values
            .GroupBy(
                value => $"{Normalize(value.AuthorityKey)}:{Normalize(value.SubjectId)}:{Normalize(value.Value)}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(value => value.AuthorityKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RealmSubjectIndexConflict
        {
            Id = BuildConflictId(type, subjectNamespace, subjectId, key),
            Type = type,
            SubjectNamespace = subjectNamespace,
            SubjectId = subjectId,
            Key = key,
            Description = description,
            Values = sortedValues,
        };
    }

    private static RealmSubjectIndexConflictValue BuildConflictValue(IndexedSubjectEntry indexed, string value)
    {
        return new RealmSubjectIndexConflictValue
        {
            RealmId = indexed.Index.RealmId,
            IndexId = indexed.Index.Id,
            Revision = indexed.Index.Revision,
            SubjectId = indexed.Entry.SubjectId,
            Value = value,
            WorkTitle = indexed.Entry.WorkRef.Title,
            WorkCreator = indexed.Entry.WorkRef.Creator ?? string.Empty,
            Aliases = indexed.Entry.Aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(alias => alias.Trim())
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ExternalIds = indexed.ExternalIds
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            AuthorityKey = $"{indexed.Index.RealmId}:{indexed.Index.Id}:r{indexed.Index.Revision}",
            Provenance = BuildProvenance(indexed.Index),
        };
    }

    private static IReadOnlyDictionary<string, string> BuildExternalIdMap(RealmSubjectIndexEntry entry)
    {
        var externalIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in entry.WorkRef.ExternalIds.Concat(entry.ExternalIds))
        {
            var key = pair.Key.Trim();
            var value = pair.Value.Trim();
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                externalIds[key] = value;
            }
        }

        return externalIds;
    }

    private static IReadOnlyList<string> GetRecordingIds(IndexedSubjectEntry indexed)
    {
        return indexed.ExternalIds
            .Where(pair =>
                string.Equals(pair.Key, "musicbrainz", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "musicbrainz:recording", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildWorkIdentity(RealmSubjectIndexEntry entry)
    {
        return $"{entry.WorkRef.Title.Trim()}|{entry.WorkRef.Creator?.Trim() ?? string.Empty}";
    }

    private static string BuildConflictId(string type, string subjectNamespace, string subjectId, string key)
    {
        return $"{Normalize(type)}:{Normalize(subjectNamespace)}:{Normalize(subjectId)}:{Normalize(key)}";
    }

    private static string BuildProvenance(RealmSubjectIndex index)
    {
        return $"realm:{index.RealmId}:subject-index:{index.Id}:r{index.Revision}";
    }

    private bool IsAuthorityEnabled(RealmSubjectIndex index)
    {
        return !_authorityDecisions.TryGetValue(BuildIndexKey(index.RealmId, index.Id), out var decision) || decision.Enabled;
    }

    private void LoadState()
    {
        lock (_storageSync)
        {
            if (!File.Exists(_storagePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_storagePath);
                var state = JsonSerializer.Deserialize<RealmSubjectIndexStoreState>(json, JsonOptions);
                if (state == null)
                {
                    return;
                }

                foreach (var index in state.Indexes)
                {
                    _indexes[BuildIndexKey(index.RealmId, index.Id)] = index;
                }

                foreach (var proposal in state.Proposals)
                {
                    _proposals[proposal.ProposalId] = proposal;
                }

                foreach (var decision in state.AuthorityDecisions)
                {
                    _authorityDecisions[BuildIndexKey(decision.RealmId, decision.IndexId)] = decision;
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "[RealmSubjectIndex] Failed to load persisted subject-index state from {Path}", _storagePath);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[RealmSubjectIndex] Failed to parse persisted subject-index state from {Path}", _storagePath);
            }
        }
    }

    private void PersistState()
    {
        lock (_storageSync)
        {
            var directory = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new RealmSubjectIndexStoreState
            {
                Indexes = _indexes.Values
                    .OrderBy(index => index.RealmId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(index => index.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Proposals = _proposals.Values
                    .OrderBy(proposal => proposal.RealmId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(proposal => proposal.IndexId, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(proposal => proposal.Revision)
                    .ToList(),
                AuthorityDecisions = _authorityDecisions.Values
                    .OrderBy(decision => decision.RealmId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(decision => decision.IndexId, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };

            var tempPath = $"{_storagePath}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(tempPath, _storagePath, overwrite: true);
        }
    }

    private sealed class RealmSubjectIndexStoreState
    {
        public List<RealmSubjectIndex> Indexes { get; set; } = new();

        public List<RealmSubjectIndexProposal> Proposals { get; set; } = new();

        public List<RealmSubjectIndexAuthorityDecision> AuthorityDecisions { get; set; } = new();
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
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
