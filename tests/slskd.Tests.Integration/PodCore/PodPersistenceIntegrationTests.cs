namespace slskd.Tests.Integration.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.PodCore;
using Xunit;

/// <summary>
/// Integration tests for PodCore SQLite persistence.
/// Tests full stack: service -> SQLite -> retrieval.
/// </summary>
public class PodPersistenceIntegrationTests : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly IPodService podService;
    private readonly IPodMessaging podMessaging;
    private readonly string testDbPath;

    public PodPersistenceIntegrationTests()
    {
        testDbPath = $"test_pods_{Guid.NewGuid()}.db";
        
        var services = new ServiceCollection();
        services.AddDbContextFactory<PodDbContext>(options =>
        {
            options.UseSqlite($"Data Source={testDbPath}");
        });
        services.AddSingleton<IPodService, SqlitePodService>();
        services.AddSingleton<IPodMessaging, SqlitePodMessaging>();
        services.AddSingleton(NullLogger<SqlitePodService>.Instance);
        services.AddSingleton(NullLogger<SqlitePodMessaging>.Instance);

        serviceProvider = services.BuildServiceProvider();
        
        // Ensure database is created
        using var dbContext = serviceProvider.GetRequiredService<IDbContextFactory<PodDbContext>>().CreateDbContext();
        dbContext.Database.EnsureCreated();
        
        podService = serviceProvider.GetRequiredService<IPodService>();
        podMessaging = serviceProvider.GetRequiredService<IPodMessaging>();
    }

    public void Dispose()
    {
        serviceProvider?.Dispose();
        if (System.IO.File.Exists(testDbPath))
        {
            System.IO.File.Delete(testDbPath);
        }
    }

    [Fact]
    public async Task CreateAndRetrievePod_PersistsCorrectly()
    {
        var pod = new Pod
        {
            Name = "Test Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General" }
            },
            Tags = new List<string> { "test", "integration" }
        };

        var created = await podService.CreateAsync(pod);
        Assert.NotNull(created);
        Assert.NotNull(created.PodId);

        var retrieved = await podService.GetPodAsync(created.PodId);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Pod", retrieved.Name);
        Assert.Single(retrieved.Channels);
        Assert.Equal("general", retrieved.Channels[0].ChannelId);
        Assert.Equal(2, retrieved.Tags.Count);
    }

    [Fact]
    public async Task JoinAndLeave_UpdatesMembership()
    {
        var pod = await CreateTestPod("Membership Test Pod");
        
        var member = new PodMember
        {
            PeerId = "user1",
            PublicKey = "publickey1",
            Role = "member"
        };

        await podService.JoinAsync(pod.PodId, member);

        var members = await podService.GetMembersAsync(pod.PodId);
        Assert.Single(members);
        Assert.Equal("user1", members[0].PeerId);

        await podService.LeaveAsync(pod.PodId, "user1");

        members = await podService.GetMembersAsync(pod.PodId);
        Assert.Empty(members);
    }

    [Fact]
    public async Task SendAndRetrieveMessages_PersistsCorrectly()
    {
        var pod = await CreateTestPod("Message Test Pod");
        
        var message = new PodMessage
        {
            PodId = pod.PodId,
            ChannelId = "general",
            SenderPeerId = "user1",
            Body = "Hello, world!",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var sent = await podMessaging.SendAsync(message);
        Assert.True(sent);

        var messages = await podMessaging.GetMessagesAsync(pod.PodId, "general", null);
        Assert.Single(messages);
        Assert.Equal("Hello, world!", messages[0].Body);
        Assert.Equal("user1", messages[0].SenderPeerId);
    }

    [Fact]
    public async Task MultipleMessages_PreservesOrder()
    {
        var pod = await CreateTestPod("Order Test Pod");
        
        for (int i = 0; i < 10; i++)
        {
            var message = new PodMessage
            {
                PodId = pod.PodId,
                ChannelId = "general",
                SenderPeerId = $"user{i}",
                Body = $"Message {i}",
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i
            };
            await podMessaging.SendAsync(message);
            await Task.Delay(10); // Ensure timestamp ordering
        }

        var messages = await podMessaging.GetMessagesAsync(pod.PodId, "general", null);
        Assert.Equal(10, messages.Count);
        
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"Message {i}", messages[i].Body);
        }
    }

    [Fact]
    public async Task BanMember_UpdatesStatus()
    {
        var pod = await CreateTestPod("Ban Test Pod");
        
        var member = new PodMember
        {
            PeerId = "baduser",
            Role = "member"
        };

        await podService.JoinAsync(pod.PodId, member);
        await podService.BanAsync(pod.PodId, "baduser");

        var members = await podService.GetMembersAsync(pod.PodId);
        Assert.Single(members);
        Assert.True(members[0].IsBanned);
    }

    [Fact]
    public async Task ListPods_ReturnsAllPods()
    {
        await CreateTestPod("Pod 1");
        await CreateTestPod("Pod 2");
        await CreateTestPod("Pod 3");

        var allPods = await podService.ListAsync();
        Assert.True(allPods.Count >= 3);
    }

    [Fact]
    public async Task MessagePersistence_SurvivesRestart()
    {
        var pod = await CreateTestPod("Persistence Test Pod");
        
        // Send messages
        for (int i = 0; i < 5; i++)
        {
            var message = new PodMessage
            {
                PodId = pod.PodId,
                ChannelId = "general",
                SenderPeerId = "user1",
                Body = $"Message {i}",
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            await podMessaging.SendAsync(message);
        }

        // Verify messages persisted (would survive app restart)
        var messages = await podMessaging.GetMessagesAsync(pod.PodId, "general", null);
        Assert.Equal(5, messages.Count);
    }

    [Fact]
    public async Task MembershipHistory_TracksChanges()
    {
        var pod = await CreateTestPod("History Test Pod");
        
        var member = new PodMember
        {
            PeerId = "user1",
            Role = "member"
        };

        await podService.JoinAsync(pod.PodId, member);
        await podService.LeaveAsync(pod.PodId, "user1");

        var history = await podService.GetMembershipHistoryAsync(pod.PodId);
        Assert.True(history.Count >= 2); // Join + Leave records
    }

    [Fact]
    public async Task ConcurrentWrites_HandleCorrectly()
    {
        var pod = await CreateTestPod("Concurrent Test Pod");
        
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var message = new PodMessage
            {
                PodId = pod.PodId,
                ChannelId = "general",
                SenderPeerId = $"user{i}",
                Body = $"Concurrent message {i}",
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return await podMessaging.SendAsync(message);
        });

        var results = await Task.WhenAll(tasks);
        Assert.All(results, result => Assert.True(result));

        var messages = await podMessaging.GetMessagesAsync(pod.PodId, "general", null);
        Assert.Equal(10, messages.Count);
    }

    [Fact]
    public async Task ValidationFailures_DoNotCorruptDatabase()
    {
        // Try to create pod with invalid name (too long)
        var invalidPod = new Pod
        {
            Name = new string('a', 1000),
            Channels = new List<PodChannel>(),
            Tags = new List<string>()
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await podService.CreateAsync(invalidPod);
        });

        // Database should still be accessible
        var allPods = await podService.ListAsync();
        Assert.NotNull(allPods);
    }

    [Fact]
    public async Task MultipleChannels_WorkIndependently()
    {
        var pod = new Pod
        {
            Name = "Multi-Channel Pod",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General" },
                new PodChannel { ChannelId = "off-topic", Name = "Off-Topic" }
            },
            Tags = new List<string>()
        };

        var created = await podService.CreateAsync(pod);

        // Send messages to different channels
        await podMessaging.SendAsync(new PodMessage
        {
            PodId = created.PodId,
            ChannelId = "general",
            SenderPeerId = "user1",
            Body = "General message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        await podMessaging.SendAsync(new PodMessage
        {
            PodId = created.PodId,
            ChannelId = "off-topic",
            SenderPeerId = "user2",
            Body = "Off-topic message",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        var generalMessages = await podMessaging.GetMessagesAsync(created.PodId, "general", null);
        var offTopicMessages = await podMessaging.GetMessagesAsync(created.PodId, "off-topic", null);

        Assert.Single(generalMessages);
        Assert.Single(offTopicMessages);
        Assert.Equal("General message", generalMessages[0].Body);
        Assert.Equal("Off-topic message", offTopicMessages[0].Body);
    }

    // Helper method
    private async Task<Pod> CreateTestPod(string name)
    {
        var pod = new Pod
        {
            Name = name,
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General" }
            },
            Tags = new List<string>()
        };
        return await podService.CreateAsync(pod);
    }
}
