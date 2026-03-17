// <copyright file="SongIdScoringTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SongID;

using slskd.Audio;
using slskd.SongID;
using Xunit;

public sealed class SongIdScoringTests
{
    [Fact]
    public void ApplyCanonicalTrackSignals_BoostsTrackAndFlagsLosslessSupport()
    {
        var track = new SongIdTrackCandidate
        {
            RecordingId = "rec-1",
            Title = "Signal Song",
            Artist = "Signal Artist",
            IdentityScore = 0.62,
            ByzantineScore = 0.55,
            ActionScore = 0.58,
        };

        var variants = new List<AudioVariant>
        {
            new()
            {
                Codec = "FLAC",
                QualityScore = 0.94,
                SeenCount = 6,
                TranscodeSuspect = false,
            },
            new()
            {
                Codec = "MP3",
                QualityScore = 0.73,
                SeenCount = 2,
                TranscodeSuspect = false,
            },
        };

        SongIdScoring.ApplyCanonicalTrackSignals(track, variants);

        Assert.True(track.CanonicalScore > 0.6);
        Assert.Equal(2, track.CanonicalVariantCount);
        Assert.True(track.HasLosslessCanonical);
        Assert.True(track.IdentityScore > 0.62);
        Assert.True(track.ByzantineScore > 0.55);
        Assert.True(track.ActionScore > 0.58);
    }

    [Fact]
    public void ApplyRunQualityConsensus_BoostsAlbumAndArtistFromSupportedTracks()
    {
        var run = new SongIdRun
        {
            Tracks = new List<SongIdTrackCandidate>
            {
                new()
                {
                    Artist = "Consensus Artist",
                    Title = "Track One",
                    CanonicalScore = 0.88,
                    ActionScore = 0.64,
                    IdentityScore = 0.65,
                    ByzantineScore = 0.60,
                },
                new()
                {
                    Artist = "Consensus Artist",
                    Title = "Track Two",
                    CanonicalScore = 0.74,
                    ActionScore = 0.62,
                    IdentityScore = 0.63,
                    ByzantineScore = 0.59,
                },
            },
            Albums = new List<SongIdAlbumCandidate>
            {
                new()
                {
                    Artist = "Consensus Artist",
                    Title = "Consensus Album",
                    ActionScore = 0.61,
                    IdentityScore = 0.66,
                    ByzantineScore = 0.58,
                },
            },
            Artists = new List<SongIdArtistCandidate>
            {
                new()
                {
                    Name = "Consensus Artist",
                    ActionScore = 0.57,
                    IdentityScore = 0.60,
                    ByzantineScore = 0.56,
                },
            },
        };

        SongIdScoring.ApplyRunQualityConsensus(run);

        Assert.Equal(2, run.Albums[0].CanonicalSupportCount);
        Assert.True(run.Albums[0].CanonicalScore >= 0.88);
        Assert.True(run.Albums[0].ActionScore > 0.61);
        Assert.Equal(2, run.Artists[0].CanonicalSupportCount);
        Assert.True(run.Artists[0].CanonicalScore >= 0.88);
        Assert.True(run.Artists[0].ActionScore > 0.57);
    }

    [Fact]
    public void ApplyCorpusReranking_ReordersTrackCandidatesByCorpusEvidence()
    {
        var run = new SongIdRun
        {
            Tracks = new List<SongIdTrackCandidate>
            {
                new()
                {
                    CandidateId = "b",
                    RecordingId = "rec-b",
                    Artist = "Artist B",
                    Title = "Song B",
                    IdentityScore = 0.70,
                    ByzantineScore = 0.66,
                    ActionScore = 0.72,
                },
                new()
                {
                    CandidateId = "a",
                    RecordingId = "rec-a",
                    Artist = "Artist A",
                    Title = "Song A",
                    IdentityScore = 0.68,
                    ByzantineScore = 0.63,
                    ActionScore = 0.69,
                },
            },
            CorpusMatches = new List<SongIdCorpusMatch>
            {
                new()
                {
                    RecordingId = "rec-a",
                    Artist = "Artist A",
                    Title = "Song A",
                    SimilarityScore = 0.91,
                },
            },
        };

        SongIdScoring.ApplyCorpusReranking(run);

        Assert.Equal("rec-a", run.Tracks[0].RecordingId);
        Assert.True(run.Tracks[0].ActionScore > 0.69);
    }

