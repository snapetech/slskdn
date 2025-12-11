namespace slskd.Tests.Unit.PodCore;

using System.Collections.Generic;
using System.Linq;
using slskd.PodCore;
using Xunit;

public class PodValidationTests
{
    [Theory]
    [InlineData("Test Pod", true)]
    [InlineData("My-Pod_123", true)]
    [InlineData("Pod with spaces!", true)]
    [InlineData("", false)] // Empty name
    [InlineData(null, false)] // Null name
    public void ValidatePod_Name_ValidatesCorrectly(string name, bool shouldBeValid)
    {
        var pod = new Pod
        {
            Name = name,
            Channels = new List<PodChannel>(),
            Tags = new List<string>()
        };

        var (isValid, error) = PodValidation.ValidatePod(pod);

        Assert.Equal(shouldBeValid, isValid);
        if (!shouldBeValid)
        {
            Assert.NotEmpty(error);
        }
    }

    [Fact]
    public void ValidatePod_NameTooLong_ReturnsInvalid()
    {
        var pod = new Pod
        {
            Name = new string('a', PodValidation.MaxPodNameLength + 1),
            Channels = new List<PodChannel>(),
            Tags = new List<string>()
        };

        var (isValid, error) = PodValidation.ValidatePod(pod);

        Assert.False(isValid);
        Assert.Contains("exceeds", error);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("'; DROP TABLE pods;--", false)]
    [InlineData("Normal Pod Name", true)]
    public void ValidatePod_DetectsDangerousContent(string name, bool shouldBeValid)
    {
        var pod = new Pod
        {
            Name = name,
            Channels = new List<PodChannel>(),
            Tags = new List<string>()
        };

        var (isValid, error) = PodValidation.ValidatePod(pod);

        Assert.Equal(shouldBeValid, isValid);
    }

    [Fact]
    public void ValidatePod_TooManyTags_ReturnsInvalid()
    {
        var pod = new Pod
        {
            Name = "Test Pod",
            Channels = new List<PodChannel>(),
            Tags = Enumerable.Range(0, PodValidation.MaxTagsCount + 1)
                .Select(i => $"tag{i}")
                .ToList()
        };

        var (isValid, error) = PodValidation.ValidatePod(pod);

        Assert.False(isValid);
        Assert.Contains("Exceeds maximum", error);
    }

    [Fact]
    public void ValidatePod_TagTooLong_ReturnsInvalid()
    {
        var pod = new Pod
        {
            Name = "Test Pod",
            Channels = new List<PodChannel>(),
            Tags = new List<string> { new string('a', PodValidation.MaxTagLength + 1) }
        };

        var (isValid, error) = PodValidation.ValidatePod(pod);

        Assert.False(isValid);
        Assert.Contains("Tag exceeds", error);
    }

    [Fact]
    public void ValidatePod_TooManyChannels_ReturnsInvalid()
    {
        var pod = new Pod
        {
            Name = "Test Pod",
            Channels = Enumerable.Range(0, PodValidation.MaxChannelsCount + 1)
                .Select(i => new PodChannel { ChannelId = $"channel{i}", Name = $"Channel {i}" })
                .ToList(),
            Tags = new List<string>()
        };

        var (isValid, error) = PodValidation.ValidatePod(pod);

        Assert.False(isValid);
        Assert.Contains("Exceeds maximum", error);
    }

    [Theory]
    [InlineData("channel-1", true)]
    [InlineData("general", true)]
    [InlineData("test_channel", true)]
    [InlineData("Channel With Spaces", false)] // Spaces not allowed
    [InlineData("channel!", false)] // Special chars not allowed
    [InlineData("", false)] // Empty not allowed
    public void ValidatePod_ChannelId_ValidatesFormat(string channelId, bool shouldBeValid)
    {
        var pod = new Pod
        {
            Name = "Test Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = channelId, Name = "Test Channel" }
            },
            Tags = new List<string>()
        };

