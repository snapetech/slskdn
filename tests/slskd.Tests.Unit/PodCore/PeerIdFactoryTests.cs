// <copyright file="PeerIdFactoryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.PodCore;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PeerIdFactoryTests
{
    [Fact]
    public void FromSoulseekUsername_WithValidUsername_ReturnsBridgePeerId()
    {
        // Arrange
        var username = "testuser123";

        // Act
        var peerId = PeerIdFactory.FromSoulseekUsername(username);

        // Assert
        Assert.Equal("bridge:testuser123", peerId);
    }

    [Fact]
    public void FromSoulseekUsername_WithEmptyUsername_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => PeerIdFactory.FromSoulseekUsername(""));
    }

    [Fact]
    public void FromSoulseekUsername_WithNullUsername_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PeerIdFactory.FromSoulseekUsername(null!));
    }

    [Fact]
    public void FromSoulseekUsername_WithWhitespaceUsername_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => PeerIdFactory.FromSoulseekUsername("   "));
    }

    [Theory]
    [InlineData("user_name")]
    [InlineData("user-name")]
    [InlineData("user.name")]
    [InlineData("UserName123")]
    [InlineData("u")]
    [InlineData("a_very_long_username_with_underscores_and_dots.and-dashes")]
    public void FromSoulseekUsername_WithVariousValidUsernames_ReturnsCorrectFormat(string username)
    {
        // Act
        var peerId = PeerIdFactory.FromSoulseekUsername(username);

        // Assert
        Assert.Equal($"bridge:{username}", peerId);
        Assert.StartsWith("bridge:", peerId);
    }

    [Theory]
    [InlineData("user@domain.com")] // Contains @
    [InlineData("user domain")] // Contains space
    [InlineData("user\tdomain")] // Contains tab
    [InlineData("user\ndomain")] // Contains newline
    public void FromSoulseekUsername_WithInvalidCharacters_StillWorks(string username)
    {
        // Act
        var peerId = PeerIdFactory.FromSoulseekUsername(username);

        // Assert
        Assert.Equal($"bridge:{username}", peerId);
        // Note: The factory doesn't validate username format, just prefixes with "bridge:"
    }

    [Fact]
    public void FromSoulseekUsername_IsDeterministic()
    {
        // Arrange
        var username = "consistent_user";

        // Act
        var peerId1 = PeerIdFactory.FromSoulseekUsername(username);
        var peerId2 = PeerIdFactory.FromSoulseekUsername(username);

        // Assert
        Assert.Equal(peerId1, peerId2);
    }
}


