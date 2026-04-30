// <copyright file="MusicBrainzOverlayModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Overlay;

using System.Security.Cryptography;
using System.Text;
using slskd.Integrations.MusicBrainz.Models;
using slskd.SocialFederation;

public sealed class MusicBrainzOverlayEdit
{
    public string Id { get; set; } = string.Empty;

    public MusicBrainzOverlayEditType Type { get; set; }

    public MusicBrainzOverlayTargetType TargetType { get; set; }

    public string TargetId { get; set; } = string.Empty;

    public string Field { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string SourceScope { get; set; } = "local";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<MusicBrainzOverlayEvidence> Evidence { get; set; } = new();

    public MusicBrainzOverlaySignature Signature { get; set; } = new();

    public string ComputePayloadHash()
    {
        var canonical = new StringBuilder()
            .Append(Id.Trim().ToLowerInvariant()).Append('|')
            .Append(Type).Append('|')
            .Append(TargetType).Append('|')
            .Append(TargetId.Trim().ToLowerInvariant()).Append('|')
            .Append(Field.Trim().ToLowerInvariant()).Append('|')
            .Append(Value.Trim()).Append('|')
            .Append(SourceScope.Trim().ToLowerInvariant()).Append('|')
            .Append(CreatedAt.ToUniversalTime().ToString("O")).Append('|');

        foreach (var evidence in Evidence.OrderBy(evidence => evidence.Type).ThenBy(evidence => evidence.Reference, StringComparer.OrdinalIgnoreCase))
        {
            canonical
                .Append(evidence.Type).Append(':')
                .Append(evidence.Reference.Trim().ToLowerInvariant()).Append(':')
                .Append(evidence.Note.Trim()).Append('|');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }
}

public enum MusicBrainzOverlayEditType
{
    Alias,
    TitleCorrection,
    ArtistCorrection,
    ReleaseGrouping,
    RecordingLinkage,
    MissingAltTitle,
    DuplicateMarker,
}

public enum MusicBrainzOverlayTargetType
{
    Artist,
    ReleaseGroup,
    Release,
    Recording,
}

public sealed class MusicBrainzOverlayEvidence
{
    public MusicBrainzOverlayEvidenceType Type { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public WorkRef? WorkRef { get; set; }
}

public enum MusicBrainzOverlayEvidenceType
{
    SongIdRun,
    WorkRef,
    RealmSubjectIndex,
    UserNote,
}

public sealed class MusicBrainzOverlaySignature
{
    public string Signer { get; set; } = string.Empty;

    public string Algorithm { get; set; } = "local-sha256";

    public string PayloadHash { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class MusicBrainzOverlayValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; set; } = new();
}

public sealed class MusicBrainzOverlayApplication<T>
{
    public T Original { get; set; } = default!;

    public T Effective { get; set; } = default!;

    public List<MusicBrainzOverlayProvenance> Provenance { get; set; } = new();
}

public sealed class MusicBrainzOverlayProvenance
{
    public string EditId { get; set; } = string.Empty;

    public MusicBrainzOverlayEditType Type { get; set; }

    public MusicBrainzOverlayTargetType TargetType { get; set; }

    public string TargetId { get; set; } = string.Empty;

    public string Field { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string SourceScope { get; set; } = string.Empty;

    public List<MusicBrainzOverlayEvidence> Evidence { get; set; } = new();
}

public sealed class MusicBrainzOverlayReleaseGraphResponse
{
    public ArtistReleaseGraph Original { get; set; } = new();

    public ArtistReleaseGraph Effective { get; set; } = new();

    public List<MusicBrainzOverlayProvenance> Provenance { get; set; } = new();
}
