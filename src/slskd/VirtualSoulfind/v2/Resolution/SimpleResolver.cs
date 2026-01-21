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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Execution;
    using slskd.VirtualSoulfind.v2.Planning;

    /// <summary>
    ///     Simple implementation of <see cref="IResolver"/>.
    /// </summary>
    /// <remarks>
    ///     Phase 2 implementation: Sequential step execution with basic fallback.
    ///     Future: Parallel execution, advanced retry strategies, work budget integration.
    /// </remarks>
    public sealed class SimpleResolver : IResolver
    {
        private readonly IOptionsMonitor<ResolverOptions> _options;
        private readonly IContentBackend[] _backends;
        private readonly ConcurrentDictionary<string, PlanExecutionState> _executions = new();

        public SimpleResolver(
            IOptionsMonitor<ResolverOptions> options,
            IEnumerable<IContentBackend> backends)
        {
            _options = options;
            _backends = backends.ToArray();
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

                    var stepResult = await ExecuteStepAsync(step, cancellationToken);

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
            PlanStep step,
            CancellationToken cancellationToken)
        {
            // Find backend
            var backend = _backends.FirstOrDefault(b => b.Type == step.Backend);
            if (backend == null)
            {
                return StepResult.Failure($"Backend {step.Backend} not available");
            }

            // Try each candidate in the step
            foreach (var candidate in step.Candidates.Take(step.MaxParallel))
            {
                try
                {
                    // Validate candidate
                    var validationResult = await backend.ValidateCandidateAsync(candidate, cancellationToken);
                    if (!validationResult.IsValid)
                    {
                        continue; // Try next candidate
                    }

                    // TODO: T-V2-P5-01 - Actually fetch/download content here
                    // For now, we just simulate success for valid candidates
                    return StepResult.Success();
                }
                catch (Exception ex)
                {
                    // Log and continue to next candidate
                    continue;
                }
            }

            return StepResult.Failure("All candidates in step failed");
        }

        private PlanExecutionState UpdateState(PlanExecutionState newState)
        {
            _executions[newState.ExecutionId] = newState;
            return newState;
        }

        private sealed class StepResult
        {
            public bool IsSuccess { get; init; }
            public string? ErrorMessage { get; init; }

            public static StepResult Success() => new() { IsSuccess = true };
            public static StepResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
        }
    }
}
