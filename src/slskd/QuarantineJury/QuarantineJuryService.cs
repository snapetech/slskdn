// <copyright file="QuarantineJuryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.QuarantineJury;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

public sealed class QuarantineJuryService : IQuarantineJuryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ConcurrentDictionary<string, QuarantineJuryRequest> _requests = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, QuarantineJuryVerdictRecord> _verdicts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _storageSync = new();
    private readonly string _storagePath;
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

    public QuarantineJuryService(ILogger<QuarantineJuryService> logger, string storagePath)
    {
        _logger = logger;
        _storagePath = storagePath;
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
        if (!_requests.TryGetValue(requestId, out var request))
        {
            return Task.FromResult(new QuarantineJuryAggregate
            {
                RequestId = requestId,
                RequiredVotes = 1,
                Reason = "Request not found.",
            });
        }

        var verdicts = _verdicts.Values
            .Where(verdict => string.Equals(verdict.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
            .ToList();
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

        return Task.FromResult(new QuarantineJuryAggregate
        {
            RequestId = requestId,
            Recommendation = recommendation,
            TotalVerdicts = verdicts.Count,
            RequiredVotes = requiredVotes,
            VerdictCounts = counts,
            DissentingJurors = dissentingJurors,
            QuorumReached = verdicts.Count >= requiredVotes,
            Reason = BuildAggregateReason(recommendation, verdicts.Count, requiredVotes),
        });
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
    }
}
