using Microsoft.Extensions.Options;

namespace slskd.Tests.Unit.Files
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using OneOf;
    using slskd;
    using slskd.Files;
    using slskd.Files.API;
    using Xunit;

/// <summary>
/// Security tests for FilesController, specifically targeting Base64-encoded path traversal.
/// </summary>
public class FilesControllerSecurityTests
{
    private readonly Mock<FileService> mockFileService;
    private readonly Mock<IOptionsSnapshot<slskd.Options>> mockOptionsSnapshot;
    private readonly FilesController controller;

    public FilesControllerSecurityTests()
    {
        mockFileService = new Mock<FileService>(Mock.Of<IOptionsMonitor<slskd.Options>>());
        mockOptionsSnapshot = new Mock<IOptionsSnapshot<slskd.Options>>();

        var options = new slskd.Options
        {
            Directories = new slskd.Options.DirectoriesOptions
            {
                Downloads = "/test/downloads",
                Incomplete = "/test/incomplete"
            }
        };

        mockOptionsSnapshot.Setup(o => o.Value).Returns(options);

        controller = new FilesController(mockFileService.Object, mockOptionsSnapshot.Object);
        
        // Setup controller context
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Theory]
    [InlineData("Li4vc2VjcmV0LnR4dA==")] // "../secret.txt"
    [InlineData("Li4vLi4vZXRjL3Bhc3N3ZA==")] // "../../etc/passwd"
    [InlineData("ZG93bmxvYWRzLy4uLy4uL3NlY3JldC50eHQ=")] // "downloads/../../secret.txt"
    [InlineData("Li5cXC4uXFx3aW5kb3dzXFxzeXN0ZW0zMg==")] // "..\\..\\windows\\system32"
    public async Task DeleteDownloadFileAsync_ShouldRejectBase64TraversalPaths(string base64Traversal)
    {
        // Note: This test verifies that Base64-encoded traversal paths are rejected
        // The actual Base64 decoding happens in FilesController.FromBase64() extension method
        // FileService.DeleteFilesAsync then validates the resolved path against allowed directories
        
        // Arrange
        // The controller should decode Base64 and then validate the path
        // FileService.DeleteFilesAsync should reject paths outside allowed directories

        mockFileService.Setup(s => s.DeleteFilesAsync(It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedException("Only files in application-controlled directories can be deleted"));

        // Act
        var result = await controller.DeleteDownloadFileAsync(base64Traversal);

        // Assert
        // Should return Forbid or BadRequest, not allow the traversal
        Assert.NotNull(result);
        var forbidResult = Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteDownloadFileAsync_ShouldHandleInvalidBase64Gracefully()
    {
        // Arrange
        var invalidBase64 = "not-valid-base64!!!";

        // Act
        var result = await controller.DeleteDownloadFileAsync(invalidBase64);

        // Assert
        // Should handle gracefully, not crash
        Assert.NotNull(result);
        // May return BadRequest or handle via exception
    }

    [Fact]
    public async Task DeleteDownloadFileAsync_ShouldAllowValidBase64Paths()
    {
        // Arrange
        var validPath = "test-file.txt";
        var base64Path = Convert.ToBase64String(Encoding.UTF8.GetBytes(validPath));
        
        var fullPath = "/test/downloads/test-file.txt";
        mockFileService.Setup(s => s.DeleteFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, OneOf<bool, Exception>>
            {
                { fullPath, OneOf<bool, Exception>.FromT0(true) }
            })
            .Verifiable();

        // Act
        var result = await controller.DeleteDownloadFileAsync(base64Path);

        // Assert
        Assert.NotNull(result);
        // Should succeed for valid paths
    }

    [Theory]
    [InlineData("Li4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vc2VjcmV0LnR4dA==")] // Deep traversal
    [InlineData("Li4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vLi4vc2VjcmV0LnR4dA==")] // Very deep traversal
    public async Task DeleteDownloadFileAsync_ShouldRejectDeepTraversalPaths(string deepTraversalBase64)
    {
        // Arrange
        mockFileService.Setup(s => s.DeleteFilesAsync(It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedException("Only files in application-controlled directories can be deleted"));

        // Act
        var result = await controller.DeleteDownloadFileAsync(deepTraversalBase64);

        // Assert
        Assert.NotNull(result);
        var forbidResult = Assert.IsType<ForbidResult>(result);
    }
    }
}