    [Fact]
    public void ComputeTrackSearchQualityScore_ReflectsCanonicalBoost()
    {
        var track = new SongIdTrackCandidate
        {
            CanonicalScore = 0.80,
            HasLosslessCanonical = true,
        };

        var quality = SongIdScoring.ComputeTrackSearchQualityScore(track, 0.74);

        Assert.True(quality > 0.88);
        Assert.True(quality <= 1.0);
    }

    [Fact]
    public void BuildForensicMatrix_OneStrongSyntheticLaneCapsConfidence()
    {
        var run = new SongIdRun
        {
            Scorecard = new SongIdScorecard
            {
                ClipCount = 3,
                AiArtifactClipCount = 3,
                HighAiArtifactClipCount = 3,
            },
            AiHeuristics = new SongIdAiHeuristicFinding
            {
                ArtifactScore = 0.82,
                ArtifactLabel = "high",
                PeakCount = 18,
                PeakDensity = 0.12,
                PeriodicityStrength = 0.71,
                ResidualRatio = 0.34,
            },
            Clips = new List<SongIdClipFinding>
            {
                new()
                {
                    ClipId = "clip-1",
                    AiHeuristics = new SongIdAiHeuristicFinding
                    {
                        ArtifactScore = 0.82,
                        ArtifactLabel = "high",
                    },
                },
            },
        };

        var matrix = SongIdScoring.BuildForensicMatrix(run);
        var synthetic = SongIdScoring.BuildSyntheticAssessment(run, matrix);

        Assert.True(matrix.ConfidenceScore <= 44);
        Assert.True(matrix.SyntheticScore <= 44);
        Assert.Contains("one_strong_synthetic_lane_is_not_enough", matrix.Notes);
        Assert.Equal("low_signal", synthetic.Verdict);
    }

    [Fact]
    public void BuildForensicMatrix_StrongIdentitySuppressesSyntheticOverclaim()
    {
        var run = new SongIdRun
        {
            Tracks = new List<SongIdTrackCandidate>
            {
                new()
                {
                    IsExact = true,
                    IdentityScore = 0.96,
                    Artist = "Known Artist",
                    Title = "Known Song",
                },
            },
            Scorecard = new SongIdScorecard
            {
                ClipCount = 4,
                AiArtifactClipCount = 4,
                HighAiArtifactClipCount = 4,
                ProvenanceSignalCount = 2,
            },
            Provenance = new SongIdProvenanceFinding
            {
                SignalCount = 2,
                Signals = new List<string> { "c2pa", "content credentials" },
                ManifestHint = true,
                Verified = true,
                ValidationState = "valid",
            },
            AiHeuristics = new SongIdAiHeuristicFinding
            {
                ArtifactScore = 0.90,
                ArtifactLabel = "high",
                PeakCount = 24,
                PeakDensity = 0.18,
                PeriodicityStrength = 0.83,
                ResidualRatio = 0.41,
            },
            Clips = new List<SongIdClipFinding>
            {
                new()
                {
                    ClipId = "clip-1",
                    SongRec = new SongIdRecognizerFinding
                    {
                        Artist = "Known Artist",
                        Title = "Known Song",
                    },
                    AiHeuristics = new SongIdAiHeuristicFinding
                    {
                        ArtifactScore = 0.90,
                        ArtifactLabel = "high",
                    },
                },
                new()
                {
                    ClipId = "clip-2",
                    SongRec = new SongIdRecognizerFinding
                    {
                        Artist = "Known Artist",
                        Title = "Known Song",
                    },
                    AiHeuristics = new SongIdAiHeuristicFinding
                    {
                        ArtifactScore = 0.89,
                        ArtifactLabel = "high",
                    },
                },
            },
        };

        var identity = SongIdScoring.BuildIdentityAssessment(run);
        var matrix = SongIdScoring.BuildForensicMatrix(run);
        var synthetic = SongIdScoring.BuildSyntheticAssessment(run, matrix);

        Assert.Equal("recognized_cataloged_track", identity.Verdict);
        Assert.True(matrix.IdentityScore >= 75);
        Assert.True(matrix.SyntheticScore <= 34);
        Assert.Contains("strong_identity_suppresses_synthetic_overclaim", matrix.Notes);
        Assert.Equal("mixed_or_inconclusive", synthetic.Verdict);
        Assert.Equal("medium", synthetic.Confidence);
    }

