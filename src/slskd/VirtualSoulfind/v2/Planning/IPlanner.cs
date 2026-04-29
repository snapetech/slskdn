// <copyright file="IPlanner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Planning
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.v2.Intents;

    /// <summary>
    ///     Interface for the multi-source planner.
    /// </summary>
    /// <remarks>
    ///     The planner is the "brain" of VirtualSoulfind v2. Given a desired track/release,
    ///     it produces an acquisition plan that:
    ///
    ///     - Selects appropriate backends based on domain rules
    ///     - Filters sources through MCP (no blocked/quarantined)
    ///     - Orders candidates by trust + quality scores
    ///     - Respects per-backend caps (H-08 for Soulseek)
    ///     - Respects work budgets (H-02)
    ///     - Enforces planning mode (OfflinePlanning/MeshOnly/SoulseekFriendly)
    ///
    ///     The planner is pure logic - it doesn't execute plans, just creates them.
    /// </remarks>
    public interface IPlanner
    {
        /// <summary>
        ///     Creates an acquisition plan for a desired track.
        /// </summary>
        /// <param name="desiredTrack">The intent to plan for.</param>
        /// <param name="mode">The planning mode (overrides default if specified).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An acquisition plan (may have empty steps if no sources available).</returns>
        /// <remarks>
        ///     This method:
        ///     - Looks up the track in the catalogue
        ///     - Queries source registry for candidates
        ///     - Queries backends for additional candidates
        ///     - Filters through MCP (CheckContentIdAsync)
        ///     - Orders by trust/quality
        ///     - Builds PlanSteps respecting backend caps
        ///
        ///     This is async because it may query backends and MCP.
        ///     It does NOT execute the plan (that's the resolver's job).
        /// </remarks>
        Task<TrackAcquisitionPlan> CreatePlanAsync(
            DesiredTrack desiredTrack,
            PlanningMode? mode = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Validates that a plan is still executable.
        /// </summary>
        /// <param name="plan">The plan to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if plan can be executed; false if it violates current constraints.</returns>
        /// <remarks>
        ///     Validation checks:
        ///     - Backend availability (are backends still enabled?)
        ///     - Work budget (would this plan exceed budgets?)
        ///     - Per-backend caps (would this violate Soulseek caps?)
        ///
        ///     Used before execution to avoid starting a plan that will fail.
        /// </remarks>
        Task<bool> ValidatePlanAsync(
            TrackAcquisitionPlan plan,
            CancellationToken cancellationToken = default);
    }
}
