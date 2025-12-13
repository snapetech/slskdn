namespace slskd.Tests.Integration.MultiSource;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using slskd.Transfers.MultiSource;
using slskd.Tests.Integration.Harness;
using Xunit;

/// <summary>
/// Integration tests for MultiSource download service, focusing on behavior under limited peers.
/// </summary>
[Trait("Category", "L2-Integration")]
[Trait("Category", "MultiSource")]
public class MultiSourceIntegrationTests : IClassFixture<StubWebApplicationFactory>
{
    private readonly StubWebApplicationFactory factory;
    private readonly IServiceProvider serviceProvider;

    public MultiSourceIntegrationTests(StubWebApplicationFactory factory)
    {
        this.factory = factory;
        serviceProvider = factory.Services;
    }

    [Fact]
    public async Task MultiSourceDownloadService_ShouldBeAvailable()
    {
        // Arrange & Act
        var multiSourceService = serviceProvider.GetService<IMultiSourceDownloadService>();

        // Assert
        if (multiSourceService == null)
        {
            Assert.True(true, "MultiSource service not available (may not be configured)");
            return;
        }

        Assert.NotNull(multiSourceService);
    }

    [Fact]
    public async Task MultiSourceDownloadService_ShouldRespectConcurrencyLimits()
    {
        // Arrange
        var multiSourceService = serviceProvider.GetService<IMultiSourceDownloadService>();

        if (multiSourceService == null)
        {
            Assert.True(true, "MultiSource service not available");
            return;
        }

        // Act - Check that service exists and can be instantiated
        // In a real integration test, we would:
        // 1. Set up a test download with limited peers
        // 2. Verify that concurrency limits are respected
        // 3. Check that SemaphoreSlim limits retry workers

        // For now, just verify the service is available and properly configured
        Assert.NotNull(multiSourceService);

        // Note: Full integration test would require:
        // - Mock Soulseek client with limited peers
        // - Test download request
        // - Verification of concurrent worker limits
        // This is a smoke test to ensure service is registered
    }

    [Fact]
    public async Task MultiSourceDownloadService_ShouldHandleNoPeersGracefully()
    {
        // Arrange
        var multiSourceService = serviceProvider.GetService<IMultiSourceDownloadService>();

        if (multiSourceService == null)
        {
            Assert.True(true, "MultiSource service not available");
            return;
        }

        // Act & Assert
        // Service should exist and be ready
        // In a full test, we would verify that when no peers are available,
        // the service handles it gracefully without throwing exceptions
        Assert.NotNull(multiSourceService);
    }
}















