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

public sealed class MusicBrainzOverlayExportReview
{
    public MusicBrainzOverlayEdit Edit { get; set; } = new();

    public string UpstreamTarget { get; set; } = string.Empty;

    public string ProposedChange { get; set; } = string.Empty;

    public List<MusicBrainzOverlayEvidence> Evidence { get; set; } = new();

    public bool CanApproveExport { get; set; }

    public string ReviewReason { get; set; } = string.Empty;

    public MusicBrainzOverlayExportDecision? Decision { get; set; }
}

public sealed class MusicBrainzOverlayExportApprovalRequest
{
    public string ApprovedBy { get; set; } = "local-user";

    public string Note { get; set; } = string.Empty;
}

public sealed class MusicBrainzOverlayExportDecision
{
    public string Id { get; set; } = string.Empty;

    public string EditId { get; set; } = string.Empty;

    public string ApprovedBy { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string UpstreamTarget { get; set; } = string.Empty;

    public string ProposedChange { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MusicBrainzOverlayExportApprovalResult
{
    public bool IsApproved => Errors.Count == 0 && Decision != null;

    public List<string> Errors { get; set; } = new();

    public MusicBrainzOverlayExportDecision? Decision { get; set; }
}

public sealed class MusicBrainzOverlayRouteRequest
{
    public string SenderPeerId { get; set; } = "local-musicbrainz-overlay";

    public string PodId { get; set; } = "musicbrainz-overlay";

    public string ChannelId { get; set; } = string.Empty;

    public List<string> TargetPeerIds { get; set; } = new();
}

public sealed class MusicBrainzOverlayRouteAttempt
{
    public string Id { get; set; } = string.Empty;

    public string EditId { get; set; } = string.Empty;

    public string MessageId { get; set; } = string.Empty;

    public string PodId { get; set; } = string.Empty;

    public string ChannelId { get; set; } = string.Empty;

    public List<string> TargetPeerIds { get; set; } = new();

    public List<string> RoutedPeerIds { get; set; } = new();

    public List<string> FailedPeerIds { get; set; } = new();

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
