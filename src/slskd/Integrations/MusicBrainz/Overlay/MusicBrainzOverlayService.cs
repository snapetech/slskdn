// <copyright file="MusicBrainzOverlayService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Overlay;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using slskd.Integrations.MusicBrainz.Models;

public sealed class MusicBrainzOverlayService : IMusicBrainzOverlayService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConcurrentDictionary<string, MusicBrainzOverlayEdit> _edits = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<MusicBrainzOverlayService> _logger;

    public MusicBrainzOverlayService(ILogger<MusicBrainzOverlayService> logger)
    {
        _logger = logger;
    }

    public Task<MusicBrainzOverlayValidationResult> StoreAsync(
        MusicBrainzOverlayEdit edit,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(edit);
        if (!validation.IsValid)
        {
            return Task.FromResult(validation);
        }

        _edits[edit.Id] = edit;
        _logger.LogInformation(
            "[MusicBrainzOverlay] Stored {EditType} edit {EditId} for {TargetType}:{TargetId}",
            edit.Type,
            edit.Id,
            edit.TargetType,
            edit.TargetId);

        return Task.FromResult(validation);
    }

    public Task<IReadOnlyList<MusicBrainzOverlayEdit>> GetEditsForTargetAsync(
        MusicBrainzOverlayTargetType targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        var edits = _edits.Values
            .Where(edit => edit.TargetType == targetType)
            .Where(edit => string.Equals(edit.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(edit => edit.CreatedAt)
            .ThenBy(edit => edit.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<MusicBrainzOverlayEdit>>(edits);
    }

    public Task<MusicBrainzOverlayApplication<ArtistReleaseGraph>> ApplyToArtistReleaseGraphAsync(
        ArtistReleaseGraph graph,
        CancellationToken cancellationToken = default)
    {
        var effective = Clone(graph);
        var provenance = new List<MusicBrainzOverlayProvenance>();
        var edits = _edits.Values
            .Where(edit => EditTargetsGraph(edit, graph))
            .OrderBy(edit => edit.CreatedAt)
            .ThenBy(edit => edit.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var edit in edits)
        {
            if (ApplyEdit(effective, edit))
            {
                provenance.Add(ToProvenance(edit));
            }
        }

        return Task.FromResult(new MusicBrainzOverlayApplication<ArtistReleaseGraph>
        {
            Original = graph,
            Effective = effective,
            Provenance = provenance,
        });
    }

    private static MusicBrainzOverlayValidationResult Validate(MusicBrainzOverlayEdit edit)
    {
        var result = new MusicBrainzOverlayValidationResult();

        if (string.IsNullOrWhiteSpace(edit.Id))
        {
            result.Errors.Add("Edit id is required.");
        }

        if (string.IsNullOrWhiteSpace(edit.TargetId))
        {
            result.Errors.Add("Target id is required.");
        }

        if (string.IsNullOrWhiteSpace(edit.Field))
        {
            result.Errors.Add("Field is required.");
        }

        if (string.IsNullOrWhiteSpace(edit.Value))
        {
            result.Errors.Add("Value is required.");
        }

        if (edit.Evidence.Count == 0)
        {
            result.Errors.Add("At least one evidence item is required.");
        }

        foreach (var evidence in edit.Evidence)
        {
            ValidateEvidence(evidence, result);
        }

        if (string.IsNullOrWhiteSpace(edit.Signature.Signer))
        {
            result.Errors.Add("Signature signer is required.");
        }

        if (string.IsNullOrWhiteSpace(edit.Signature.Value))
        {
            result.Errors.Add("Signature value is required.");
        }

        var expectedHash = edit.ComputePayloadHash();
        if (!string.Equals(edit.Signature.PayloadHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Signature payload hash does not match edit contents.");
        }

        if (!IsSupportedEdit(edit))
        {
            result.Errors.Add("Edit type, target, and field combination is not supported.");
        }

        return result;
    }

    private static void ValidateEvidence(MusicBrainzOverlayEvidence evidence, MusicBrainzOverlayValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(evidence.Reference))
        {
            result.Errors.Add("Evidence reference is required.");
        }

        if (evidence.Type == MusicBrainzOverlayEvidenceType.WorkRef &&
            evidence.WorkRef?.ValidateSecurity() != true)
        {
            result.Errors.Add("WorkRef evidence is missing or unsafe.");
        }
    }

    private static bool IsSupportedEdit(MusicBrainzOverlayEdit edit)
    {
        return edit.Type switch
        {
            MusicBrainzOverlayEditType.TitleCorrection =>
                edit.TargetType is MusicBrainzOverlayTargetType.ReleaseGroup or MusicBrainzOverlayTargetType.Release &&
                string.Equals(edit.Field, "title", StringComparison.OrdinalIgnoreCase),
            MusicBrainzOverlayEditType.ArtistCorrection =>
                edit.TargetType == MusicBrainzOverlayTargetType.Artist &&
                string.Equals(edit.Field, "name", StringComparison.OrdinalIgnoreCase),
            MusicBrainzOverlayEditType.Alias or MusicBrainzOverlayEditType.MissingAltTitle =>
                edit.TargetType is MusicBrainzOverlayTargetType.Artist or MusicBrainzOverlayTargetType.ReleaseGroup or MusicBrainzOverlayTargetType.Release &&
                string.Equals(edit.Field, "alias", StringComparison.OrdinalIgnoreCase),
            MusicBrainzOverlayEditType.DuplicateMarker =>
                edit.TargetType is MusicBrainzOverlayTargetType.ReleaseGroup or MusicBrainzOverlayTargetType.Release &&
                string.Equals(edit.Field, "duplicateOf", StringComparison.OrdinalIgnoreCase),
            MusicBrainzOverlayEditType.ReleaseGrouping =>
                edit.TargetType == MusicBrainzOverlayTargetType.Release &&
                string.Equals(edit.Field, "releaseGroupId", StringComparison.OrdinalIgnoreCase),
            MusicBrainzOverlayEditType.RecordingLinkage =>
                edit.TargetType == MusicBrainzOverlayTargetType.Recording &&
                string.Equals(edit.Field, "releaseId", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static ArtistReleaseGraph Clone(ArtistReleaseGraph graph)
    {
        var json = JsonSerializer.Serialize(graph, JsonOptions);
        return JsonSerializer.Deserialize<ArtistReleaseGraph>(json, JsonOptions) ?? new ArtistReleaseGraph();
    }

    private static bool EditTargetsGraph(MusicBrainzOverlayEdit edit, ArtistReleaseGraph graph)
    {
        return edit.TargetType switch
        {
            MusicBrainzOverlayTargetType.Artist => string.Equals(edit.TargetId, graph.ArtistId, StringComparison.OrdinalIgnoreCase),
            MusicBrainzOverlayTargetType.ReleaseGroup => graph.ReleaseGroups.Any(group =>
                string.Equals(group.ReleaseGroupId, edit.TargetId, StringComparison.OrdinalIgnoreCase)),
            MusicBrainzOverlayTargetType.Release => graph.ReleaseGroups.Any(group => group.Releases.Any(release =>
                string.Equals(release.ReleaseId, edit.TargetId, StringComparison.OrdinalIgnoreCase))),
            _ => false,
        };
    }

    private static bool ApplyEdit(ArtistReleaseGraph graph, MusicBrainzOverlayEdit edit)
    {
        return edit.TargetType switch
        {
            MusicBrainzOverlayTargetType.Artist => ApplyArtistEdit(graph, edit),
            MusicBrainzOverlayTargetType.ReleaseGroup => ApplyReleaseGroupEdit(graph, edit),
            MusicBrainzOverlayTargetType.Release => ApplyReleaseEdit(graph, edit),
            _ => false,
        };
    }

    private static bool ApplyArtistEdit(ArtistReleaseGraph graph, MusicBrainzOverlayEdit edit)
    {
        if (edit.Type == MusicBrainzOverlayEditType.ArtistCorrection &&
            string.Equals(edit.Field, "name", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(graph.ArtistId, edit.TargetId, StringComparison.OrdinalIgnoreCase))
        {
            graph.Name = edit.Value.Trim();
            return true;
        }

        return false;
    }

    private static bool ApplyReleaseGroupEdit(ArtistReleaseGraph graph, MusicBrainzOverlayEdit edit)
    {
        var releaseGroup = graph.ReleaseGroups.FirstOrDefault(group =>
            string.Equals(group.ReleaseGroupId, edit.TargetId, StringComparison.OrdinalIgnoreCase));
        if (releaseGroup == null)
        {
            return false;
        }

        if (edit.Type == MusicBrainzOverlayEditType.TitleCorrection &&
            string.Equals(edit.Field, "title", StringComparison.OrdinalIgnoreCase))
        {
            releaseGroup.Title = edit.Value.Trim();
            return true;
        }

        return edit.Type is MusicBrainzOverlayEditType.Alias or MusicBrainzOverlayEditType.MissingAltTitle or MusicBrainzOverlayEditType.DuplicateMarker;
    }

    private static bool ApplyReleaseEdit(ArtistReleaseGraph graph, MusicBrainzOverlayEdit edit)
    {
        var release = graph.ReleaseGroups
            .SelectMany(group => group.Releases)
            .FirstOrDefault(release => string.Equals(release.ReleaseId, edit.TargetId, StringComparison.OrdinalIgnoreCase));
        if (release == null)
        {
            return false;
        }

        if (edit.Type == MusicBrainzOverlayEditType.TitleCorrection &&
            string.Equals(edit.Field, "title", StringComparison.OrdinalIgnoreCase))
        {
            release.Title = edit.Value.Trim();
            return true;
        }

        return edit.Type is MusicBrainzOverlayEditType.Alias or MusicBrainzOverlayEditType.MissingAltTitle or MusicBrainzOverlayEditType.DuplicateMarker or MusicBrainzOverlayEditType.ReleaseGrouping;
    }

    private static MusicBrainzOverlayProvenance ToProvenance(MusicBrainzOverlayEdit edit)
    {
        return new MusicBrainzOverlayProvenance
        {
            EditId = edit.Id,
            Type = edit.Type,
            TargetType = edit.TargetType,
            TargetId = edit.TargetId,
            Field = edit.Field,
            Value = edit.Value,
            SourceScope = edit.SourceScope,
            Evidence = edit.Evidence,
        };
    }
}
