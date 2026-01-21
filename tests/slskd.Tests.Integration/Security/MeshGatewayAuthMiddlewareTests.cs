// <copyright file="MeshGatewayAuthMiddlewareTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.Security;

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using slskd.Mesh.ServiceFabric;

/// <summary>
/// Unit tests for MeshGatewayAuthMiddleware configuration validation.
/// Note: Full middleware integration tests require a running application instance.
/// These tests verify the MeshGatewayOptions validation logic.
/// </summary>
public class MeshGatewayAuthMiddlewareTests
{
    [Fact]
    public void MeshGatewayOptions_Disabled_IsValid()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = false
        };

        // Act
        var (isValid, error) = options.Validate();

        // Assert
        Assert.True(isValid, "Disabled gateway should be valid");
        Assert.Null(error);
    }

    [Fact]
    public void MeshGatewayOptions_Enabled_WithoutAllowedServices_IsInvalid()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            AllowedServices = new System.Collections.Generic.List<string>()
        };

        // Act
        var (isValid, error) = options.Validate();

        // Assert
        Assert.False(isValid, "Enabled gateway without allowed services should be invalid");
        Assert.NotNull(error);
        Assert.Contains("AllowedServices", error);
    }

    [Fact]
    public void MeshGatewayOptions_Enabled_NonLocalhost_WithoutApiKey_IsInvalid()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            BindAddress = "0.0.0.0",
            ApiKey = null,
            AllowedServices = new System.Collections.Generic.List<string> { "test-service" }
        };

        // Act
        var (isValid, error) = options.Validate();

        // Assert
        Assert.False(isValid, "Non-localhost binding without API key should be invalid");
        Assert.NotNull(error);
        Assert.Contains("ApiKey", error);
    }

    [Fact]
    public void MeshGatewayOptions_Enabled_Localhost_WithAllowedServices_IsValid()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            BindAddress = "127.0.0.1",
            AllowedServices = new System.Collections.Generic.List<string> { "test-service" }
        };

        // Act
        var (isValid, error) = options.Validate();

        // Assert
        Assert.True(isValid, "Localhost binding with allowed services should be valid");
        Assert.Null(error);
    }
}
