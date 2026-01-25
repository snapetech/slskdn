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
using Moq;
using slskd.API.Native;
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
    private const string PeerTest = "peer-mesh-testuser";
    private const string PeerUser1 = "peer-mesh-user1";
    private const string PeerUser2 = "peer-mesh-user2";
    private const string PeerUser3 = "peer-mesh-user3";
    // Valid PodIds: pod:[a-f0-9]{32}
    private static readonly string PodIdLifecycle = "pod:00000000000000000000000000000001";
    private static readonly string PodIdSoulseek = "pod:00000000000000000000000000000002";
    private static readonly string PodIdMulti = "pod:00000000000000000000000000000003";

    public PodCoreApiIntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _contextOptions = new DbContextOptionsBuilder<PodDbContext>()
            .UseSqlite(_connection)
            .Options;

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddScoped<PodDbContext>(_ => new PodDbContext(_contextOptions));
        services.AddSingleton<IDbContextFactory<PodDbContext>>(new TestPodDbContextFactory(_contextOptions));

        services.AddSingleton<IOptionsMonitor<MeshOptions>>(
            Mock.Of<IOptionsMonitor<MeshOptions>>(x => x.CurrentValue == new MeshOptions { SelfPeerId = "peer-mesh-self" }));

        var pubMock = new Mock<IPodPublisher>();
        pubMock.Setup(x => x.PublishPodAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        pubMock.Setup(x => x.PublishAsync(It.IsAny<Pod>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        services.AddSingleton(pubMock.Object);

        var signerMock = new Mock<IPodMembershipSigner>();
        signerMock.Setup(x => x.SignMembershipAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignedMembershipRecord { PodId = "", PeerId = "", TimestampUnixMs = 0, Action = "join", Signature = "" });
        services.AddSingleton(signerMock.Object);

        services.AddScoped<IPodService>(sp => new SqlitePodService(
            sp.GetRequiredService<IDbContextFactory<PodDbContext>>(),
            sp.GetRequiredService<IPodPublisher>(),
            sp.GetRequiredService<IPodMembershipSigner>(),
            sp.GetRequiredService<ILogger<SqlitePodService>>(),
            sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddScoped<IPodMessaging, SqlitePodMessaging>();

        var chatMock = new Mock<ISoulseekChatBridge>();
        chatMock.Setup(x => x.BindRoomAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        chatMock.Setup(x => x.UnbindRoomAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        services.AddSingleton(chatMock.Object);

        services.AddScoped<ConversationPodCoordinator>();
        services.AddSingleton<IServiceScopeFactory>(sp => new TestServiceScopeFactory(sp));

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<PodDbContext>().Database.EnsureCreated();
    }

    private sealed class TestPodDbContextFactory : IDbContextFactory<PodDbContext>
    {
        private readonly DbContextOptions<PodDbContext> _o;
        public TestPodDbContextFactory(DbContextOptions<PodDbContext> o) => _o = o;
        public PodDbContext CreateDbContext() => new PodDbContext(_o);
        public ValueTask<PodDbContext> CreateDbContextAsync(CancellationToken ct = default) => ValueTask.FromResult(CreateDbContext());
    }

    private sealed class TestServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _root;
        public TestServiceScopeFactory(IServiceProvider root) => _root = root;
        public IServiceScope CreateScope() => new TestScope(_root);
    }

    private sealed class TestScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }
        public TestScope(IServiceProvider sp) => ServiceProvider = sp;
        public void Dispose() { }
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task PodsControllerEndToEndPodLifecycle()
    {
        using var scope = _serviceProvider.CreateScope();
        var controller = CreatePodsController(scope.ServiceProvider, PeerTest);

        var newPod = new Pod
        {
            PodId = PodIdLifecycle,
            Name = "API Test Pod",
            Description = "Pod created via API integration test",
            Visibility = PodVisibility.Listed,
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General Discussion", Kind = PodChannelKind.General }
            }
        };
        var createResult = await controller.CreatePod(new CreatePodRequest(newPod, PeerTest));

        var createdResult = Assert.IsType<CreatedAtActionResult>(createResult);
        Assert.Equal("GetPod", createdResult.ActionName);

        var getResult = await controller.GetPod(PodIdLifecycle);
        var okResult = Assert.IsType<OkObjectResult>(getResult);
        var retrievedPod = Assert.IsType<Pod>(okResult.Value);
        Assert.Equal("API Test Pod", retrievedPod.Name);

        var listResult = await controller.ListPods();
        var listOkResult = Assert.IsType<OkObjectResult>(listResult);
        var pods = Assert.IsType<List<Pod>>(listOkResult.Value);
        Assert.Contains(pods, p => p.PodId == PodIdLifecycle);

        var joinResult = await controller.JoinPod(PodIdLifecycle, new JoinPodRequest(PeerTest));
        Assert.IsType<OkObjectResult>(joinResult); // Creator already member; idempotent join returns Ok

        var sendResult = await controller.SendMessage(PodIdLifecycle, "general", new SendMessageRequest("Hello from API test!", PeerTest));
        Assert.IsType<OkObjectResult>(sendResult);

        var messagesResult = await controller.GetMessages(PodIdLifecycle, "general");
        var messagesOkResult = Assert.IsType<OkObjectResult>(messagesResult);
        var messages = Assert.IsType<List<PodMessage>>(messagesOkResult.Value);
        Assert.Single(messages);
        Assert.Equal("Hello from API test!", messages[0].Body);

        var leaveResult = await controller.LeavePod(PodIdLifecycle, new LeavePodRequest(PeerTest));
        Assert.IsType<OkObjectResult>(leaveResult);
    }

    [Fact]
    public async Task ConversationPodCoordinatorApiIntegration()
    {
        using var scope = _serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<ConversationPodCoordinator>();
        var controller = CreatePodsController(scope.ServiceProvider, PeerTest);

        var (podId, channelId) = await coordinator.EnsureDirectMessagePodAsync("dmuser");

        var getResult = await controller.GetPod(podId);
        var okResult = Assert.IsType<OkObjectResult>(getResult);
        var pod = Assert.IsType<Pod>(okResult.Value);
        Assert.Equal("dmuser", pod.Name);
        Assert.Equal(PodVisibility.Private, pod.Visibility);

        var selfPeerId = "peer-mesh-self"; // Must match PeerId pattern ^[a-zA-Z0-9\-_.@]{1,255}$ (no colons)
        var sendResult = await controller.SendMessage(podId, channelId, new SendMessageRequest("DM via API!", selfPeerId));
        Assert.IsType<OkObjectResult>(sendResult);

        var messagesResult = await controller.GetMessages(podId, channelId);
        var messagesOkResult = Assert.IsType<OkObjectResult>(messagesResult);
        var messages = Assert.IsType<List<PodMessage>>(messagesOkResult.Value);
        Assert.Single(messages);
        Assert.Equal("DM via API!", messages[0].Body);
    }

    [Fact]
    public async Task SoulseekDmBindingIntegration()
    {
        using var scope = _serviceProvider.CreateScope();
        var controller = CreatePodsController(scope.ServiceProvider, PeerTest);

        var dmPod = new Pod
        {
            PodId = PodIdSoulseek,
            Name = "Soulseek DM Test",
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "dm", Name = "DM", Kind = PodChannelKind.DirectMessage, BindingInfo = "soulseek-dm:testuser" }
            }
        };
        await controller.CreatePod(new CreatePodRequest(dmPod, PeerTest));

        var sendResult = await controller.SendMessage(PodIdSoulseek, "dm", new SendMessageRequest("Message to Soulseek user", PeerTest));
        Assert.IsType<OkObjectResult>(sendResult);

        var messagesResult = await controller.GetMessages(PodIdSoulseek, "dm");
        var messagesOkResult = Assert.IsType<OkObjectResult>(messagesResult);
        Assert.IsType<List<PodMessage>>(messagesOkResult.Value);
    }

    [Fact]
    public async Task ErrorHandlingAndValidation()
    {
        using var scope = _serviceProvider.CreateScope();
        var controller = CreatePodsController(scope.ServiceProvider, PeerTest);

        var getResult = await controller.GetPod("pod:00000000000000000000000000000099");
        Assert.IsType<NotFoundObjectResult>(getResult); // valid format but pod does not exist

        var createResult = await controller.CreatePod(null!);
        Assert.IsType<BadRequestObjectResult>(createResult);

        var sendResult = await controller.SendMessage(PodIdLifecycle, "general", null!);
        Assert.IsType<BadRequestObjectResult>(sendResult);

        var joinResult = await controller.JoinPod("pod:00000000000000000000000000000099", new JoinPodRequest(PeerTest));
        Assert.IsType<BadRequestObjectResult>(joinResult); // pod not in DB -> !joined

        var leaveResult = await controller.LeavePod("pod:00000000000000000000000000000099", new LeavePodRequest(PeerTest));
        Assert.IsType<NotFoundObjectResult>(leaveResult); // member not found -> !left
    }

    [Fact]
    public async Task MultiUserPodScenario()
    {
        using var scope = _serviceProvider.CreateScope();
        var controller1 = CreatePodsController(scope.ServiceProvider, "user1");
        var controller2 = CreatePodsController(scope.ServiceProvider, "user2");
        var controller3 = CreatePodsController(scope.ServiceProvider, "user3");

        var publicPod = new Pod
        {
            PodId = PodIdMulti,
            Name = "Multi-User Test Pod",
            Description = "Testing multiple users",
            Visibility = PodVisibility.Listed,
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General", Kind = PodChannelKind.General },
                new PodChannel { ChannelId = "offtopic", Name = "Off Topic", Kind = PodChannelKind.General }
            }
        };
        await controller1.CreatePod(new CreatePodRequest(publicPod, PeerUser1));

        await controller2.JoinPod(PodIdMulti, new JoinPodRequest(PeerUser2));
        await controller3.JoinPod(PodIdMulti, new JoinPodRequest(PeerUser3));

        await controller1.SendMessage(PodIdMulti, "general", new SendMessageRequest("Hello from user1 in general!", PeerUser1));
        await controller2.SendMessage(PodIdMulti, "general", new SendMessageRequest("Hi from user2 in general!", PeerUser2));
        await controller3.SendMessage(PodIdMulti, "offtopic", new SendMessageRequest("Off topic message from user3", PeerUser3));

        var generalMessages = await controller1.GetMessages(PodIdMulti, "general");
        var generalOkResult = Assert.IsType<OkObjectResult>(generalMessages);
        var generalMsgs = Assert.IsType<List<PodMessage>>(generalOkResult.Value);
        Assert.Equal(2, generalMsgs.Count);

        var offtopicMessages = await controller1.GetMessages(PodIdMulti, "offtopic");
        var offtopicOkResult = Assert.IsType<OkObjectResult>(offtopicMessages);
        var offtopicMsgs = Assert.IsType<List<PodMessage>>(offtopicOkResult.Value);
        Assert.Single(offtopicMsgs);
        Assert.Equal("Off topic message from user3", offtopicMsgs[0].Body);

        await controller2.LeavePod(PodIdMulti, new LeavePodRequest(PeerUser2));

        var membersResult = await controller1.GetMembers(PodIdMulti);
        var membersOk = Assert.IsType<OkObjectResult>(membersResult);
        var members = Assert.IsType<List<PodMember>>(membersOk.Value);
        Assert.True(members.Count >= 2);
    }

    private PodsController CreatePodsController(IServiceProvider services, string username = "testuser")
    {
        var controller = new PodsController(
            services.GetRequiredService<IPodService>(),
            services.GetRequiredService<IPodMessaging>(),
            services.GetRequiredService<ISoulseekChatBridge>(),
            services.GetRequiredService<ILogger<PodsController>>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) })) }
        };
        return controller;
    }
}


