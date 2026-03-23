// <copyright file="ApiLibraryHealthControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.LibraryHealth;

using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.LibraryHealth;
using ApiLibraryHealthController = slskd.LibraryHealth.API.LibraryHealthController;
using Xunit;

public class ApiLibraryHealthControllerTests
{
    [Fact]
    public async Task GetScanStatus_WhenScanMissing_ReturnsSanitizedNotFound()
    {
        var healthService = new Mock<ILibraryHealthService>();
        healthService
            .Setup(service => service.GetScanStatusAsync("scan-123", default))
            .ReturnsAsync((LibraryHealthScan?)null);

        var controller = new ApiLibraryHealthController(
            healthService.Object,
            NullLogger<ApiLibraryHealthController>.Instance);

        var result = await controller.GetScanStatus("scan-123", default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains("Scan not found", notFound.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("scan-123", notFound.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
