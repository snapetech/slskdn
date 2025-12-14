// <copyright file="MeshTransportServiceIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Common.Security;
using slskd.Mesh;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class MeshTransportServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<MeshTransportService>> _loggerMock;
    private readonly Mock<IAnonymityTransportSelector> _anonymitySelectorMock;
    private readonly MeshOptions _meshOptions;
    private readonly AdversarialOptions _adversarialOptions;

    public MeshTransportServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<MeshTransportService>>();
        _anonymitySelectorMock = new Mock<IAnonymityTransportSelector>();

        _meshOptions = new MeshOptions
        {
            TransportPreference = MeshTransportPreference.DhtFirst
        };

        _adversarialOptions = new AdversarialOptions
        {
            AnonymityLayer = new AnonymityLayerOptions
            {
                Enabled = true,
                Mode = AnonymityMode.Tor
            }
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public async Task ChooseTransportAsync_WithoutAnonymity_ReturnsStandardPreference()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<MeshOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_meshOptions);

        var service = new MeshTransportService(_loggerMock.Object, optionsMock.Object);

        // Act
        var decision = await service.ChooseTransportAsync("content123");

        // Assert
        Assert.Equal(MeshTransportPreference.DhtFirst, decision.Preference);
        Assert.Contains("DHT-first for efficiency", decision.Reason);
        Assert.Null(decision.AnonymityTransport);
    }

    [Fact]
    public async Task ChooseTransportAsync_WithAnonymityDisabled_ReturnsStandardPreference()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<MeshOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_meshOptions);

        var adversarialOptionsMock = new Mock<IOptions<AdversarialOptions>>();
        adversarialOptionsMock.Setup(o => o.Value).Returns(new AdversarialOptions { AnonymityLayer = new AnonymityLayerOptions { Enabled = false } });

        var service = new MeshTransportService(_loggerMock.Object, optionsMock.Object, null, adversarialOptionsMock.Object);

        // Act
        var decision = await service.ChooseTransportAsync("peer123", null, "content123");

        // Assert
        Assert.Equal(MeshTransportPreference.DhtFirst, decision.Preference);
        Assert.Contains("DHT-first for efficiency", decision.Reason);
        Assert.Null(decision.AnonymityTransport);
    }

    [Fact]
    public async Task ChooseTransportAsync_WithTorAnonymityEnabled_SelectsTorTransport()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<MeshOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_meshOptions);

        var adversarialOptionsMock = new Mock<IOptions<AdversarialOptions>>();
        adversarialOptionsMock.Setup(o => o.Value).Returns(_adversarialOptions);

        // Mock successful Tor transport selection
        var torTransport = new TorSocksTransport(_adversarialOptions.AnonymityLayer.Tor, _loggerMock.Object);
        _anonymitySelectorMock
            .Setup(s => s.SelectAndConnectAsync("peer123", null, "dummy-host", 0, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((torTransport, Mock.Of<Stream>()));

        var service = new MeshTransportService(_loggerMock.Object, optionsMock.Object, _anonymitySelectorMock.Object, adversarialOptionsMock.Object);

        // Act
        var decision = await service.ChooseTransportAsync("peer123", null, "content123");

        // Assert
        Assert.Equal(MeshTransportPreference.OverlayFirst, decision.Preference); // Switches to overlay-first for privacy
        Assert.Contains("Overlay-first with Tor anonymity", decision.Reason);
        Assert.Equal(AnonymityTransportType.Tor, decision.AnonymityTransport);
    }

    [Fact]
    public async Task ChooseTransportAsync_WithTorSelectionFailure_FallsBackToStandard()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<MeshOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_meshOptions);

        var adversarialOptionsMock = new Mock<IOptions<AdversarialOptions>>();
        adversarialOptionsMock.Setup(o => o.Value).Returns(_adversarialOptions);

        // Mock failed Tor transport selection
        _anonymitySelectorMock
            .Setup(s => s.SelectAndConnectAsync("peer123", null, "dummy-host", 0, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No transport available"));

        var service = new MeshTransportService(_loggerMock.Object, optionsMock.Object, _anonymitySelectorMock.Object, adversarialOptionsMock.Object);

        // Act
        var decision = await service.ChooseTransportAsync("peer123", null, "content123");

        // Assert - Falls back to standard preference
        Assert.Equal(MeshTransportPreference.DhtFirst, decision.Preference);
        Assert.Contains("DHT-first for efficiency", decision.Reason);
        Assert.Null(decision.AnonymityTransport);
    }

    [Fact]
    public async Task ChooseTransportAsync_WithPodContext_UsesPodInSelection()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<MeshOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_meshOptions);

        var adversarialOptionsMock = new Mock<IOptions<AdversarialOptions>>();
        adversarialOptionsMock.Setup(o => o.Value).Returns(_adversarialOptions);

        var torTransport = new TorSocksTransport(_adversarialOptions.AnonymityLayer.Tor, _loggerMock.Object);
        _anonymitySelectorMock
            .Setup(s => s.SelectAndConnectAsync("peer123", "pod456", "dummy-host", 0, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((torTransport, Mock.Of<Stream>()));

        var service = new MeshTransportService(_loggerMock.Object, optionsMock.Object, _anonymitySelectorMock.Object, adversarialOptionsMock.Object);

        // Act
        var decision = await service.ChooseTransportAsync("peer123", "pod456", "content123");

        // Assert
        Assert.Equal(MeshTransportPreference.OverlayFirst, decision.Preference);
        Assert.Equal(AnonymityTransportType.Tor, decision.AnonymityTransport);
        _anonymitySelectorMock.Verify(s => s.SelectAndConnectAsync("peer123", "pod456", "dummy-host", 0, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChooseTransportAsync_WithDirectAnonymityMode_DoesNotOverride()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<MeshOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_meshOptions);

        var directAnonymityOptions = new AdversarialOptions
        {
            AnonymityLayer = new AnonymityLayerOptions
            {
                Enabled = true,
                Mode = AnonymityMode.Direct
            }
        };

        var adversarialOptionsMock = new Mock<IOptions<AdversarialOptions>>();
        adversarialOptionsMock.Setup(o => o.Value).Returns(directAnonymityOptions);

        var service = new MeshTransportService(_loggerMock.Object, optionsMock.Object, _anonymitySelectorMock.Object, adversarialOptionsMock.Object);

        // Act
        var decision = await service.ChooseTransportAsync("peer123", null, "content123");

        // Assert - Direct mode doesn't trigger anonymity transport selection
        Assert.Equal(MeshTransportPreference.DhtFirst, decision.Preference);
        Assert.Null(decision.AnonymityTransport);
        _anonymitySelectorMock.Verify(s => s.SelectAndConnectAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Preference_ReturnsConfiguredTransportPreference()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<MeshOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new MeshOptions { TransportPreference = MeshTransportPreference.OverlayFirst });

        var service = new MeshTransportService(_loggerMock.Object, optionsMock.Object);

        // Act & Assert
        Assert.Equal(MeshTransportPreference.OverlayFirst, service.Preference);
    }

    [Theory]
    [InlineData(MeshTransportPreference.DhtFirst, "DHT-first for efficiency")]
    [InlineData(MeshTransportPreference.Mirrored, "Mirrored DHT+overlay for resiliency")]
    [InlineData(MeshTransportPreference.OverlayFirst, "Overlay-first for private paths")]
    public async Task ChooseTransportAsync_ReturnsCorrectReasonForPreference(MeshTransportPreference preference, string expectedReason)
    {
        // Arrange
        var optionsMock = new Mock<IOptions<MeshOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new MeshOptions { TransportPreference = preference });

        var service = new MeshTransportService(_loggerMock.Object, optionsMock.Object);

        // Act
        var decision = await service.ChooseTransportAsync("content123");

        // Assert
        Assert.Equal(preference, decision.Preference);
        Assert.Equal(expectedReason, decision.Reason);
    }
}

