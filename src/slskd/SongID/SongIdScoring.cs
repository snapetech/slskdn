// <copyright file="SongIdScoring.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SongID;

using System.Text.RegularExpressions;
using slskd.Audio;

internal static class SongIdScoring
{
    public static SongIdAssessment BuildIdentityAssessment(SongIdRun run)
    {
        var exactTrack = run.Tracks.FirstOrDefault(track => track.IsExact);
        var songRecHits = run.Clips.Count(clip => clip.SongRec != null);
        var distinctSongRecMatches = run.Clips
            .Select(clip => clip.SongRec)
            .Where(finding => finding != null)
            .Cast<SongIdRecognizerFinding>()
            .Select(finding => NormalizeLooseText(BuildIdentityKey(finding.Artist, finding.Title, finding.ExternalId)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var acoustIdHits = run.Clips.Count(clip => !string.IsNullOrWhiteSpace(clip.AcoustId?.RecordingId));
        var publicMatchHits = acoustIdHits + songRecHits;
        var panakoHits = run.Clips.Count(clip => clip.Panako != null);
        var audfprintHits = run.Clips.Count(clip => clip.Audfprint != null);
        var topCorpus = run.CorpusMatches.FirstOrDefault();
        var strongCorpus = topCorpus?.SimilarityScore >= 0.72;
        var aiConfidence = run.AiHeuristics?.ArtifactScore ?? 0;

        if (songRecHits >= 2 && distinctSongRecMatches <= 2)
        {
            return new SongIdAssessment
            {
                Verdict = "recognized_cataloged_track",
                Confidence = 0.96,
                Summary = "SongID found repeated SongRec recognition agreement across multiple clip windows.",
            };
        }

        if (exactTrack != null && (publicMatchHits > 0 || panakoHits > 0 || audfprintHits > 0))
        {
            return new SongIdAssessment
            {
                Verdict = "recognized_cataloged_track",
                Confidence = 0.93,
                Summary = "SongID fused an exact catalog target with independent audio recognizer evidence.",
            };
        }

        if (publicMatchHits > 0 || panakoHits > 0 || audfprintHits > 0)
        {
            return new SongIdAssessment
            {
                Verdict = "candidate_match_found",
                Confidence = 0.82,
                Summary = "SongID found database-backed audio matches, but not enough repeated consensus to call it fully recognized.",
            };
        }

        if (strongCorpus)
        {
            return new SongIdAssessment
            {
                Verdict = "candidate_match_found",
                Confidence = 0.76,
                Summary = "SongID found a strong local corpus match tied to a previously identified catalog recording.",
            };
        }

        if ((run.Transcripts.Count > 0 || run.Ocr.Count > 0 || run.Chapters.Count > 0 || run.Comments.Any(comment => comment.TimestampSeconds.HasValue)) &&
            aiConfidence < 0.6)
        {
            return new SongIdAssessment
            {
                Verdict = "needs_manual_review",
                Confidence = 0.58,
                Summary = "SongID found secondary evidence from transcript, OCR, comments, or chapters, but no strong recognizer hit yet.",
            };
        }

        if (run.Scorecard.CommentFindingCount > 0 && run.Scorecard.ProvenanceSignalCount > 0 && aiConfidence >= 0.4)
        {
            return new SongIdAssessment
            {
                Verdict = "likely_ai_or_channel_original",
                Confidence = aiConfidence >= 0.65 ? 0.86 : 0.68,
                Summary = "SongID found no strong catalog recognizer hits and did find comment, provenance, and AI-audio artifact signals.",
            };
        }

        return new SongIdAssessment
        {
            Verdict = "likely_uncataloged_or_original",
            Confidence = 0.41,
            Summary = "SongID did not find strong catalog recognizer evidence; this source may be uncataloged, transformed, or channel-original.",
        };
    }

    public static SongIdForensicMatrix BuildForensicMatrix(SongIdRun run)
    {
        var identityLane = BuildIdentityLane(run);
        var provenanceLane = BuildProvenanceLane(run);
        var spectralLane = BuildSpectralArtifactLane(run);
        var descriptorLane = BuildDescriptorPriorsLane(run);
        var lyricsLane = BuildLyricsSpeechLane(run);
        var structuralLane = BuildStructuralLane(run);
        var generatorFamilyLane = BuildGeneratorFamilyLane(spectralLane, provenanceLane, descriptorLane);
        var confidenceLane = BuildConfidenceLane(run, identityLane, provenanceLane, spectralLane, descriptorLane, lyricsLane, structuralLane);

        var laneScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["identity"] = identityLane.Score,
            ["provenance"] = provenanceLane.Score,
            ["spectral_artifacts"] = spectralLane.Score,
            ["descriptor_priors"] = descriptorLane.Score,
            ["lyrics_speech"] = lyricsLane.Score,
            ["structure"] = structuralLane.Score,
            ["generator_family"] = generatorFamilyLane.Score,
        };

        var laneConfidences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["identity"] = identityLane.Confidence,
            ["provenance"] = provenanceLane.Confidence,
            ["spectral_artifacts"] = spectralLane.Confidence,
            ["descriptor_priors"] = descriptorLane.Confidence,
            ["lyrics_speech"] = lyricsLane.Confidence,
            ["structure"] = structuralLane.Confidence,
            ["generator_family"] = generatorFamilyLane.Confidence,
        };

        var strongSyntheticLaneCount = new[] { provenanceLane.Score, spectralLane.Score, descriptorLane.Score, lyricsLane.Score, structuralLane.Score, generatorFamilyLane.Score }
            .Count(score => score >= 0.55);
        var weightedSynthetic = ClampScore(
            (provenanceLane.Score * 0.28) +
            (spectralLane.Score * 0.24) +
            (descriptorLane.Score * 0.08) +
            (lyricsLane.Score * 0.12) +
            (structuralLane.Score * 0.12) +
            (generatorFamilyLane.Score * 0.16));
        var syntheticScore = ClampScore(weightedSynthetic * confidenceLane.Score);
        var notes = new List<string>();

        if (strongSyntheticLaneCount <= 1 && spectralLane.Score >= 0.55)
        {
            syntheticScore = Math.Min(syntheticScore, 0.44);
            notes.Add("single_strong_lane_only");
        }

        if (identityLane.Score >= 0.75)
        {
            syntheticScore = Math.Min(syntheticScore, 0.34);
            notes.Add("strong_identity_suppresses_synthetic_overclaim");
        }

        if (confidenceLane.Score < 0.55)
        {
            notes.Add("artifact_fragile_under_perturbation");
        }

        notes.AddRange(confidenceLane.Metrics
            .Where(pair => string.Equals(pair.Key, "penalties", StringComparison.Ordinal))
            .SelectMany(pair => pair.Value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        return new SongIdForensicMatrix
        {
            IdentityScore = (int)Math.Round(identityLane.Score * 100),
            SyntheticScore = (int)Math.Round(syntheticScore * 100),
            ConfidenceScore = (int)Math.Round(confidenceLane.Score * 100),
            KnownFamilyScore = (int)Math.Round(generatorFamilyLane.Score * 100),
            FamilyLabel = generatorFamilyLane.Label,
            QualityClass = GetQualityClass(run, confidenceLane),
            TopEvidenceFor = BuildTopEvidenceFor(provenanceLane, spectralLane, descriptorLane, lyricsLane, structuralLane, generatorFamilyLane).Take(5).ToList(),
            TopEvidenceAgainst = BuildTopEvidenceAgainst(identityLane, confidenceLane, descriptorLane, lyricsLane, structuralLane).Take(5).ToList(),
            LaneScores = laneScores,
            LaneConfidences = laneConfidences,
            PerturbationStability = ParseDoubleMetric(confidenceLane, "perturbation_stability"),
            Notes = notes.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            IdentityLane = identityLane,
            ConfidenceLane = confidenceLane,
            SpectralArtifactLane = spectralLane,
            DescriptorPriorsLane = descriptorLane,
            LyricsSpeechLane = lyricsLane,
            StructuralLane = structuralLane,
            ProvenanceLane = provenanceLane,
            GeneratorFamilyLane = generatorFamilyLane,
        };
    }

    public static SongIdSyntheticAssessment BuildSyntheticAssessment(SongIdRun run, SongIdForensicMatrix forensicMatrix)
    {
        if (forensicMatrix.SyntheticScore <= 0 && forensicMatrix.ConfidenceScore <= 0)
        {
            return new SongIdSyntheticAssessment
            {
                Verdict = "insufficient_evidence",
                Confidence = "low",
                SyntheticScore = forensicMatrix.SyntheticScore,
                ConfidenceScore = forensicMatrix.ConfidenceScore,
                KnownFamilyScore = forensicMatrix.KnownFamilyScore,
                FamilyLabel = forensicMatrix.FamilyLabel,
                QualityClass = forensicMatrix.QualityClass,
                PerturbationStability = forensicMatrix.PerturbationStability,
                TopEvidenceFor = forensicMatrix.TopEvidenceFor,
                TopEvidenceAgainst = forensicMatrix.TopEvidenceAgainst,
                Notes = forensicMatrix.Notes,
                Summary = "No synthetic lane accumulated enough evidence to justify a synthetic-likelihood claim.",
            };
        }

        var verdict = forensicMatrix.SyntheticScore switch
        {
            >= 72 when forensicMatrix.ConfidenceScore >= 70 => "strong_suspicion",
            >= 52 when forensicMatrix.ConfidenceScore >= 45 => "moderate_suspicion",
            >= 26 => "mixed_or_inconclusive",
            _ => "low_signal",
        };
        var confidence = forensicMatrix.ConfidenceScore >= 70
            ? "high"
            : forensicMatrix.ConfidenceScore >= 35
                ? "medium"
                : "low";
        var reason = string.Join("; ", forensicMatrix.TopEvidenceFor.Take(3));
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "No synthetic lane accumulated strong evidence.";
        }

        return new SongIdSyntheticAssessment
        {
            Verdict = verdict,
            Confidence = confidence,
            SyntheticScore = forensicMatrix.SyntheticScore,
            ConfidenceScore = forensicMatrix.ConfidenceScore,
            KnownFamilyScore = forensicMatrix.KnownFamilyScore,
            FamilyLabel = forensicMatrix.FamilyLabel,
            QualityClass = forensicMatrix.QualityClass,
            PerturbationStability = forensicMatrix.PerturbationStability,
            TopEvidenceFor = forensicMatrix.TopEvidenceFor,
            TopEvidenceAgainst = forensicMatrix.TopEvidenceAgainst,
            Notes = forensicMatrix.Notes,
            Summary = reason,
        };
    }

    public static void ApplyCorpusFamilyHints(SongIdRun run)
    {
        if (run.ForensicMatrix == null || run.CorpusMatches.Count == 0)
        {
            return;
        }

        var hintedMatches = run.CorpusMatches
            .Where(match => !string.IsNullOrWhiteSpace(match.FamilyLabel) && !string.Equals(match.FamilyLabel, "none", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(match => match.KnownFamilyScore)
            .ThenByDescending(match => match.SimilarityScore)
            .ToList();
        if (hintedMatches.Count == 0)
        {
            return;
        }

        var bestHint = hintedMatches[0];
        if (!string.IsNullOrWhiteSpace(run.ForensicMatrix.FamilyLabel) &&
            !string.Equals(run.ForensicMatrix.FamilyLabel, "none", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(run.ForensicMatrix.FamilyLabel, bestHint.FamilyLabel, StringComparison.OrdinalIgnoreCase))
        {
            run.ForensicMatrix.KnownFamilyScore = Math.Max(run.ForensicMatrix.KnownFamilyScore, bestHint.KnownFamilyScore);
            return;
        }

        if (string.IsNullOrWhiteSpace(run.ForensicMatrix.FamilyLabel) ||
            string.Equals(run.ForensicMatrix.FamilyLabel, "none", StringComparison.OrdinalIgnoreCase))
        {
            run.ForensicMatrix.FamilyLabel = bestHint.FamilyLabel ?? "none";
        }

        run.ForensicMatrix.KnownFamilyScore = Math.Max(run.ForensicMatrix.KnownFamilyScore, bestHint.KnownFamilyScore);
        if (!run.ForensicMatrix.Notes.Contains("corpus_family_hint_reused", StringComparer.OrdinalIgnoreCase))
        {
            run.ForensicMatrix.Notes.Add("corpus_family_hint_reused");
        }
    }

    public static void ApplyCanonicalTrackSignals(SongIdTrackCandidate track, IReadOnlyCollection<AudioVariant> variants)
    {
        if (variants.Count == 0)
        {
            return;
        }

        var bestQuality = variants.Max(variant => variant.QualityScore);
        var losslessCount = variants.Count(variant => IsLossless(variant.Codec));
        var trustedCount = variants.Count(variant => !variant.TranscodeSuspect);
        var seenCount = variants.Sum(variant => Math.Max(1, variant.SeenCount));
        var canonicalScore = ClampScore(
            (bestQuality * 0.45) +
            (Math.Min(1, losslessCount / 3.0) * 0.25) +
            (Math.Min(1, trustedCount / (double)variants.Count) * 0.20) +
            (Math.Min(1, seenCount / 12.0) * 0.10));

        track.CanonicalScore = canonicalScore;
        track.CanonicalVariantCount = variants.Count;
        track.HasLosslessCanonical = losslessCount > 0;
        track.IdentityScore = ClampScore(track.IdentityScore + (canonicalScore * 0.18));
        track.ByzantineScore = ClampScore(track.ByzantineScore + (canonicalScore * 0.12));
        track.ActionScore = ClampScore(track.ActionScore + (canonicalScore * 0.14));
    }

    public static double ComputeIdentityFirstOverallScore(
        double identityScore,
        double qualityScore,
        double byzantineScore,
        double readinessScore)
    {
        var clampedIdentity = ClampScore(identityScore);
        var clampedQuality = ClampScore(qualityScore);
        var clampedByzantine = ClampScore(byzantineScore);
        var clampedReadiness = ClampScore(readinessScore);

        return ClampScore(
            (clampedIdentity * 0.42) +
            (clampedQuality * 0.26) +
            (clampedByzantine * 0.14) +
            (clampedReadiness * 0.18));
    }

    public static void ApplyRunQualityConsensus(SongIdRun run)
    {
        foreach (var album in run.Albums)
        {
            var supportingTracks = run.Tracks
                .Where(track => CompareLooseText(track.Artist, album.Artist) >= 0.8 && track.CanonicalScore > 0)
                .ToList();
            if (supportingTracks.Count == 0)
            {
                continue;
            }

            album.CanonicalSupportCount = supportingTracks.Count;
            album.CanonicalScore = supportingTracks.Max(track => track.CanonicalScore);
            album.IdentityScore = ClampScore(album.IdentityScore + (album.CanonicalScore * 0.12));
            album.ByzantineScore = ClampScore(album.ByzantineScore + (album.CanonicalScore * 0.08));
            album.ActionScore = ClampScore(album.ActionScore + (Math.Min(1, supportingTracks.Count / 3.0) * 0.10) + (album.CanonicalScore * 0.05));
        }

        foreach (var artist in run.Artists)
        {
            var supportingTracks = run.Tracks
                .Where(track => CompareLooseText(track.Artist, artist.Name) >= 0.8 && track.CanonicalScore > 0)
                .ToList();
            if (supportingTracks.Count == 0)
            {
                continue;
            }

            artist.CanonicalSupportCount = supportingTracks.Count;
            artist.CanonicalScore = supportingTracks.Max(track => track.CanonicalScore);
            artist.IdentityScore = ClampScore(artist.IdentityScore + (artist.CanonicalScore * 0.10));
            artist.ByzantineScore = ClampScore(artist.ByzantineScore + (artist.CanonicalScore * 0.06));
            artist.ActionScore = ClampScore(artist.ActionScore + (Math.Min(1, supportingTracks.Count / 4.0) * 0.12) + (artist.CanonicalScore * 0.04));
        }
    }

    public static void ApplyCorpusReranking(SongIdRun run)
    {
        if (run.CorpusMatches.Count == 0)
        {
            return;
        }

        foreach (var track in run.Tracks)
        {
            var boost = GetTrackCorpusBoost(track, run.CorpusMatches);
            if (boost <= 0)
            {
                continue;
            }

            track.IdentityScore = ClampScore(track.IdentityScore + (boost * 0.24));
            track.ByzantineScore = ClampScore(track.ByzantineScore + (boost * 0.16));
            track.ActionScore = ClampScore(track.ActionScore + (boost * 0.18));
        }

        foreach (var album in run.Albums)
        {
            var boost = GetAlbumCorpusBoost(album, run.CorpusMatches);
            if (boost <= 0)
            {
                continue;
            }

            album.IdentityScore = ClampScore(album.IdentityScore + (boost * 0.18));
            album.ByzantineScore = ClampScore(album.ByzantineScore + (boost * 0.12));
            album.ActionScore = ClampScore(album.ActionScore + (boost * 0.14));
        }

        foreach (var artist in run.Artists)
        {
            var boost = GetArtistCorpusBoost(artist, run.CorpusMatches);
            if (boost <= 0)
            {
                continue;
            }

            artist.IdentityScore = ClampScore(artist.IdentityScore + (boost * 0.16));
            artist.ByzantineScore = ClampScore(artist.ByzantineScore + (boost * 0.10));
            artist.ActionScore = ClampScore(artist.ActionScore + (boost * 0.12));
        }

        run.Tracks = run.Tracks
            .OrderByDescending(track => track.ActionScore)
            .ThenByDescending(track => track.IdentityScore)
            .ThenByDescending(track => track.ByzantineScore)
            .ToList();
        run.Albums = run.Albums
            .OrderByDescending(album => album.ActionScore)
            .ThenByDescending(album => album.IdentityScore)
            .ThenByDescending(album => album.ByzantineScore)
            .ToList();
        run.Artists = run.Artists
            .OrderByDescending(artist => artist.ActionScore)
            .ThenByDescending(artist => artist.IdentityScore)
            .ThenByDescending(artist => artist.ByzantineScore)
            .ToList();
    }

    public static double ComputeTrackSearchQualityScore(SongIdTrackCandidate track, double baseQuality)
    {
        var canonicalBoost = (track.CanonicalScore * 0.18) + (track.HasLosslessCanonical ? 0.05 : 0);
        return ClampScore(baseQuality + canonicalBoost);
    }

    public static double ClampScore(double value)
    {
        return Math.Max(0, Math.Min(1, value));
    }

    public static double CompareLooseText(string left, string? right)
    {
        var normalizedLeft = NormalizeLooseText(left);
        var normalizedRight = NormalizeLooseText(right);
        if (string.IsNullOrWhiteSpace(normalizedLeft) || string.IsNullOrWhiteSpace(normalizedRight))
        {
            return 0;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return 1;
        }

        var leftTokens = normalizedLeft.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var rightTokens = normalizedRight.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0;
        }

        var intersection = leftTokens.Intersect(rightTokens).Count();
        var union = leftTokens.Union(rightTokens).Count();
        return union == 0 ? 0 : intersection / (double)union;
    }

    private static string NormalizeLooseText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }

    private static bool IsLossless(string? codec)
    {
        return codec switch
        {
            "FLAC" => true,
            "ALAC" => true,
            "WAV" => true,
            "AIFF" => true,
            _ => false,
        };
    }

    private static double GetTrackCorpusBoost(SongIdTrackCandidate track, IReadOnlyCollection<SongIdCorpusMatch> matches)
    {
        var directMatch = matches
            .Where(match => !string.IsNullOrWhiteSpace(match.RecordingId) &&
                string.Equals(match.RecordingId, track.RecordingId, StringComparison.OrdinalIgnoreCase))
            .Select(match => match.SimilarityScore)
            .DefaultIfEmpty(0)
            .Max();
        if (directMatch > 0)
        {
            return directMatch;
        }

        return matches
            .Select(match =>
            {
                var titleSimilarity = CompareLooseText(track.Title, match.Title);
                var artistSimilarity = CompareLooseText(track.Artist, match.Artist);
                return (titleSimilarity * 0.65) + (artistSimilarity * 0.35);
            })
            .DefaultIfEmpty(0)
            .Max();
    }

    private static double GetAlbumCorpusBoost(SongIdAlbumCandidate album, IReadOnlyCollection<SongIdCorpusMatch> matches)
    {
        return matches
            .Select(match =>
            {
                var artistSimilarity = CompareLooseText(album.Artist, match.Artist);
                var titleSimilarity = CompareLooseText(album.Title, match.Title);
                return Math.Max(artistSimilarity * 0.75, titleSimilarity * 0.55);
            })
            .DefaultIfEmpty(0)
            .Max();
    }

    private static double GetArtistCorpusBoost(SongIdArtistCandidate artist, IReadOnlyCollection<SongIdCorpusMatch> matches)
    {
        return matches
            .Select(match => CompareLooseText(artist.Name, match.Artist))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static SongIdForensicLane BuildIdentityLane(SongIdRun run)
    {
        var score = ClampScore(
            (run.Tracks.FirstOrDefault(track => track.IsExact)?.IdentityScore ?? 0) * 0.48 +
            (Math.Min(1, run.Clips.Count(clip => clip.SongRec != null) / 2.0) * 0.30) +
            (Math.Min(1, run.Clips.Count(clip => !string.IsNullOrWhiteSpace(clip.AcoustId?.RecordingId)) / 2.0) * 0.16) +
            (Math.Min(1, run.CorpusMatches.FirstOrDefault()?.SimilarityScore ?? 0) * 0.18));

        return new SongIdForensicLane
        {
            Label = score >= 0.78 ? "strong_identity" : score >= 0.48 ? "candidate_identity" : "weak_identity",
            Score = Math.Round(score, 4),
            Confidence = (int)Math.Round(score * 100),
            Summary = score >= 0.78
                ? "Multiple identity signals support a known or reused recording."
                : score >= 0.48
                    ? "Some identity signals support a candidate recording match."
                    : "Identity evidence is weak or unresolved.",
            Metrics = new Dictionary<string, string>
            {
                ["exact_track"] = (run.Tracks.Any(track => track.IsExact)).ToString().ToLowerInvariant(),
                ["songrec_hits"] = run.Clips.Count(clip => clip.SongRec != null).ToString(),
                ["acoustid_recording_hits"] = run.Clips.Count(clip => !string.IsNullOrWhiteSpace(clip.AcoustId?.RecordingId)).ToString(),
                ["corpus_top_score"] = (run.CorpusMatches.FirstOrDefault()?.SimilarityScore ?? 0).ToString("F2"),
            },
        };
    }

    private static SongIdForensicLane BuildProvenanceLane(SongIdRun run)
    {
        var score = ClampScore(
            (Math.Min(1, run.Provenance.SignalCount / 3.0) * 0.55) +
            (run.Provenance.ManifestHint ? 0.20 : 0) +
            (run.Provenance.Verified ? 0.25 : 0));

        return new SongIdForensicLane
        {
            Label = run.Provenance.Verified ? "verified_provenance" : run.Provenance.ManifestHint ? "manifest_hint" : score > 0 ? "metadata_signal" : "none",
            Score = Math.Round(score, 4),
            Confidence = run.Provenance.Verified ? 92 : run.Provenance.ManifestHint ? 70 : run.Provenance.SignalCount > 0 ? 52 : 20,
            Summary = run.Provenance.SignalCount > 0
                ? "Metadata or container-level provenance markers were detected."
                : "No provenance markers were detected.",
            Metrics = new Dictionary<string, string>
            {
                ["signal_count"] = run.Provenance.SignalCount.ToString(),
                ["tool_available"] = run.Provenance.ToolAvailable.ToString().ToLowerInvariant(),
                ["manifest_hint"] = run.Provenance.ManifestHint.ToString().ToLowerInvariant(),
                ["verified"] = run.Provenance.Verified.ToString().ToLowerInvariant(),
                ["validation_state"] = run.Provenance.ValidationState ?? string.Empty,
            },
        };
    }

    private static SongIdForensicLane BuildSpectralArtifactLane(SongIdRun run)
    {
        var heuristics = run.AiHeuristics;
        var score = heuristics?.ArtifactScore ?? 0;
        var confidence = ClampScore((run.Scorecard.AiArtifactClipCount / 6.0) + (run.Scorecard.HighAiArtifactClipCount / 6.0));

        return new SongIdForensicLane
        {
            Label = heuristics?.ArtifactLabel switch
            {
                "high" => "strong_artifact_signal",
                "medium" => "moderate_artifact_signal",
                "low" => "weak_artifact_signal",
                _ => "no_artifact_signal",
            },
            Score = Math.Round(score, 4),
            Confidence = (int)Math.Round(confidence * 100),
            Summary = heuristics == null
                ? "No clip-level AI audio artifact analysis was available."
                : "Clip-level spectral heuristics were aggregated into a synthetic-artifact signal.",
            Metrics = new Dictionary<string, string>
            {
                ["artifact_score"] = (heuristics?.ArtifactScore ?? 0).ToString("F2"),
                ["peak_count"] = (heuristics?.PeakCount ?? 0).ToString(),
                ["peak_density"] = (heuristics?.PeakDensity ?? 0).ToString("F4"),
                ["periodicity"] = (heuristics?.PeriodicityStrength ?? 0).ToString("F4"),
                ["dominant_spacing_hz"] = (heuristics?.DominantSpacingHz ?? 0).ToString("F2"),
            },
        };
    }

    private static SongIdForensicLane BuildDescriptorPriorsLane(SongIdRun run)
    {
        var heuristics = run.AiHeuristics;
        var centroidSignal = heuristics == null ? 0 : Math.Min(1, heuristics.SpectralCentroid / 4200.0);
        var fluxSignal = heuristics == null ? 0 : Math.Min(1, heuristics.SpectralFlux / 0.18);
        var pitchSignal = heuristics == null ? 0 : Math.Min(1, heuristics.PitchSalience / 0.75);
        var durationSignal = heuristics?.DurationSuspicion ?? 0;
        var score = ClampScore(
            (centroidSignal * 0.28) +
            (fluxSignal * 0.24) +
            (pitchSignal * 0.24) +
            (durationSignal * 0.24));

        return new SongIdForensicLane
        {
            Label = score >= 0.55 ? "descriptor_prior_shift" : score >= 0.28 ? "weak_descriptor_shift" : "neutral_descriptor_profile",
            Score = Math.Round(score, 4),
            Confidence = heuristics == null ? 16 : 42,
            Summary = heuristics == null
                ? "No descriptor-prior signal was available."
                : "Broad spectral descriptors and duration priors were folded into a weak synthetic prior lane.",
            Metrics = new Dictionary<string, string>
            {
                ["spectral_centroid"] = (heuristics?.SpectralCentroid ?? 0).ToString("F2"),
                ["spectral_flux"] = (heuristics?.SpectralFlux ?? 0).ToString("F4"),
                ["pitch_salience"] = (heuristics?.PitchSalience ?? 0).ToString("F4"),
                ["duration_suspicion"] = (heuristics?.DurationSuspicion ?? 0).ToString("F4"),
            },
        };
    }

    private static SongIdForensicLane BuildLyricsSpeechLane(SongIdRun run)
    {
        var transcriptText = string.Join("\n", run.Transcripts.Select(transcript => transcript.Text));
        var repeatedLineRatio = ComputeRepeatedLineRatio(transcriptText);
        var repeatedNgramRatio = ComputeRepeatedNgramRatio(transcriptText);
        var aiMentions = run.Comments.Count(comment => LooksSyntheticComment(comment.Text)) + CountSyntheticMentions(transcriptText);
        var tokenCount = CountTokens(transcriptText);
        var vocalsPresent = run.Stems.Any(stem => string.Equals(stem.ArtifactId, "vocals", StringComparison.OrdinalIgnoreCase));
        var score = ClampScore(
            (Math.Min(1, repeatedLineRatio * 2.0) * 0.28) +
            (Math.Min(1, repeatedNgramRatio * 2.0) * 0.28) +
            (Math.Min(1, aiMentions / 4.0) * 0.22) +
            (vocalsPresent ? 0.08 : 0) +
            (tokenCount > 0 ? 0.10 : 0));

        return new SongIdForensicLane
        {
            Label = score >= 0.55 ? "patterned_lyrics_signal" : score >= 0.28 ? "weak_lyrics_signal" : "minimal_lyrics_signal",
            Score = Math.Round(score, 4),
            Confidence = tokenCount >= 24 ? 74 : tokenCount > 0 ? 46 : 18,
            Summary = tokenCount > 0
                ? "Transcript-derived repetition and speech-adjacent cues were analyzed."
                : "No transcript-rich evidence was available for the lyrics/speech lane.",
            Metrics = new Dictionary<string, string>
            {
                ["token_count"] = tokenCount.ToString(),
                ["repeated_line_ratio"] = repeatedLineRatio.ToString("F4"),
                ["repeated_ngram_ratio"] = repeatedNgramRatio.ToString("F4"),
                ["synthetic_mentions"] = aiMentions.ToString(),
                ["vocals_stem_present"] = vocalsPresent.ToString().ToLowerInvariant(),
            },
        };
    }

    private static SongIdForensicLane BuildStructuralLane(SongIdRun run)
    {
        var timestampDensity = run.Scorecard.ClipCount > 0 ? run.Scorecard.TimestampHintCount / (double)Math.Max(run.Scorecard.ClipCount, 1) : 0;
        var chapterDensity = run.Chapters.Count > 0 ? Math.Min(1, run.Chapters.Count / 8.0) : 0;
        var unresolvedWithActivity = run.Clips.Count > 0 && run.Scorecard.SongRecHitCount == 0 && run.Scorecard.AcoustIdHitCount == 0 ? 0.20 : 0;
        var score = ClampScore(
            (Math.Min(1, timestampDensity) * 0.28) +
            (chapterDensity * 0.24) +
            (Math.Min(1, run.Scorecard.PlaylistRequestCount / 4.0) * 0.20) +
            (Math.Min(1, run.Scorecard.CommentFindingCount / 8.0) * 0.08) +
            unresolvedWithActivity);

        return new SongIdForensicLane
        {
            Label = score >= 0.55 ? "structured_source_clues" : score >= 0.28 ? "some_structural_clues" : "minimal_structural_clues",
            Score = Math.Round(score, 4),
            Confidence = run.Chapters.Count > 0 || run.Scorecard.TimestampHintCount > 0 ? 70 : 30,
            Summary = "Chapter, timestamp, and source-structure clues were folded into a structural lane.",
            Metrics = new Dictionary<string, string>
            {
                ["chapter_count"] = run.Chapters.Count.ToString(),
                ["timestamp_hints"] = run.Scorecard.TimestampHintCount.ToString(),
                ["playlist_requests"] = run.Scorecard.PlaylistRequestCount.ToString(),
                ["comment_count"] = run.Scorecard.CommentFindingCount.ToString(),
            },
        };
    }

    private static SongIdForensicLane BuildConfidenceLane(
        SongIdRun run,
        SongIdForensicLane identityLane,
        SongIdForensicLane provenanceLane,
        SongIdForensicLane spectralLane,
        SongIdForensicLane descriptorLane,
        SongIdForensicLane lyricsLane,
        SongIdForensicLane structuralLane)
    {
        var penalties = new List<string>();
        var baseScore = ClampScore(
            (spectralLane.Score * 0.35) +
            (descriptorLane.Score * 0.10) +
            (lyricsLane.Score * 0.15) +
            (structuralLane.Score * 0.15) +
            (provenanceLane.Score * 0.20) +
            (Math.Min(1, run.Scorecard.AiArtifactClipCount / 6.0) * 0.15));
        var strongLaneCount = new[] { provenanceLane.Score, spectralLane.Score, descriptorLane.Score, lyricsLane.Score, structuralLane.Score }
            .Count(score => score >= 0.55);
        var perturbationStability = ComputePerturbationStability(run);

        if (strongLaneCount <= 1)
        {
            perturbationStability = Math.Min(perturbationStability, 0.44);
            penalties.Add("one_strong_synthetic_lane_is_not_enough");
        }

        if (identityLane.Score >= 0.75)
        {
            perturbationStability = Math.Min(perturbationStability, 0.36);
            penalties.Add("strong_identity_evidence_present");
        }

        if (run.Scorecard.ClipCount < 2)
        {
            perturbationStability = Math.Min(perturbationStability, 0.40);
            penalties.Add("limited_clip_coverage");
        }

        return new SongIdForensicLane
        {
            Label = perturbationStability >= 0.70 ? "high" : perturbationStability >= 0.35 ? "medium" : "low",
            Score = Math.Round(ClampScore(baseScore * 0.40 + perturbationStability * 0.60), 4),
            Confidence = (int)Math.Round(perturbationStability * 100),
            Summary = "Synthetic confidence is reduced when evidence is brittle, sparse, or contradicted by strong identity signals.",
            Metrics = new Dictionary<string, string>
            {
                ["strong_lane_count"] = strongLaneCount.ToString(),
                ["perturbation_stability"] = perturbationStability.ToString("F4"),
                ["perturbation_probe_count"] = run.Perturbations.Count.ToString(),
                ["penalties"] = string.Join("|", penalties),
            },
        };
    }

    private static SongIdForensicLane BuildGeneratorFamilyLane(
        SongIdForensicLane spectralLane,
        SongIdForensicLane provenanceLane,
        SongIdForensicLane descriptorLane)
    {
        var score = ClampScore((spectralLane.Score * 0.58) + (provenanceLane.Score * 0.24) + (descriptorLane.Score * 0.18));
        if (provenanceLane.Metrics.TryGetValue("manifest_hint", out var manifestHint) && bool.TryParse(manifestHint, out var hasHint) && hasHint)
        {
            return new SongIdForensicLane
            {
                Label = "known",
                Score = Math.Round(ClampScore(score + 0.10), 4),
                Confidence = 72,
                Summary = "Provenance and waveform cues support a known synthetic-family hint.",
                Metrics = new Dictionary<string, string>
                {
                    ["manifest_hint"] = "true",
                    ["descriptor_support"] = descriptorLane.Score.ToString("F4"),
                },
            };
        }

        if (spectralLane.Score >= 0.6)
        {
            return new SongIdForensicLane
            {
                Label = "unknown",
                Score = Math.Round(score, 4),
                Confidence = 58,
                Summary = "The artifact profile looks synthetic-relevant, but does not justify a precise family label.",
                Metrics = new Dictionary<string, string>
                {
                    ["manifest_hint"] = "false",
                    ["descriptor_support"] = descriptorLane.Score.ToString("F4"),
                },
            };
        }

        return new SongIdForensicLane
        {
            Label = "none",
            Score = Math.Round(score * 0.4, 4),
            Confidence = 24,
            Summary = "No credible generator-family hint is present.",
            Metrics = new Dictionary<string, string>
            {
                ["manifest_hint"] = "false",
                ["descriptor_support"] = descriptorLane.Score.ToString("F4"),
            },
        };
    }

    private static string GetQualityClass(SongIdRun run, SongIdForensicLane confidenceLane)
    {
        if (run.Scorecard.ClipCount < 2 || confidenceLane.Confidence < 35)
        {
            return "masked";
        }

        if (run.Scorecard.AnalysisAudioSource.Contains("preview", StringComparison.OrdinalIgnoreCase))
        {
            return "clean_excerpt";
        }

        if (run.SourceType is "local_file" or "youtube_url" && run.Scorecard.ClipCount >= 3 && run.Perturbations.Count >= 3)
        {
            return "clean_full_track";
        }

        if ((run.AiHeuristics?.ResidualRatio ?? 0) < 0.08 || confidenceLane.Confidence < 50)
        {
            return "heavily_transcoded";
        }

        return "clean_excerpt";
    }

    private static IEnumerable<string> BuildTopEvidenceFor(
        SongIdForensicLane provenanceLane,
        SongIdForensicLane spectralLane,
        SongIdForensicLane descriptorLane,
        SongIdForensicLane lyricsLane,
        SongIdForensicLane structuralLane,
        SongIdForensicLane generatorFamilyLane)
    {
        if (provenanceLane.Score >= 0.4)
        {
            yield return $"Provenance lane: {provenanceLane.Label}";
        }

        if (spectralLane.Score >= 0.45)
        {
            yield return $"Spectral artifact lane: {spectralLane.Label}";
        }

        if (descriptorLane.Score >= 0.35)
        {
            yield return $"Descriptor priors lane: {descriptorLane.Label}";
        }

        if (lyricsLane.Score >= 0.35)
        {
            yield return $"Lyrics/speech lane: {lyricsLane.Label}";
        }

        if (structuralLane.Score >= 0.35)
        {
            yield return $"Structure lane: {structuralLane.Label}";
        }

        if (generatorFamilyLane.Score >= 0.35)
        {
            yield return $"Generator family: {generatorFamilyLane.Label}";
        }
    }

    private static IEnumerable<string> BuildTopEvidenceAgainst(
        SongIdForensicLane identityLane,
        SongIdForensicLane confidenceLane,
        SongIdForensicLane descriptorLane,
        SongIdForensicLane lyricsLane,
        SongIdForensicLane structuralLane)
    {
        if (identityLane.Score >= 0.75)
        {
            yield return "Strong identity evidence points to a known or reused recording.";
        }

        if (confidenceLane.Confidence < 60)
        {
            yield return "Artifact lane is fragile under mild perturbation-style confidence checks.";
        }

        if (descriptorLane.Score < 0.20)
        {
            yield return "Descriptor-prior lane stays weak, so the read depends on stronger lanes.";
        }

        if (lyricsLane.Score < 0.25)
        {
            yield return "Lyrics/speech lane is weak or unavailable.";
        }

        if (structuralLane.Score < 0.25)
        {
            yield return "Structure lane is weak on the analyzed excerpt.";
        }
    }

    private static string BuildIdentityKey(string? artist, string? title, string? externalId)
    {
        return string.Join(" ", new[] { artist, title, externalId }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int CountTokens(string text)
    {
        return Regex.Matches(text ?? string.Empty, @"[a-zA-Z']+").Count;
    }

    private static double ComputeRepeatedLineRatio(string text)
    {
        var lines = (text ?? string.Empty)
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeLooseText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (lines.Count == 0)
        {
            return 0;
        }

        var repeated = lines.GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1).Sum(group => group.Count());
        return repeated / (double)lines.Count;
    }

    private static double ComputeRepeatedNgramRatio(string text)
    {
        var tokens = Regex.Matches((text ?? string.Empty).ToLowerInvariant(), @"[a-zA-Z']+")
            .Select(match => match.Value)
            .ToList();
        if (tokens.Count < 6)
        {
            return 0;
        }

        var ngrams = Enumerable.Range(0, tokens.Count - 2)
            .Select(index => string.Join(" ", tokens.Skip(index).Take(3)))
            .ToList();
        var repeated = ngrams.GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1).Sum(group => group.Count());
        return repeated / (double)ngrams.Count;
    }

    private static int CountSyntheticMentions(string text)
    {
        return Regex.Matches(text ?? string.Empty, @"\b(ai|generated|suno|udio|cover by ai|ai-made)\b", RegexOptions.IgnoreCase).Count;
    }

    private static bool LooksSyntheticComment(string text)
    {
        return Regex.IsMatch(text ?? string.Empty, @"\b(ai|generated|suno|udio|fake artist|not real band)\b", RegexOptions.IgnoreCase);
    }

    private static double ComputePerturbationStability(SongIdRun run)
    {
        if (run.Perturbations.Count == 0)
        {
            return ClampScore(
                0.45 +
                ((run.AiHeuristics?.PeriodicityStrength ?? 0) * 0.30) +
                ((run.AiHeuristics?.ResidualRatio ?? 0) * 0.10));
        }

        var meanDelta = run.Perturbations.Average(item => item.BaselineDelta);
        var survivingSignals = run.Perturbations.Count(item => (item.Heuristics?.ArtifactScore ?? 0) >= 0.45);
        return ClampScore(
            0.30 +
            ((1 - Math.Min(1, meanDelta)) * 0.45) +
            (Math.Min(1, survivingSignals / 3.0) * 0.25));
    }

    private static double ParseDoubleMetric(SongIdForensicLane lane, string key)
    {
        if (lane.Metrics.TryGetValue(key, out var value) &&
            double.TryParse(value, out var parsed))
        {
            return Math.Round(parsed, 4);
        }

        return 0;
    }
}
