// <copyright file="PodModelsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.PodCore;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PodModelsTests
{
    [Fact]
    public void PodMessage_Constructor_SetsDefaults()
    {
        // Act
        var message = new PodMessage();

        // Assert
        Assert.NotNull(message.MessageId);
        Assert.Equal(string.Empty, message.MessageId);
        Assert.Equal(0, message.TimestampUnixMs);
        Assert.Equal(string.Empty, message.PodId);
        Assert.Equal(string.Empty, message.ChannelId);
        Assert.Equal(string.Empty, message.SenderPeerId);
        Assert.Equal(string.Empty, message.Body);
        Assert.Equal(string.Empty, message.Signature);
        Assert.Equal(1, message.SigVersion);
    }

    [Fact]
    public void PodMessage_CustomConstructor_SetsProperties()
    {
        // Arrange
        var messageId = "msg-1";
        var podId = "pod:abcdef123456";
        var channelId = "general";
        var senderPeerId = "peer:mesh:self";
        var body = "Hello world!";
        var signature = "signature123";
        var timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act
        var message = new PodMessage
        {
            MessageId = messageId,
            PodId = podId,
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = body,
            Signature = signature,
            TimestampUnixMs = timestampUnixMs
        };

        // Assert
        Assert.Equal(messageId, message.MessageId);
        Assert.Equal(podId, message.PodId);
        Assert.Equal(channelId, message.ChannelId);
        Assert.Equal(senderPeerId, message.SenderPeerId);
        Assert.Equal(body, message.Body);
        Assert.Equal(signature, message.Signature);
        Assert.Equal(timestampUnixMs, message.TimestampUnixMs);
    }

    [Fact]
    public void PodMessage_MessageId_CanBeSetUnique()
    {
        // Act
        var message1 = new PodMessage { MessageId = Guid.NewGuid().ToString("N") };
        var message2 = new PodMessage { MessageId = Guid.NewGuid().ToString("N") };

        // Assert
        Assert.NotEqual(message1.MessageId, message2.MessageId);
    }

    [Fact]
    public void PodMessage_TimestampUnixMs_CanBeSet()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000;

        // Act
        var message = new PodMessage { TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000;

        // Assert
        Assert.True(message.TimestampUnixMs >= before);
        Assert.True(message.TimestampUnixMs <= after);
    }

    [Fact]
    public void PodChannel_Constructor_SetsDefaults()
    {
        // Act
        var channel = new PodChannel();

        // Assert
        Assert.Equal(string.Empty, channel.ChannelId);
        Assert.Equal(string.Empty, channel.Name);
        Assert.Equal(PodChannelKind.General, channel.Kind);
        Assert.Null(channel.BindingInfo);
        Assert.Null(channel.Description);
    }

    [Fact]
    public void PodChannel_CustomConstructor_SetsProperties()
    {
        // Arrange
        var channelId = "general";
        var name = "General Discussion";
        var kind = PodChannelKind.Custom;
        var bindingInfo = "soulseek-dm:user123";
        var description = "General discussion channel";

        // Act
        var channel = new PodChannel
        {
            ChannelId = channelId,
            Name = name,
            Kind = kind,
            BindingInfo = bindingInfo,
            Description = description
        };

        // Assert
        Assert.Equal(channelId, channel.ChannelId);
        Assert.Equal(name, channel.Name);
        Assert.Equal(kind, channel.Kind);
        Assert.Equal(bindingInfo, channel.BindingInfo);
        Assert.Equal(description, channel.Description);
    }

    [Fact]
    public void Pod_Constructor_SetsDefaults()
    {
        // Act
        var pod = new Pod();

        // Assert
        Assert.Equal(string.Empty, pod.PodId);
        Assert.Equal(string.Empty, pod.Name);
        Assert.Null(pod.Description);
        Assert.Equal(PodVisibility.Unlisted, pod.Visibility);
        Assert.NotNull(pod.Tags);
        Assert.Empty(pod.Tags);
        Assert.NotNull(pod.Channels);
        Assert.Empty(pod.Channels);
        Assert.Null(pod.Members);
    }

    [Fact]
    public void Pod_CustomConstructor_SetsProperties()
    {
        // Arrange
        var podId = "pod:abcdef123456";
        var name = "Test Pod";
        var description = "A test pod";
        var visibility = PodVisibility.Listed;
        var tags = new List<string> { "test", "demo" };
        var channels = new List<PodChannel> { new PodChannel { ChannelId = "general" } };
        var members = new List<PodMember> { new PodMember { PeerId = "peer:mesh:self" } };

        // Act
        var pod = new Pod
        {
            PodId = podId,
            Name = name,
            Description = description,
            Visibility = visibility,
            Tags = tags,
            Channels = channels,
            Members = members
        };

        // Assert
        Assert.Equal(podId, pod.PodId);
        Assert.Equal(name, pod.Name);
        Assert.Equal(description, pod.Description);
        Assert.Equal(visibility, pod.Visibility);
        Assert.Equal(tags, pod.Tags);
        Assert.Equal(channels, pod.Channels);
        Assert.Equal(members, pod.Members);
    }

    [Fact]
    public void PodMember_Constructor_SetsDefaults()
    {
        // Act
        var member = new PodMember();

        // Assert
        Assert.Equal(string.Empty, member.PeerId);
        Assert.Equal(PodRoles.Member, member.Role);
        Assert.Null(member.JoinedAt);
        Assert.Null(member.LastSeen);
        Assert.False(member.IsBanned);
    }

    [Fact]
    public void PodMember_CustomConstructor_SetsProperties()
    {
        // Arrange
        var peerId = "peer:mesh:self";
        var role = PodRoles.Owner;
        var joinedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var lastSeen = DateTimeOffset.UtcNow;
        var isBanned = false;

        // Act
        var member = new PodMember
        {
            PeerId = peerId,
            Role = role,
            JoinedAt = joinedAt,
            LastSeen = lastSeen,
            IsBanned = isBanned
        };

        // Assert
        Assert.Equal(peerId, member.PeerId);
        Assert.Equal(role, member.Role);
        Assert.Equal(joinedAt, member.JoinedAt);
        Assert.Equal(lastSeen, member.LastSeen);
        Assert.Equal(isBanned, member.IsBanned);
    }

    [Theory]
    [InlineData(PodChannelKind.General)]
    [InlineData(PodChannelKind.Custom)]
    [InlineData(PodChannelKind.Bound)]
    [InlineData(PodChannelKind.DirectMessage)]
    public void PodChannelKind_EnumValues_AreDefined(PodChannelKind kind)
    {
        Assert.True(Enum.IsDefined(typeof(PodChannelKind), kind));
    }

    [Theory]
    [InlineData(PodVisibility.Listed)]
    [InlineData(PodVisibility.Unlisted)]
    [InlineData(PodVisibility.Private)]
    public void PodVisibility_EnumValues_AreDefined(PodVisibility visibility)
    {
        Assert.True(Enum.IsDefined(typeof(PodVisibility), visibility));
    }

    [Fact]
    public void PodRoles_Constants_AreDefined()
    {
        Assert.False(string.IsNullOrEmpty(PodRoles.Owner));
        Assert.False(string.IsNullOrEmpty(PodRoles.Moderator));
        Assert.False(string.IsNullOrEmpty(PodRoles.Member));
    }

    [Fact]
    public void PodMessage_Properties_AreSettable()
    {
        // Arrange
        var message = new PodMessage();
        var messageId = "msg-test";
        var timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var podId = "pod:test";
        var channelId = "general";
        var senderPeerId = "peer:mesh:self";
        var body = "Test message";
        var signature = "test-signature";
        var sigVersion = 2;

        // Act
        message.MessageId = messageId;
        message.TimestampUnixMs = timestampUnixMs;
        message.PodId = podId;
        message.ChannelId = channelId;
        message.SenderPeerId = senderPeerId;
        message.Body = body;
        message.Signature = signature;
        message.SigVersion = sigVersion;

        // Assert
        Assert.Equal(messageId, message.MessageId);
        Assert.Equal(timestampUnixMs, message.TimestampUnixMs);
        Assert.Equal(podId, message.PodId);
        Assert.Equal(channelId, message.ChannelId);
        Assert.Equal(senderPeerId, message.SenderPeerId);
        Assert.Equal(body, message.Body);
        Assert.Equal(signature, message.Signature);
        Assert.Equal(sigVersion, message.SigVersion);
    }
}
