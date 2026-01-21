// <copyright file="CanonicalStats.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Audio
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Aggregated statistics for a (Recording ID, Codec Profile) pair.
    /// </summary>
    public class CanonicalStats
    {
        public string Id { get; set; }  // recordingId + ':' + codecProfileKey
        public string MusicBrainzRecordingId { get; set; }
        public string CodecProfileKey { get; set; }

        // Aggregated counts
        public int VariantCount { get; set; }
        public int TotalSeenCount { get; set; }

        // Quality metrics
        public double AvgQualityScore { get; set; }
        public double MaxQualityScore { get; set; }
        public double PercentTranscodeSuspect { get; set; }

        // Distributions
        public Dictionary<string, int> CodecDistribution { get; set; } = new();
        public Dictionary<int, int> BitrateDistribution { get; set; } = new();
        public Dictionary<int, int> SampleRateDistribution { get; set; } = new();

        // Canonical candidate
        public string BestVariantId { get; set; }
        public double CanonicalityScore { get; set; }

        public DateTimeOffset LastUpdated { get; set; }
    }
}
