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
        Assert.NotNull(message.Id);
        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.NotNull(message.Timestamp);
        Assert.True(message.Timestamp > DateTimeOffset.MinValue);
        Assert.Null(message.PodId);
        Assert.Null(message.ChannelId);
        Assert.Null(message.SenderPeerId);
        Assert.Null(message.Body);
        Assert.Null(message.Signature);
    }

    [Fact]
    public void PodMessage_CustomConstructor_SetsProperties()
    {
        // Arrange
        var podId = "pod:abcdef123456";
        var channelId = "general";
        var senderPeerId = "peer:mesh:self";
        var body = "Hello world!";
        var signature = "signature123";

        // Act
        var message = new PodMessage
        {
            PodId = podId,
            ChannelId = channelId,
            SenderPeerId = senderPeerId,
            Body = body,
            Signature = signature
        };

        // Assert
        Assert.Equal(podId, message.PodId);
        Assert.Equal(channelId, message.ChannelId);
        Assert.Equal(senderPeerId, message.SenderPeerId);
        Assert.Equal(body, message.Body);
        Assert.Equal(signature, message.Signature);
        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.NotNull(message.Timestamp);
    }

    [Fact]
    public void PodMessage_Id_IsUnique()
    {
        // Act
        var message1 = new PodMessage();
        var message2 = new PodMessage();

        // Assert
        Assert.NotEqual(message1.Id, message2.Id);
    }

    [Fact]
    public void PodMessage_Timestamp_IsRecent()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        var message = new PodMessage();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(message.Timestamp >= before);
        Assert.True(message.Timestamp <= after);
    }

    [Fact]
    public void PodChannel_Constructor_SetsDefaults()
    {
        // Act
        var channel = new PodChannel();

        // Assert
        Assert.Null(channel.ChannelId);
        Assert.Null(channel.Name);
        Assert.Null(channel.Kind);
        Assert.Null(channel.BindingInfo);
        Assert.Null(channel.Description);
    }

    [Fact]
    public void PodChannel_CustomConstructor_SetsProperties()
    {
        // Arrange
        var channelId = "general";
        var name = "General Discussion";
        var kind = ChannelKind.Public;
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
        Assert.Null(pod.PodId);
        Assert.Null(pod.Name);
        Assert.Null(pod.Description);
        Assert.Equal(Visibility.Private, pod.Visibility);
        Assert.Null(pod.Tags);
        Assert.Null(pod.Channels);
        Assert.Null(pod.Members);
    }

    [Fact]
    public void Pod_CustomConstructor_SetsProperties()
    {
        // Arrange
        var podId = "pod:abcdef123456";
        var name = "Test Pod";
        var description = "A test pod";
        var visibility = Visibility.Public;
        var tags = new[] { "test", "demo" };
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
        Assert.Null(member.PeerId);
        Assert.Equal(PodRole.Member, member.Role);
        Assert.Null(member.JoinedAt);
        Assert.Null(member.LastSeen);
        Assert.False(member.IsBanned);
    }

    [Fact]
    public void PodMember_CustomConstructor_SetsProperties()
    {
        // Arrange
        var peerId = "peer:mesh:self";
        var role = PodRole.Owner;
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
    [InlineData(ChannelKind.Public)]
    [InlineData(ChannelKind.Private)]
    [InlineData(ChannelKind.DirectMessage)]
    public void ChannelKind_EnumValues_AreDefined(ChannelKind kind)
    {
        // Act & Assert - Should not throw
        Assert.True(Enum.IsDefined(typeof(ChannelKind), kind));
    }

    [Theory]
    [InlineData(Visibility.Public)]
    [InlineData(Visibility.Unlisted)]
    [InlineData(Visibility.Private)]
    public void Visibility_EnumValues_AreDefined(Visibility visibility)
    {
        // Act & Assert - Should not throw
        Assert.True(Enum.IsDefined(typeof(Visibility), visibility));
    }

    [Theory]
    [InlineData(PodRole.Owner)]
    [InlineData(PodRole.Moderator)]
    [InlineData(PodRole.Member)]
    public void PodRole_EnumValues_AreDefined(PodRole role)
    {
        // Act & Assert - Should not throw
        Assert.True(Enum.IsDefined(typeof(PodRole), role));
    }

    [Fact]
    public void PodMessage_Properties_AreSettable()
    {
        // Arrange
        var message = new PodMessage();
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var podId = "pod:test";
        var channelId = "general";
        var senderPeerId = "peer:mesh:self";
        var body = "Test message";
        var signature = "test-signature";

        // Act
        message.Id = id;
        message.Timestamp = timestamp;
        message.PodId = podId;
        message.ChannelId = channelId;
        message.SenderPeerId = senderPeerId;
        message.Body = body;
        message.Signature = signature;

        // Assert
        Assert.Equal(id, message.Id);
        Assert.Equal(timestamp, message.Timestamp);
        Assert.Equal(podId, message.PodId);
        Assert.Equal(channelId, message.ChannelId);
        Assert.Equal(senderPeerId, message.SenderPeerId);
        Assert.Equal(body, message.Body);
        Assert.Equal(signature, message.Signature);
    }
}
