// <copyright file="RealmSubjectIndexService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh.Realm.SubjectIndex;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using slskd.SocialFederation;

public sealed class RealmSubjectIndexService : IRealmSubjectIndexService
{
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
    private readonly ConcurrentDictionary<string, RealmSubjectIndex> _indexes = new(StringComparer.OrdinalIgnoreCase);

    public RealmSubjectIndexService(
        IRealmService realmService,
        ILogger<RealmSubjectIndexService> logger)
    {
        _realmService = realmService;
        _logger = logger;
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
}
