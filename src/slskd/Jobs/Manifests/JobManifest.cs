// <copyright file="JobManifest.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Jobs.Manifests
{
    using System;
    using System.Collections.Generic;

    public class JobManifest
    {
        public string ManifestVersion { get; set; } = "1.0";

        public string JobId { get; set; } = Guid.NewGuid().ToString("N");

        public JobType JobType { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        ///     Polymorphic spec based on JobType (see spec types below).
        /// </summary>
        public object Spec { get; set; } = new();

        public JobManifestStatus Status { get; set; } = new();
    }

    public enum JobType
    {
        MbRelease,
        Discography,
        LabelCrate,
        MultiSource,
    }

    public class JobManifestStatus
    {
        /// <summary>pending | running | completed | failed | cancelled</summary>
        public string State { get; set; } = "pending";

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public List<int> CompletedTracks { get; set; } = new();

        public List<int> InProgressTracks { get; set; } = new();

        public List<int> PendingTracks { get; set; } = new();

        public long BytesTotal { get; set; }

        public long BytesDone { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class DownloadConstraints
    {
        public List<string> PreferredCodecs { get; set; } = new();

        public bool AllowLossy { get; set; } = true;

        public bool PreferCanonical { get; set; } = true;

        public bool UseOverlay { get; set; } = true;

        public int? OverlayBandwidthKbps { get; set; }

        public int? MaxLossyTracksPerAlbum { get; set; }
    }

    public class TrackSpec
    {
        public int Position { get; set; }

        public string Title { get; set; } = string.Empty;

        public string MbRecordingId { get; set; } = string.Empty;

        public int? DurationMs { get; set; }
    }

    public class MbReleaseJobSpec
    {
        public string MbReleaseId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string TargetDir { get; set; } = string.Empty;

        public List<TrackSpec> Tracks { get; set; } = new();

        public DownloadConstraints Constraints { get; set; } = new();
    }

    public class DiscographyJobSpec
    {
        public string ArtistId { get; set; } = string.Empty;

        public string ArtistName { get; set; } = string.Empty;

        /// <summary>core | extended | all</summary>
        public string Profile { get; set; } = string.Empty;

        public string TargetDir { get; set; } = string.Empty;

        public DownloadConstraints Constraints { get; set; } = new();
    }

    public class LabelCrateJobSpec
    {
        public string LabelId { get; set; } = string.Empty;

        public string LabelName { get; set; } = string.Empty;

        public int Limit { get; set; } = 10;

        public DownloadConstraints Constraints { get; set; } = new();
    }
}
