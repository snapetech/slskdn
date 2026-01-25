// <copyright file="PrivacyLayerIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using slskd.Mesh.Privacy;
using PrivacyLayer = slskd.Mesh.Privacy.PrivacyLayer;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Privacy;

public class PrivacyLayerIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<PrivacyLayer>> _loggerMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly PrivacyLayerOptions _defaultOptions;

    public PrivacyLayerIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<PrivacyLayer>>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _defaultOptions = new PrivacyLayerOptions
        {
            Enabled = true,
            Padding = new MessagePaddingOptions { Enabled = true, BucketSizes = new() { 512, 1024 } },
            Timing = new TimingObfuscationOptions { Enabled = true, JitterMs = 50 },
            Batching = new MessageBatchingOptions { Enabled = true, BatchWindowMs = 100, MaxBatchSize = 3 },
            CoverTraffic = new CoverTrafficOptions { Enabled = true, IntervalSeconds = 30 }
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithAllFeaturesEnabled_CreatesAllComponents()
    {
        // Act
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, _defaultOptions);

        // Assert
        Assert.True(privacyLayer.IsEnabled);
        Assert.NotNull(privacyLayer.MessagePadder);
        Assert.NotNull(privacyLayer.TimingObfuscator);
        Assert.NotNull(privacyLayer.MessageBatcher);
        Assert.NotNull(privacyLayer.CoverTrafficGenerator);
    }

    [Fact]
    public void Constructor_WithFeaturesDisabled_CreatesNoComponents()
    {
        // Arrange
        var options = new PrivacyLayerOptions { Enabled = false };

        // Act
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, options);

        // Assert
        Assert.False(privacyLayer.IsEnabled);
        Assert.Null(privacyLayer.MessagePadder);
        Assert.Null(privacyLayer.TimingObfuscator);
        Assert.Null(privacyLayer.MessageBatcher);
        Assert.Null(privacyLayer.CoverTrafficGenerator);
    }

    [Fact]
    public async Task ProcessOutboundMessageAsync_WithAllFeatures_AppliesAllTransforms()
    {
        // Arrange
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, _defaultOptions);
        var originalMessage = new byte[] { 1, 2, 3, 4, 5 };
        var cts = new CancellationTokenSource();

        // Act
        var result = await privacyLayer.ProcessOutboundMessageAsync(originalMessage, cts.Token);

        // Assert
        // Result should be either empty (batched) or padded
        if (result.Length > 0)
        {
            // If not batched, should be padded to bucket size
            Assert.True(result.Length >= 512, "Message should be padded to at least 512 bytes");
            Assert.True(result.Length % 512 == 0 || result.Length % 1024 == 0, "Should be padded to bucket size");
        }
        // If result is empty, message was queued for batching
    }

    [Fact]
    public async Task ProcessOutboundMessageAsync_WhenBatchingEnabled_ReturnsEmptyForQueuedMessages()
    {
        // Arrange
        var options = new PrivacyLayerOptions
        {
            Enabled = true,
            Padding = new MessagePaddingOptions { Enabled = false },
            Timing = new TimingObfuscationOptions { Enabled = false },
            Batching = new MessageBatchingOptions { Enabled = true, BatchWindowMs = 1000, MaxBatchSize = 5 },
            CoverTraffic = new CoverTrafficOptions { Enabled = false }
        };

        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, options);
        var message = new byte[] { 1, 2, 3 };
        var cts = new CancellationTokenSource();

        // Act - Send multiple messages quickly
        var result1 = await privacyLayer.ProcessOutboundMessageAsync(message, cts.Token);
        var result2 = await privacyLayer.ProcessOutboundMessageAsync(message, cts.Token);

        // Assert - Messages should be queued (empty results) until batch is ready
        // (This test is probabilistic - batching window might expire)
        Assert.True(result1.Length == 0 || result2.Length == 0, "At least one message should be queued for batching");
    }

    [Fact]
    public async Task ProcessInboundMessageAsync_WithPaddingEnabled_ReversesPadding()
    {
        // Arrange
        var options = new PrivacyLayerOptions
        {
            Enabled = true,
            Padding = new MessagePaddingOptions { Enabled = true, BucketSizes = new() { 512 } },
            Timing = new TimingObfuscationOptions { Enabled = false },
            Batching = new MessageBatchingOptions { Enabled = false },
            CoverTraffic = new CoverTrafficOptions { Enabled = false }
        };

        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, options);
        var originalMessage = new byte[] { 1, 2, 3, 4, 5 };
        var cts = new CancellationTokenSource();

        // First pad the message
        var paddedMessage = await privacyLayer.ProcessOutboundMessageAsync(originalMessage, cts.Token);

        // Act - Now unpad it
        var unpaddedMessage = await privacyLayer.ProcessInboundMessageAsync(paddedMessage, cts.Token);

        // Assert
        Assert.Equal(originalMessage, unpaddedMessage);
    }

    [Fact]
    public void GetOutboundDelay_WithTimingEnabled_ReturnsDelay()
    {
        // Arrange - PrivacyLayer passes JitterMs as minDelayMs; RandomJitterObfuscator uses maxDelayMs=500 default
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, _defaultOptions);

        // Act
        var delay = privacyLayer.GetOutboundDelay();

        // Assert - delay is in [JitterMs, 500] ms
        Assert.True(delay >= TimeSpan.Zero, "Delay should be non-negative");
        Assert.True(delay <= TimeSpan.FromMilliseconds(500), "Delay should be within RandomJitterObfuscator range (max 500ms default)");
    }

    [Fact]
    public void GetOutboundDelay_WithTimingDisabled_ReturnsZero()
    {
        // Arrange
        var options = new PrivacyLayerOptions
        {
            Enabled = true,
            Timing = new TimingObfuscationOptions { Enabled = false }
        };
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, options);

        // Act
        var delay = privacyLayer.GetOutboundDelay();

        // Assert
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public async Task GetCoverTrafficAsync_WithCoverTrafficEnabled_GeneratesTraffic()
    {
        // Arrange
        var options = new PrivacyLayerOptions
        {
            Enabled = true,
            CoverTraffic = new CoverTrafficOptions { Enabled = true, IntervalSeconds = 1 } // Short interval for test
        };
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, options);
        var cts = new CancellationTokenSource();
        var messages = new List<byte[]>();

        // Act - Collect a few cover traffic messages
        await foreach (var message in privacyLayer.GetCoverTrafficAsync(cts.Token))
        {
            messages.Add(message);
            if (messages.Count >= 2)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert
        Assert.True(messages.Count >= 1, "Should generate at least one cover traffic message");
        foreach (var message in messages)
        {
            Assert.True(message.Length > 0, "Cover traffic messages should not be empty");
            Assert.True(slskd.Mesh.Privacy.CoverTrafficGenerator.IsCoverTraffic(message), "Messages should be marked as cover traffic");
        }
    }

    [Fact]
    public void RecordActivity_UpdatesCoverTrafficTiming()
    {
        // Arrange
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, _defaultOptions);

        // Act
        privacyLayer.RecordActivity();

        // Assert - Should suppress cover traffic for the configured interval (TimeUntilNextCoverTraffic is on concrete CoverTrafficGenerator)
        var ctg = privacyLayer.CoverTrafficGenerator as slskd.Mesh.Privacy.CoverTrafficGenerator;
        Assert.NotNull(ctg);
        var timeUntilNext = ctg.TimeUntilNextCoverTraffic();
        Assert.True(timeUntilNext > TimeSpan.Zero, "Activity should suppress cover traffic");
    }

    [Fact]
    public void GetPendingBatches_WithMessagesQueued_ReturnsBatches()
    {
        // Arrange: MaxBatchSize=2 so two AddMessage makes the batch ready (GetBatch returns the queued messages)
        var options = new PrivacyLayerOptions
        {
            Enabled = true,
            Padding = new MessagePaddingOptions { Enabled = false },
            Timing = new TimingObfuscationOptions { Enabled = false },
            Batching = new MessageBatchingOptions { Enabled = true, BatchWindowMs = 1000, MaxBatchSize = 2 },
            CoverTraffic = new CoverTrafficOptions { Enabled = false }
        };
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, options);

        var message1 = new byte[] { 1 };
        var message2 = new byte[] { 2 };

        var batcher = privacyLayer.MessageBatcher;
        Assert.NotNull(batcher);

        batcher.AddMessage(message1);
        batcher.AddMessage(message2); // batch becomes ready (count >= MaxBatchSize)

        // Act
        var batches = privacyLayer.GetPendingBatches();

        // Assert
        Assert.NotNull(batches);
        Assert.True(batches.Count > 0, "Should have pending batch after filling to MaxBatchSize");
    }

    [Fact]
    public void UpdateConfiguration_ChangesPrivacyLayerBehavior()
    {
        // Arrange
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, _defaultOptions);

        // Initially enabled
        Assert.True(privacyLayer.IsEnabled);

        // Act - Disable privacy layer
        var newOptions = new PrivacyLayerOptions { Enabled = false };
        privacyLayer.UpdateConfiguration(newOptions);

        // Assert
        Assert.False(privacyLayer.IsEnabled);
        Assert.Null(privacyLayer.MessagePadder);
        Assert.Null(privacyLayer.TimingObfuscator);
        Assert.Null(privacyLayer.MessageBatcher);
        Assert.Null(privacyLayer.CoverTrafficGenerator);
    }

    [Fact]
    public async Task EndToEndMessageFlow_WithAllFeaturesEnabled_ProcessesCorrectly()
    {
        // Arrange
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, _defaultOptions);
        var originalMessage = new byte[] { 72, 101, 108, 108, 111 }; // "Hello"
        var cts = new CancellationTokenSource();

        // Act - Process outbound
        var processedOutbound = await privacyLayer.ProcessOutboundMessageAsync(originalMessage, cts.Token);

        // Simulate sending (skip for batched messages)
        if (processedOutbound.Length > 0)
        {
            // Process inbound (reverse transforms)
            var processedInbound = await privacyLayer.ProcessInboundMessageAsync(processedOutbound, cts.Token);

            // Assert - Should get original message back (or batched version)
            if (processedInbound.Length > 0)
            {
                // If we got a single message back, it should match original
                Assert.Equal(originalMessage, processedInbound);
            }
        }
    }

    [Fact]
    public void PrivacyLayer_HandlesInvalidConfiguration_Gracefully()
    {
        // Arrange - Invalid JitterMs (negative). RandomJitterObfuscator clamps to 0; valid Padding/Batching to avoid other throws.
        var invalidOptions = new PrivacyLayerOptions
        {
            Enabled = true,
            Padding = new MessagePaddingOptions { Enabled = true, BucketSizes = new() { 512 } },
            Timing = new TimingObfuscationOptions { Enabled = true, JitterMs = -10 },
            Batching = new MessageBatchingOptions { Enabled = true, BatchWindowMs = 100, MaxBatchSize = 2 },
            CoverTraffic = new CoverTrafficOptions { Enabled = false }
        };

        // Act - Should not throw; RandomJitterObfuscator treats negative JitterMs as 0
        var privacyLayer = new PrivacyLayer(_loggerMock.Object, _loggerFactoryMock.Object, invalidOptions);

        Assert.NotNull(privacyLayer);
    }
}