        var (isValid, error) = PodValidation.ValidatePod(pod);

        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData("user123", true)]
    [InlineData("user-test@domain.com", true)]
    [InlineData("a_b_c", true)]
    [InlineData("", false)]
    [InlineData("user with spaces", false)] // Spaces not allowed
    public void ValidateMember_PeerId_ValidatesFormat(string peerId, bool shouldBeValid)
    {
        var member = new PodMember
        {
            PeerId = peerId,
            Role = "member"
        };

        var (isValid, error) = PodValidation.ValidateMember(member);

        Assert.Equal(shouldBeValid, isValid);
    }

    [Theory]
    [InlineData("owner", true)]
    [InlineData("mod", true)]
    [InlineData("member", true)]
    [InlineData("admin", false)] // Not a valid role
    [InlineData("", true)] // Empty is allowed (defaults to member)
    public void ValidateMember_Role_ValidatesKnownRoles(string role, bool shouldBeValid)
    {
        var member = new PodMember
        {
            PeerId = "user1",
            Role = role
        };

        var (isValid, error) = PodValidation.ValidateMember(member);

        Assert.Equal(shouldBeValid, isValid);
    }

    [Fact]
    public void ValidateMessage_BodyTooLong_ReturnsInvalid()
    {
        var message = new PodMessage
        {
            SenderPeerId = "user1",
            Body = new string('a', PodValidation.MaxMessageBodyLength + 1)
        };

        var (isValid, error) = PodValidation.ValidateMessage(message);

        Assert.False(isValid);
        Assert.Contains("exceeds", error);
    }

    [Theory]
    [InlineData("Hello world!", true)]
    [InlineData("<script>alert('xss')</script>", false)]
    [InlineData("'; DELETE FROM messages;--", false)]
    [InlineData("Normal message content", true)]
    public void ValidateMessage_DetectsDangerousContent(string body, bool shouldBeValid)
    {
        var message = new PodMessage
        {
            SenderPeerId = "user1",
            Body = body
        };

        var (isValid, error) = PodValidation.ValidateMessage(message);

        Assert.Equal(shouldBeValid, isValid);
    }

    [Fact]
    public void Sanitize_RemovesControlCharacters()
    {
        var input = "Hello\x00\x01\x02World";
        var sanitized = PodValidation.Sanitize(input, 100);

        Assert.Equal("HelloWorld", sanitized);
    }

    [Fact]
    public void Sanitize_PreservesNewlines()
    {
        var input = "Line1\nLine2\rLine3";
        var sanitized = PodValidation.Sanitize(input, 100);

        Assert.Contains("\n", sanitized);
        Assert.Contains("\r", sanitized);
    }

    [Fact]
    public void Sanitize_TruncatesLongStrings()
    {
        var input = new string('a', 200);
        var sanitized = PodValidation.Sanitize(input, 50);

        Assert.Equal(50, sanitized.Length);
    }

    [Theory]
    [InlineData("pod:0123456789abcdef0123456789abcdef", true)]
    [InlineData("pod:invalid", false)]
    [InlineData("not-a-pod-id", false)]
    [InlineData("", false)]
    public void IsValidPodId_ValidatesFormat(string podId, bool expected)
    {
        var isValid = PodValidation.IsValidPodId(podId);
        Assert.Equal(expected, isValid);
    }

    [Theory]
    [InlineData("user123", true)]
    [InlineData("user-test", true)]
    [InlineData("user_test@example.com", true)]
    [InlineData("user with spaces", false)]
    [InlineData("", false)]
    public void IsValidPeerId_ValidatesFormat(string peerId, bool expected)
    {
        var isValid = PodValidation.IsValidPeerId(peerId);
        Assert.Equal(expected, isValid);
    }

    [Theory]
    [InlineData("general", true)]
    [InlineData("off-topic", true)]
    [InlineData("channel_1", true)]
    [InlineData("Channel With Spaces", false)]
    [InlineData("", false)]
    public void IsValidChannelId_ValidatesFormat(string channelId, bool expected)
    {
        var isValid = PodValidation.IsValidChannelId(channelId);
        Assert.Equal(expected, isValid);
    }
}
