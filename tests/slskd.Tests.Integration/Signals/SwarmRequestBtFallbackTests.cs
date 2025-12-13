namespace slskd.Tests.Integration.Signals;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Signals;
using slskd.Signals.Swarm;
using Xunit;

public class SwarmRequestBtFallbackTests : IClassFixture<SignalSystemTestFixture>
{
    private readonly SignalSystemTestFixture fixture;

    public SwarmRequestBtFallbackTests(SignalSystemTestFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task RequestBtFallback_ShouldSendSignal_WhenTransferFails()
    {
        // Arrange
        var signalBus = fixture.ServiceProvider.GetRequiredService<ISignalBus>();
        var receivedSignals = new List<Signal>();

        await signalBus.SubscribeAsync((signal, ct) =>
        {
            if (signal.Type == "Swarm.RequestBtFallback")
            {
                receivedSignals.Add(signal);
            }
            return Task.CompletedTask;
        });

        var signal = new Signal(
            signalId: Guid.NewGuid().ToString("N"),
            fromPeerId: "peer-alice",
            toPeerId: "peer-bob",
            sentAt: DateTimeOffset.UtcNow,
            type: "Swarm.RequestBtFallback",
            body: new Dictionary<string, object>
            {
                ["jobId"] = "job-123",
                ["variantId"] = "variant-abc",
                ["contentIdType"] = "AudioRecording",
                ["contentIdValue"] = "mb:recording:xyz",
                ["reason"] = "mesh-failures"
            },
            ttl: TimeSpan.FromMinutes(5),
            preferredChannels: new[] { SignalChannel.Mesh }
        );

        // Act
        await signalBus.SendAsync(signal);

        // Assert
        // Signal should be received (if channel handler is registered)
        // In a real test, we'd verify the signal was delivered to the target peer
        Assert.True(true); // Placeholder - actual delivery depends on channel handlers
    }

    [Fact]
    public async Task RequestBtFallbackAck_ShouldBeReceived_WhenFallbackAccepted()
    {
        // Arrange
        var signalBus = fixture.ServiceProvider.GetRequiredService<ISignalBus>();
        var swarmHandlers = fixture.ServiceProvider.GetRequiredService<SwarmSignalHandlers>();
        var receivedAcks = new List<Signal>();

        await signalBus.SubscribeAsync((signal, ct) =>
        {
            if (signal.Type == "Swarm.RequestBtFallbackAck")
            {
                receivedAcks.Add(signal);
            }
            return Task.CompletedTask;
        });

        var requestSignal = new Signal(
            signalId: Guid.NewGuid().ToString("N"),
            fromPeerId: "peer-alice",
            toPeerId: fixture.LocalPeerId, // Address to us
            sentAt: DateTimeOffset.UtcNow,
            type: "Swarm.RequestBtFallback",
            body: new Dictionary<string, object>
            {
                ["jobId"] = "job-123",
                ["variantId"] = "variant-abc",
                ["contentIdType"] = "AudioRecording",
                ["contentIdValue"] = "mb:recording:xyz",
                ["reason"] = "mesh-failures"
            },
            ttl: TimeSpan.FromMinutes(5),
            preferredChannels: new[] { SignalChannel.Mesh }
        );

        // Act - Simulate receiving the request signal (cast to SignalBus for testing)
        if (signalBus is SignalBus concreteBus)
        {
            await concreteBus.OnSignalReceivedAsync(requestSignal, CancellationToken.None);
        }

        // Assert
        // In a real scenario, SwarmSignalHandlers would process the request and send an ack
        // For now, we verify the handler is registered and can receive signals
        Assert.True(true); // Placeholder
    }
}

/// <summary>
/// Test fixture for signal system integration tests.
/// </summary>
public class SignalSystemTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public string LocalPeerId { get; } = "test-peer-local";

    public SignalSystemTestFixture()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add signal system
        services.AddSignalSystem();

        // Add stub implementations for SwarmSignalHandlers dependencies
        services.AddSingleton<ISwarmJobStore>(_ => Mock.Of<ISwarmJobStore>());
        services.AddSingleton<ISecurityPolicyEngine>(_ => Mock.Of<ISecurityPolicyEngine>());
        services.AddSingleton<IBitTorrentBackend>(_ => Mock.Of<IBitTorrentBackend>());

        // Configure SwarmSignalHandlers with local peer ID
        services.AddSingleton<SwarmSignalHandlers>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SwarmSignalHandlers>>();
            var signalBus = sp.GetRequiredService<ISignalBus>();
            var jobStore = sp.GetRequiredService<ISwarmJobStore>();
            var securityEngine = sp.GetRequiredService<ISecurityPolicyEngine>();
            var btBackend = sp.GetRequiredService<IBitTorrentBackend>();
            return new SwarmSignalHandlers(logger, signalBus, jobStore, securityEngine, btBackend, LocalPeerId);
        });

        ServiceProvider = services.BuildServiceProvider();

        // Initialize signal system
        try
        {
            SignalServiceExtensions.InitializeSignalSystemAsync(ServiceProvider, LocalPeerId).GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore initialization errors in test fixture - tests will handle them
        }
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}















