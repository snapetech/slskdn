// <copyright file="SongIdModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SongID;

public sealed class SongIdRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Source { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string Status { get; set; } = "completed";

    public string Query { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string CurrentStage { get; set; } = string.Empty;

    public double PercentComplete { get; set; }

    public int? QueuePosition { get; set; }

    public int? WorkerSlot { get; set; }

    public string ArtifactDirectory { get; set; } = string.Empty;

    public List<string> Evidence { get; set; } = new();

    public List<SongIdTrackCandidate> Tracks { get; set; } = new();

    public List<SongIdAlbumCandidate> Albums { get; set; } = new();

    public List<SongIdArtistCandidate> Artists { get; set; } = new();

    public List<SongIdPlan> Plans { get; set; } = new();

    public List<SongIdAcquisitionOption> Options { get; set; } = new();

    public SongIdScorecard Scorecard { get; set; } = new();

    public SongIdAssessment Assessment { get; set; } = new();

    public SongIdMetadata Metadata { get; set; } = new();

    public SongIdFingerprintFinding? FullSourceFingerprint { get; set; }

    public SongIdProvenanceFinding Provenance { get; set; } = new();

    public SongIdAiHeuristicFinding? AiHeuristics { get; set; }

    public List<SongIdPerturbationFinding> Perturbations { get; set; } = new();

    public List<SongIdArtifactFinding> Stems { get; set; } = new();

    public List<SongIdCorpusMatch> CorpusMatches { get; set; } = new();

    public List<SongIdClipFinding> Clips { get; set; } = new();

    public List<SongIdTranscriptFinding> Transcripts { get; set; } = new();

    public List<SongIdOcrFinding> Ocr { get; set; } = new();

    public List<SongIdCommentFinding> Comments { get; set; } = new();

    public List<SongIdChapterFinding> Chapters { get; set; } = new();

    public List<SongIdSegmentResult> Segments { get; set; } = new();

    public List<SongIdMixGroup> MixGroups { get; set; } = new();

    public SongIdAssessment IdentityAssessment { get; set; } = new();

    public SongIdSyntheticAssessment SyntheticAssessment { get; set; } = new();

    public SongIdForensicMatrix? ForensicMatrix { get; set; }
}

public sealed class SongIdQueueSummary
{
    public int QueuedCount { get; set; }

    public int RunningCount { get; set; }

    public int CompletedCount { get; set; }

    public int FailedCount { get; set; }

    public int MaxConcurrentRuns { get; set; }

