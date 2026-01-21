// <copyright file="PodCoreApiIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Conversation;
using slskd.Mesh;
using slskd.PodCore;
using System.Data.Common;
using System.Security.Claims;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PodCoreApiIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly DbConnection _connection;
    private readonly DbContextOptions<PodDbContext> _contextOptions;

    public PodCoreApiIntegrationTests()
    {
        // Set up in-memory database
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<PodDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Set up DI container
        var services = new ServiceCollection();

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

        // Controllers need IServiceScopeFactory
        services.AddScoped<IServiceScopeFactory>(provider => provider.GetRequiredService<IServiceProvider>() as IServiceScopeFactory);

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
    public async Task PodsControllerEndToEndPodLifecycle()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var controller = CreatePodsController(scope.ServiceProvider);

        // Act - Create a pod
        var newPod = new Pod
        {
            PodId = "pod:api-test",
            Name = "API Test Pod",
            Description = "Pod created via API integration test",
            Visibility = Visibility.Public,
            Channels = new List<PodChannel>
            {
                new PodChannel
                {
                    ChannelId = "general",
                    Name = "General Discussion",
                    Kind = ChannelKind.Public
                }
            }
        };

        var createResult = await controller.CreatePod(newPod);

        // Assert - Pod was created
        var createdResult = Assert.IsType<CreatedAtActionResult>(createResult);
        Assert.Equal("GetPod", createdResult.ActionName);

        // Act - Get the pod
        var getResult = await controller.GetPod("pod:api-test");

        // Assert - Pod was retrieved
        var okResult = Assert.IsType<OkObjectResult>(getResult);
        var retrievedPod = Assert.IsType<Pod>(okResult.Value);
        Assert.Equal("API Test Pod", retrievedPod.Name);

        // Act - List all pods
        var listResult = await controller.GetPods();

        // Assert - Pod is in the list
        var listOkResult = Assert.IsType<OkObjectResult>(listResult);
        var pods = Assert.IsType<List<Pod>>(listOkResult.Value);
        Assert.Contains(pods, p => p.PodId == "pod:api-test");

        // Act - Join the pod
        var joinResult = await controller.JoinPod("pod:api-test");

        // Assert - Joined successfully
        Assert.IsType<OkResult>(joinResult);

        // Act - Send a message
        var message = new PodMessage
        {
            Body = "Hello from API test!"
        };

        var sendResult = await controller.SendMessage("pod:api-test", "general", message);

        // Assert - Message was sent
        Assert.IsType<CreatedResult>(sendResult);

        // Act - Get messages
        var messagesResult = await controller.GetMessages("pod:api-test", "general");

        // Assert - Message was retrieved
        var messagesOkResult = Assert.IsType<OkObjectResult>(messagesResult);
        var messages = Assert.IsType<List<PodMessage>>(messagesOkResult.Value);
        Assert.Single(messages);
        Assert.Equal("Hello from API test!", messages[0].Body);

        // Act - Leave the pod
        var leaveResult = await controller.LeavePod("pod:api-test");

        // Assert - Left successfully
        Assert.IsType<NoContentResult>(leaveResult);

        // Note: In a real implementation, leaving might not remove the pod
        // depending on ownership and other members
    }

    [Fact]
    public async Task ConversationPodCoordinatorApiIntegration()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<ConversationPodCoordinator>();
        var controller = CreatePodsController(scope.ServiceProvider);

        // Act - Create DM pod via coordinator
        var (podId, channelId) = await coordinator.EnsureDirectMessagePodAsync("dmuser");

        // Assert - Pod exists via API
        var getResult = await controller.GetPod(podId);
        var okResult = Assert.IsType<OkObjectResult>(getResult);
        var pod = Assert.IsType<Pod>(okResult.Value);
        Assert.Equal("dmuser", pod.Name);
        Assert.Equal(Visibility.Private, pod.Visibility);

        // Act - Send DM via API
        var message = new PodMessage
        {
            Body = "DM via API!"
        };

        var sendResult = await controller.SendMessage(podId, channelId, message);
        Assert.IsType<CreatedResult>(sendResult);

        // Assert - Message is retrievable
        var messagesResult = await controller.GetMessages(podId, channelId);
        var messagesOkResult = Assert.IsType<OkObjectResult>(messagesResult);
        var messages = Assert.IsType<List<PodMessage>>(messagesOkResult.Value);
        Assert.Single(messages);
        Assert.Equal("DM via API!", messages[0].Body);
    }

    [Fact]
    public async Task SoulseekDmBindingIntegration()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var controller = CreatePodsController(scope.ServiceProvider);

        // Create a pod with Soulseek DM binding
        var dmPod = new Pod
        {
            PodId = "pod:soulseek-dm-test",
            Name = "Soulseek DM Test",
            Channels = new List<PodChannel>
            {
                new PodChannel
                {
                    ChannelId = "dm",
                    Name = "DM",
                    Kind = ChannelKind.DirectMessage,
                    BindingInfo = "soulseek-dm:testuser"
                }
            }
        };

        await controller.CreatePod(dmPod);

        // Act - Send message to DM channel (should go to Soulseek)
        var message = new PodMessage
        {
            Body = "Message to Soulseek user"
        };

        var sendResult = await controller.SendMessage("pod:soulseek-dm-test", "dm", message);

        // Assert - Message was accepted (would go to Soulseek in real implementation)
        Assert.IsType<CreatedResult>(sendResult);

        // Act - Get messages from DM channel (would come from Soulseek conversation)
        var messagesResult = await controller.GetMessages("pod:soulseek-dm-test", "dm");

        // Assert - API handled the request (empty in test since no real Soulseek messages)
        var messagesOkResult = Assert.IsType<OkObjectResult>(messagesResult);
        var messages = Assert.IsType<List<PodMessage>>(messagesOkResult.Value);
        // In a real scenario with Soulseek messages, this would return conversation messages
        // For this test, we just verify the API integration works
    }

    [Fact]
    public async Task ErrorHandlingAndValidation()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var controller = CreatePodsController(scope.ServiceProvider);

        // Act - Try to get non-existent pod
        var getResult = await controller.GetPod("pod:nonexistent");

        // Assert - Returns NotFound
        Assert.IsType<NotFoundResult>(getResult);

        // Act - Try to create pod with null
        var createResult = await controller.CreatePod(null!);

        // Assert - Returns BadRequest
        Assert.IsType<BadRequestResult>(createResult);

        // Act - Try to send null message
        var sendResult = await controller.SendMessage("pod:test", "general", null!);

        // Assert - Returns BadRequest
        Assert.IsType<BadRequestResult>(sendResult);

        // Act - Try to join non-existent pod
        var joinResult = await controller.JoinPod("pod:nonexistent");

        // Assert - Returns NotFound
        Assert.IsType<NotFoundResult>(joinResult);

        // Act - Try to leave non-existent pod
        var leaveResult = await controller.LeavePod("pod:nonexistent");

        // Assert - Returns NotFound
        Assert.IsType<NotFoundResult>(leaveResult);
    }

    [Fact]
    public async Task MultiUserPodScenario()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var controller1 = CreatePodsController(scope.ServiceProvider, "user1");
        var controller2 = CreatePodsController(scope.ServiceProvider, "user2");
        var controller3 = CreatePodsController(scope.ServiceProvider, "user3");

        // Create a public pod
        var publicPod = new Pod
        {
            PodId = "pod:multi-user-test",
            Name = "Multi-User Test Pod",
            Description = "Testing multiple users",
            Visibility = Visibility.Public,
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General", Kind = ChannelKind.Public },
                new PodChannel { ChannelId = "offtopic", Name = "Off Topic", Kind = ChannelKind.Public }
            }
        };

        await controller1.CreatePod(publicPod);

        // Act - Multiple users join
        await controller2.JoinPod("pod:multi-user-test");
        await controller3.JoinPod("pod:multi-user-test");

        // Act - Users send messages to different channels
        var message1 = new PodMessage { Body = "Hello from user1 in general!" };
        var message2 = new PodMessage { Body = "Hi from user2 in general!" };
        var message3 = new PodMessage { Body = "Off topic message from user3" };

        await controller1.SendMessage("pod:multi-user-test", "general", message1);
        await controller2.SendMessage("pod:multi-user-test", "general", message2);
        await controller3.SendMessage("pod:multi-user-test", "offtopic", message3);

        // Assert - Messages are properly isolated by channel
        var generalMessages = await controller1.GetMessages("pod:multi-user-test", "general");
        var generalOkResult = Assert.IsType<OkObjectResult>(generalMessages);
        var generalMsgs = Assert.IsType<List<PodMessage>>(generalOkResult.Value);
        Assert.Equal(2, generalMsgs.Count);

        var offtopicMessages = await controller1.GetMessages("pod:multi-user-test", "offtopic");
        var offtopicOkResult = Assert.IsType<OkObjectResult>(offtopicMessages);
        var offtopicMsgs = Assert.IsType<List<PodMessage>>(offtopicOkResult.Value);
        Assert.Single(offtopicMsgs);
        Assert.Equal("Off topic message from user3", offtopicMsgs[0].Body);

        // Act - One user leaves
        await controller2.LeavePod("pod:multi-user-test");

        // Assert - Pod still exists with remaining members
        var podResult = await controller1.GetPod("pod:multi-user-test");
        var podOkResult = Assert.IsType<OkObjectResult>(podResult);
        var pod = Assert.IsType<Pod>(podOkResult.Value);
        Assert.True(pod.Members!.Count >= 2); // Owner + at least one other member
    }

    private PodsController CreatePodsController(IServiceProvider services, string username = "testuser")
    {
        var logger = services.GetRequiredService<ILogger<PodsController>>();
        var podService = services.GetRequiredService<IPodService>();
        var messaging = services.GetRequiredService<IPodMessaging>();
        var conversationService = services.GetRequiredService<IConversationService>();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        var controller = new PodsController(logger, podService, messaging, conversationService);

        // Set up HTTP context with user
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username)
                }))
            }
        };

        return controller;
    }
}


