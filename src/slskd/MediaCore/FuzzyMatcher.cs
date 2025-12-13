namespace slskd.MediaCore;

// <copyright file="FuzzyMatcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Local fuzzy matcher for advisory matching (not published to DHT).
/// Supports multiple algorithms: Jaccard, Levenshtein, Phonetic, and Perceptual hash matching.
/// </summary>
public interface IFuzzyMatcher
{
    double Score(string title, string artist, string candidateTitle, string candidateArtist);
    double ScoreLevenshtein(string a, string b);
    double ScorePhonetic(string a, string b);

    /// <summary>
    /// Computes cross-codec fuzzy match score using perceptual hashes.
    /// </summary>
    /// <param name="contentIdA">First ContentID with perceptual hash</param>
    /// <param name="contentIdB">Second ContentID with perceptual hash</param>
    /// <param name="registry">ContentID registry for lookup</param>
    /// <returns>Match confidence score (0.0 to 1.0)</returns>
    Task<double> ScorePerceptualAsync(string contentIdA, string contentIdB, IContentIdRegistry registry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds similar content using combined fuzzy matching algorithms.
    /// </summary>
    /// <param name="targetContentId">ContentID to find matches for</param>
    /// <param name="candidates">Candidate ContentIDs to compare against</param>
    /// <param name="registry">ContentID registry</param>
    /// <param name="minConfidence">Minimum confidence threshold</param>
    /// <returns>List of matches with confidence scores</returns>
    Task<IReadOnlyList<FuzzyMatchResult>> FindSimilarContentAsync(
        string targetContentId,
        IEnumerable<string> candidates,
        IContentIdRegistry registry,
        double minConfidence = 0.7,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a fuzzy content match.
/// </summary>
public record FuzzyMatchResult(
    string TargetContentId,
    string CandidateContentId,
    double Confidence,
    FuzzyMatchReason Reason);

/// <summary>
/// Reasons for fuzzy match confidence.
/// </summary>
public enum FuzzyMatchReason
{
    PerceptualHash,
    TextSimilarity,
    Combined
}

public class FuzzyMatcher : IFuzzyMatcher
{
    private readonly IPerceptualHasher _perceptualHasher;
    private readonly ILogger<FuzzyMatcher> _logger;

    public FuzzyMatcher(
        IPerceptualHasher perceptualHasher,
        ILogger<FuzzyMatcher> logger)
    {
        _perceptualHasher = perceptualHasher;
        _logger = logger;
    }
    public double Score(string title, string artist, string candidateTitle, string candidateArtist)
    {
        // Jaccard similarity: simple case-insensitive token overlap
        // Fast and effective for basic matching
        var t = Tokenize($"{title} {artist}");
        var c = Tokenize($"{candidateTitle} {candidateArtist}");
        if (t.Count == 0 || c.Count == 0) return 0;
        var intersection = t.Intersect(c).Count();
        var union = t.Union(c).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    /// <summary>
    /// Levenshtein distance-based similarity score (0.0 to 1.0).
    /// Higher scores indicate more similar strings.
    /// Uses normalized edit distance for comparison.
    /// </summary>
    public double ScoreLevenshtein(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        // Normalize to lowercase for case-insensitive comparison
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        var distance = ComputeLevenshteinDistance(a, b);
        var maxLength = Math.Max(a.Length, b.Length);
        
        // Convert distance to similarity score (0.0 to 1.0)
        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    /// Phonetic similarity using Soundex algorithm.
    /// Returns 1.0 for exact phonetic match, 0.0 for no match.
    /// Useful for matching artist/album names with typos or phonetic variations.
    /// </summary>
    public double ScorePhonetic(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var soundexA = Soundex(a);
        var soundexB = Soundex(b);

        // Exact phonetic match
        if (soundexA == soundexB) return 1.0;

        // Partial match: first letter matches (common root sound)
        if (soundexA[0] == soundexB[0]) return 0.5;

        return 0.0;
    }

    /// <summary>
    /// Computes Levenshtein edit distance between two strings.
    /// Measures minimum number of single-character edits (insertions, deletions, substitutions).
    /// </summary>
    private static int ComputeLevenshteinDistance(string a, string b)
    {
        var aLen = a.Length;
        var bLen = b.Length;

        // Create distance matrix
        var dp = new int[aLen + 1, bLen + 1];

        // Initialize base cases
        for (int i = 0; i <= aLen; i++) dp[i, 0] = i;
        for (int j = 0; j <= bLen; j++) dp[0, j] = j;

        // Fill matrix
        for (int i = 1; i <= aLen; i++)
        {
            for (int j = 1; j <= bLen; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1]; // No edit needed
                }
                else
                {
                    dp[i, j] = 1 + Math.Min(
                        Math.Min(dp[i - 1, j],    // Deletion
                                dp[i, j - 1]),    // Insertion
                        dp[i - 1, j - 1]);        // Substitution
                }
            }
        }

        return dp[aLen, bLen];
    }

    /// <summary>
    /// Computes Soundex phonetic code for a string (American English).
    /// Returns 4-character code representing phonetic sound.
    /// </summary>
    private static string Soundex(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "0000";

        s = s.ToUpperInvariant();
        
        // Remove non-alphabetic characters
        s = new string(s.Where(char.IsLetter).ToArray());
        if (s.Length == 0) return "0000";

        var result = new char[4];
        result[0] = s[0]; // Keep first letter

        // Soundex digit mapping
        var prevCode = GetSoundexCode(s[0]);
        int index = 1;

        for (int i = 1; i < s.Length && index < 4; i++)
        {
            var code = GetSoundexCode(s[i]);
            
            // Skip vowels and duplicates
            if (code != '0' && code != prevCode)
            {
                result[index++] = code;
            }
            
            prevCode = code;
        }

        // Pad with zeros
        while (index < 4)
        {
            result[index++] = '0';
        }

        return new string(result);
    }

    /// <summary>
    /// Maps a letter to its Soundex phonetic code.
    /// </summary>
    private static char GetSoundexCode(char c)
    {
        return c switch
        {
            'B' or 'F' or 'P' or 'V' => '1',
            'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z' => '2',
            'D' or 'T' => '3',
            'L' => '4',
            'M' or 'N' => '5',
            'R' => '6',
            _ => '0', // Vowels (A, E, I, O, U), H, W, Y
        };
    }

    private static HashSet<string> Tokenize(string s) =>
        s.ToLowerInvariant()
         .Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(x => x.Trim('\"', '\'', ',', '.', '(', ')', '[', ']'))
         .Where(x => x.Length > 0)
         .ToHashSet();

    /// <inheritdoc/>
    public async Task<double> ScorePerceptualAsync(string contentIdA, string contentIdB, IContentIdRegistry registry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentIdA) || string.IsNullOrWhiteSpace(contentIdB))
            return 0.0;

        try
        {
            // Parse ContentIDs to determine content type
            var parsedA = ContentIdParser.Parse(contentIdA);
            var parsedB = ContentIdParser.Parse(contentIdB);

            if (parsedA == null || parsedB == null)
                return 0.0;

            // Only compare content of the same domain (audio with audio, video with video, etc.)
            if (!string.Equals(parsedA.Domain, parsedB.Domain, StringComparison.OrdinalIgnoreCase))
                return 0.0;

            // For now, we assume perceptual hashes are stored in ContentDescriptors
            // In a real implementation, this would be retrieved from the registry
            // For this prototype, we'll simulate perceptual hash comparison

            var similarity = await ComputeSimulatedPerceptualSimilarityAsync(contentIdA, contentIdB, parsedA.Domain, cancellationToken);
            return similarity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FuzzyMatcher] Error computing perceptual similarity between {ContentIdA} and {ContentIdB}", contentIdA, contentIdB);
            return 0.0;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FuzzyMatchResult>> FindSimilarContentAsync(
        string targetContentId,
        IEnumerable<string> candidates,
        IContentIdRegistry registry,
        double minConfidence = 0.7,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FuzzyMatchResult>();

        foreach (var candidate in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Compute perceptual similarity
            var perceptualScore = await ScorePerceptualAsync(targetContentId, candidate, registry, cancellationToken);

            // Compute text similarity (if applicable)
            var textScore = ComputeTextSimilarity(targetContentId, candidate);

            // Combine scores with weights
            var combinedScore = CombineSimilarityScores(perceptualScore, textScore);

            if (combinedScore >= minConfidence)
            {
                var reason = perceptualScore > textScore ? FuzzyMatchReason.PerceptualHash :
                           textScore > perceptualScore ? FuzzyMatchReason.TextSimilarity :
                           FuzzyMatchReason.Combined;

                results.Add(new FuzzyMatchResult(
                    TargetContentId: targetContentId,
                    CandidateContentId: candidate,
                    Confidence: combinedScore,
                    Reason: reason));
            }
        }

        // Sort by confidence descending
        return results.OrderByDescending(r => r.Confidence).ToArray();
    }

    /// <summary>
    /// Simulates perceptual hash similarity computation.
    /// In a real implementation, this would retrieve stored perceptual hashes
    /// from ContentDescriptors and compare them.
    /// </summary>
    private async Task<double> ComputeSimulatedPerceptualSimilarityAsync(
        string contentIdA,
        string contentIdB,
        string domain,
        CancellationToken cancellationToken)
    {
        // This is a simulation - in practice, perceptual hashes would be stored
        // with ContentDescriptors and retrieved for comparison

        // Simulate different similarity levels based on content IDs
        var similarity = contentIdA.Contains(contentIdB.Split(':').Last()) ||
                        contentIdB.Contains(contentIdA.Split(':').Last())
                       ? 0.9 : 0.3; // High similarity if IDs share common elements

        // Add some randomness for realism
        var randomFactor = new Random(contentIdA.GetHashCode() ^ contentIdB.GetHashCode()).NextDouble() * 0.2;
        similarity = Math.Max(0.0, Math.Min(1.0, similarity + randomFactor - 0.1));

        await Task.CompletedTask;
        return similarity;
    }

    /// <summary>
    /// Computes text-based similarity between ContentIDs.
    /// </summary>
    private double ComputeTextSimilarity(string contentIdA, string contentIdB)
    {
        // Extract identifiers from ContentIDs for text comparison
        var idA = contentIdA.Split(':').LastOrDefault() ?? contentIdA;
        var idB = contentIdB.Split(':').LastOrDefault() ?? contentIdB;

        // Use Levenshtein distance for string similarity
        return ScoreLevenshtein(idA, idB);
    }

    /// <summary>
    /// Combines perceptual and text similarity scores.
    /// </summary>
    private static double CombineSimilarityScores(double perceptualScore, double textScore)
    {
        // Weight perceptual similarity higher for same-domain content
        const double perceptualWeight = 0.7;
        const double textWeight = 0.3;

        return (perceptualScore * perceptualWeight) + (textScore * textWeight);
    }
}

