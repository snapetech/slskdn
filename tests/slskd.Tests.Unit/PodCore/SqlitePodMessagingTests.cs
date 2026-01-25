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
    private readonly PodDbContext _dbContext;
    private readonly SqlitePodMessaging _messaging;

    private const string PodId1 = "pod:00000000000000000000000000000001";
    private const string PodId2 = "pod:00000000000000000000000000000002";
    private const string SenderPeer = "peer-mesh-self";
    private const string ChannelGeneral = "general";

    public SqlitePodMessagingTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _contextOptions = new DbContextOptionsBuilder<PodDbContext>()
            .UseSqlite(_connection)
            .Options;

        _loggerMock = new Mock<ILogger<SqlitePodMessaging>>();
        _dbContext = new PodDbContext(_contextOptions);
        _dbContext.Database.EnsureCreated();
        SeedPodAndMember(_dbContext, PodId1, SenderPeer);
        SeedPodAndMember(_dbContext, PodId2, SenderPeer);
        _messaging = new SqlitePodMessaging(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private static void SeedPodAndMember(PodDbContext ctx, string podId, string peerId)
    {
        ctx.Pods.Add(new PodEntity
        {
            PodId = podId,
            Name = "Test",
            Visibility = PodVisibility.Unlisted,
            IsPublic = false,
            MaxMembers = 50,
            AllowGuests = false,
            RequireApproval = false,
            UpdatedAt = DateTimeOffset.UtcNow,
            FocusContentId = "",
            Tags = "[]",
            Channels = "[]",
            ExternalBindings = "[]"
        });
        ctx.Members.Add(new PodMemberEntity
        {
            PodId = podId,
            PeerId = peerId,
            Role = "member",
            PublicKey = "",
            IsBanned = false
        });
        ctx.SaveChanges();
    }

    private static PodMessage NewMessage(string podId, string channelId, string sender, string body, long ts)
    {
        return new PodMessage
        {
            PodId = podId,
            ChannelId = channelId,
            SenderPeerId = sender,
            Body = body,
            TimestampUnixMs = ts,
            Signature = "sig"
        };
    }

    [Fact]
    public async Task SendAsync_WithValidMessage_StoresMessage()
    {
        var message = NewMessage(PodId1, ChannelGeneral, SenderPeer, "Hello world!", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var ok = await _messaging.SendAsync(message);
        Assert.True(ok);

        var stored = await _dbContext.Messages.FirstOrDefaultAsync(m => m.PodId == PodId1 && m.ChannelId == ChannelGeneral);
        Assert.NotNull(stored);
        Assert.Equal(PodId1, stored.PodId);
        Assert.Equal(ChannelGeneral, stored.ChannelId);
        Assert.Equal(SenderPeer, stored.SenderPeerId);
        Assert.Equal("Hello world!", stored.Body);
        Assert.Equal("sig", stored.Signature);
    }

    [Fact]
    public async Task SendAsync_WithNullMessage_Throws()
    {
        await Assert.ThrowsAsync<NullReferenceException>(() => _messaging.SendAsync(null!));
    }

    [Fact]
    public async Task GetMessagesAsync_WithValidParameters_ReturnsMessages()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _messaging.SendAsync(NewMessage(PodId1, ChannelGeneral, SenderPeer, "Message 1", ts));
        await _messaging.SendAsync(NewMessage(PodId1, ChannelGeneral, SenderPeer, "Message 2", ts + 1));

        var messages = await _messaging.GetMessagesAsync(PodId1, ChannelGeneral);
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Body == "Message 1");
        Assert.Contains(messages, m => m.Body == "Message 2");
    }

    [Fact]
    public async Task GetMessagesAsync_WithDifferentChannel_ReturnsEmpty()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _messaging.SendAsync(NewMessage(PodId1, ChannelGeneral, SenderPeer, "Test message", ts));

        var messages = await _messaging.GetMessagesAsync(PodId1, "different-channel");
        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetMessagesAsync_WithDifferentPod_ReturnsEmpty()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _messaging.SendAsync(NewMessage(PodId1, ChannelGeneral, SenderPeer, "Test message", ts));

        var messages = await _messaging.GetMessagesAsync(PodId2, ChannelGeneral);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetMessagesAsync_OrdersByTimestampAscending()
    {
        var baseTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _messaging.SendAsync(NewMessage(PodId1, ChannelGeneral, SenderPeer, "M0", baseTs));
        await _messaging.SendAsync(NewMessage(PodId1, ChannelGeneral, SenderPeer, "M1", baseTs + 1));
        await _messaging.SendAsync(NewMessage(PodId1, ChannelGeneral, SenderPeer, "M2", baseTs + 2));

        var messages = await _messaging.GetMessagesAsync(PodId1, ChannelGeneral);
        Assert.Equal(3, messages.Count);
        Assert.True(messages[0].TimestampUnixMs <= messages[1].TimestampUnixMs);
        Assert.True(messages[1].TimestampUnixMs <= messages[2].TimestampUnixMs);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsMappedFields()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _messaging.SendAsync(NewMessage(PodId1, ChannelGeneral, SenderPeer, "Test body", ts));

        var messages = await _messaging.GetMessagesAsync(PodId1, ChannelGeneral);
        Assert.Single(messages);
        var m = messages[0];
        Assert.Equal(PodId1, m.PodId);
        Assert.Equal(ChannelGeneral, m.ChannelId);
        Assert.Equal(SenderPeer, m.SenderPeerId);
        Assert.Equal("Test body", m.Body);
        Assert.Equal("sig", m.Signature);
        Assert.Equal(ts, m.TimestampUnixMs);
    }

    [Fact]
    public async Task SendAsync_InvalidPodId_ReturnsFalse()
    {
        var msg = NewMessage("pod:invalid", ChannelGeneral, SenderPeer, "x", 1);
        var ok = await _messaging.SendAsync(msg);
        Assert.False(ok);
    }

    [Fact]
    public async Task SendAsync_PodNotInDb_ReturnsFalse()
    {
        var msg = NewMessage("pod:00000000000000000000000000000099", ChannelGeneral, SenderPeer, "x", 1);
        var ok = await _messaging.SendAsync(msg);
        Assert.False(ok);
    }
}
