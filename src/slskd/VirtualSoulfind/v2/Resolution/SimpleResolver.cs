// <copyright file="SimpleResolver.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.VirtualSoulfind.v2.Resolution
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using slskd.Mesh.ServiceFabric;
    using slskd.Signals.Swarm;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Execution;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Simple implementation of <see cref="IResolver"/>.
    /// </summary>
    /// <remarks>
    ///     Phase 2 implementation: Sequential step execution with basic fallback.
    ///     Performs fetch for Http, WebDav, S3 (via IContentFetchBackend), Torrent (via IBitTorrentBackend when supported),
    ///     and NativeMesh (explicit fail until mesh GetContentByContentId RPC exists).
    /// </remarks>
    public sealed class SimpleResolver : IResolver
    {
        private readonly IOptionsMonitor<ResolverOptions> _options;
        private readonly IContentBackend[] _backends;
        private readonly IBitTorrentBackend? _btBackend;
        private readonly IMeshServiceClient? _meshClient;
        private readonly ConcurrentDictionary<string, PlanExecutionState> _executions = new();

        public SimpleResolver(
            IOptionsMonitor<ResolverOptions> options,
            IEnumerable<IContentBackend> backends,
            IBitTorrentBackend? btBackend = null,
            IMeshServiceClient? meshClient = null)
        {
            _options = options;
            _backends = backends.ToArray();
            _btBackend = btBackend;
            _meshClient = meshClient;
        }

        /// <summary>
        ///     Execute a plan.
        /// </summary>
        public async Task<PlanExecutionState> ExecutePlanAsync(
            TrackAcquisitionPlan plan,
            CancellationToken cancellationToken = default)
        {
            if (plan == null || !plan.IsExecutable)
            {
                throw new ArgumentException("Plan is null or not executable", nameof(plan));
            }

            var executionId = Guid.NewGuid().ToString();
            var state = new PlanExecutionState
            {
                ExecutionId = executionId,
                TrackId = plan.TrackId,
                Status = PlanExecutionStatus.Running,
                CurrentStepIndex = 0,
                TotalSteps = plan.Steps.Count,
                StartedAt = DateTimeOffset.UtcNow,
            };

            _executions[executionId] = state;

            try
            {
                // Execute steps sequentially
                for (int i = 0; i < plan.Steps.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var step = plan.Steps[i];
                    state = UpdateState(new PlanExecutionState
                    {
                        ExecutionId = state.ExecutionId,
                        TrackId = state.TrackId,
                        Status = PlanExecutionStatus.Running,
                        CurrentStepIndex = i,
                        TotalSteps = state.TotalSteps,
                        StartedAt = state.StartedAt,
                    });

                    var stepResult = await ExecuteStepAsync(plan, step, cancellationToken);

                    if (stepResult.IsSuccess)
                    {
                        // Success! Mark as complete
                        state = UpdateState(new PlanExecutionState
                        {
                            ExecutionId = state.ExecutionId,
                            TrackId = state.TrackId,
                            Status = PlanExecutionStatus.Succeeded,
                            CurrentStepIndex = state.CurrentStepIndex,
                            TotalSteps = state.TotalSteps,
                            StartedAt = state.StartedAt,
                            CompletedAt = DateTimeOffset.UtcNow,
                            FetchedFilePath = stepResult.FetchedFilePath,
                        });
                        return state;
                    }

                    // Step failed, continue to next step if available
                    if (i == plan.Steps.Count - 1)
                    {
                        // Last step failed
                        state = UpdateState(new PlanExecutionState
                        {
                            ExecutionId = state.ExecutionId,
                            TrackId = state.TrackId,
                            Status = PlanExecutionStatus.Failed,
                            CurrentStepIndex = state.CurrentStepIndex,
                            TotalSteps = state.TotalSteps,
                            StartedAt = state.StartedAt,
                            CompletedAt = DateTimeOffset.UtcNow,
                            ErrorMessage = stepResult.ErrorMessage ?? "All steps failed",
                        });
                        return state;
                    }
                }

                // Shouldn't reach here, but handle gracefully
                state = UpdateState(new PlanExecutionState
                {
                    ExecutionId = state.ExecutionId,
                    TrackId = state.TrackId,
                    Status = PlanExecutionStatus.Failed,
                    CurrentStepIndex = state.CurrentStepIndex,
                    TotalSteps = state.TotalSteps,
                    StartedAt = state.StartedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = "Plan completed without success",
                });
                return state;
            }
            catch (OperationCanceledException)
            {
                state = UpdateState(new PlanExecutionState
                {
                    ExecutionId = state.ExecutionId,
                    TrackId = state.TrackId,
                    Status = PlanExecutionStatus.Cancelled,
                    CurrentStepIndex = state.CurrentStepIndex,
                    TotalSteps = state.TotalSteps,
                    StartedAt = state.StartedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = "Execution cancelled",
                });
                return state;
            }
            catch (Exception ex)
            {
                state = UpdateState(new PlanExecutionState
                {
                    ExecutionId = state.ExecutionId,
                    TrackId = state.TrackId,
                    Status = PlanExecutionStatus.Failed,
                    CurrentStepIndex = state.CurrentStepIndex,
                    TotalSteps = state.TotalSteps,
                    StartedAt = state.StartedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                });
                return state;
            }
        }

        /// <summary>
        ///     Get execution status.
        /// </summary>
        public Task<PlanExecutionState?> GetExecutionStatusAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            _executions.TryGetValue(executionId, out var state);
            return Task.FromResult<PlanExecutionState?>(state);
        }

        private async Task<StepResult> ExecuteStepAsync(
            TrackAcquisitionPlan plan,
            PlanStep step,
            CancellationToken cancellationToken)
        {
            var backend = _backends.FirstOrDefault(b => b.Type == step.Backend);
            if (backend == null)
                return StepResult.Failure($"Backend {step.Backend} not available");

            var downloadDir = string.IsNullOrWhiteSpace(_options.CurrentValue.DownloadDirectory)
                ? Path.GetTempPath()
                : _options.CurrentValue.DownloadDirectory!;

            using var stepTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (step.Timeout > TimeSpan.Zero)
            {
                stepTimeoutCts.CancelAfter(step.Timeout);
            }

            var stepCancellationToken = stepTimeoutCts.Token;

            if (step.FallbackMode == PlanStepFallbackMode.FanOut)
            {
                return await ExecuteFanOutStepAsync(step, backend, downloadDir, stepCancellationToken);
            }

            return await ExecuteCascadeStepAsync(step, backend, downloadDir, stepCancellationToken);
        }

        private async Task<StepResult> ExecuteCascadeStepAsync(
            PlanStep step,
            IContentBackend backend,
            string downloadDir,
            CancellationToken cancellationToken)
        {
            foreach (var candidate in step.Candidates.Take(Math.Max(1, step.MaxParallel)))
            {
                var result = backend.Type == ContentBackendType.NativeMesh
                    ? await ExecuteNativeMeshCandidateAsync(candidate, downloadDir, cancellationToken)
                    : await ExecuteCandidateAsync(step.Backend, backend, candidate, downloadDir, cancellationToken);

                if (result.IsSuccess)
                {
                    return result;
                }
            }

            return StepResult.Failure("All candidates in step failed");
        }

        private async Task<StepResult> ExecuteFanOutStepAsync(
            PlanStep step,
            IContentBackend backend,
            string downloadDir,
            CancellationToken cancellationToken)
        {
            var candidates = step.Candidates.ToList();
            var chunkSize = Math.Max(1, step.MaxParallel);

            for (var offset = 0; offset < candidates.Count; offset += chunkSize)
            {
                var chunk = candidates.Skip(offset).Take(chunkSize).ToList();
                var tasks = chunk.Select(candidate =>
                    backend.Type == ContentBackendType.NativeMesh
                        ? ExecuteNativeMeshCandidateAsync(candidate, downloadDir, cancellationToken)
                        : ExecuteCandidateAsync(step.Backend, backend, candidate, downloadDir, cancellationToken))
                    .ToList();

                var results = await Task.WhenAll(tasks);
                var success = results.FirstOrDefault(result => result.IsSuccess);
                if (success != null)
                {
                    return success;
                }
            }

            return StepResult.Failure("All candidates in step failed");
        }

        private async Task<StepResult> ExecuteNativeMeshCandidateAsync(
            SourceCandidate candidate,
            string downloadDir,
            CancellationToken cancellationToken)
        {
            if (_meshClient == null)
            {
                return StepResult.Failure("NativeMesh fetch requires IMeshServiceClient (not injected)");
            }

            if (!TryParseMeshBackendRef(candidate.BackendRef, out var peerId, out var contentId) ||
                string.IsNullOrWhiteSpace(peerId))
            {
                return StepResult.Failure("Invalid NativeMesh candidate reference");
            }

            try
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(new { contentId });
                var call = new ServiceCall
                {
                    ServiceName = "MeshContent",
                    Method = "GetByContentId",
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    Payload = payload,
                };
                var reply = await _meshClient.CallAsync(peerId, call, cancellationToken);
                if (!reply.IsSuccess || reply.Payload == null || reply.Payload.Length == 0)
                {
                    return StepResult.Failure("NativeMesh GetByContentId failed");
                }

                var tmpPath = Path.Combine(downloadDir, $"vs2_nativemesh_{Guid.NewGuid():N}.tmp");
                await File.WriteAllBytesAsync(tmpPath, reply.Payload, cancellationToken);
                return StepResult.Success(tmpPath);
            }
            catch (Exception ex)
            {
                return StepResult.Failure(ex.Message);
            }
        }

        private async Task<StepResult> ExecuteCandidateAsync(
            ContentBackendType backendType,
            IContentBackend backend,
            SourceCandidate candidate,
            string downloadDir,
            CancellationToken cancellationToken)
        {
            try
            {
                var validationResult = await backend.ValidateCandidateAsync(candidate, cancellationToken);
                if (!validationResult.IsValid)
                {
                    return StepResult.Failure("Candidate validation failed");
                }

                if (backend is IContentFetchBackend fetchBackend)
                {
                    var tmpPath = Path.Combine(downloadDir, $"vs2_{backendType}_{Guid.NewGuid():N}.tmp");
                    await using (var fs = File.Create(tmpPath))
                    {
                        await fetchBackend.FetchToStreamAsync(candidate, fs, cancellationToken);
                    }

                    return StepResult.Success(tmpPath);
                }

                if (backendType == ContentBackendType.Torrent && _btBackend != null)
                {
                    var path = await _btBackend.FetchByInfoHashOrMagnetAsync(candidate.BackendRef, downloadDir, cancellationToken);
                    return path == null
                        ? StepResult.Failure("Torrent fetch failed")
                        : StepResult.Success(path);
                }

                if (backendType == ContentBackendType.LocalLibrary)
                {
                    return StepResult.Success(candidate.BackendRef);
                }

                return StepResult.Failure($"Backend {backendType} has no fetch implementation");
            }
            catch (Exception ex)
            {
                return StepResult.Failure(ex.Message);
            }
        }

        private PlanExecutionState UpdateState(PlanExecutionState newState)
        {
            _executions[newState.ExecutionId] = newState;
            return newState;
        }

        private static bool TryParseMeshBackendRef(string? backendRef, out string? peerId, out string? contentId)
        {
            peerId = null;
            contentId = null;
            if (string.IsNullOrWhiteSpace(backendRef) || !backendRef.StartsWith("mesh:", StringComparison.OrdinalIgnoreCase))
                return false;
            var rest = backendRef.Substring(5);
            var idx = rest.IndexOf(':');
            if (idx <= 0 || idx >= rest.Length - 1)
                return false;
            peerId = rest.Substring(0, idx);
            contentId = rest.Substring(idx + 1);
            return !string.IsNullOrEmpty(peerId) && !string.IsNullOrEmpty(contentId);
        }

        private sealed class StepResult
        {
            public bool IsSuccess { get; init; }
            public string? ErrorMessage { get; init; }
            public string? FetchedFilePath { get; init; }

            public static StepResult Success(string? fetchedFilePath = null) => new() { IsSuccess = true, FetchedFilePath = fetchedFilePath };
            public static StepResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
        }
    }
}
