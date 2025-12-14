// <copyright file="SqlitePodMessagingTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.PodCore;
using System.Data.Common;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class SqlitePodMessagingTests : IDisposable
{
    private readonly DbConnection _connection;
    private readonly DbContextOptions<PodDbContext> _contextOptions;
    private readonly Mock<ILogger<SqlitePodMessaging>> _loggerMock;
    private readonly SqlitePodMessaging _messaging;

    public SqlitePodMessagingTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<PodDbContext>()
            .UseSqlite(_connection)
            .Options;

        _loggerMock = new Mock<ILogger<SqlitePodMessaging>>();
        _messaging = new SqlitePodMessaging(_loggerMock.Object, CreateContext());

        // Create the database schema
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task SendAsync_WithValidMessage_StoresMessage()
    {
        // Arrange
        var message = new PodMessage
        {
            PodId = "pod:test123",
            ChannelId = "general",
            SenderPeerId = "peer:mesh:self",
            Body = "Hello world!",
            Signature = "test-signature"
        };

        // Act
        await _messaging.SendAsync(message);

        // Assert
        using var context = CreateContext();
        var storedMessage = await context.PodMessages.FirstOrDefaultAsync();
        Assert.NotNull(storedMessage);
        Assert.Equal(message.PodId, storedMessage!.PodId);
        Assert.Equal(message.ChannelId, storedMessage.ChannelId);
        Assert.Equal(message.SenderPeerId, storedMessage.SenderPeerId);
        Assert.Equal(message.Body, storedMessage.Body);
        Assert.Equal(message.Signature, storedMessage.Signature);
    }

    [Fact]
    public async Task SendAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _messaging.SendAsync(null!));
    }

    [Fact]
    public async Task GetMessagesAsync_WithValidParameters_ReturnsMessages()
    {
        // Arrange
        var podId = "pod:test123";
        var channelId = "general";

        var message1 = new PodMessage
        {
            PodId = podId,
            ChannelId = channelId,
            SenderPeerId = "peer:mesh:self",
            Body = "Message 1",
            Signature = "sig1"
        };

        var message2 = new PodMessage
        {
            PodId = podId,
            ChannelId = channelId,
            SenderPeerId = "peer:mesh:other",
            Body = "Message 2",
            Signature = "sig2"
        };

        await _messaging.SendAsync(message1);
        await _messaging.SendAsync(message2);

        // Act
        var messages = await _messaging.GetMessagesAsync(podId, channelId);

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Body == "Message 1");
        Assert.Contains(messages, m => m.Body == "Message 2");
    }

    [Fact]
    public async Task GetMessagesAsync_WithLimit_ReturnsLimitedMessages()
    {
        // Arrange
        var podId = "pod:test123";
        var channelId = "general";

        for (int i = 0; i < 5; i++)
        {
            var message = new PodMessage
            {
                PodId = podId,
                ChannelId = channelId,
                SenderPeerId = $"peer:mesh:user{i}",
                Body = $"Message {i}",
                Signature = $"sig{i}"
            };
            await _messaging.SendAsync(message);
        }

        // Act
        var messages = await _messaging.GetMessagesAsync(podId, channelId, limit: 3);

        // Assert
        Assert.Equal(3, messages.Count);
    }

    [Fact]
    public async Task GetMessagesAsync_WithBeforeId_ReturnsMessagesBeforeId()
    {
        // Arrange
        var podId = "pod:test123";
        var channelId = "general";

        PodMessage? middleMessage = null;
        for (int i = 0; i < 5; i++)
        {
            var message = new PodMessage
            {
                PodId = podId,
                ChannelId = channelId,
                SenderPeerId = $"peer:mesh:user{i}",
                Body = $"Message {i}",
                Signature = $"sig{i}"
            };
            await _messaging.SendAsync(message);

            if (i == 2)
            {
                middleMessage = message;
            }
        }

        // Act
        var messages = await _messaging.GetMessagesAsync(podId, channelId, beforeId: middleMessage!.Id);

        // Assert
        Assert.Equal(2, messages.Count); // Should get messages 0 and 1 (before message 2)
        Assert.DoesNotContain(messages, m => m.Id == middleMessage.Id);
    }

    [Fact]
    public async Task GetMessagesAsync_WithDifferentChannel_ReturnsEmpty()
    {
        // Arrange
        var podId = "pod:test123";

        var message = new PodMessage
        {
            PodId = podId,
            ChannelId = "general",
            SenderPeerId = "peer:mesh:self",
            Body = "Test message",
            Signature = "test-sig"
        };
        await _messaging.SendAsync(message);

        // Act
        var messages = await _messaging.GetMessagesAsync(podId, "different-channel");

        // Assert
        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetMessagesAsync_WithDifferentPod_ReturnsEmpty()
    {
        // Arrange
        var message = new PodMessage
        {
            PodId = "pod:other",
            ChannelId = "general",
            SenderPeerId = "peer:mesh:self",
            Body = "Test message",
            Signature = "test-sig"
        };
        await _messaging.SendAsync(message);

        // Act
        var messages = await _messaging.GetMessagesAsync("pod:different", "general");

        // Assert
        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetMessagesAsync_OrdersByTimestampDescending()
    {
        // Arrange
        var podId = "pod:test123";
        var channelId = "general";

        // Create messages with small time delays to ensure ordering
        for (int i = 0; i < 3; i++)
        {
            var message = new PodMessage
            {
                PodId = podId,
                ChannelId = channelId,
                SenderPeerId = $"peer:mesh:user{i}",
                Body = $"Message {i}",
                Signature = $"sig{i}"
            };
            await _messaging.SendAsync(message);
            await Task.Delay(1); // Small delay to ensure different timestamps
        }

        // Act
        var messages = await _messaging.GetMessagesAsync(podId, channelId);

        // Assert
        Assert.Equal(3, messages.Count);
        // Should be ordered by timestamp descending (newest first)
        Assert.True(messages[0].Timestamp >= messages[1].Timestamp);
        Assert.True(messages[1].Timestamp >= messages[2].Timestamp);
    }

    [Fact]
    public async Task SendAsync_SetsIdAndTimestamp()
    {
        // Arrange
        var message = new PodMessage
        {
            PodId = "pod:test123",
            ChannelId = "general",
            SenderPeerId = "peer:mesh:self",
            Body = "Test message",
            Signature = "test-sig"
        };

        var originalId = message.Id;
        var beforeSend = DateTimeOffset.UtcNow;

        // Act
        await _messaging.SendAsync(message);
        var afterSend = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotEqual(originalId, message.Id);
        Assert.True(message.Timestamp >= beforeSend);
        Assert.True(message.Timestamp <= afterSend);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsDtoObjects()
    {
        // Arrange
        var message = new PodMessage
        {
            PodId = "pod:test123",
            ChannelId = "general",
            SenderPeerId = "peer:mesh:self",
            Body = "Test message",
            Signature = "test-sig"
        };
        await _messaging.SendAsync(message);

        // Act
        var messages = await _messaging.GetMessagesAsync("pod:test123", "general");

        // Assert
        Assert.Single(messages);
        var retrievedMessage = messages[0];
        Assert.Equal(message.Id, retrievedMessage.Id);
        Assert.Equal(message.PodId, retrievedMessage.PodId);
        Assert.Equal(message.ChannelId, retrievedMessage.ChannelId);
        Assert.Equal(message.SenderPeerId, retrievedMessage.SenderPeerId);
        Assert.Equal(message.Body, retrievedMessage.Body);
        Assert.Equal(message.Signature, retrievedMessage.Signature);
        Assert.Equal(message.Timestamp, retrievedMessage.Timestamp);
    }

    private PodDbContext CreateContext() => new PodDbContext(_contextOptions);
}

