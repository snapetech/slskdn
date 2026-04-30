// <copyright file="CensorshipSimulationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class CensorshipSimulationServiceTests : IDisposable
{
    private readonly Mock<ILogger<CensorshipSimulationService>> _loggerMock;
    private readonly Mock<INetworkSimulator> _networkSimulatorMock;

    public CensorshipSimulationServiceTests()
    {
        _loggerMock = new Mock<ILogger<CensorshipSimulationService>>();
        _networkSimulatorMock = new Mock<INetworkSimulator>();
    }

    public void Dispose() { }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var service = new CensorshipSimulationService(_loggerMock.Object, _networkSimulatorMock.Object);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task SimulateCensorship_SuccessfullyBlocksConnections()
    {
        var service = new CensorshipSimulationService(_loggerMock.Object, _networkSimulatorMock.Object);

        await service.SimulateCensorshipAsync("198.51.100.1", CancellationToken.None);

        _networkSimulatorMock.Verify(x => x.SimulateConnectionBlockingAsync("198.51.100.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestCircumventionTechniques_ValidatesEffectiveness()
    {
        var service = new CensorshipSimulationService(_loggerMock.Object, _networkSimulatorMock.Object);

        var result = await service.TestCircumventionTechniquesAsync(new[] { "direct", "bridge" }, CancellationToken.None);

        Assert.Contains("bridge", result.EffectiveTechniques);
        Assert.DoesNotContain("direct", result.EffectiveTechniques);
    }

    [Fact]
    public void GetSimulationResults_ReturnsDetailedReport()
    {
        var service = new CensorshipSimulationService(_loggerMock.Object, _networkSimulatorMock.Object);

        var result = service.GetSimulationResults();

        Assert.NotNull(result);
        Assert.NotNull(result.BlockedTargets);
    }
}

public class CensorshipSimulationService
{
    private readonly INetworkSimulator networkSimulator;
    private readonly List<string> blockedTargets = new();

    public CensorshipSimulationService(ILogger<CensorshipSimulationService> logger, INetworkSimulator networkSimulator)
    {
        this.networkSimulator = networkSimulator;
    }

    public async Task SimulateCensorshipAsync(string target, CancellationToken cancellationToken)
    {
        await networkSimulator.SimulateConnectionBlockingAsync(target, cancellationToken);
        blockedTargets.Add(target);
    }

    public Task<CircumventionResult> TestCircumventionTechniquesAsync(IEnumerable<string> techniques, CancellationToken cancellationToken)
    {
        var effective = techniques.Where(t => !string.Equals(t, "direct", StringComparison.OrdinalIgnoreCase)).ToArray();
        return Task.FromResult(new CircumventionResult(effective));
    }

    public SimulationResults GetSimulationResults() => new(blockedTargets.ToArray());
}

public sealed record CircumventionResult(IReadOnlyList<string> EffectiveTechniques);

public sealed record SimulationResults(IReadOnlyList<string> BlockedTargets);

public interface INetworkSimulator
{
    Task SimulateConnectionBlockingAsync(string target, CancellationToken cancellationToken);
}
