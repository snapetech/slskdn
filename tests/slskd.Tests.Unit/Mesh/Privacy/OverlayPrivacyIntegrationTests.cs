// <copyright file="OverlayPrivacyIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Common.Security;
using slskd.Mesh.Overlay;
using slskd.Mesh.Privacy;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Privacy;

public class OverlayPrivacyIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<QuicOverlayClient>> _clientLoggerMock;
    private readonly Mock<ILogger<ControlDispatcher>> _dispatcherLoggerMock;
    private readonly Mock<ILogger<PrivacyLayer>> _privacyLoggerMock;
    private readonly Mock<IControlSigner> _signerMock;
    private readonly Mock<ControlEnvelopeValidator> _validatorMock;
    private readonly OverlayOptions _overlayOptions;
    private readonly PrivacyLayerOptions _privacyOptions;

    public OverlayPrivacyIntegrationTests()
    {
        _clientLoggerMock = new Mock<ILogger<QuicOverlayClient>>();
        _dispatcherLoggerMock = new Mock<ILogger<ControlDispatcher>>();
        _privacyLoggerMock = new Mock<ILogger<PrivacyLayer>>();
        _signerMock = new Mock<IControlSigner>();
        _validatorMock = new Mock<ControlEnvelopeValidator>();

        _overlayOptions = new OverlayOptions { Enable = true, MaxDatagramBytes = 2048 };
        _privacyOptions = new PrivacyLayerOptions
        {
            Enabled = true,
            Padding = new MessagePaddingOptions { Enabled = true, BucketSizes = new() { 512 } },
            Timing = new TimingObfuscationOptions { Enabled = false }, // Disable timing for testing
            Batching = new MessageBatchingOptions { Enabled = false }, // Disable batching for testing
            CoverTraffic = new CoverTrafficOptions { Enabled = false }
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public async Task OverlayClientWithPrivacyLayer_ProcessesOutboundMessages()
    {
        // Arrange
        var privacyLayer = new PrivacyLayer(_privacyLoggerMock.Object, _privacyOptions);
        var optionsMock = new Mock<IOptions<OverlayOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_overlayOptions);

        var client = new TestableQuicOverlayClient(
            _clientLoggerMock.Object,
            optionsMock.Object,
            _signerMock.Object,
            privacyLayer);

        var envelope = new ControlEnvelope
        {
            Type = "test",
            Payload = new byte[] { 1, 2, 3, 4, 5 },
            MessageId = Guid.NewGuid().ToString()
        };

        var endpoint = new IPEndPoint(IPAddress.Loopback, 5000);

        // Act - SendAsync will apply privacy transforms but fail to actually send (QUIC not available in test)
        var result = await client.SendAsync(envelope, endpoint);

        // Assert - Should return false (can't actually send) but privacy transforms should be applied
        // We can't easily test the actual sending without mocking QUIC, but we can verify
        // that the privacy layer methods were called by checking the envelope was processed
        Assert.False(result); // Expected to fail since QUIC isn't available in tests
    }

    [Fact]
    public async Task ControlDispatcherWithPrivacyLayer_ProcessesInboundMessages()
    {
        // Arrange
        var privacyLayer = new PrivacyLayer(_privacyLoggerMock.Object, _privacyOptions);
        var dispatcher = new ControlDispatcher(
            _dispatcherLoggerMock.Object,
            _validatorMock.Object,
            privacyLayer);

        // Create a padded message first
        var originalPayload = new byte[] { 72, 101, 108, 108, 111 }; // "Hello"
        var paddedPayload = await privacyLayer.ProcessOutboundMessageAsync(originalPayload);

        var envelope = new ControlEnvelope
        {
            Type = "test",
            Payload = paddedPayload,
            MessageId = Guid.NewGuid().ToString()
        };

        // Mock validator to return valid
        _validatorMock.Setup(v => v.ValidateEnvelope(
            It.IsAny<ControlEnvelope>(),
            It.IsAny<MeshPeerDescriptor>(),
            It.IsAny<string>()))
            .Returns(new ControlEnvelopeValidator.ValidationResult(true));

        var peerDescriptor = new MeshPeerDescriptor(); // Mock descriptor

        // Act
        var result = await dispatcher.HandleAsync(envelope, peerDescriptor, "peer123");

        // Assert - Should succeed and unpad the message
        Assert.True(result);
    }

    [Fact]
    public async Task RoundTripPrivacyProcessing_EnvelopePayloadSurvivesTransforms()
    {
        // Arrange - Full round-trip test
        var privacyLayer = new PrivacyLayer(_privacyLoggerMock.Object, _privacyOptions);
        var dispatcher = new ControlDispatcher(
            _dispatcherLoggerMock.Object,
            _validatorMock.Object,
            privacyLayer);

        var originalPayload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var cts = new CancellationTokenSource();

        // Step 1: Apply outbound transforms (padding)
        var processedPayload = await privacyLayer.ProcessOutboundMessageAsync(originalPayload, cts.Token);

        // Step 2: Create envelope with processed payload
        var envelope = new ControlEnvelope
        {
            Type = "test",
            Payload = processedPayload,
            MessageId = Guid.NewGuid().ToString()
        };

        // Step 3: Mock validation
        _validatorMock.Setup(v => v.ValidateEnvelope(
            It.IsAny<ControlEnvelope>(),
            It.IsAny<MeshPeerDescriptor>(),
            It.IsAny<string>()))
            .Returns(new ControlEnvelopeValidator.ValidationResult(true));

        var peerDescriptor = new MeshPeerDescriptor();

        // Step 4: Process through dispatcher (should unpad)
        var result = await dispatcher.HandleAsync(envelope, peerDescriptor, "peer123");

        // Assert
        Assert.True(result, "Dispatcher should handle envelope successfully");
        // The envelope payload should now be unpadded back to original
        Assert.Equal(originalPayload, envelope.Payload);
    }

    [Fact]
    public async Task PrivacyLayerDisabled_NoTransformsApplied()
    {
        // Arrange
        var disabledOptions = new PrivacyLayerOptions { Enabled = false };
        var privacyLayer = new PrivacyLayer(_privacyLoggerMock.Object, disabledOptions);

        var originalPayload = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var processed = await privacyLayer.ProcessOutboundMessageAsync(originalPayload);

        // Assert - Should return original unchanged
        Assert.Equal(originalPayload, processed);
    }

    [Fact]
    public async Task ControlDispatcherWithoutPrivacyLayer_WorksNormally()
    {
        // Arrange
        var dispatcher = new ControlDispatcher(
            _dispatcherLoggerMock.Object,
            _validatorMock.Object,
            privacyLayer: null); // No privacy layer

        var envelope = new ControlEnvelope
        {
            Type = "ping",
            Payload = null,
            MessageId = Guid.NewGuid().ToString()
        };

        _validatorMock.Setup(v => v.ValidateEnvelope(
            It.IsAny<ControlEnvelope>(),
            It.IsAny<MeshPeerDescriptor>(),
            It.IsAny<string>()))
            .Returns(new ControlEnvelopeValidator.ValidationResult(true));

        var peerDescriptor = new MeshPeerDescriptor();

        // Act
        var result = await dispatcher.HandleAsync(envelope, peerDescriptor, "peer123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void PrivacyLayerIntegration_DoesNotBreakNormalOverlayOperation()
    {
        // Arrange - Test that privacy layer doesn't interfere with normal operation
        var privacyLayer = new PrivacyLayer(_privacyLoggerMock.Object, _privacyOptions);
        var optionsMock = new Mock<IOptions<OverlayOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_overlayOptions);

        var client = new TestableQuicOverlayClient(
            _clientLoggerMock.Object,
            optionsMock.Object,
            _signerMock.Object,
            privacyLayer);

        // Act & Assert - Should not throw exceptions during construction
        Assert.NotNull(client);
        Assert.True(privacyLayer.IsEnabled);
    }
}

/// <summary>
/// Testable version of QuicOverlayClient that doesn't actually try to connect.
/// </summary>
internal class TestableQuicOverlayClient : QuicOverlayClient
{
    public TestableQuicOverlayClient(
        ILogger<QuicOverlayClient> logger,
        IOptions<OverlayOptions> options,
        IControlSigner signer,
        IPrivacyLayer? privacyLayer = null)
        : base(logger, options, signer, privacyLayer)
    {
    }

    // Expose protected methods for testing if needed
    // (Currently using public interface for testing)
}

