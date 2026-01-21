// <copyright file="SecurityMiddlewareTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.Security;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using slskd.Common.Security;

/// <summary>
/// Integration tests for SecurityMiddleware path traversal protection.
/// Note: Full middleware integration tests require a running application instance.
/// These tests verify the PathGuard component which is used by SecurityMiddleware.
/// </summary>
public class SecurityMiddlewareTests
{
    [Fact]
    public void PathGuard_PlainTraversal_Detects()
    {
        // Arrange & Act
        var hasTraversal = PathGuard.ContainsTraversal("/api/../etc/passwd");

        // Assert
        Assert.True(hasTraversal, "PathGuard should detect plain path traversal");
    }

    [Fact]
    public void PathGuard_UrlEncodedTraversal_Detects()
    {
        // Arrange & Act
        var hasTraversal = PathGuard.ContainsTraversal("/api/..%2F..%2Fetc%2Fpasswd");

        // Assert
        Assert.True(hasTraversal, "PathGuard should detect URL-encoded path traversal");
    }

    [Fact]
    public void PathGuard_NormalPath_Allows()
    {
        // Arrange & Act
        var hasTraversal = PathGuard.ContainsTraversal("/api/v0/application");

        // Assert
        Assert.False(hasTraversal, "PathGuard should allow normal paths");
    }

    [Fact]
    public void PathGuard_DoubleEncodedTraversal_Detects()
    {
        // Arrange & Act - double URL encoding: %252e%252e = %2e%2e = ..
        var hasTraversal = PathGuard.ContainsTraversal("/api/%252e%252e/etc/passwd");

        // Assert
        Assert.True(hasTraversal, "PathGuard should detect double-encoded path traversal");
    }
}
