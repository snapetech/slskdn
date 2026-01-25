// <copyright file="PodCoreIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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

    private static readonly string PodIdIntegration = "pod:0000000000000000000000000000000a";
    private static readonly string PodIdMembership = "pod:0000000000000000000000000000000b";
    private static readonly string PodIdPagination = "pod:0000000000000000000000000000000c";
    private static readonly string PodIdVpn = "pod:0000000000000000000000000000000d";
    private static readonly string PodIdRegular = "pod:0000000000000000000000000000000e";

    public PodCoreIntegrationTests()
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
        services.AddScoped<IPodMessaging>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<PodDbContext>>();
            return new SqlitePodMessaging(f.CreateDbContext(), sp.GetRequiredService<ILogger<SqlitePodMessaging>>());
        });
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

    public void Dispose() { _serviceProvider.Dispose(); _connection.Dispose(); }

    private static PodMessage Msg(string podId, string ch, string sender, string body, long ts) =>
        new PodMessage { PodId = podId, ChannelId = ch, SenderPeerId = sender, Body = body, TimestampUnixMs = ts, Signature = "s" };

    [Fact]
    public async Task EndToEndPodCreationAndMessagingFlow()
    {
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();
        var coordinator = scope.ServiceProvider.GetRequiredService<ConversationPodCoordinator>();

        var (podId, channelId) = await coordinator.EnsureDirectMessagePodAsync("testuser");

        var pod = await podService.GetPodAsync(podId);
        Assert.NotNull(pod);
        Assert.Equal("testuser", pod!.Name);
        Assert.Equal(PodVisibility.Private, pod.Visibility);
        Assert.Contains("dm", pod.Tags!);

        var dmChannel = pod.Channels!.FirstOrDefault(c => c.ChannelId == channelId);
        Assert.NotNull(dmChannel);
        Assert.Equal("DM", dmChannel!.Name);
        Assert.Equal("soulseek-dm:testuser", dmChannel.BindingInfo);

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await messaging.SendAsync(Msg(podId, channelId, "peer-mesh-self", "Hello from integration test!", ts)));
        Assert.True(await messaging.SendAsync(Msg(podId, channelId, "peer-mesh-self", "Hello back!", ts + 1)));

        var messages = await messaging.GetMessagesAsync(podId, channelId);
        Assert.Equal(2, messages.Count);
        Assert.Equal("Hello from integration test!", messages[0].Body);
        Assert.Equal("Hello back!", messages[1].Body);
        Assert.True(messages[0].TimestampUnixMs <= messages[1].TimestampUnixMs);
    }

    [Fact]
    public async Task PodServiceAndMessagingIntegration()
    {
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        var pod = new Pod
        {
            PodId = PodIdIntegration,
            Name = "Integration Test Pod",
            Description = "Test pod for integration testing",
            Visibility = PodVisibility.Listed,
            Tags = new List<string> { "test", "integration" },
            Channels = new List<PodChannel>
            {
                new PodChannel { ChannelId = "general", Name = "General Discussion", Kind = PodChannelKind.General, Description = "General chat" },
                new PodChannel { ChannelId = "random", Name = "Random", Kind = PodChannelKind.General }
            }
        };
        var created = await podService.CreateAsync(pod);
        await podService.JoinAsync(created.PodId, new PodMember { PeerId = "peer-mesh-self", Role = "owner" });

        Assert.Equal(PodIdIntegration, created.PodId);
        var retrieved = await podService.GetPodAsync(created.PodId);
        Assert.NotNull(retrieved);
        Assert.Equal("Integration Test Pod", retrieved!.Name);
        Assert.Equal(2, retrieved.Channels!.Count);

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await messaging.SendAsync(Msg(created.PodId, "general", "peer-mesh-self", "Hello in general!", ts)));
        Assert.True(await messaging.SendAsync(Msg(created.PodId, "random", "peer-mesh-self", "Random message", ts + 1)));

        var generalMessages = await messaging.GetMessagesAsync(created.PodId, "general");
        var randomMessages = await messaging.GetMessagesAsync(created.PodId, "random");
        Assert.Single(generalMessages);
        Assert.Single(randomMessages);
        Assert.Equal("Hello in general!", generalMessages[0].Body);
        Assert.Equal("Random message", randomMessages[0].Body);

        var pods = await podService.ListAsync();
        Assert.Contains(pods, p => p.PodId == created.PodId);
    }

    [Fact]
    public async Task ConversationPodCoordinatorWithRealServices()
    {
        using var scope = _serviceProvider.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<ConversationPodCoordinator>();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        var (podId1, channelId1) = await coordinator.EnsureDirectMessagePodAsync("alice");
        var (podId2, channelId2) = await coordinator.EnsureDirectMessagePodAsync("bob");

        Assert.NotEqual(podId1, podId2);
        Assert.Equal("dm", channelId1);
        Assert.Equal("dm", channelId2);

        var pod1 = await podService.GetPodAsync(podId1);
        var pod2 = await podService.GetPodAsync(podId2);
        Assert.NotNull(pod1);
        Assert.NotNull(pod2);
        Assert.Equal("alice", pod1!.Name);
        Assert.Equal("bob", pod2!.Name);

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await messaging.SendAsync(Msg(podId1, channelId1, "peer-mesh-self", "Hello Alice!", ts)));
        Assert.True(await messaging.SendAsync(Msg(podId2, channelId2, "peer-mesh-self", "Hello Bob!", ts + 1)));

        var aliceMessages = await messaging.GetMessagesAsync(podId1, channelId1);
        var bobMessages = await messaging.GetMessagesAsync(podId2, channelId2);
        Assert.Single(aliceMessages);
        Assert.Single(bobMessages);
        Assert.Equal("Hello Alice!", aliceMessages[0].Body);
        Assert.Equal("Hello Bob!", bobMessages[0].Body);

        var (podId1Again, channelId1Again) = await coordinator.EnsureDirectMessagePodAsync("alice");
        Assert.Equal(podId1, podId1Again);
        Assert.Equal(channelId1, channelId1Again);
    }

    [Fact]
    public async Task PodMembershipAndMessagingIntegration()
    {
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        var pod = new Pod
        {
            PodId = PodIdMembership,
            Name = "Membership Test",
            Visibility = PodVisibility.Listed,
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General", Kind = PodChannelKind.General } }
        };
        await podService.CreateAsync(pod);
        await podService.JoinAsync(PodIdMembership, new PodMember { PeerId = "peer-mesh-alice", Role = "owner" });
        await podService.JoinAsync(PodIdMembership, new PodMember { PeerId = "peer-mesh-bob", Role = "member" });

        var joined = await podService.JoinAsync(PodIdMembership, new PodMember { PeerId = "peer-mesh-charlie", Role = "member" });
        Assert.True(joined);
        var members = await podService.GetMembersAsync(PodIdMembership);
        Assert.Equal(3, members.Count);
        Assert.Contains(members, m => m.PeerId == "peer-mesh-charlie");

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await messaging.SendAsync(Msg(PodIdMembership, "general", "peer-mesh-alice", "Hi from Alice", ts)));
        Assert.True(await messaging.SendAsync(Msg(PodIdMembership, "general", "peer-mesh-bob", "Hi from Bob", ts + 1)));
        Assert.True(await messaging.SendAsync(Msg(PodIdMembership, "general", "peer-mesh-charlie", "Hi from Charlie", ts + 2)));

        var stored = await messaging.GetMessagesAsync(PodIdMembership, "general");
        Assert.Equal(3, stored.Count);
        Assert.Equal(3, stored.Select(m => m.SenderPeerId).Distinct().Count());

        var left = await podService.LeaveAsync(PodIdMembership, "peer-mesh-bob");
        Assert.True(left);
        members = await podService.GetMembersAsync(PodIdMembership);
        Assert.Equal(2, members.Count);
        Assert.DoesNotContain(members, m => m.PeerId == "peer-mesh-bob");
    }

    [Fact]
    public async Task MessageOrdering()
    {
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();

        var pod = new Pod
        {
            PodId = PodIdPagination,
            Name = "Pagination Test",
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General", Kind = PodChannelKind.General } }
        };
        await podService.CreateAsync(pod);
        await podService.JoinAsync(PodIdPagination, new PodMember { PeerId = "peer-mesh-sender", Role = "owner" });

        var baseTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (int i = 0; i < 10; i++)
            Assert.True(await messaging.SendAsync(Msg(PodIdPagination, "general", "peer-mesh-sender", $"Message {i}", baseTs + i)));

        var all = await messaging.GetMessagesAsync(PodIdPagination, "general");
        Assert.Equal(10, all.Count);
        for (int i = 0; i < 9; i++)
            Assert.True(all[i].TimestampUnixMs <= all[i + 1].TimestampUnixMs);
    }

    [Fact]
    public async Task PodDeletionCleansUpMessages()
    {
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();
        var messaging = scope.ServiceProvider.GetRequiredService<IPodMessaging>();
        const string podId = "pod:0000000000000000000000000000000f";

        var pod = new Pod
        {
            PodId = podId,
            Name = "Deletion Test Pod",
            Visibility = PodVisibility.Private,
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General", Kind = PodChannelKind.General } }
        };
        await podService.CreateAsync(pod);
        await podService.JoinAsync(podId, new PodMember { PeerId = "peer-mesh-self", Role = "owner" });

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await messaging.SendAsync(Msg(podId, "general", "peer-mesh-self", "Message 1", ts)));
        Assert.True(await messaging.SendAsync(Msg(podId, "general", "peer-mesh-self", "Message 2", ts + 1)));

        var before = await messaging.GetMessagesAsync(podId, "general");
        Assert.Equal(2, before.Count);

        var deleted = await podService.DeletePodAsync(podId);
        Assert.True(deleted);

        var after = await podService.GetPodAsync(podId);
        Assert.Null(after);
    }

    [Fact]
    public async Task VpnPod_MaxMembers_EnforcedDuringJoin()
    {
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();

        var vpnPod = new Pod
        {
            PodId = PodIdVpn,
            Name = "VPN Test Pod",
            Channels = new List<PodChannel>(),
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer-gateway",
                MaxMembers = 2,
                AllowedDestinations = new List<AllowedDestination> { new AllowedDestination { HostPattern = "printer.local", Port = 9100 } },
                RegisteredServices = new List<RegisteredService>()
            }
        };
        await podService.CreateAsync(vpnPod);

        Assert.True(await podService.JoinAsync(PodIdVpn, new PodMember { PeerId = "peer-gateway", Role = "owner" }));
        Assert.True(await podService.JoinAsync(PodIdVpn, new PodMember { PeerId = "peer-member2", Role = "member" }));
        Assert.False(await podService.JoinAsync(PodIdVpn, new PodMember { PeerId = "peer-member3", Role = "member" }));

        var members = await podService.GetMembersAsync(PodIdVpn);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.PeerId == "peer-gateway");
        Assert.Contains(members, m => m.PeerId == "peer-member2");
        Assert.DoesNotContain(members, m => m.PeerId == "peer-member3");
    }

    [Fact]
    public async Task RegularPod_NoMemberLimitEnforcement()
    {
        using var scope = _serviceProvider.CreateScope();
        var podService = scope.ServiceProvider.GetRequiredService<IPodService>();

        var regularPod = new Pod { PodId = PodIdRegular, Name = "Regular Test Pod", Channels = new List<PodChannel>(), FocusContentId = "" };
        await podService.CreateAsync(regularPod);

        for (int i = 1; i <= 10; i++)
            Assert.True(await podService.JoinAsync(PodIdRegular, new PodMember { PeerId = $"peer-mesh-member{i}", Role = "member" }));

        var members = await podService.GetMembersAsync(PodIdRegular);
        Assert.Equal(10, members.Count);
    }
}
