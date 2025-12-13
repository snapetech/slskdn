namespace slskd.Tests.Unit.MediaCore;

using slskd.MediaCore;
using Xunit;

public class FuzzyMatcherTests
{
    private readonly FuzzyMatcher matcher = new();

    [Theory]
    [InlineData("The Beatles", "Abbey Road", "The Beatles", "Abbey Road", 1.0)]
    [InlineData("Beatles", "Abbey Road", "The Beatles", "Abbey Road", 0.75)] // Missing "The"
    [InlineData("Led Zeppelin", "Stairway to Heaven", "Led Zepplin", "Stairway to Heaven", 0.88)] // Typo
    [InlineData("Pink Floyd", "Dark Side of the Moon", "Pink Floyd", "The Dark Side of the Moon", 0.86)] // Extra "The"
    [InlineData("Queen", "Bohemian Rhapsody", "Madonna", "Like a Virgin", 0.0)] // Completely different
    public void Score_ReturnsExpectedJaccardSimilarity(
        string title1, string artist1, string title2, string artist2, double expected)
    {
        var score = matcher.Score(title1, artist1, title2, artist2);
        Assert.InRange(score, expected - 0.1, expected + 0.1);
    }

    [Theory]
    [InlineData("hello", "hello", 1.0)]
    [InlineData("hello", "helo", 0.8)] // 1 deletion
    [InlineData("hello", "hallo", 0.8)] // 1 substitution
    [InlineData("kitten", "sitting", 0.57)] // 3 edits, normalized
    [InlineData("saturday", "sunday", 0.625)] // 3 edits, normalized
    [InlineData("", "", 1.0)]
    [InlineData("hello", "", 0.0)]
    public void ScoreLevenshtein_ReturnsExpectedEditDistance(string a, string b, double expected)
    {
        var score = matcher.ScoreLevenshtein(a, b);
        Assert.InRange(score, expected - 0.05, expected + 0.05);
    }

    [Theory]
    [InlineData("Robert", "Rupert", 1.0)] // R163 - same Soundex
    [InlineData("Smith", "Smythe", 1.0)] // S530 - same Soundex
    [InlineData("Johnson", "Jonson", 1.0)] // J525 - same Soundex
    [InlineData("Lee", "Leigh", 1.0)] // L000 - same Soundex
    [InlineData("Smith", "Schmidt", 0.5)] // S530 vs S530 - first letter matches
    [InlineData("Smith", "Jones", 0.0)] // S530 vs J520 - no match
    [InlineData("", "", 1.0)]
    public void ScorePhonetic_ReturnsExpectedSoundexMatch(string a, string b, double expected)
    {
        var score = matcher.ScorePhonetic(a, b);
        Assert.Equal(expected, score);
    }

    [Fact]
    public void ScoreLevenshtein_IsCaseInsensitive()
    {
        var score1 = matcher.ScoreLevenshtein("HELLO", "hello");
        var score2 = matcher.ScoreLevenshtein("Hello", "hElLo");
        Assert.Equal(1.0, score1);
        Assert.Equal(1.0, score2);
    }

    [Fact]
    public void ScorePhonetic_IsCaseInsensitive()
    {
        var score1 = matcher.ScorePhonetic("SMITH", "smith");
        var score2 = matcher.ScorePhonetic("Smith", "sMiTh");
        Assert.Equal(1.0, score1);
        Assert.Equal(1.0, score2);
    }

    [Theory]
    [InlineData("Metallica", "Metalica", 0.88)] // Typo
    [InlineData("Led Zeppelin", "Led Zepplin", 0.91)] // Typo
    [InlineData("Jimi Hendrix", "Jimmy Hendrix", 0.85)] // Variation
    public void ScoreLevenshtein_HandlesCommonTypos(string a, string b, double minExpected)
    {
        var score = matcher.ScoreLevenshtein(a, b);
        Assert.True(score >= minExpected, $"Expected >= {minExpected}, got {score}");
    }

    [Theory]
    [InlineData("Stevie", "Stephen", 1.0)] // Both start with 'S', phonetically similar
    [InlineData("Catherine", "Katherine", 0.5)] // Different first letter, but similar sound
    [InlineData("Philip", "Phillip", 1.0)] // Spelling variation, same sound
    public void ScorePhonetic_HandlesPhoneticVariations(string a, string b, double expected)
    {
        var score = matcher.ScorePhonetic(a, b);
        Assert.Equal(expected, score);
    }

    [Fact]
    public void Score_EmptyStrings_ReturnsZero()
    {
        var score = matcher.Score("", "", "", "");
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void ScoreLevenshtein_EmptyStrings_ReturnsOne()
    {
        var score = matcher.ScoreLevenshtein("", "");
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void ScorePhonetic_EmptyStrings_ReturnsOne()
    {
        var score = matcher.ScorePhonetic("", "");
        Assert.Equal(1.0, score);
    }
}















