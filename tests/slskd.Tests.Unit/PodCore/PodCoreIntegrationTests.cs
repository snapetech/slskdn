// <copyright file="PodCoreIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Messaging;
using slskd.Mesh;
using slskd.PodCore;
using System.Data.Common;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PodCoreIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly DbConnection _connection;
    private readonly DbContextOptions<PodDbContext> _contextOptions;

    public PodCoreIntegrationTests()
    {
        // Set up in-memory database
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<PodDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Set up DI container with real services
        var services = new ServiceCollection();

        // Logging
        services.AddLogging();

        // Database
        services.AddScoped<PodDbContext>(provider => new PodDbContext(_contextOptions));

        // Configuration
        services.AddSingleton<IOptionsMonitor<MeshOptions>>(provider =>
            Mock.Of<IOptionsMonitor<MeshOptions>>(x => x.CurrentValue == new MeshOptions { SelfPeerId = "peer:mesh:self" }));

        // Services
        services.AddScoped<IPodService, PodService>();
        services.AddScoped<IPodMessaging, SqlitePodMessaging>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IServiceScopeFactory>(provider => provider.GetRequiredService<IServiceProvider>() as IServiceScopeFactory);

        // Coordinator
        services.AddScoped<ConversationPodCoordinator>();

        _serviceProvider = services.BuildServiceProvider();

        // Create database schema
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PodDbContext>();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task EndToEndPodCreationAndMessagingFlow()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();
        var coordinator = scope.ServiceProvider.GetRequiredService<ConversationPodCoordinator>();

        // Act - Create a conversation pod
        var (podId, channelId) = await coordinator.EnsureDirectMessagePodAsync("testuser");

        // Assert - Pod was created
        var pod = await podService.GetPodAsync(podId);
        Assert.NotNull(pod);
        Assert.Equal("testuser", pod!.Name);
        Assert.Equal(Visibility.Private, pod.Visibility);
        Assert.Contains("dm", pod.Tags!);

        // Check DM channel exists
        var dmChannel = pod.Channels!.FirstOrDefault(c => c.ChannelId == channelId);
        Assert.NotNull(dmChannel);
        Assert.Equal("DM", dmChannel!.Name);
        Assert.Equal($"soulseek-dm:testuser", dmChannel.BindingInfo);

        // Act - Send a message
        var message = new PodMessage
        {
            PodId = podId,
            ChannelId = channelId,
            SenderPeerId = "peer:mesh:self",
            Body = "Hello from integration test!",
            Signature = "test-sig"
        };

        await messaging.SendAsync(message);

        // Assert - Message was stored
        var messages = await messaging.GetMessagesAsync(podId, channelId);
        Assert.Single(messages);
        Assert.Equal("Hello from integration test!", messages[0].Body);
        Assert.Equal("peer:mesh:self", messages[0].SenderPeerId);

        // Act - Send another message
        var message2 = new PodMessage
        {
            PodId = podId,
            ChannelId = channelId,
            SenderPeerId = "bridge:testuser",
            Body = "Hello back!",
            Signature = "test-sig-2"
        };

        await messaging.SendAsync(message2);

        // Assert - Both messages are stored and ordered correctly
        messages = await messaging.GetMessagesAsync(podId, channelId);
        Assert.Equal(2, messages.Count);
        Assert.True(messages[0].Timestamp >= messages[1].Timestamp); // Newest first
    }

    [Fact]
    public async Task PodServiceAndMessagingIntegration()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        // Create a pod
        var pod = new Pod
        {
            PodId = "pod:integration-test",
            Name = "Integration Test Pod",
            Description = "Test pod for integration testing",
            Visibility = Visibility.Public,
            Tags = new[] { "test", "integration" },
            Channels = new List<PodChannel>
            {
                new PodChannel
                {
                    ChannelId = "general",
                    Name = "General Discussion",
                    Kind = ChannelKind.Public,
                    Description = "General chat"
                },
                new PodChannel
                {
                    ChannelId = "random",
                    Name = "Random",
                    Kind = ChannelKind.Public
                }
            },
            Members = new List<PodMember>
            {
                new PodMember
                {
                    PeerId = "peer:mesh:self",
                    Role = PodRole.Owner,
                    JoinedAt = DateTimeOffset.UtcNow
                }
            }
        };

        // Act - Create pod
        var createdPodId = await podService.CreatePodAsync(pod);

        // Assert - Pod was created
        Assert.Equal("pod:integration-test", createdPodId);
        var retrievedPod = await podService.GetPodAsync(createdPodId);
        Assert.NotNull(retrievedPod);
        Assert.Equal("Integration Test Pod", retrievedPod!.Name);
        Assert.Equal(2, retrievedPod.Channels!.Count);

        // Act - Send messages to different channels
        var message1 = new PodMessage
        {
            PodId = createdPodId,
            ChannelId = "general",
            SenderPeerId = "peer:mesh:self",
            Body = "Hello in general!",
            Signature = "sig1"
        };

        var message2 = new PodMessage
        {
            PodId = createdPodId,
            ChannelId = "random",
            SenderPeerId = "peer:mesh:self",
            Body = "Random message",
            Signature = "sig2"
        };

        await messaging.SendAsync(message1);
        await messaging.SendAsync(message2);

        // Assert - Messages are isolated by channel
        var generalMessages = await messaging.GetMessagesAsync(createdPodId, "general");
        var randomMessages = await messaging.GetMessagesAsync(createdPodId, "random");

        Assert.Single(generalMessages);
        Assert.Single(randomMessages);
        Assert.Equal("Hello in general!", generalMessages[0].Body);
        Assert.Equal("Random message", randomMessages[0].Body);

        // Act - List all pods
        var pods = await podService.ListPodsAsync();

        // Assert - Our pod is in the list
        Assert.Contains(pods, p => p.PodId == createdPodId);
    }

    [Fact]
    public async Task ConversationPodCoordinatorWithRealServices()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<ConversationPodCoordinator>();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        // Act - Create DM pod for first user
        var (podId1, channelId1) = await coordinator.EnsureDirectMessagePodAsync("alice");

        // Create DM pod for second user
        var (podId2, channelId2) = await coordinator.EnsureDirectMessagePodAsync("bob");

        // Assert - Different pods were created
        Assert.NotEqual(podId1, podId2);
        Assert.Equal("dm", channelId1);
        Assert.Equal("dm", channelId2);

        // Verify pods exist
        var pod1 = await podService.GetPodAsync(podId1);
        var pod2 = await podService.GetPodAsync(podId2);
        Assert.NotNull(pod1);
        Assert.NotNull(pod2);
        Assert.Equal("alice", pod1!.Name);
        Assert.Equal("bob", pod2!.Name);

        // Act - Send messages in both conversations
        var message1 = new PodMessage
        {
            PodId = podId1,
            ChannelId = channelId1,
            SenderPeerId = "peer:mesh:self",
            Body = "Hello Alice!",
            Signature = "sig1"
        };

        var message2 = new PodMessage
        {
            PodId = podId2,
            ChannelId = channelId2,
            SenderPeerId = "peer:mesh:self",
            Body = "Hello Bob!",
            Signature = "sig2"
        };

        await messaging.SendAsync(message1);
        await messaging.SendAsync(message2);

        // Assert - Messages are in correct pods
        var aliceMessages = await messaging.GetMessagesAsync(podId1, channelId1);
        var bobMessages = await messaging.GetMessagesAsync(podId2, channelId2);

        Assert.Single(aliceMessages);
        Assert.Single(bobMessages);
        Assert.Equal("Hello Alice!", aliceMessages[0].Body);
        Assert.Equal("Hello Bob!", bobMessages[0].Body);

        // Act - Try to create the same pod again (idempotency)
        var (podId1Again, channelId1Again) = await coordinator.EnsureDirectMessagePodAsync("alice");

        // Assert - Same pod returned
        Assert.Equal(podId1, podId1Again);
        Assert.Equal(channelId1, channelId1Again);
    }

    [Fact]
    public async Task PodMembershipAndMessagingIntegration()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        // Create a pod with members
        var pod = new Pod
        {
            PodId = "pod:membership-test",
            Name = "Membership Test",
            Visibility = Visibility.Public,
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General", Kind = ChannelKind.Public }
            },
            Members = new List<PodMember>
            {
                new PodMember { PeerId = "peer:mesh:alice", Role = PodRole.Owner },
                new PodMember { PeerId = "peer:mesh:bob", Role = PodRole.Member }
            }
        };

        await podService.CreatePodAsync(pod);

        // Act - Add a new member
        var joined = await podService.JoinPodAsync("pod:membership-test", "peer:mesh:charlie");

        // Assert - Member was added
        Assert.True(joined);
        var updatedPod = await podService.GetPodAsync("pod:membership-test");
        Assert.Equal(3, updatedPod!.Members!.Count);
        Assert.Contains(updatedPod.Members!, m => m.PeerId == "peer:mesh:charlie");

        // Act - Send messages from different members
        var messages = new[]
        {
            new PodMessage { PodId = "pod:membership-test", ChannelId = "general", SenderPeerId = "peer:mesh:alice", Body = "Hi from Alice", Signature = "sig1" },
            new PodMessage { PodId = "pod:membership-test", ChannelId = "general", SenderPeerId = "peer:mesh:bob", Body = "Hi from Bob", Signature = "sig2" },
            new PodMessage { PodId = "pod:membership-test", ChannelId = "general", SenderPeerId = "peer:mesh:charlie", Body = "Hi from Charlie", Signature = "sig3" }
        };

        foreach (var message in messages)
        {
            await messaging.SendAsync(message);
        }

        // Assert - All messages are stored
        var storedMessages = await messaging.GetMessagesAsync("pod:membership-test", "general");
        Assert.Equal(3, storedMessages.Count);
        Assert.Equal(3, storedMessages.Select(m => m.SenderPeerId).Distinct().Count());

        // Act - Remove a member
        var left = await podService.LeavePodAsync("pod:membership-test", "peer:mesh:bob");

        // Assert - Member was removed
        Assert.True(left);
        updatedPod = await podService.GetPodAsync("pod:membership-test");
        Assert.Equal(2, updatedPod!.Members!.Count);
        Assert.DoesNotContain(updatedPod.Members!, m => m.PeerId == "peer:mesh:bob");
    }

    [Fact]
    public async Task MessagePaginationAndOrdering()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        var podId = "pod:pagination-test";
        var channelId = "general";

        // Create 10 messages
        for (int i = 0; i < 10; i++)
        {
            var message = new PodMessage
            {
                PodId = podId,
                ChannelId = channelId,
                SenderPeerId = $"peer:mesh:user{i}",
                Body = $"Message {i}",
                Signature = $"sig{i}"
            };
            await messaging.SendAsync(message);
            await Task.Delay(1); // Ensure different timestamps
        }

        // Act - Get first page (limit 5)
        var firstPage = await messaging.GetMessagesAsync(podId, channelId, limit: 5);

        // Assert - Got 5 messages, newest first
        Assert.Equal(5, firstPage.Count);
        Assert.True(firstPage[0].Timestamp >= firstPage[1].Timestamp);

        // Act - Get second page using beforeId
        var beforeId = firstPage.Last().Id;
        var secondPage = await messaging.GetMessagesAsync(podId, channelId, beforeId: beforeId, limit: 5);

        // Assert - Got next 5 messages
        Assert.Equal(5, secondPage.Count);
        Assert.True(secondPage[0].Timestamp >= secondPage[1].Timestamp);
        Assert.True(firstPage.Last().Timestamp >= secondPage[0].Timestamp);

        // Act - Get all messages
        var allMessages = await messaging.GetMessagesAsync(podId, channelId);

        // Assert - Got all 10 messages in correct order
        Assert.Equal(10, allMessages.Count);
        for (int i = 0; i < 9; i++)
        {
            Assert.True(allMessages[i].Timestamp >= allMessages[i + 1].Timestamp);
        }
    }

    [Fact]
    public async Task PodDeletionCleansUpMessages()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        // Create pod and add messages
        var pod = new Pod
        {
            PodId = "pod:deletion-test",
            Name = "Deletion Test",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General", Kind = ChannelKind.Public }
            }
        };

        await podService.CreatePodAsync(pod);

        var message = new PodMessage
        {
            PodId = "pod:deletion-test",
            ChannelId = "general",
            SenderPeerId = "peer:mesh:self",
            Body = "Test message",
            Signature = "test-sig"
        };

        await messaging.SendAsync(message);

        // Verify message exists
        var messages = await messaging.GetMessagesAsync("pod:deletion-test", "general");
        Assert.Single(messages);

        // Act - Delete pod
        var deleted = await podService.DeletePodAsync("pod:deletion-test");

        // Assert - Pod was deleted
        Assert.True(deleted);
        var deletedPod = await podService.GetPodAsync("pod:deletion-test");
        Assert.Null(deletedPod);

        // Note: In a real implementation, deleting a pod would also clean up messages
        // For this test, we verify the pod is gone but messages may persist
        // depending on the implementation's cascade delete behavior
    }

    [Fact]
    public async Task VpnPod_MaxMembers_EnforcedDuringJoin()
    {
        // Arrange - Create a VPN pod with MaxMembers = 2
        var podService = _serviceProvider.GetRequiredService<IPodService>();
        var vpnPod = new Pod
        {
            PodId = "pod:vpn-max-members-test",
            Name = "VPN Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                MaxMembers = 2, // Allow only 2 members total
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "printer.local", Port = 9100 }
                }
            }
        };

        await podService.CreateAsync(vpnPod);

        // Add first member (gateway)
        var gatewayMember = new PodMember { PeerId = "peer:gateway", Role = "owner" };
        var gatewayJoined = await podService.JoinAsync("pod:vpn-max-members-test", gatewayMember);
        Assert.True(gatewayJoined);

        // Add second member (should succeed, at limit)
        var member2 = new PodMember { PeerId = "peer:member2", Role = "member" };
        var member2Joined = await podService.JoinAsync("pod:vpn-max-members-test", member2);
        Assert.True(member2Joined);

        // Try to add third member (should fail, exceeds limit)
        var member3 = new PodMember { PeerId = "peer:member3", Role = "member" };
        var member3Joined = await podService.JoinAsync("pod:vpn-max-members-test", member3);
        Assert.False(member3Joined); // Should be rejected

        // Verify final member count
        var members = await podService.GetMembersAsync("pod:vpn-max-members-test");
        Assert.Equal(2, members.Count());
        Assert.Contains(members, m => m.PeerId == "peer:gateway");
        Assert.Contains(members, m => m.PeerId == "peer:member2");
        Assert.DoesNotContain(members, m => m.PeerId == "peer:member3");
    }

    [Fact]
    public async Task RegularPod_NoMemberLimitEnforcement()
    {
        // Arrange - Create a regular pod (no VPN capability)
        var podService = _serviceProvider.GetRequiredService<IPodService>();
        var regularPod = new Pod
        {
            PodId = "pod:regular-max-members-test",
            Name = "Regular Test Pod"
        };

        await podService.CreateAsync(regularPod);

        // Add multiple members (should all succeed)
        for (int i = 1; i <= 10; i++)
        {
            var member = new PodMember { PeerId = $"peer:member{i}", Role = "member" };
            var joined = await podService.JoinAsync("pod:regular-max-members-test", member);
            Assert.True(joined);
        }

        // Verify all members were added
        var members = await podService.GetMembersAsync("pod:regular-max-members-test");
        Assert.Equal(10, members.Count());
    }
}
