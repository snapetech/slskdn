namespace slskd.Tests.Unit.Common.Moderation;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Moderation;
using Xunit;

public class ModerationDecisionTests
{
    [Fact]
    public void ModerationDecision_Allow_CreatesCorrectDecision()
    {
        // Act
        var decision = ModerationDecision.Allow("test_reason");

        // Assert
        Assert.Equal(ModerationVerdict.Allowed, decision.Verdict);
        Assert.Equal("test_reason", decision.Reason);
        Assert.Empty(decision.EvidenceKeys);
        Assert.True(decision.IsAllowed());
        Assert.False(decision.IsBlocking());
    }

    [Fact]
    public void ModerationDecision_Allow_WithoutReason_UsesDefault()
    {
        // Act
        var decision = ModerationDecision.Allow();

        // Assert
        Assert.Equal(ModerationVerdict.Allowed, decision.Verdict);
        Assert.Equal("no_blockers_triggered", decision.Reason);
    }

    [Fact]
    public void ModerationDecision_Block_CreatesCorrectDecision()
    {
        // Act
        var decision = ModerationDecision.Block("hash_blocklist", "evidence:1", "evidence:2");

        // Assert
        Assert.Equal(ModerationVerdict.Blocked, decision.Verdict);
        Assert.Equal("hash_blocklist", decision.Reason);
        Assert.Equal(2, decision.EvidenceKeys.Length);
        Assert.Contains("evidence:1", decision.EvidenceKeys);
        Assert.Contains("evidence:2", decision.EvidenceKeys);
        Assert.False(decision.IsAllowed());
        Assert.True(decision.IsBlocking());
    }

    [Fact]
    public void ModerationDecision_Block_WithoutReason_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ModerationDecision.Block(""));
        Assert.Throws<ArgumentException>(() => ModerationDecision.Block(null));
    }

    [Fact]
    public void ModerationDecision_Quarantine_CreatesCorrectDecision()
    {
        // Act
        var decision = ModerationDecision.Quarantine("legal_hold");

        // Assert
        Assert.Equal(ModerationVerdict.Quarantined, decision.Verdict);
        Assert.Equal("legal_hold", decision.Reason);
        Assert.True(decision.IsBlocking());
        Assert.False(decision.IsAllowed());
    }

    [Fact]
    public void ModerationDecision_Quarantine_WithoutReason_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ModerationDecision.Quarantine(""));
        Assert.Throws<ArgumentException>(() => ModerationDecision.Quarantine(null));
    }

    [Fact]
    public void ModerationDecision_Unknown_CreatesCorrectDecision()
    {
        // Act
        var decision = ModerationDecision.Unknown("no_providers");

        // Assert
        Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
        Assert.Equal("no_providers", decision.Reason);
        Assert.False(decision.IsBlocking());
        Assert.False(decision.IsAllowed());
    }

    [Fact]
    public void ModerationDecision_IsBlocking_WorksForAllVerdicts()
    {
        // Assert
        Assert.False(ModerationDecision.Allow().IsBlocking());
        Assert.True(ModerationDecision.Block("reason").IsBlocking());
        Assert.True(ModerationDecision.Quarantine("reason").IsBlocking());
        Assert.False(ModerationDecision.Unknown().IsBlocking());
    }

    [Fact]
    public void ModerationDecision_IsAllowed_WorksForAllVerdicts()
    {
        // Assert
        Assert.True(ModerationDecision.Allow().IsAllowed());
        Assert.False(ModerationDecision.Block("reason").IsAllowed());
        Assert.False(ModerationDecision.Quarantine("reason").IsAllowed());
        Assert.False(ModerationDecision.Unknown().IsAllowed());
    }
}

public class NoopModerationProviderTests
{
    [Fact]
    public async Task CheckLocalFileAsync_AlwaysReturnsUnknown()
    {
        // Arrange
        var provider = new NoopModerationProvider();
        var file = new LocalFileMetadata
        {
            Id = "test-file",
            SizeBytes = 1000,
            PrimaryHash = "abc123"
        };

        // Act
        var decision = await provider.CheckLocalFileAsync(file, CancellationToken.None);

        // Assert
        Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
        Assert.Equal("moderation_disabled", decision.Reason);
    }

    [Fact]
    public async Task CheckContentIdAsync_AlwaysReturnsUnknown()
    {
        // Arrange
        var provider = new NoopModerationProvider();

        // Act
        var decision = await provider.CheckContentIdAsync("content-id-123", CancellationToken.None);

        // Assert
        Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
        Assert.Equal("moderation_disabled", decision.Reason);
    }

    [Fact]
    public async Task ReportContentAsync_DoesNothing()
    {
        // Arrange
        var provider = new NoopModerationProvider();
        var report = new ModerationReport { ReasonCode = "test" };

        // Act (should not throw)
        await provider.ReportContentAsync("content-id", report, CancellationToken.None);

        // Assert: No exception thrown
    }

    [Fact]
    public async Task ReportPeerAsync_DoesNothing()
    {
        // Arrange
        var provider = new NoopModerationProvider();
        var report = new PeerReport { ReasonCode = "test" };

        // Act (should not throw)
        await provider.ReportPeerAsync("peer-id", report, CancellationToken.None);

        // Assert: No exception thrown
    }
}

public class CompositeModerationProviderTests
{
    [Fact]
    public async Task CheckLocalFileAsync_WithNoProviders_ReturnsUnknown()
    {
        // Arrange
        var options = CreateOptionsMonitor();
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger);
        var file = CreateTestFile();

        // Act
        var decision = await provider.CheckLocalFileAsync(file, CancellationToken.None);

