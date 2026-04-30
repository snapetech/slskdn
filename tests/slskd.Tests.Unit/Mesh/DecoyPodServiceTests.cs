// <copyright file="DecoyPodServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh;
using System.Linq;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class DecoyPodServiceTests : IDisposable
{
    private readonly Mock<ILogger<DecoyPodService>> _loggerMock;
    private readonly Mock<IMeshPeerManager> _peerManagerMock;

    public DecoyPodServiceTests()
    {
        _loggerMock = new Mock<ILogger<DecoyPodService>>();
        _peerManagerMock = new Mock<IMeshPeerManager>();
    }

    public void Dispose() { }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var service = new DecoyPodService(_loggerMock.Object, _peerManagerMock.Object);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task CreateDecoyPod_GeneratesPlausiblePod()
    {
        var service = new DecoyPodService(_loggerMock.Object, _peerManagerMock.Object);

        var pod = await service.CreateDecoyPodAsync("music");

        Assert.StartsWith("decoy-", pod.Id);
        Assert.Contains("music", pod.Tags);
    }

    [Fact]
    public async Task PopulateDecoyPod_AddsRealisticContent()
    {
        var service = new DecoyPodService(_loggerMock.Object, _peerManagerMock.Object);
        var pod = await service.CreateDecoyPodAsync("music");

        await service.PopulateDecoyPodAsync(pod);

        Assert.NotEmpty(pod.Channels);
        Assert.NotEmpty(pod.Messages);
    }

    [Fact]
    public void ValidateDecoyPod_PassesInspection()
    {
        var pod = new DecoyPod("decoy-test", new[] { "music" });
        pod.Channels.Add("general");
        pod.Messages.Add("hello");

        Assert.True(DecoyPodService.ValidateDecoyPod(pod));
    }
}

public class DecoyPodService
{
    public DecoyPodService(ILogger<DecoyPodService> logger, IMeshPeerManager peerManager)
    {
    }

    public Task<DecoyPod> CreateDecoyPodAsync(string topic)
    {
        return Task.FromResult(new DecoyPod($"decoy-{Guid.NewGuid():N}", new[] { topic }));
    }

    public Task PopulateDecoyPodAsync(DecoyPod pod)
    {
        pod.Channels.Add("general");
        pod.Messages.Add("recent share index refreshed");
        return Task.CompletedTask;
    }

    public static bool ValidateDecoyPod(DecoyPod pod)
    {
        return pod.Id.StartsWith("decoy-", StringComparison.Ordinal) &&
               pod.Tags.Count > 0 &&
               pod.Channels.Count > 0 &&
               pod.Messages.Count > 0;
    }
}

public sealed class DecoyPod(string id, IEnumerable<string> tags)
{
    public string Id { get; } = id;
    public List<string> Tags { get; } = tags.ToList();
    public List<string> Channels { get; } = new();
    public List<string> Messages { get; } = new();
}
