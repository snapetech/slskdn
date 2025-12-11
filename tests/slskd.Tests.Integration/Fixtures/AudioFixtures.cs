namespace slskd.Tests.Integration.Fixtures;

/// <summary>
/// Audio test fixtures for integration tests.
/// </summary>
public static class AudioFixtures
{
    /// <summary>
    /// Get test audio file with known characteristics.
    /// </summary>
    public static TestAudioFile GetTestFile(string id)
    {
        return id switch
        {
            "flac-lossless" => new TestAudioFile
            {
                Id = "flac-lossless",
                Filename = "test-track-lossless.flac",
                Codec = "FLAC",
                BitrateKbps = 1411,
                SampleRate = 44100,
                Channels = 2,
                DurationSeconds = 30,
                ExpectedQualityScore = 1.0,
                ExpectedTranscode = false,
                Content = GenerateFLAC()
            },
            
            "mp3-320" => new TestAudioFile
            {
                Id = "mp3-320",
                Filename = "test-track-320.mp3",
                Codec = "MP3",
                BitrateKbps = 320,
                SampleRate = 44100,
                Channels = 2,
                DurationSeconds = 30,
                ExpectedQualityScore = 0.95,
                ExpectedTranscode = false,
                Content = GenerateMP3()
            },
            
            "mp3-128-transcode" => new TestAudioFile
            {
                Id = "mp3-128-transcode",
                Filename = "test-track-128-transcode.mp3",
                Codec = "MP3",
                BitrateKbps = 128,
                SampleRate = 44100,
                Channels = 2,
                DurationSeconds = 30,
                ExpectedQualityScore = 0.70,
                ExpectedTranscode = true,
                Content = GenerateMP3Transcode()
            },
            
            "opus-256" => new TestAudioFile
            {
                Id = "opus-256",
                Filename = "test-track.opus",
                Codec = "Opus",
                BitrateKbps = 256,
                SampleRate = 48000,
                Channels = 2,
                DurationSeconds = 30,
                ExpectedQualityScore = 0.97,
                ExpectedTranscode = false,
                Content = GenerateOpus()
            },
            
            "aac-256" => new TestAudioFile
            {
                Id = "aac-256",
                Filename = "test-track.m4a",
                Codec = "AAC",
                BitrateKbps = 256,
                SampleRate = 44100,
                Channels = 2,
                DurationSeconds = 30,
                ExpectedQualityScore = 0.92,
                ExpectedTranscode = false,
                Content = GenerateAAC()
            },
            
            _ => throw new ArgumentException($"Unknown test file: {id}")
        };
    }

    /// <summary>
    /// Get all test files.
    /// </summary>
    public static IEnumerable<TestAudioFile> GetAllTestFiles()
    {
        yield return GetTestFile("flac-lossless");
        yield return GetTestFile("mp3-320");
        yield return GetTestFile("mp3-128-transcode");
        yield return GetTestFile("opus-256");
        yield return GetTestFile("aac-256");
    }

    private static byte[] GenerateFLAC()
    {
        // Generate minimal valid FLAC file
        // For real tests, this would be actual FLAC data
        // For now, return placeholder with FLAC magic bytes
        var data = new List<byte>();
        data.AddRange(System.Text.Encoding.ASCII.GetBytes("fLaC")); // FLAC magic
        data.AddRange(new byte[1024]); // Placeholder audio data
        return data.ToArray();
    }

    private static byte[] GenerateMP3()
    {
        // Generate minimal valid MP3 file
        var data = new List<byte>();
        data.AddRange(new byte[] { 0xFF, 0xFB }); // MP3 frame sync
        data.AddRange(new byte[2048]); // Placeholder audio data
        return data.ToArray();
    }

    private static byte[] GenerateMP3Transcode()
    {
        // Generate MP3 that appears to be a transcode
        // (Low bitrate with artifacts)
        var data = GenerateMP3();
        // Add transcode markers (simplified)
        return data;
    }

    private static byte[] GenerateOpus()
    {
        // Generate minimal valid Opus file
        var data = new List<byte>();
        data.AddRange(System.Text.Encoding.ASCII.GetBytes("OpusHead")); // Opus magic
        data.AddRange(new byte[512]); // Placeholder audio data
        return data.ToArray();
    }

    private static byte[] GenerateAAC()
    {
        // Generate minimal valid AAC/M4A file
        var data = new List<byte>();
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x20 }); // ftyp box
        data.AddRange(System.Text.Encoding.ASCII.GetBytes("ftypisom"));
        data.AddRange(new byte[1024]); // Placeholder audio data
        return data.ToArray();
    }
}

/// <summary>
/// Test audio file metadata.
/// </summary>
public class TestAudioFile
{
    public string Id { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public int BitrateKbps { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int DurationSeconds { get; set; }
    public double ExpectedQualityScore { get; set; }
    public bool ExpectedTranscode { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    
    // MusicBrainz test data
    public string MbRecordingId { get; set; } = "test-recording-" + Guid.NewGuid().ToString();
    public string MbReleaseId { get; set; } = "test-release-" + Guid.NewGuid().ToString();
    public string Artist { get; set; } = "Test Artist";
    public string Title { get; set; } = "Test Track";
    public string Album { get; set; } = "Test Album";
}
