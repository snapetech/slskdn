using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Common.Security;
using slskd.Mesh.ServiceFabric;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace slskd.Tests.Mesh.ServiceFabric;

/// <summary>
/// Security-focused tests for MeshServiceRouter.
/// Tests rate limiting, circuit breakers, payload limits, and abuse scenarios.
/// </summary>
public class MeshServiceRouterSecurityTests
{
    private readonly ILogger<MeshServiceRouter> _logger;
    private readonly ILogger<SecurityEventLogger> _securityLogger;

    public MeshServiceRouterSecurityTests()
    {
        _logger = new LoggerFactory().CreateLogger<MeshServiceRouter>();
        _securityLogger = new LoggerFactory().CreateLogger<SecurityEventLogger>();
    }

    [Fact]
    public async Task GlobalRateLimit_BlocksExcessiveCalls()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new MeshServiceFabricOptions
        {
            GlobalMaxCallsPerPeer = 5,
            DefaultMaxCallsPerMinute = 100
        });
        var router = new MeshServiceRouter(_logger, options);
        var service = new TestService();
        router.RegisterService(service);

        var peerId = "peer-aggressive";

        // Act: Make 6 calls (limit is 5)
        ServiceReply? lastReply = null;
        for (int i = 0; i < 6; i++)
        {
            var call = new ServiceCall
            {
                ServiceName = "test-service",
                Method = "TestMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            };
            lastReply = await router.RouteAsync(call, peerId);
        }

        // Assert: 6th call should be rate limited
        Assert.NotNull(lastReply);
        Assert.Equal(ServiceStatusCodes.RateLimited, lastReply.StatusCode);
        Assert.Contains("global limit", lastReply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PerServiceRateLimit_BlocksExcessiveCalls()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new MeshServiceFabricOptions
        {
            GlobalMaxCallsPerPeer = 1000,
            PerServiceRateLimits = new()
            {
                ["test-service"] = 3
            }
        });
        var router = new MeshServiceRouter(_logger, options);
        var service = new TestService();
        router.RegisterService(service);

        var peerId = "peer-spammy";

        // Act: Make 4 calls to same service (limit is 3)
        ServiceReply? lastReply = null;
        for (int i = 0; i < 4; i++)
        {
            var call = new ServiceCall
            {
                ServiceName = "test-service",
                Method = "TestMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            };
            lastReply = await router.RouteAsync(call, peerId);
        }

        // Assert: 4th call should be rate limited
        Assert.NotNull(lastReply);
        Assert.Equal(ServiceStatusCodes.RateLimited, lastReply.StatusCode);
        Assert.Contains("this service", lastReply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PayloadSizeLimit_RejectsOversizedPayload()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new MeshServiceFabricOptions
        {
            MaxDescriptorBytes = 1024 // 1 KB
        });
        var router = new MeshServiceRouter(_logger, options);
        var service = new TestService();
        router.RegisterService(service);

        // Act: Send 2 KB payload (exceeds 1 KB limit)
        var call = new ServiceCall
        {
            ServiceName = "test-service",
            Method = "TestMethod",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = new byte[2048]
        };
        var reply = await router.RouteAsync(call, "peer-large-payload");

        // Assert
        Assert.Equal(ServiceStatusCodes.PayloadTooLarge, reply.StatusCode);
        Assert.Contains("exceeds", reply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfter5ConsecutiveFailures()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new MeshServiceFabricOptions());
        var router = new MeshServiceRouter(_logger, options);
        var service = new FailingService(); // Always throws
        router.RegisterService(service);

        var peerId = "peer-circuit-test";

        // Act: Make 6 calls (circuit opens after 5 failures)
        ServiceReply? lastReply = null;
        for (int i = 0; i < 6; i++)
        {
            var call = new ServiceCall
            {
                ServiceName = "failing-service",
                Method = "Fail",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            };
            lastReply = await router.RouteAsync(call, peerId);
        }

        // Assert: 6th call should be blocked by circuit breaker
        Assert.NotNull(lastReply);
        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
        Assert.Contains("circuit breaker", lastReply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CircuitBreaker_ResetsAfterSuccessfulCall()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new MeshServiceFabricOptions());
        var router = new MeshServiceRouter(_logger, options);
        var service = new IntermittentService();
        router.RegisterService(service);

        var peerId = "peer-intermittent";

        // Act: 4 failures, then 1 success (should reset)
        for (int i = 0; i < 4; i++)
        {
            var call = new ServiceCall
            {
                ServiceName = "intermittent-service",
                Method = "Fail",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            };
            await router.RouteAsync(call, peerId);
        }

        // Success call
        var successCall = new ServiceCall
        {
            ServiceName = "intermittent-service",
            Method = "Succeed",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = Array.Empty<byte>()
        };
        var successReply = await router.RouteAsync(successCall, peerId);

        // Now 4 more failures should NOT open circuit (counter was reset)
        ServiceReply? lastReply = null;
        for (int i = 0; i < 4; i++)
        {
            var call = new ServiceCall
            {
                ServiceName = "intermittent-service",
                Method = "Fail",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            };
            lastReply = await router.RouteAsync(call, peerId);
        }

        // Assert: Should still be getting UnknownError (not circuit breaker)
        Assert.NotNull(lastReply);
        Assert.Equal(ServiceStatusCodes.UnknownError, lastReply.StatusCode);
        Assert.DoesNotContain("circuit breaker", lastReply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ServiceTimeout_TriggersCircuitBreaker()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new MeshServiceFabricOptions
        {
            PerServiceTimeoutSeconds = new()
            {
                ["slow-service"] = 1 // 1 second timeout
            }
        });
        var router = new MeshServiceRouter(_logger, options);
        var service = new SlowService();
        router.RegisterService(service);

        var peerId = "peer-timeout-test";

        // Act: Make 5 slow calls (will timeout and count as failures)
        for (int i = 0; i < 5; i++)
        {
            var call = new ServiceCall
            {
                ServiceName = "slow-service",
                Method = "SlowMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            };
            await router.RouteAsync(call, peerId);
        }

        // 6th call should be blocked by circuit breaker
        var lastCall = new ServiceCall
        {
            ServiceName = "slow-service",
            Method = "SlowMethod",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = Array.Empty<byte>()
        };
        var lastReply = await router.RouteAsync(lastCall, peerId);

        // Assert
        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
        Assert.Contains("circuit breaker", lastReply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultiPeerIsolation_OnePeerRateLimitDoesNotAffectOthers()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new MeshServiceFabricOptions
        {
            GlobalMaxCallsPerPeer = 2
        });
        var router = new MeshServiceRouter(_logger, options);
        var service = new TestService();
        router.RegisterService(service);

        // Act: Peer A exhausts their quota
        for (int i = 0; i < 3; i++)
        {
            var call = new ServiceCall
            {
                ServiceName = "test-service",
                Method = "TestMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            };
            await router.RouteAsync(call, "peer-a-exhausted");
        }

        // Peer B should still work
        var peerBCall = new ServiceCall
        {
            ServiceName = "test-service",
            Method = "TestMethod",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = Array.Empty<byte>()
        };
        var peerBReply = await router.RouteAsync(peerBCall, "peer-b-fresh");

        // Assert: Peer B is not affected by Peer A's rate limit
        Assert.Equal(ServiceStatusCodes.OK, peerBReply.StatusCode);
    }

    // Test service implementations

    private class TestService : IMeshService
    {
        public string ServiceName => "test-service";

        public Task<ServiceReply> HandleCallAsync(
            ServiceCall call,
            MeshServiceContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = Encoding.UTF8.GetBytes("success")
            });
        }

        public Task HandleStreamAsync(MeshServiceStream stream, MeshServiceContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private class FailingService : IMeshService
    {
        public string ServiceName => "failing-service";

        public Task<ServiceReply> HandleCallAsync(
            ServiceCall call,
            MeshServiceContext context,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Service is broken");
        }

        public Task HandleStreamAsync(MeshServiceStream stream, MeshServiceContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private class IntermittentService : IMeshService
    {
        public string ServiceName => "intermittent-service";

        public Task<ServiceReply> HandleCallAsync(
            ServiceCall call,
            MeshServiceContext context,
            CancellationToken cancellationToken)
        {
            if (call.Method == "Fail")
            {
                throw new InvalidOperationException("Intermittent failure");
            }

            return Task.FromResult(new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = Array.Empty<byte>()
            });
        }

        public Task HandleStreamAsync(MeshServiceStream stream, MeshServiceContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private class SlowService : IMeshService
    {
        public string ServiceName => "slow-service";

        public async Task<ServiceReply> HandleCallAsync(
            ServiceCall call,
            MeshServiceContext context,
            CancellationToken cancellationToken)
        {
            // Simulate slow operation (2 seconds, timeout is 1 second)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = Array.Empty<byte>()
            };
        }

        public Task HandleStreamAsync(MeshServiceStream stream, MeshServiceContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

