namespace slskd.MediaCore;

/// <summary>
/// Local fuzzy matcher for advisory matching (not published to DHT).
/// </summary>
public interface IFuzzyMatcher
{
    double Score(string title, string artist, string candidateTitle, string candidateArtist);
}

public class FuzzyMatcher : IFuzzyMatcher
{
    public double Score(string title, string artist, string candidateTitle, string candidateArtist)
    {
        // Placeholder: simple case-insensitive token overlap
        var t = Tokenize($"{title} {artist}");
        var c = Tokenize($"{candidateTitle} {candidateArtist}");
        if (t.Count == 0 || c.Count == 0) return 0;
        var intersection = t.Intersect(c).Count();
        var union = t.Union(c).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> Tokenize(string s) =>
        s.ToLowerInvariant()
         .Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(x => x.Trim('\"', '\'', ',', '.', '(', ')', '[', ']'))
         .Where(x => x.Length > 0)
         .ToHashSet();
}
