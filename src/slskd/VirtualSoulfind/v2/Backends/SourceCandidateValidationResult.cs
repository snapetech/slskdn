// <copyright file="SourceCandidateValidationResult.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Backends
{
    /// <summary>
    ///     Result of validating a source candidate.
    /// </summary>
    public sealed class SourceCandidateValidationResult
    {
        /// <summary>
        ///     Gets or initializes whether the candidate is still valid.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        ///     Gets or initializes the updated trust score (0.0 - 1.0).
        /// </summary>
        /// <remarks>
        ///     May be unchanged from the candidate's original trust score,
        ///     or adjusted based on validation results.
        /// </remarks>
        public float TrustScore { get; init; }

        /// <summary>
        ///     Gets or initializes the updated quality score (0.0 - 1.0).
        /// </summary>
        public float QualityScore { get; init; }

        /// <summary>
        ///     Gets or initializes the reason for invalidity (if IsValid = false).
        /// </summary>
        public string? InvalidityReason { get; init; }

        /// <summary>
        ///     Creates a valid result with unchanged scores.
        /// </summary>
        public static SourceCandidateValidationResult Valid(float trustScore, float qualityScore)
        {
            return new SourceCandidateValidationResult
            {
                IsValid = true,
                TrustScore = trustScore,
                QualityScore = qualityScore,
                InvalidityReason = null
            };
        }

        /// <summary>
        ///     Creates an invalid result with a reason.
        /// </summary>
        public static SourceCandidateValidationResult Invalid(string reason)
        {
            return new SourceCandidateValidationResult
            {
                IsValid = false,
                TrustScore = 0.0f,
                QualityScore = 0.0f,
                InvalidityReason = reason
            };
        }
    }
}
