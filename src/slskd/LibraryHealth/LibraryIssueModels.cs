// <copyright file="LibraryIssueModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.LibraryHealth
{
    using System;
    using System.Collections.Generic;

    public enum LibraryIssueType
    {
        SuspectedTranscode,
        NonCanonicalVariant,
        TrackNotInTaggedRelease,
        MissingTrackInRelease,
        CorruptedFile,
        MissingMetadata,
        MultipleVariants,
        WrongDuration,
    }

    public enum LibraryIssueSeverity
    {
        Info,
        Low,
        Medium,
        High,
        Critical,
    }

    public enum LibraryIssueStatus
    {
        Detected,
        Acknowledged,
        Ignored,
        Fixing,
        Resolved,
        Failed,
    }

    public class LibraryIssue
    {
        public string IssueId { get; set; }
        public LibraryIssueType Type { get; set; }
        public LibraryIssueSeverity Severity { get; set; }

        // Affected entities
        public string FilePath { get; set; }
        public string MusicBrainzRecordingId { get; set; }
        public string MusicBrainzReleaseId { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Title { get; set; }

        // Issue details
        public string Reason { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        // Remediation
        public bool CanAutoFix { get; set; }
        public string SuggestedAction { get; set; }
        public string RemediationJobId { get; set; }

        // Status
        public LibraryIssueStatus Status { get; set; }
        public DateTimeOffset DetectedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }
    }

    public class LibraryHealthScan
    {
        public string ScanId { get; set; }
        public string LibraryPath { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public ScanStatus Status { get; set; }
        public int FilesScanned { get; set; }
        public int IssuesDetected { get; set; }
        public string ErrorMessage { get; set; }
    }

    public enum ScanStatus
    {
        Running,
        Completed,
        Failed,
        Cancelled,
    }

    public class LibraryHealthSummary
    {
        public string LibraryPath { get; set; }
        public int TotalIssues { get; set; }
        public int IssuesOpen { get; set; }
        public int IssuesResolved { get; set; }
    }

    public class IssueCodecGroup
    {
        public string Codec { get; set; }

        public int Count { get; set; }

        public int TranscodeSuspect { get; set; }
    }

    public class LibraryHealthScanRequest
    {
        public string LibraryPath { get; set; }
        public bool IncludeSubdirectories { get; set; } = true;
        public List<string> FileExtensions { get; set; } = new() { ".flac", ".mp3", ".m4a", ".ogg" };
        public bool SkipPreviouslyScanned { get; set; } = false;
        public int MaxConcurrentFiles { get; set; } = 4;
    }

    public class LibraryHealthIssueFilter
    {
        public string LibraryPath { get; set; }
        public List<LibraryIssueType> Types { get; set; }
        public List<LibraryIssueSeverity> Severities { get; set; }
        public List<LibraryIssueStatus> Statuses { get; set; }
        public string MusicBrainzReleaseId { get; set; }
        public int Limit { get; set; } = 100;
        public int Offset { get; set; } = 0;
    }
}