    [Fact]
    public void BuildForensicMatrix_UsesPerturbationProbeStabilityWhenAvailable()
    {
        var run = new SongIdRun
        {
            SourceType = "local_file",
            Scorecard = new SongIdScorecard
            {
                ClipCount = 4,
                AiArtifactClipCount = 4,
                HighAiArtifactClipCount = 2,
            },
            AiHeuristics = new SongIdAiHeuristicFinding
            {
                ArtifactScore = 0.61,
                ArtifactLabel = "medium",
                SpectralCentroid = 3560,
                SpectralFlux = 0.12,
                PitchSalience = 0.49,
                DurationSuspicion = 0.11,
            },
            Perturbations = new List<SongIdPerturbationFinding>
            {
                new()
                {
                    PerturbationId = "lowpass",
                    BaselineDelta = 0.08,
                    Heuristics = new SongIdAiHeuristicFinding
                    {
                        ArtifactScore = 0.58,
                        ArtifactLabel = "medium",
                    },
                },
                new()
                {
                    PerturbationId = "resample",
                    BaselineDelta = 0.09,
                    Heuristics = new SongIdAiHeuristicFinding
                    {
                        ArtifactScore = 0.56,
                        ArtifactLabel = "medium",
                    },
                },
                new()
                {
                    PerturbationId = "pitch_shift",
                    BaselineDelta = 0.07,
                    Heuristics = new SongIdAiHeuristicFinding
                    {
                        ArtifactScore = 0.55,
                        ArtifactLabel = "medium",
                    },
                },
            },
        };

        var matrix = SongIdScoring.BuildForensicMatrix(run);

        Assert.True(matrix.PerturbationStability > 0.6);
        Assert.Equal("clean_full_track", matrix.QualityClass);
        Assert.True(matrix.LaneScores.ContainsKey("descriptor_priors"));
    }

    [Fact]
    public void ApplyCorpusFamilyHints_ReusesFamilyLabelFromCorpusMatch()
    {
        var run = new SongIdRun
        {
            ForensicMatrix = new SongIdForensicMatrix
            {
                FamilyLabel = "none",
                KnownFamilyScore = 0,
            },
            CorpusMatches = new List<SongIdCorpusMatch>
            {
                new()
                {
                    FamilyLabel = "suno_like",
                    KnownFamilyScore = 84,
                    SimilarityScore = 0.88,
                },
            },
        };

        SongIdScoring.ApplyCorpusFamilyHints(run);

        Assert.Equal("suno_like", run.ForensicMatrix.FamilyLabel);
        Assert.Equal(84, run.ForensicMatrix.KnownFamilyScore);
        Assert.Contains("corpus_family_hint_reused", run.ForensicMatrix.Notes);
    }

    [Fact]
    public void ComputeIdentityFirstOverallScore_PrefersHigherIdentityAtComparableQuality()
    {
        var lowerIdentity = SongIdScoring.ComputeIdentityFirstOverallScore(
            identityScore: 0.52,
            qualityScore: 0.88,
            byzantineScore: 0.76,
            readinessScore: 0.79);
        var higherIdentity = SongIdScoring.ComputeIdentityFirstOverallScore(
            identityScore: 0.91,
            qualityScore: 0.82,
            byzantineScore: 0.74,
            readinessScore: 0.77);

        Assert.True(higherIdentity > lowerIdentity);
    }
}