    public List<SongIdQueueSummaryRun> ActiveRuns { get; set; } = new();
}

public sealed class SongIdQueueSummaryRun
{
    public Guid RunId { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string CurrentStage { get; set; } = string.Empty;

    public double PercentComplete { get; set; }

    public int? QueuePosition { get; set; }

    public int? WorkerSlot { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Summary { get; set; } = string.Empty;
}

public sealed class SongIdRunEvidencePackage
{
    public Guid RunId { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string CurrentStage { get; set; } = string.Empty;

    public double PercentComplete { get; set; }

    public SongIdScorecard Scorecard { get; set; } = new();

    public SongIdAssessment IdentityAssessment { get; set; } = new();

    public SongIdSyntheticAssessment SyntheticAssessment { get; set; } = new();

    public SongIdForensicMatrix? ForensicMatrix { get; set; }

    public List<SongIdTrackCandidate> TrackCandidates { get; set; } = new();

    public List<SongIdAlbumCandidate> AlbumCandidates { get; set; } = new();

    public List<SongIdArtistCandidate> ArtistCandidates { get; set; } = new();

    public List<SongIdSegmentResult> Segments { get; set; } = new();

    public List<SongIdMixGroup> MixGroups { get; set; } = new();

    public List<SongIdPlan> Plans { get; set; } = new();

    public List<SongIdAcquisitionOption> AcquisitionOptions { get; set; } = new();

    public List<string> Evidence { get; set; } = new();

    public List<SongIdEvidenceArtifact> Artifacts { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
}

public sealed class SongIdEvidenceArtifact
{
    public string Kind { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public double? StartSeconds { get; set; }

    public double? DurationSeconds { get; set; }
}

public sealed class SongIdTrackCandidate
{
    public string CandidateId { get; set; } = string.Empty;

    public string RecordingId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string? MusicBrainzArtistId { get; set; }

    public bool IsExact { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public double CanonicalScore { get; set; }

    public int CanonicalVariantCount { get; set; }

    public bool HasLosslessCanonical { get; set; }

    public double IdentityScore { get; set; }

    public double ByzantineScore { get; set; }

    public double ActionScore { get; set; }
}

public sealed class SongIdAlbumCandidate
{
    public string CandidateId { get; set; } = string.Empty;

    public string ReleaseId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string? MusicBrainzArtistId { get; set; }

    public int TrackCount { get; set; }

    public bool IsExact { get; set; }

    public double CanonicalScore { get; set; }

    public int CanonicalSupportCount { get; set; }

    public double IdentityScore { get; set; }

    public double ByzantineScore { get; set; }

    public double ActionScore { get; set; }
}

public sealed class SongIdAcquisitionOption
{
    public string OptionId { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string Mode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ActionKind { get; set; } = string.Empty;

    public string ActionLabel { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string? SearchText { get; set; }

    public List<string> SearchTexts { get; set; } = new();

    public string? Profile { get; set; }

    public double QualityScore { get; set; }

    public double ByzantineScore { get; set; }

    public double ReadinessScore { get; set; }

    public double OverallScore { get; set; }
}

public sealed class SongIdScorecard
{
    public string SourceType { get; set; } = string.Empty;

    public string AnalysisAudioSource { get; set; } = string.Empty;

    public bool SpotifyTrackIdPresent { get; set; }

    public bool SpotifyPreviewUrlPresent { get; set; }

    public bool MatchedYoutubeCandidatePresent { get; set; }

    public int YoutubeCandidateCount { get; set; }

    public bool EmbeddedMetadataPresent { get; set; }

    public List<string> EmbeddedMetadataKeys { get; set; } = new();

    public int ClipCount { get; set; }

    public int AcoustIdHitCount { get; set; }

    public int SongRecHitCount { get; set; }

    public int SongRecDistinctMatchCount { get; set; }

    public int PanakoHitCount { get; set; }

    public int AudfprintHitCount { get; set; }

    public int CorpusMatchCount { get; set; }

    public int RawAcoustIdHitCount { get; set; }

    public int TranscriptCount { get; set; }

    public int OcrCount { get; set; }

    public int CommentFindingCount { get; set; }

    public int TimestampHintCount { get; set; }

    public int ChapterHintCount { get; set; }

    public int PlaylistRequestCount { get; set; }

    public int AiCommentMentionCount { get; set; }

    public int ProvenanceSignalCount { get; set; }

    public List<string> ProvenanceSignals { get; set; } = new();

    public int AiArtifactClipCount { get; set; }

    public int HighAiArtifactClipCount { get; set; }

    public double MeanAiArtifactScore { get; set; }

    public double MaxAiArtifactScore { get; set; }
}

public sealed class SongIdAssessment
{
    public string Verdict { get; set; } = "unclassified";

    public double Confidence { get; set; }

    public string Summary { get; set; } = string.Empty;
}

public sealed class SongIdSyntheticAssessment
{
    public string Verdict { get; set; } = "insufficient_evidence";

    public string Confidence { get; set; } = "low";

    public int SyntheticScore { get; set; }

    public int ConfidenceScore { get; set; }

    public int KnownFamilyScore { get; set; }

    public string FamilyLabel { get; set; } = "none";

    public string QualityClass { get; set; } = "clean_excerpt";

    public double PerturbationStability { get; set; }

    public List<string> TopEvidenceFor { get; set; } = new();

    public List<string> TopEvidenceAgainst { get; set; } = new();

    public List<string> Notes { get; set; } = new();

    public string Summary { get; set; } = string.Empty;
}

public sealed class SongIdMetadata
{
    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string Album { get; set; } = string.Empty;

    public string? SpotifyTrackId { get; set; }

    public string? PreviewUrl { get; set; }

    public string? AnalysisAudioSource { get; set; }

    public Dictionary<string, string> Extra { get; set; } = new();
}

public sealed class SongIdClipFinding
{
    public string ClipId { get; set; } = string.Empty;

    public string Profile { get; set; } = string.Empty;

    public int StartSeconds { get; set; }

    public int DurationSeconds { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public SongIdRecognizerFinding? AcoustId { get; set; }

    public SongIdRecognizerFinding? SongRec { get; set; }

    public SongIdRecognizerFinding? Panako { get; set; }

    public SongIdRecognizerFinding? Audfprint { get; set; }

    public SongIdAiHeuristicFinding? AiHeuristics { get; set; }
}

public sealed class SongIdRecognizerFinding
{
    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string? RecordingId { get; set; }

    public string? ExternalId { get; set; }

    public string? SourcePath { get; set; }

    public double Score { get; set; }

    public int MatchCount { get; set; }

    public string Summary { get; set; } = string.Empty;
}

public sealed class SongIdTranscriptFinding
{
    public string TranscriptId { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int SegmentCount { get; set; }

    public string? Language { get; set; }

    public int ExcerptStartSeconds { get; set; }

    public int ExcerptDurationSeconds { get; set; }

    public List<string> MusicBrainzQueries { get; set; } = new();
}

public sealed class SongIdOcrFinding
{
    public string OcrId { get; set; } = string.Empty;

    public int TimestampSeconds { get; set; }

    public string Text { get; set; } = string.Empty;
}

public sealed class SongIdCommentFinding
{
    public string CommentId { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int? TimestampSeconds { get; set; }
}

public sealed class SongIdChapterFinding
{
    public string ChapterId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int StartSeconds { get; set; }

    public int? EndSeconds { get; set; }
}

public sealed class SongIdArtifactFinding
{
    public string ArtifactId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

public sealed class SongIdCorpusMatch
{
    public string MatchId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public double SimilarityScore { get; set; }

    public string FingerprintPath { get; set; } = string.Empty;

    public string? RecordingId { get; set; }

    public string? Artist { get; set; }

    public string? Title { get; set; }

    public string? FamilyLabel { get; set; }

    public int KnownFamilyScore { get; set; }
}

public sealed class SongIdFingerprintFinding
{
    public string Path { get; set; } = string.Empty;

    public double DurationSeconds { get; set; }

    public int FingerprintLength { get; set; }
}

public sealed class SongIdProvenanceFinding
{
    public int SignalCount { get; set; }

    public List<string> Signals { get; set; } = new();

    public bool ToolAvailable { get; set; }

    public bool ManifestHint { get; set; }

    public bool Verified { get; set; }

    public string? ValidationState { get; set; }
}

public sealed class SongIdAiHeuristicFinding
{
    public double ArtifactScore { get; set; }

    public string ArtifactLabel { get; set; } = string.Empty;

    public int PeakCount { get; set; }

    public double PeakDensity { get; set; }

    public double PeriodicityStrength { get; set; }

    public double DominantSpacingHz { get; set; }

    public double ResidualRatio { get; set; }

    public double SpectralCentroid { get; set; }

    public double SpectralFlux { get; set; }

    public double PitchSalience { get; set; }

    public double DurationSuspicion { get; set; }

    public int SampleRate { get; set; }

    public double AnalysisSeconds { get; set; }
}

public sealed class SongIdPerturbationFinding
{
    public string PerturbationId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public double BaselineDelta { get; set; }

    public SongIdAiHeuristicFinding? Heuristics { get; set; }
}

public sealed class SongIdSegmentResult
{
    public string SegmentId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string SourceLabel { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public string DecompositionLabel { get; set; } = string.Empty;

    public int StartSeconds { get; set; }

    public double Confidence { get; set; }

    public List<SongIdTrackCandidate> Candidates { get; set; } = new();

    public List<SongIdPlan> Plans { get; set; } = new();

    public List<SongIdAcquisitionOption> Options { get; set; } = new();
}

public sealed class SongIdMixGroup
{
    public string MixId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public List<string> SegmentIds { get; set; } = new();

    public double Confidence { get; set; }

    public double IdentityScore { get; set; }

    public double ByzantineScore { get; set; }

    public double ActionScore { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public int SegmentCount => SegmentIds.Count;
}

public sealed class SongIdForensicMatrix
{
    public int IdentityScore { get; set; }

    public int SyntheticScore { get; set; }

    public int ConfidenceScore { get; set; }

    public int KnownFamilyScore { get; set; }

    public string FamilyLabel { get; set; } = "none";

    public string QualityClass { get; set; } = "clean_excerpt";

    public List<string> TopEvidenceFor { get; set; } = new();

    public List<string> TopEvidenceAgainst { get; set; } = new();

    public Dictionary<string, double> LaneScores { get; set; } = new();

    public Dictionary<string, int> LaneConfidences { get; set; } = new();

    public double PerturbationStability { get; set; }

    public List<string> Notes { get; set; } = new();

    public SongIdForensicLane IdentityLane { get; set; } = new();

    public SongIdForensicLane ConfidenceLane { get; set; } = new();

    public SongIdForensicLane SpectralArtifactLane { get; set; } = new();

    public SongIdForensicLane DescriptorPriorsLane { get; set; } = new();

    public SongIdForensicLane LyricsSpeechLane { get; set; } = new();

    public SongIdForensicLane StructuralLane { get; set; } = new();

    public SongIdForensicLane ProvenanceLane { get; set; } = new();

    public SongIdForensicLane GeneratorFamilyLane { get; set; } = new();
}

public sealed class SongIdForensicLane
{
    public string Label { get; set; } = string.Empty;

    public double Score { get; set; }

    public int Confidence { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Dictionary<string, string> Metrics { get; set; } = new();
}

public sealed class SongIdArtistCandidate
{
    public string CandidateId { get; set; } = string.Empty;

    public string ArtistId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int ReleaseGroupCount { get; set; }

    public string RecommendedProfile { get; set; } = "CoreDiscography";

    public double CanonicalScore { get; set; }

    public int CanonicalSupportCount { get; set; }

    public double IdentityScore { get; set; }

    public double ByzantineScore { get; set; }

    public double ActionScore { get; set; }
}

public sealed class SongIdPlan
{
    public string PlanId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string ActionLabel { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string? SearchText { get; set; }

    public string? Profile { get; set; }

    public double IdentityScore { get; set; }

    public double ByzantineScore { get; set; }

    public double ActionScore { get; set; }
}
