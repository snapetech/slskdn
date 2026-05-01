// <copyright file="QuarantineJuryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.QuarantineJury;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using slskd.PodCore;

public sealed class QuarantineJuryService : IQuarantineJuryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ConcurrentDictionary<string, QuarantineJuryRequest> _requests = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, QuarantineJuryVerdictRecord> _verdicts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, QuarantineJuryRouteAttempt> _routeAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, QuarantineJuryReviewDecision> _reviewDecisions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _storageSync = new();
    private readonly string _storagePath;
    private readonly IPodMessageRouter? _messageRouter;
    private readonly ILogger<QuarantineJuryService> _logger;

    public QuarantineJuryService(ILogger<QuarantineJuryService> logger)
        : this(
            logger,
            Path.Combine(
                string.IsNullOrWhiteSpace(global::slskd.Program.AppDirectory)
                    ? global::slskd.Program.DefaultAppDirectory
                    : global::slskd.Program.AppDirectory,
                "quarantine-jury.json"))
    {
    }

    public QuarantineJuryService(ILogger<QuarantineJuryService> logger, IPodMessageRouter messageRouter)
        : this(
            logger,
            Path.Combine(
                string.IsNullOrWhiteSpace(global::slskd.Program.AppDirectory)
                    ? global::slskd.Program.DefaultAppDirectory
                    : global::slskd.Program.AppDirectory,
                "quarantine-jury.json"),
            messageRouter)
    {
    }

    public QuarantineJuryService(ILogger<QuarantineJuryService> logger, string storagePath)
        : this(logger, storagePath, null)
    {
    }

    public QuarantineJuryService(ILogger<QuarantineJuryService> logger, string storagePath, IPodMessageRouter? messageRouter)
    {
        _logger = logger;
        _storagePath = storagePath;
        _messageRouter = messageRouter;
        LoadState();
    }

    public Task<QuarantineJuryValidationResult> CreateRequestAsync(
        QuarantineJuryRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Id = string.IsNullOrWhiteSpace(request.Id)
            ? $"quarantine-jury:{Guid.NewGuid():N}"
            : request.Id.Trim();
        request.LocalReason = request.LocalReason?.Trim() ?? string.Empty;
        request.CreatedAt = request.CreatedAt == default ? DateTimeOffset.UtcNow : request.CreatedAt;
        request.MinJurorVotes = Math.Max(1, request.MinJurorVotes);
        request.Jurors = request.Jurors
            .Select(juror => juror.Trim())
            .Where(juror => !string.IsNullOrWhiteSpace(juror))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(juror => juror, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var validation = ValidateRequest(request);
        if (!validation.IsValid)
        {
            return Task.FromResult(validation);
        }

        _requests[request.Id] = request;
        PersistState();
        _logger.LogInformation("[QuarantineJury] Created request {RequestId} for {JurorCount} jurors", request.Id, request.Jurors.Count);
        return Task.FromResult(validation);
    }

    public Task<IReadOnlyList<QuarantineJuryRequest>> GetRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = _requests.Values
            .OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<QuarantineJuryRequest>>(requests);
    }

    public Task<QuarantineJuryRequest?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        _requests.TryGetValue(requestId.Trim(), out var request);
        return Task.FromResult(request);
    }

    public Task<QuarantineJuryValidationResult> SubmitVerdictAsync(
        QuarantineJuryVerdictRecord verdict,
        CancellationToken cancellationToken = default)
    {
        verdict.RequestId = verdict.RequestId?.Trim() ?? string.Empty;
        verdict.Juror = verdict.Juror?.Trim() ?? string.Empty;
        verdict.Reason = verdict.Reason?.Trim() ?? string.Empty;
        verdict.Id = string.IsNullOrWhiteSpace(verdict.Id)
            ? BuildVerdictId(verdict.RequestId, verdict.Juror)
            : verdict.Id.Trim();
        verdict.CreatedAt = verdict.CreatedAt == default ? DateTimeOffset.UtcNow : verdict.CreatedAt;

        var validation = ValidateVerdict(verdict);
        if (!validation.IsValid)
        {
            return Task.FromResult(validation);
        }

        _verdicts[BuildVerdictKey(verdict.RequestId, verdict.Juror)] = verdict;
        PersistState();
        _logger.LogInformation(
            "[QuarantineJury] Recorded {Verdict} verdict for request {RequestId}",
            verdict.Verdict,
            verdict.RequestId);
        return Task.FromResult(validation);
    }

    public Task<QuarantineJuryAggregate> GetAggregateAsync(string requestId, CancellationToken cancellationToken = default)
    {
        requestId = requestId.Trim();
        return Task.FromResult(BuildAggregate(requestId));
    }

    public Task<QuarantineJuryReview?> GetReviewAsync(string requestId, CancellationToken cancellationToken = default)
    {
        requestId = requestId.Trim();
        if (!_requests.TryGetValue(requestId, out var request))
        {
            return Task.FromResult<QuarantineJuryReview?>(null);
        }

        var aggregate = BuildAggregate(requestId);
        var verdicts = GetVerdictsForRequest(requestId)
            .OrderBy(verdict => verdict.Juror, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var routeAttempts = GetRouteAttemptsForRequest(requestId)
            .OrderByDescending(attempt => attempt.CreatedAt)
            .ThenBy(attempt => attempt.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _reviewDecisions.TryGetValue(requestId, out var acceptance);

        return Task.FromResult<QuarantineJuryReview?>(new QuarantineJuryReview
        {
            Request = request,
            Aggregate = aggregate,
            Verdicts = verdicts,
            RouteAttempts = routeAttempts,
            Acceptance = acceptance,
            CanAcceptReleaseCandidate = CanAcceptReleaseCandidate(aggregate, acceptance),
            AcceptanceReason = BuildAcceptanceReason(aggregate, acceptance),
        });
    }

    public Task<QuarantineJuryAuditReport> GetAuditReportAsync(
        int staleAfterHours = 72,
        CancellationToken cancellationToken = default)
    {
        var staleAfter = TimeSpan.FromHours(Math.Max(1, staleAfterHours));
        var generatedAt = DateTimeOffset.UtcNow;
        var entries = _requests.Values
            .OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Id, StringComparer.OrdinalIgnoreCase)
            .Select(request => BuildAuditEntry(request, generatedAt, staleAfter))
            .ToList();

        var report = new QuarantineJuryAuditReport
        {
            GeneratedAt = generatedAt,
            RequestCount = entries.Count,
            AcceptedReleaseCandidateCount = entries.Count(entry => entry.Status == "accepted-release-candidate"),
            PendingReleaseCandidateCount = entries.Count(entry => entry.Status == "pending-release-acceptance"),
            PendingManualReviewCount = entries.Count(entry => entry.Status == "manual-review"),
            UpholdQuarantineCount = entries.Count(entry => entry.Status == "uphold-quarantine"),
            StaleRequestCount = entries.Count(entry => entry.IsStale),
            Entries = entries,
        };

        return Task.FromResult(report);
    }

    public Task<QuarantineJuryAcceptanceResult> AcceptReleaseCandidateAsync(
        string requestId,
        QuarantineJuryAcceptanceRequest acceptanceRequest,
        CancellationToken cancellationToken = default)
    {
        requestId = requestId.Trim();
        acceptanceRequest ??= new QuarantineJuryAcceptanceRequest();
        acceptanceRequest.AcceptedBy = string.IsNullOrWhiteSpace(acceptanceRequest.AcceptedBy)
            ? "local-user"
            : acceptanceRequest.AcceptedBy.Trim();
        acceptanceRequest.Note = acceptanceRequest.Note?.Trim() ?? string.Empty;

        var result = new QuarantineJuryAcceptanceResult();
        if (!_requests.ContainsKey(requestId))
        {
            result.Errors.Add("Request not found.");
            return Task.FromResult(result);
        }

        if (_reviewDecisions.TryGetValue(requestId, out var existingDecision))
        {
            result.Decision = existingDecision;
            return Task.FromResult(result);
        }

        if (!IsSafeOpaqueReference(acceptanceRequest.AcceptedBy))
        {
            result.Errors.Add("Accepted-by identifier must be opaque and safe.");
        }

        if (acceptanceRequest.Note.Length > 512)
        {
            result.Errors.Add("Acceptance note must be 512 characters or fewer.");
        }

        var aggregate = BuildAggregate(requestId);
        if (!CanAcceptReleaseCandidate(aggregate, acceptance: null))
        {
            result.Errors.Add(BuildAcceptanceReason(aggregate, acceptance: null));
        }

        if (result.Errors.Count > 0)
        {
            return Task.FromResult(result);
        }

        var decision = new QuarantineJuryReviewDecision
        {
            Id = $"quarantine-jury-acceptance:{Guid.NewGuid():N}",
            RequestId = requestId,
            AcceptedBy = acceptanceRequest.AcceptedBy,
            AcceptedRecommendation = QuarantineJuryVerdict.ReleaseCandidate,
            Note = acceptanceRequest.Note,
            AggregateSnapshot = aggregate,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _reviewDecisions[requestId] = decision;
        PersistState();

        result.Decision = decision;
        return Task.FromResult(result);
    }

    public Task<QuarantineJuryReleasePackageResult> GetReleasePackageAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        requestId = requestId.Trim();
        var result = new QuarantineJuryReleasePackageResult();
        if (!_requests.TryGetValue(requestId, out var request))
        {
            result.Errors.Add("Request not found.");
            return Task.FromResult(result);
        }

        if (!_reviewDecisions.TryGetValue(requestId, out var acceptance) ||
            acceptance.AcceptedRecommendation != QuarantineJuryVerdict.ReleaseCandidate)
        {
            result.Errors.Add("Release candidate has not been accepted locally.");
            return Task.FromResult(result);
        }

        var currentAggregate = BuildAggregate(requestId);
        var verdicts = GetVerdictsForRequest(requestId)
            .OrderBy(verdict => verdict.Juror, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var routeAttempts = GetRouteAttemptsForRequest(requestId)
            .OrderByDescending(attempt => attempt.CreatedAt)
            .ThenBy(attempt => attempt.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var warnings = new List<string>
        {
            "Release package is evidence-only and does not change local quarantine enforcement.",
        };

        if (currentAggregate.Recommendation != acceptance.AggregateSnapshot.Recommendation ||
            currentAggregate.TotalVerdicts != acceptance.AggregateSnapshot.TotalVerdicts)
        {
            warnings.Add("Current verdict aggregate differs from the aggregate accepted by the operator.");
        }

        result.Package = new QuarantineJuryReleasePackage
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            RequestId = request.Id,
            LocalReason = request.LocalReason,
            RequestCreatedAt = request.CreatedAt,
            RequestEvidence = request.Evidence
                .OrderBy(evidence => evidence.Type)
                .ThenBy(evidence => evidence.Reference, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Jurors = request.Jurors
                .OrderBy(juror => juror, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CurrentAggregate = currentAggregate,
            Acceptance = acceptance,
            Verdicts = verdicts,
            RouteAttempts = routeAttempts,
            Warnings = warnings,
            MutatesLocalQuarantineState = false,
        };

        return Task.FromResult(result);
    }

    private QuarantineJuryAuditEntry BuildAuditEntry(
        QuarantineJuryRequest request,
        DateTimeOffset generatedAt,
        TimeSpan staleAfter)
    {
        var aggregate = BuildAggregate(request.Id);
        var verdicts = GetVerdictsForRequest(request.Id);
        var routeAttempts = GetRouteAttemptsForRequest(request.Id);
        _reviewDecisions.TryGetValue(request.Id, out var acceptance);
        var canAccept = CanAcceptReleaseCandidate(aggregate, acceptance);
        var status = BuildAuditStatus(aggregate, acceptance);
        var isStale = acceptance == null &&
            generatedAt - request.CreatedAt >= staleAfter &&
            status is "manual-review" or "pending-release-acceptance";

        return new QuarantineJuryAuditEntry
        {
            RequestId = request.Id,
            LocalReason = request.LocalReason,
            CreatedAt = request.CreatedAt,
            EvidenceCount = request.Evidence.Count,
            JurorCount = request.Jurors.Count,
            VerdictCount = verdicts.Count,
            RequiredVotes = aggregate.RequiredVotes,
            Recommendation = aggregate.Recommendation,
            QuorumReached = aggregate.QuorumReached,
            HasAcceptance = acceptance != null,
            CanAcceptReleaseCandidate = canAccept,
            HasRouteAttempts = routeAttempts.Count > 0,
            HasFailedRouteAttempts = routeAttempts.Any(attempt => !attempt.Success || attempt.FailedJurors.Count > 0),
            IsStale = isStale,
            Status = status,
            Reason = acceptance != null ? "Release-candidate recommendation accepted locally." : aggregate.Reason,
            DissentingJurors = aggregate.DissentingJurors,
        };
    }

    private static string BuildAuditStatus(QuarantineJuryAggregate aggregate, QuarantineJuryReviewDecision? acceptance)
    {
        if (acceptance != null)
        {
            return "accepted-release-candidate";
        }

        if (aggregate.Recommendation == QuarantineJuryVerdict.ReleaseCandidate && aggregate.QuorumReached)
        {
            return "pending-release-acceptance";
        }

        if (aggregate.Recommendation == QuarantineJuryVerdict.UpholdQuarantine && aggregate.QuorumReached)
        {
            return "uphold-quarantine";
        }

        return "manual-review";
    }

    private QuarantineJuryAggregate BuildAggregate(string requestId)
    {
        if (!_requests.TryGetValue(requestId, out var request))
        {
            return new QuarantineJuryAggregate
            {
                RequestId = requestId,
                RequiredVotes = 1,
                Reason = "Request not found.",
            };
        }

        var verdicts = GetVerdictsForRequest(requestId);
        var counts = verdicts
            .GroupBy(verdict => verdict.Verdict)
            .ToDictionary(group => group.Key, group => group.Count());
        var requiredVotes = Math.Max(1, request.MinJurorVotes);
        var recommendation = ResolveRecommendation(counts, verdicts.Count, requiredVotes);
        var dissentingJurors = verdicts
            .Where(verdict => verdict.Verdict != recommendation)
            .Select(verdict => verdict.Juror)
            .OrderBy(juror => juror, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new QuarantineJuryAggregate
        {
            RequestId = requestId,
            Recommendation = recommendation,
            TotalVerdicts = verdicts.Count,
            RequiredVotes = requiredVotes,
            VerdictCounts = counts,
            DissentingJurors = dissentingJurors,
            QuorumReached = verdicts.Count >= requiredVotes,
            Reason = BuildAggregateReason(recommendation, verdicts.Count, requiredVotes),
        };
    }

    public async Task<QuarantineJuryRouteAttempt> RouteRequestAsync(
        string requestId,
        QuarantineJuryRouteRequest routeRequest,
        CancellationToken cancellationToken = default)
    {
        requestId = requestId.Trim();
        routeRequest ??= new QuarantineJuryRouteRequest();
        if (!_requests.TryGetValue(requestId, out var request))
        {
            return StoreRouteAttempt(BuildRouteAttempt(requestId, routeRequest, Array.Empty<string>(), success: false, "Request not found."));
        }

        if (HasUnsafeRouteMetadata(routeRequest))
        {
            return StoreRouteAttempt(BuildRouteAttempt(requestId, routeRequest, Array.Empty<string>(), success: false, "Route metadata must be opaque and safe."));
        }

        var targetJurors = NormalizeRouteTargets(routeRequest.TargetJurors.Count == 0 ? request.Jurors : routeRequest.TargetJurors);
        var invalidTarget = targetJurors.FirstOrDefault(juror =>
            !request.Jurors.Contains(juror, StringComparer.OrdinalIgnoreCase) ||
            !IsSafeOpaqueReference(juror));
        if (invalidTarget != null)
        {
            return StoreRouteAttempt(BuildRouteAttempt(requestId, routeRequest, targetJurors, success: false, "Route targets must be selected safe jurors."));
        }

        if (_messageRouter == null)
        {
            return StoreRouteAttempt(BuildRouteAttempt(requestId, routeRequest, targetJurors, success: false, "Routing backend is not available."));
        }

        var message = BuildRouteMessage(request, routeRequest, targetJurors);
        var routingResult = await _messageRouter.RouteMessageToPeersAsync(message, targetJurors, cancellationToken).ConfigureAwait(false);
        var failedJurors = routingResult.FailedPeerIds?.ToList() ?? new List<string>();
        var routedJurors = targetJurors
            .Where(juror => !failedJurors.Contains(juror, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var attempt = new QuarantineJuryRouteAttempt
        {
            Id = $"quarantine-jury-route:{Guid.NewGuid():N}",
            RequestId = request.Id,
            MessageId = message.MessageId,
            PodId = message.PodId,
            ChannelId = message.ChannelId,
            TargetJurors = targetJurors,
            RoutedJurors = routedJurors,
            FailedJurors = failedJurors,
            Success = routingResult.Success,
            ErrorMessage = routingResult.ErrorMessage,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return StoreRouteAttempt(attempt);
    }

    public Task<IReadOnlyList<QuarantineJuryRouteAttempt>> GetRouteAttemptsAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        requestId = requestId.Trim();
        var attempts = GetRouteAttemptsForRequest(requestId)
            .OrderByDescending(attempt => attempt.CreatedAt)
            .ThenBy(attempt => attempt.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<QuarantineJuryRouteAttempt>>(attempts);
    }

    private QuarantineJuryValidationResult ValidateRequest(QuarantineJuryRequest request)
    {
        var result = new QuarantineJuryValidationResult();
        if (string.IsNullOrWhiteSpace(request.LocalReason))
        {
            result.Errors.Add("Local quarantine reason is required.");
        }

        if (request.Jurors.Count == 0)
        {
            result.Errors.Add("At least one trusted juror is required.");
        }

        if (request.Evidence.Count == 0)
        {
            result.Errors.Add("At least one minimal evidence item is required.");
        }

        foreach (var evidence in request.Evidence)
        {
            AddEvidenceErrors(result, evidence);
        }

        foreach (var juror in request.Jurors)
        {
            if (!IsSafeOpaqueReference(juror))
            {
                result.Errors.Add("Juror identifiers must be opaque and safe.");
            }
        }

        return result;
    }

    private QuarantineJuryValidationResult ValidateVerdict(QuarantineJuryVerdictRecord verdict)
    {
        var result = new QuarantineJuryValidationResult();
        if (!_requests.TryGetValue(verdict.RequestId, out var request))
        {
            result.Errors.Add("Request not found.");
            return result;
        }

        if (!request.Jurors.Contains(verdict.Juror, StringComparer.OrdinalIgnoreCase))
        {
            result.Errors.Add("Juror is not selected for this request.");
        }

        if (string.IsNullOrWhiteSpace(verdict.Signature.Signer) ||
            string.IsNullOrWhiteSpace(verdict.Signature.PayloadHash) ||
            string.IsNullOrWhiteSpace(verdict.Signature.Value))
        {
            result.Errors.Add("Signed juror verdict is required.");
        }
        else if (!string.Equals(verdict.Signature.PayloadHash, verdict.ComputePayloadHash(), StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Signature payload hash does not match verdict contents.");
        }

        foreach (var evidence in verdict.Evidence)
        {
            AddEvidenceErrors(result, evidence);
        }

        return result;
    }

    private static void AddEvidenceErrors(QuarantineJuryValidationResult result, QuarantineJuryEvidence evidence)
    {
        evidence.Reference = evidence.Reference?.Trim() ?? string.Empty;
        evidence.Summary = evidence.Summary?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(evidence.Reference))
        {
            result.Errors.Add("Evidence reference is required.");
        }

        if (!IsSafeOpaqueReference(evidence.Reference))
        {
            result.Errors.Add("Evidence references must not include paths, raw hashes, endpoints, or private identifiers.");
        }

        if (evidence.Summary.Length > 512)
        {
            result.Errors.Add("Evidence summary must be 512 characters or fewer.");
        }
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

    private static QuarantineJuryVerdict ResolveRecommendation(
        IReadOnlyDictionary<QuarantineJuryVerdict, int> counts,
        int totalVerdicts,
        int requiredVotes)
    {
        if (totalVerdicts < requiredVotes)
        {
            return QuarantineJuryVerdict.NeedsManualReview;
        }

        var best = counts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .FirstOrDefault();
        var requiredAgreement = (int)Math.Ceiling(totalVerdicts * 2 / 3.0);
        return best.Value >= requiredAgreement ? best.Key : QuarantineJuryVerdict.NeedsManualReview;
    }

    private static string BuildAggregateReason(QuarantineJuryVerdict recommendation, int totalVerdicts, int requiredVotes)
    {
        if (totalVerdicts < requiredVotes)
        {
            return $"Waiting for trusted juror quorum: {totalVerdicts}/{requiredVotes}.";
        }

        return recommendation == QuarantineJuryVerdict.NeedsManualReview
            ? "Trusted jurors did not reach a supermajority."
            : "Trusted jurors reached a supermajority recommendation.";
    }

    private static string BuildVerdictKey(string requestId, string juror)
    {
        return $"{requestId.Trim().ToLowerInvariant()}:{juror.Trim().ToLowerInvariant()}";
    }

    private static string BuildVerdictId(string requestId, string juror)
    {
        return $"quarantine-jury-verdict:{BuildVerdictKey(requestId, juror)}";
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
                var state = JsonSerializer.Deserialize<QuarantineJuryStoreState>(json, JsonOptions);
                if (state == null)
                {
                    return;
                }

                foreach (var request in state.Requests)
                {
                    _requests[request.Id] = request;
                }

                foreach (var verdict in state.Verdicts)
                {
                    _verdicts[BuildVerdictKey(verdict.RequestId, verdict.Juror)] = verdict;
                }

                foreach (var attempt in state.RouteAttempts)
                {
                    _routeAttempts[attempt.Id] = attempt;
                }

                foreach (var decision in state.ReviewDecisions)
                {
                    _reviewDecisions[decision.RequestId] = decision;
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "[QuarantineJury] Failed to load persisted jury state from {Path}", _storagePath);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[QuarantineJury] Failed to parse persisted jury state from {Path}", _storagePath);
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

            var state = new QuarantineJuryStoreState
            {
                Requests = _requests.Values
                    .OrderBy(request => request.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Verdicts = _verdicts.Values
                    .OrderBy(verdict => verdict.RequestId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(verdict => verdict.Juror, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                RouteAttempts = _routeAttempts.Values
                    .OrderBy(attempt => attempt.RequestId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(attempt => attempt.CreatedAt)
                    .ToList(),
                ReviewDecisions = _reviewDecisions.Values
                    .OrderBy(decision => decision.RequestId, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };

            var tempPath = $"{_storagePath}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(tempPath, _storagePath, overwrite: true);
        }
    }

    private sealed class QuarantineJuryStoreState
    {
        public List<QuarantineJuryRequest> Requests { get; set; } = new();

        public List<QuarantineJuryVerdictRecord> Verdicts { get; set; } = new();

        public List<QuarantineJuryRouteAttempt> RouteAttempts { get; set; } = new();

        public List<QuarantineJuryReviewDecision> ReviewDecisions { get; set; } = new();
    }

    private List<QuarantineJuryVerdictRecord> GetVerdictsForRequest(string requestId)
    {
        return _verdicts.Values
            .Where(verdict => string.Equals(verdict.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private List<QuarantineJuryRouteAttempt> GetRouteAttemptsForRequest(string requestId)
    {
        return _routeAttempts.Values
            .Where(attempt => string.Equals(attempt.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool CanAcceptReleaseCandidate(QuarantineJuryAggregate aggregate, QuarantineJuryReviewDecision? acceptance)
    {
        return acceptance == null &&
            aggregate.QuorumReached &&
            aggregate.Recommendation == QuarantineJuryVerdict.ReleaseCandidate;
    }

    private static string BuildAcceptanceReason(QuarantineJuryAggregate aggregate, QuarantineJuryReviewDecision? acceptance)
    {
        if (acceptance != null)
        {
            return "Release-candidate recommendation has already been accepted.";
        }

        if (!aggregate.QuorumReached)
        {
            return "Waiting for trusted juror quorum.";
        }

        return aggregate.Recommendation == QuarantineJuryVerdict.ReleaseCandidate
            ? "Release-candidate recommendation can be accepted manually."
            : "Only a release-candidate supermajority can be accepted.";
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

    private static bool HasUnsafeRouteMetadata(QuarantineJuryRouteRequest routeRequest)
    {
        return (!string.IsNullOrWhiteSpace(routeRequest.SenderPeerId) && !IsSafeOpaqueReference(routeRequest.SenderPeerId)) ||
            (!string.IsNullOrWhiteSpace(routeRequest.PodId) && !IsSafeOpaqueReference(routeRequest.PodId)) ||
            (!string.IsNullOrWhiteSpace(routeRequest.ChannelId) && !IsSafeOpaqueReference(routeRequest.ChannelId));
    }

    private static PodMessage BuildRouteMessage(
        QuarantineJuryRequest request,
        QuarantineJuryRouteRequest routeRequest,
        IReadOnlyList<string> targetJurors)
    {
        var podId = string.IsNullOrWhiteSpace(routeRequest.PodId) ? "quarantine-jury" : routeRequest.PodId.Trim();
        var channelId = string.IsNullOrWhiteSpace(routeRequest.ChannelId)
            ? $"request:{request.Id}"
            : routeRequest.ChannelId.Trim();
        var senderPeerId = string.IsNullOrWhiteSpace(routeRequest.SenderPeerId)
            ? "local-quarantine-jury"
            : routeRequest.SenderPeerId.Trim();
        var envelope = new QuarantineJuryRouteEnvelope
        {
            RequestId = request.Id,
            LocalReason = request.LocalReason,
            Evidence = request.Evidence,
            RequiredVotes = request.MinJurorVotes,
            TargetJurors = targetJurors.ToList(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return new PodMessage
        {
            MessageId = $"quarantine-jury-request:{Guid.NewGuid():N}",
            PodId = podId,
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = JsonSerializer.Serialize(envelope, JsonOptions),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "local-quarantine-jury-route",
        };
    }

    private static QuarantineJuryRouteAttempt BuildRouteAttempt(
        string requestId,
        QuarantineJuryRouteRequest routeRequest,
        IReadOnlyList<string> targets,
        bool success,
        string? errorMessage)
    {
        return new QuarantineJuryRouteAttempt
        {
            Id = $"quarantine-jury-route:{Guid.NewGuid():N}",
            RequestId = requestId,
            MessageId = string.Empty,
            PodId = string.IsNullOrWhiteSpace(routeRequest.PodId) ? "quarantine-jury" : routeRequest.PodId.Trim(),
            ChannelId = string.IsNullOrWhiteSpace(routeRequest.ChannelId) ? $"request:{requestId}" : routeRequest.ChannelId.Trim(),
            TargetJurors = targets.ToList(),
            Success = success,
            ErrorMessage = errorMessage,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private QuarantineJuryRouteAttempt StoreRouteAttempt(QuarantineJuryRouteAttempt attempt)
    {
        _routeAttempts[attempt.Id] = attempt;
        PersistState();
        return attempt;
    }

    private sealed class QuarantineJuryRouteEnvelope
    {
        public string Type { get; set; } = "slskdn.quarantine-jury.request.v1";

        public string RequestId { get; set; } = string.Empty;

        public string LocalReason { get; set; } = string.Empty;

        public List<QuarantineJuryEvidence> Evidence { get; set; } = new();

        public int RequiredVotes { get; set; }

        public List<string> TargetJurors { get; set; } = new();

        public DateTimeOffset CreatedAt { get; set; }
    }
}
