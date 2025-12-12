using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Common.Security;
using slskd.Mesh.ServiceFabric;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

/// <summary>
/// Unit tests for MeshServiceRouter.
/// </summary>
public class MeshServiceRouterTests
{
    private readonly Mock<ILogger<MeshServiceRouter>> _loggerMock;
    private readonly MeshServiceFabricOptions _options;
    private readonly MeshServiceRouter _router;

    public MeshServiceRouterTests()
    {
        _loggerMock = new Mock<ILogger<MeshServiceRouter>>();
        _options = new MeshServiceFabricOptions();
        
        var optionsMock = new Mock<IOptions<MeshServiceFabricOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);
        
        // Pass null for violation tracker since it's optional
        _router = new MeshServiceRouter(
            _loggerMock.Object,
            optionsMock.Object,
            violationTracker: null);
    }

    [Fact]
    public void RegisterService_WithValidService_Succeeds()
    {
        // Arrange
        var service = new TestService("test-service");

        // Act
        _router.RegisterService(service);
        var stats = _router.GetStats();

        // Assert
        Assert.Equal(1, stats.RegisteredServiceCount);
    }

    [Fact]
    public void RegisterService_WithNullService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _router.RegisterService(null!));
    }

    [Fact]
    public void RegisterService_WithEmptyServiceName_ThrowsArgumentException()
    {
        // Arrange
        var service = new TestService("");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _router.RegisterService(service));
    }

    [Fact]
    public async Task RouteAsync_WithNullCall_ReturnsInvalidPayloadError()
    {
        // Act
        var reply = await _router.RouteAsync(null!, "peer1");

        // Assert
        Assert.Equal(ServiceStatusCodes.InvalidPayload, reply.StatusCode);
        Assert.Contains("Null call", reply.ErrorMessage);
    }

    [Fact]
    public async Task RouteAsync_WithEmptyServiceName_ReturnsInvalidPayloadError()
    {
        // Arrange
        var call = new ServiceCall
        {
            ServiceName = "",
            Method = "Test",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var reply = await _router.RouteAsync(call, "peer1");

        // Assert
        Assert.Equal(ServiceStatusCodes.InvalidPayload, reply.StatusCode);
    }

    [Fact]
    public async Task RouteAsync_WithOversizedPayload_ReturnsPayloadTooLargeError()
    {
        // Arrange
        _options.MaxDescriptorBytes = 100;
        var call = new ServiceCall
        {
            ServiceName = "test",
            Method = "Test",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = new byte[200]
        };

        // Act
        var reply = await _router.RouteAsync(call, "peer1");

        // Assert
        Assert.Equal(ServiceStatusCodes.PayloadTooLarge, reply.StatusCode);
    }

    [Fact]
    public async Task RouteAsync_WithUnregisteredService_ReturnsServiceNotFoundError()
    {
        // Arrange
        var call = new ServiceCall
        {
            ServiceName = "nonexistent",
            Method = "Test",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var reply = await _router.RouteAsync(call, "peer1");

        // Assert
        Assert.Equal(ServiceStatusCodes.ServiceNotFound, reply.StatusCode);
        Assert.Contains("not found", reply.ErrorMessage);
    }

    [Fact]
    public async Task RouteAsync_WithRegisteredService_CallsHandleCallAsync()
    {
        // Arrange
        var service = new TestService("test-service");
        _router.RegisterService(service);
        
        var call = new ServiceCall
        {
            ServiceName = "test-service",
            Method = "Echo",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = new byte[] { 1, 2, 3 }
        };

        // Act
        var reply = await _router.RouteAsync(call, "peer1");

        // Assert
        Assert.Equal(ServiceStatusCodes.OK, reply.StatusCode);
        Assert.Equal(call.CorrelationId, reply.CorrelationId);
        Assert.True(service.HandleCallAsyncWasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenServiceThrows_ReturnsUnknownError()
    {
        // Arrange
        var service = new TestService("test-service", throwOnHandle: true);
        _router.RegisterService(service);
        
        var call = new ServiceCall
        {
            ServiceName = "test-service",
            Method = "Echo",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var reply = await _router.RouteAsync(call, "peer1");

        // Assert
        Assert.Equal(ServiceStatusCodes.UnknownError, reply.StatusCode);
        Assert.Contains("Internal service error", reply.ErrorMessage);
    }

    [Fact]
    public void UnregisterService_WithRegisteredService_ReturnsTrue()
    {
        // Arrange
        var service = new TestService("test-service");
        _router.RegisterService(service);

        // Act
        var result = _router.UnregisterService("test-service");
        var stats = _router.GetStats();

        // Assert
        Assert.True(result);
        Assert.Equal(0, stats.RegisteredServiceCount);
    }

    [Fact]
    public void UnregisterService_WithUnregisteredService_ReturnsFalse()
    {
        // Act
        var result = _router.UnregisterService("nonexistent");

        // Assert
        Assert.False(result);
    }

    // Test service implementation
    private class TestService : IMeshService
    {
        private readonly bool _throwOnHandle;

        public TestService(string serviceName, bool throwOnHandle = false)
        {
            ServiceName = serviceName;
            _throwOnHandle = throwOnHandle;
        }

        public string ServiceName { get; }
        public bool HandleCallAsyncWasCalled { get; private set; }

        public Task<ServiceReply> HandleCallAsync(
            ServiceCall call,
            MeshServiceContext context,
            CancellationToken cancellationToken = default)
        {
            HandleCallAsyncWasCalled = true;

            if (_throwOnHandle)
            {
                throw new InvalidOperationException("Test exception");
            }

            return Task.FromResult(new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = call.Payload
            });
        }

        public Task HandleStreamAsync(
            MeshServiceStream stream,
            MeshServiceContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Streaming not supported in test service");
        }
    }
}

