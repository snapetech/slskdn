// <copyright file="SourceCandidateValidationResult.cs" company="slskd Team">
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
