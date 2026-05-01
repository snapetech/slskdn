// <copyright file="MusicBrainzOverlayService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Overlay;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using slskd.Integrations.MusicBrainz.Models;
using slskd.PodCore;

public sealed class MusicBrainzOverlayService : IMusicBrainzOverlayService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ConcurrentDictionary<string, MusicBrainzOverlayEdit> _edits = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MusicBrainzOverlayExportDecision> _exportDecisions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MusicBrainzOverlayRouteAttempt> _routeAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _storageSync = new();
    private readonly string _storagePath;
    private readonly IPodMessageRouter? _messageRouter;
    private readonly ILogger<MusicBrainzOverlayService> _logger;

    public MusicBrainzOverlayService(ILogger<MusicBrainzOverlayService> logger)
        : this(
            logger,
            Path.Combine(
                string.IsNullOrWhiteSpace(global::slskd.Program.AppDirectory)
                    ? global::slskd.Program.DefaultAppDirectory
                    : global::slskd.Program.AppDirectory,
                "musicbrainz-overlay.json"))
    {
    }

    public MusicBrainzOverlayService(ILogger<MusicBrainzOverlayService> logger, IPodMessageRouter messageRouter)
        : this(
            logger,
            Path.Combine(
                string.IsNullOrWhiteSpace(global::slskd.Program.AppDirectory)
                    ? global::slskd.Program.DefaultAppDirectory
                    : global::slskd.Program.AppDirectory,
                "musicbrainz-overlay.json"),
            messageRouter)
    {
    }

    public MusicBrainzOverlayService(ILogger<MusicBrainzOverlayService> logger, string storagePath)
        : this(logger, storagePath, null)
    {
    }

    public MusicBrainzOverlayService(
        ILogger<MusicBrainzOverlayService> logger,
        string storagePath,
        IPodMessageRouter? messageRouter)
    {
        _logger = logger;
        _storagePath = storagePath;
        _messageRouter = messageRouter;
        LoadState();
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
        PersistState();
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

    public Task<MusicBrainzOverlayExportReview?> GetExportReviewAsync(
        string editId,
        CancellationToken cancellationToken = default)
    {
        editId = editId.Trim();
        if (!_edits.TryGetValue(editId, out var edit))
        {
            return Task.FromResult<MusicBrainzOverlayExportReview?>(null);
        }

        _exportDecisions.TryGetValue(editId, out var decision);
        var review = BuildExportReview(edit, decision);
        return Task.FromResult<MusicBrainzOverlayExportReview?>(review);
    }

    public Task<MusicBrainzOverlayExportApprovalResult> ApproveExportAsync(
        string editId,
        MusicBrainzOverlayExportApprovalRequest approvalRequest,
        CancellationToken cancellationToken = default)
    {
        editId = editId.Trim();
        approvalRequest ??= new MusicBrainzOverlayExportApprovalRequest();
        approvalRequest.ApprovedBy = string.IsNullOrWhiteSpace(approvalRequest.ApprovedBy)
            ? "local-user"
            : approvalRequest.ApprovedBy.Trim();
        approvalRequest.Note = approvalRequest.Note?.Trim() ?? string.Empty;

        var result = new MusicBrainzOverlayExportApprovalResult();
        if (!_edits.TryGetValue(editId, out var edit))
        {
            result.Errors.Add("Edit not found.");
            return Task.FromResult(result);
        }

        if (_exportDecisions.TryGetValue(editId, out var existingDecision))
        {
            result.Decision = existingDecision;
            return Task.FromResult(result);
        }

        if (!IsSafeOpaqueReference(approvalRequest.ApprovedBy))
        {
            result.Errors.Add("Approved-by identifier must be opaque and safe.");
        }

        if (approvalRequest.Note.Length > 512)
        {
            result.Errors.Add("Approval note must be 512 characters or fewer.");
        }

        var review = BuildExportReview(edit, decision: null);
        if (!review.CanApproveExport)
        {
            result.Errors.Add(review.ReviewReason);
        }

        if (result.Errors.Count > 0)
        {
            return Task.FromResult(result);
        }

        var decision = new MusicBrainzOverlayExportDecision
        {
            Id = $"musicbrainz-overlay-export:{Guid.NewGuid():N}",
            EditId = edit.Id,
            ApprovedBy = approvalRequest.ApprovedBy,
            Note = approvalRequest.Note,
            UpstreamTarget = review.UpstreamTarget,
            ProposedChange = review.ProposedChange,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _exportDecisions[editId] = decision;
        result.Decision = decision;
        PersistState();
        return Task.FromResult(result);
    }

    public async Task<MusicBrainzOverlayRouteAttempt> RouteEditAsync(
        string editId,
        MusicBrainzOverlayRouteRequest routeRequest,
        CancellationToken cancellationToken = default)
    {
        editId = editId.Trim();
        routeRequest ??= new MusicBrainzOverlayRouteRequest();
        if (!_edits.TryGetValue(editId, out var edit))
        {
            return StoreRouteAttempt(BuildRouteAttempt(editId, routeRequest, Array.Empty<string>(), success: false, "Edit not found."));
        }

        if (HasUnsafeRouteMetadata(routeRequest))
        {
            return StoreRouteAttempt(BuildRouteAttempt(editId, routeRequest, Array.Empty<string>(), success: false, "Route metadata must be opaque and safe."));
        }

        var targetPeerIds = NormalizeRouteTargets(routeRequest.TargetPeerIds);
        if (targetPeerIds.Count == 0)
        {
            return StoreRouteAttempt(BuildRouteAttempt(editId, routeRequest, targetPeerIds, success: false, "At least one target peer is required."));
        }

        if (targetPeerIds.Any(peerId => !IsSafeOpaqueReference(peerId)))
        {
            return StoreRouteAttempt(BuildRouteAttempt(editId, routeRequest, targetPeerIds, success: false, "Route targets must be opaque and safe."));
        }

        if (_messageRouter == null)
        {
            return StoreRouteAttempt(BuildRouteAttempt(editId, routeRequest, targetPeerIds, success: false, "Routing backend is not available."));
        }

        var message = BuildRouteMessage(edit, routeRequest, targetPeerIds);
        var routingResult = await _messageRouter.RouteMessageToPeersAsync(message, targetPeerIds, cancellationToken).ConfigureAwait(false);
        var failedPeerIds = routingResult.FailedPeerIds?.ToList() ?? new List<string>();
        var routedPeerIds = targetPeerIds
            .Where(peerId => !failedPeerIds.Contains(peerId, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return StoreRouteAttempt(new MusicBrainzOverlayRouteAttempt
        {
            Id = $"musicbrainz-overlay-route:{Guid.NewGuid():N}",
            EditId = edit.Id,
            MessageId = message.MessageId,
            PodId = message.PodId,
            ChannelId = message.ChannelId,
            TargetPeerIds = targetPeerIds,
            RoutedPeerIds = routedPeerIds,
            FailedPeerIds = failedPeerIds,
            Success = routingResult.Success,
            ErrorMessage = routingResult.ErrorMessage,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    public Task<IReadOnlyList<MusicBrainzOverlayRouteAttempt>> GetRouteAttemptsAsync(
        string editId,
        CancellationToken cancellationToken = default)
    {
        editId = editId.Trim();
        var attempts = _routeAttempts.Values
            .Where(attempt => string.Equals(attempt.EditId, editId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(attempt => attempt.CreatedAt)
            .ThenBy(attempt => attempt.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<MusicBrainzOverlayRouteAttempt>>(attempts);
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

    private static MusicBrainzOverlayExportReview BuildExportReview(
        MusicBrainzOverlayEdit edit,
        MusicBrainzOverlayExportDecision? decision)
    {
        return new MusicBrainzOverlayExportReview
        {
            Edit = edit,
            UpstreamTarget = $"{edit.TargetType}:{edit.TargetId}",
            ProposedChange = $"{edit.Field.Trim()} => {edit.Value.Trim()}",
            Evidence = edit.Evidence,
            CanApproveExport = decision == null && IsExportableEdit(edit),
            ReviewReason = BuildExportReviewReason(edit, decision),
            Decision = decision,
        };
    }

    private static bool IsExportableEdit(MusicBrainzOverlayEdit edit)
    {
        return edit.Type is MusicBrainzOverlayEditType.TitleCorrection
            or MusicBrainzOverlayEditType.ArtistCorrection
            or MusicBrainzOverlayEditType.Alias
            or MusicBrainzOverlayEditType.MissingAltTitle
            or MusicBrainzOverlayEditType.DuplicateMarker
            or MusicBrainzOverlayEditType.ReleaseGrouping
            or MusicBrainzOverlayEditType.RecordingLinkage;
    }

    private static string BuildExportReviewReason(MusicBrainzOverlayEdit edit, MusicBrainzOverlayExportDecision? decision)
    {
        if (decision != null)
        {
            return "Upstream export has already been approved locally.";
        }

        return IsExportableEdit(edit)
            ? "Overlay edit can be reviewed for manual upstream MusicBrainz submission."
            : "Overlay edit type is not exportable.";
    }

    private static bool IsSafeOpaqueReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
        {
            return false;
        }

        var lowered = value.ToLowerInvariant();
        if (lowered.Contains('/') ||
            lowered.Contains('\\') ||
            lowered.Contains("localhost") ||
            lowered.Contains("127.0.0.1") ||
            lowered.Contains("192.168.") ||
            lowered.Contains("10.") ||
            lowered.Contains("172.16.") ||
            lowered.Contains("path") ||
            lowered.Contains("file") ||
            lowered.Contains("private") ||
            lowered.Contains("internal"))
        {
            return false;
        }

        return !System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-fA-F0-9]{32,}$");
    }

    private static List<string> NormalizeRouteTargets(IEnumerable<string> targets)
    {
        return targets
            .Select(target => target.Trim())
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(target => target, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasUnsafeRouteMetadata(MusicBrainzOverlayRouteRequest routeRequest)
    {
        return (!string.IsNullOrWhiteSpace(routeRequest.SenderPeerId) && !IsSafeOpaqueReference(routeRequest.SenderPeerId)) ||
            (!string.IsNullOrWhiteSpace(routeRequest.PodId) && !IsSafeOpaqueReference(routeRequest.PodId)) ||
            (!string.IsNullOrWhiteSpace(routeRequest.ChannelId) && !IsSafeOpaqueReference(routeRequest.ChannelId));
    }

    private static PodMessage BuildRouteMessage(
        MusicBrainzOverlayEdit edit,
        MusicBrainzOverlayRouteRequest routeRequest,
        IReadOnlyList<string> targetPeerIds)
    {
        var envelope = new MusicBrainzOverlayRouteEnvelope
        {
            Edit = edit,
            TargetPeerIds = targetPeerIds.ToList(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return new PodMessage
        {
            MessageId = $"musicbrainz-overlay-edit:{Guid.NewGuid():N}",
            PodId = string.IsNullOrWhiteSpace(routeRequest.PodId) ? "musicbrainz-overlay" : routeRequest.PodId.Trim(),
            ChannelId = string.IsNullOrWhiteSpace(routeRequest.ChannelId) ? $"edit:{edit.Id}" : routeRequest.ChannelId.Trim(),
            SenderPeerId = string.IsNullOrWhiteSpace(routeRequest.SenderPeerId) ? "local-musicbrainz-overlay" : routeRequest.SenderPeerId.Trim(),
            Body = JsonSerializer.Serialize(envelope, JsonOptions),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "local-musicbrainz-overlay-route",
        };
    }

    private static MusicBrainzOverlayRouteAttempt BuildRouteAttempt(
        string editId,
        MusicBrainzOverlayRouteRequest routeRequest,
        IReadOnlyList<string> targets,
        bool success,
        string? errorMessage)
    {
        return new MusicBrainzOverlayRouteAttempt
        {
            Id = $"musicbrainz-overlay-route:{Guid.NewGuid():N}",
            EditId = editId,
            MessageId = string.Empty,
            PodId = string.IsNullOrWhiteSpace(routeRequest.PodId) ? "musicbrainz-overlay" : routeRequest.PodId.Trim(),
            ChannelId = string.IsNullOrWhiteSpace(routeRequest.ChannelId) ? $"edit:{editId}" : routeRequest.ChannelId.Trim(),
            TargetPeerIds = targets.ToList(),
            Success = success,
            ErrorMessage = errorMessage,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private MusicBrainzOverlayRouteAttempt StoreRouteAttempt(MusicBrainzOverlayRouteAttempt attempt)
    {
        _routeAttempts[attempt.Id] = attempt;
        PersistState();
        return attempt;
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
                var state = JsonSerializer.Deserialize<MusicBrainzOverlayStoreState>(json, JsonOptions);
                if (state == null)
                {
                    return;
                }

                foreach (var edit in state.Edits)
                {
                    _edits[edit.Id] = edit;
                }

                foreach (var decision in state.ExportDecisions)
                {
                    _exportDecisions[decision.EditId] = decision;
                }

                foreach (var attempt in state.RouteAttempts)
                {
                    _routeAttempts[attempt.Id] = attempt;
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "[MusicBrainzOverlay] Failed to load persisted overlay state from {Path}", _storagePath);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[MusicBrainzOverlay] Failed to parse persisted overlay state from {Path}", _storagePath);
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

            var state = new MusicBrainzOverlayStoreState
            {
                Edits = _edits.Values
                    .OrderBy(edit => edit.CreatedAt)
                    .ThenBy(edit => edit.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ExportDecisions = _exportDecisions.Values
                    .OrderBy(decision => decision.EditId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(decision => decision.CreatedAt)
                    .ToList(),
                RouteAttempts = _routeAttempts.Values
                    .OrderBy(attempt => attempt.EditId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(attempt => attempt.CreatedAt)
                    .ToList(),
            };

            var tempPath = $"{_storagePath}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(tempPath, _storagePath, overwrite: true);
        }
    }

    private sealed class MusicBrainzOverlayStoreState
    {
        public List<MusicBrainzOverlayEdit> Edits { get; set; } = new();

        public List<MusicBrainzOverlayExportDecision> ExportDecisions { get; set; } = new();

        public List<MusicBrainzOverlayRouteAttempt> RouteAttempts { get; set; } = new();
    }

    private sealed class MusicBrainzOverlayRouteEnvelope
    {
        public string Type { get; set; } = "slskdn.musicbrainz-overlay.edit.v1";

        public MusicBrainzOverlayEdit Edit { get; set; } = new();

        public List<string> TargetPeerIds { get; set; } = new();

        public DateTimeOffset CreatedAt { get; set; }
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
