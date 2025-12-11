namespace slskd.MediaCore;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Local fuzzy matcher for advisory matching (not published to DHT).
/// Supports multiple algorithms: Jaccard, Levenshtein, and Phonetic matching.
/// </summary>
public interface IFuzzyMatcher
{
    double Score(string title, string artist, string candidateTitle, string candidateArtist);
    double ScoreLevenshtein(string a, string b);
    double ScorePhonetic(string a, string b);
}

public class FuzzyMatcher : IFuzzyMatcher
{
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
}