        // Assert
        Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
        Assert.Equal("no_blockers_triggered", decision.Reason);
    }

    [Fact]
    public async Task CheckLocalFileAsync_WithHashBlocklistHit_ReturnsBlocked()
    {
        // Arrange
        var hashBlocklist = new Mock<IHashBlocklistChecker>();
        hashBlocklist.Setup(x => x.IsBlockedHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var options = CreateOptionsMonitor(hashBlocklistEnabled: true);
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger, hashBlocklist: hashBlocklist.Object);
        var file = CreateTestFile();

        // Act
        var decision = await provider.CheckLocalFileAsync(file, CancellationToken.None);

        // Assert
        Assert.Equal(ModerationVerdict.Blocked, decision.Verdict);
        Assert.Equal("hash_blocklist", decision.Reason);
        Assert.Contains("provider:blocklist", decision.EvidenceKeys);
    }

    [Fact]
    public async Task CheckLocalFileAsync_WithHashBlocklistMiss_ContinuesToNextProvider()
    {
        // Arrange
        var hashBlocklist = new Mock<IHashBlocklistChecker>();
        hashBlocklist.Setup(x => x.IsBlockedHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var options = CreateOptionsMonitor(hashBlocklistEnabled: true);
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger, hashBlocklist: hashBlocklist.Object);
        var file = CreateTestFile();

        // Act
        var decision = await provider.CheckLocalFileAsync(file, CancellationToken.None);

        // Assert
        Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
    }

    [Fact]
    public async Task CheckLocalFileAsync_WithExternalModerationBlocking_ReturnsBlocked()
    {
        // Arrange
        var externalClient = new Mock<IExternalModerationClient>();
        externalClient.Setup(x => x.AnalyzeFileAsync(It.IsAny<LocalFileMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ModerationDecision.Block("external_moderation"));

        var options = CreateOptionsMonitor(externalModerationEnabled: true);
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger, externalClient: externalClient.Object);
        var file = CreateTestFile();

        // Act
        var decision = await provider.CheckLocalFileAsync(file, CancellationToken.None);

        // Assert
        Assert.Equal(ModerationVerdict.Blocked, decision.Verdict);
        Assert.Equal("external_moderation", decision.Reason);
    }

    [Fact]
    public async Task CheckLocalFileAsync_FailsafeBlockMode_BlocksOnError()
    {
        // Arrange
        var hashBlocklist = new Mock<IHashBlocklistChecker>();
        hashBlocklist.Setup(x => x.IsBlockedHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var options = CreateOptionsMonitor(hashBlocklistEnabled: true, failsafeMode: "block");
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger, hashBlocklist: hashBlocklist.Object);
        var file = CreateTestFile();

        // Act
        var decision = await provider.CheckLocalFileAsync(file, CancellationToken.None);

        // Assert
        Assert.Equal(ModerationVerdict.Blocked, decision.Verdict);
        Assert.Equal("failsafe_block_on_error", decision.Reason);
    }

    [Fact]
    public async Task CheckLocalFileAsync_FailsafeAllowMode_ContinuesOnError()
    {
        // Arrange
        var hashBlocklist = new Mock<IHashBlocklistChecker>();
        hashBlocklist.Setup(x => x.IsBlockedHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var options = CreateOptionsMonitor(hashBlocklistEnabled: true, failsafeMode: "allow");
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger, hashBlocklist: hashBlocklist.Object);
        var file = CreateTestFile();

        // Act
        var decision = await provider.CheckLocalFileAsync(file, CancellationToken.None);

        // Assert
        Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
        Assert.Equal("no_blockers_triggered", decision.Reason);
    }

    [Fact]
    public async Task ReportPeerAsync_WithReputationEnabled_RecordsEvent()
    {
        // Arrange
        var peerReputation = new Mock<IPeerReputationStore>();
        var options = CreateOptionsMonitor(reputationEnabled: true);
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger, peerReputation: peerReputation.Object);
        var report = new PeerReport { ReasonCode = "bad_behavior" };

        // Act
        await provider.ReportPeerAsync("peer-123", report, CancellationToken.None);

        // Assert
        peerReputation.Verify(x => x.RecordPeerEventAsync("peer-123", report, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckLocalFileAsync_ThrowsOnNullFile()
    {
        // Arrange
        var options = CreateOptionsMonitor();
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await provider.CheckLocalFileAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task CheckContentIdAsync_ThrowsOnNullOrEmpty()
    {
        // Arrange
        var options = CreateOptionsMonitor();
        var logger = new Mock<ILogger<CompositeModerationProvider>>().Object;
        var provider = new CompositeModerationProvider(options, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.CheckContentIdAsync("", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.CheckContentIdAsync(null, CancellationToken.None));
    }

    private static LocalFileMetadata CreateTestFile()
    {
        return new LocalFileMetadata
        {
            Id = "test-file-123",
            SizeBytes = 5000000,
            PrimaryHash = "abc123def456",
            MediaInfo = "Audio: FLAC"
        };
    }

    private static WrappedOptionsMonitor<ModerationOptions> CreateOptionsMonitor(
        bool hashBlocklistEnabled = false,
        bool externalModerationEnabled = false,
        bool reputationEnabled = false,
        string failsafeMode = "block")
    {
        var opts = new ModerationOptions
        {
            Enabled = true,
            FailsafeMode = failsafeMode,
            HashBlocklist = new ModerationOptions.HashBlocklistOptions
            {
                Enabled = hashBlocklistEnabled
            },
            ExternalModeration = new ModerationOptions.ExternalModerationOptions
            {
                Enabled = externalModerationEnabled
            },
            Reputation = new ModerationOptions.ReputationOptions
            {
                Enabled = reputationEnabled
            }
        };

        return new WrappedOptionsMonitor<ModerationOptions>(
            Microsoft.Extensions.Options.Options.Create(opts));
    }
}
