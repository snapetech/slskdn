// <copyright file="PrivacyControls.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Capture;

/// <summary>
/// Privacy and data retention controls for Virtual Soulfind capture.
/// </summary>
public class PrivacyControls
{
    /// <summary>
    /// Anonymization level.
    /// </summary>
    public enum AnonymizationLevel
    {
        /// <summary>
        /// No anonymization, store raw usernames (NOT RECOMMENDED).
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Pseudonymize usernames with local salt.
        /// </summary>
        Pseudonymized = 1,
        
        /// <summary>
        /// No username storage, only aggregate statistics.
        /// </summary>
        Aggregate = 2
    }

    /// <summary>
    /// Data retention policy.
    /// </summary>
    public class RetentionPolicy
    {
        /// <summary>
        /// Maximum age for raw observations (for debugging).
        /// </summary>
        public TimeSpan RawObservationRetention { get; set; } = TimeSpan.FromDays(7);
        
        /// <summary>
        /// Maximum age for normalized variants in local cache.
        /// </summary>
        public TimeSpan VariantCacheRetention { get; set; } = TimeSpan.FromDays(30);
        
        /// <summary>
        /// Whether to persist raw observations to disk.
        /// </summary>
        public bool PersistRawObservations { get; set; } = false;
    }

    /// <summary>
    /// Get configured anonymization level.
    /// </summary>
    public static AnonymizationLevel GetAnonymizationLevel(Options options)
    {
        var level = options.VirtualSoulfind?.Privacy?.AnonymizationLevel ?? "Pseudonymized";
        return Enum.Parse<AnonymizationLevel>(level, ignoreCase: true);
    }

    /// <summary>
    /// Get configured retention policy.
    /// </summary>
    public static RetentionPolicy GetRetentionPolicy(Options options)
    {
        var config = options.VirtualSoulfind?.Privacy;
        return new RetentionPolicy
        {
            RawObservationRetention = TimeSpan.FromDays(config?.RawObservationRetentionDays ?? 7),
            VariantCacheRetention = TimeSpan.FromDays(config?.VariantCacheRetentionDays ?? 30),
            PersistRawObservations = config?.PersistRawObservations ?? false
        };
    }
}
