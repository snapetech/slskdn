// <copyright file="MeshGatewayControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.API.Mesh;
using slskd.Mesh.ServiceFabric;
using Xunit;

public class MeshGatewayControllerTests
{
    [Fact]
    public async Task CallService_WithBlankServiceOrMethod_ReturnsBadRequest()
    {
        var controller = CreateController(new MeshGatewayOptions
        {
            Enabled = true,
            AllowedServices = new() { "pods" }
        });

        var result = await controller.CallService("   ", "  ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CallService_TrimsServiceAndMethodBeforeLookup()
    {
        var directory = new Mock<IMeshServiceDirectory>();
        directory
            .Setup(service => service.FindByNameAsync("pods", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MeshServiceDescriptor
                {
                    ServiceId = "svc-1",
                    ServiceName = "pods",
                    OwnerPeerId = "peer-1",
                    Endpoint = new MeshServiceEndpoint
                    {
                        Protocol = "quic",
                        Host = "127.0.0.1",
                        Port = 1
                    },
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                    Metadata = new Dictionary<string, string>()
                }
            });

        var client = new Mock<IMeshServiceClient>();
        client
            .Setup(service => service.CallServiceAsync("pods", "list", It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceReply
            {
                StatusCode = ServiceStatusCodes.OK,
                Payload = Array.Empty<byte>()
            });

        var controller = CreateController(
            new MeshGatewayOptions
            {
                Enabled = true,
                AllowedServices = new() { "pods" }
            },
            directory,
            client);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        using var requestBody = new MemoryStream();
        controller.HttpContext.Request.Body = requestBody;

        var result = await controller.CallService(" pods ", " list ", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        directory.Verify(service => service.FindByNameAsync("pods", It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(service => service.CallServiceAsync("pods", "list", It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CallService_WhenServiceReplyFails_DoesNotLeakErrorMessage()
    {
        var directory = new Mock<IMeshServiceDirectory>();
        directory
            .Setup(service => service.FindByNameAsync("pods", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MeshServiceDescriptor
                {
                    ServiceId = "svc-1",
                    ServiceName = "pods",
                    OwnerPeerId = "peer-1",
                    Endpoint = new MeshServiceEndpoint
                    {
                        Protocol = "quic",
                        Host = "127.0.0.1",
                        Port = 1
                    },
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                    Metadata = new Dictionary<string, string>()
                }
            });

        var client = new Mock<IMeshServiceClient>();
        client
            .Setup(service => service.CallServiceAsync("pods", "list", It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceReply
            {
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "sensitive detail",
                Payload = Encoding.UTF8.GetBytes("bad")
            });

        var controller = CreateController(
            new MeshGatewayOptions
            {
                Enabled = true,
                AllowedServices = new() { "pods" }
            },
            directory,
            client);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        using var requestBody = new MemoryStream();
        controller.HttpContext.Request.Body = requestBody;

        var result = await controller.CallService("pods", "list", CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Contains("Service returned an error", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task CallService_WhenServiceNotAllowed_DoesNotLeakServiceName()
    {
        var controller = CreateController(new MeshGatewayOptions
        {
            Enabled = true,
            AllowedServices = new() { "pods" }
        });

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        using var requestBody = new MemoryStream();
        controller.HttpContext.Request.Body = requestBody;

        var result = await controller.CallService("secret-service", "list", CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, error.StatusCode);
        Assert.Contains("Requested service is not allowed", error.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("secret-service", error.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallService_WhenNoProvidersFound_DoesNotLeakServiceName()
    {
        var directory = new Mock<IMeshServiceDirectory>();
        directory
            .Setup(service => service.FindByNameAsync("pods", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MeshServiceDescriptor>());

        var controller = CreateController(
            new MeshGatewayOptions
            {
                Enabled = true,
                AllowedServices = new() { "pods" }
            },
            directory);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        using var requestBody = new MemoryStream();
        controller.HttpContext.Request.Body = requestBody;

        var result = await controller.CallService("pods", "list", CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, error.StatusCode);
        Assert.Contains("No providers found for the requested service", error.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("pods", error.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static MeshGatewayController CreateController(
        MeshGatewayOptions options,
        Mock<IMeshServiceDirectory>? directory = null,
        Mock<IMeshServiceClient>? client = null)
    {
        return new MeshGatewayController(
            NullLogger<MeshGatewayController>.Instance,
            Options.Create(options),
            (directory ?? new Mock<IMeshServiceDirectory>()).Object,
            (client ?? new Mock<IMeshServiceClient>()).Object);
    }
}
