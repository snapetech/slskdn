namespace slskd
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Warm cache options (Phase 4C).
    /// </summary>
    public class WarmCacheOptions : IValidatableObject
    {
        /// <summary>
        ///     Enable warm cache feature.
        /// </summary>
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Maximum storage budget in gigabytes.
        /// </summary>
        public int MaxStorageGb { get; init; } = 50;

        /// <summary>
        ///     Minimum popularity threshold (0..1) to qualify for caching.
        /// </summary>
        public double MinPopularityThreshold { get; init; } = 0.1;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enabled)
            {
                yield break;
            }

            if (MaxStorageGb <= 0)
            {
                yield return new ValidationResult("WarmCache.MaxStorageGb must be > 0", new[] { nameof(MaxStorageGb) });
            }

            if (MinPopularityThreshold < 0 || MinPopularityThreshold > 1)
            {
                yield return new ValidationResult("WarmCache.MinPopularityThreshold must be between 0 and 1", new[] { nameof(MinPopularityThreshold) });
            }
        }
    }
}

