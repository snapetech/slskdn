using Microsoft.Extensions.Options;

namespace slskd.Tests.Unit.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using OneOf;
    using slskd;
    using slskd.Files;
    using Xunit;

/// <summary>
/// Security tests for FileService, specifically targeting directory traversal vulnerabilities.
/// </summary>
public class FileServiceSecurityTests
{
    private readonly Mock<ILogger<FileService>> mockLogger;
    private readonly Mock<IOptionsMonitor<slskd.Options>> mockOptionsMonitor;
    private readonly FileService fileService;
    private readonly string testDownloadDir;
    private readonly string testIncompleteDir;

    public FileServiceSecurityTests()
    {
        mockLogger = new Mock<ILogger<FileService>>();
        mockOptionsMonitor = new Mock<IOptionsMonitor<Options>>();
        
        // Create temporary test directories
        testDownloadDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "downloads");
        testIncompleteDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "incomplete");
        
        Directory.CreateDirectory(testDownloadDir);
        Directory.CreateDirectory(testIncompleteDir);

        var options = new slskd.Options
        {
            Directories = new slskd.Options.DirectoriesOptions
            {
                Downloads = testDownloadDir,
                Incomplete = testIncompleteDir
            }
        };

        mockOptionsMonitor.Setup(o => o.CurrentValue).Returns(options);
        
        fileService = new FileService(mockOptionsMonitor.Object);
    }

    [Fact]
    public async Task DeleteFilesAsync_ShouldRejectRelativePaths()
    {
        // Arrange
        var relativePath = "../secret-file.txt";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fileService.DeleteFilesAsync(relativePath));
    }

    [Fact]
    public async Task DeleteFilesAsync_ShouldRejectPathsOutsideAllowedDirectories()
    {
        // Arrange
        var outsidePath = Path.Combine(Path.GetTempPath(), "outside-file.txt");
        File.Create(outsidePath).Close();

        try
        {
        // Act & Assert
        await Assert.ThrowsAsync<slskd.UnauthorizedException>(() =>
            fileService.DeleteFilesAsync(outsidePath));
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task DeleteFilesAsync_ShouldRejectPathsWithTraversalAfterGetFullPath()
    {
        // Arrange
        // Create a file in downloads
        var allowedFile = Path.Combine(testDownloadDir, "allowed.txt");
        File.Create(allowedFile).Close();

        // Try to use .. traversal to escape
        var traversalPath = Path.Combine(testDownloadDir, "..", "..", "secret.txt");
        var resolvedPath = Path.GetFullPath(traversalPath);

        // Act & Assert
        // Should reject because resolved path is outside allowed directories
        await Assert.ThrowsAsync<slskd.UnauthorizedException>(() =>
            fileService.DeleteFilesAsync(resolvedPath));

        // Cleanup
        File.Delete(allowedFile);
    }

    [Fact]
    public async Task DeleteFilesAsync_ShouldAllowValidPathsInAllowedDirectories()
    {
        // Arrange
        var allowedFile = Path.Combine(testDownloadDir, "test-file.txt");
        File.Create(allowedFile).Close();

        try
        {
            // Act
            var result = await fileService.DeleteFilesAsync(allowedFile);

            // Assert
            Assert.True(result.ContainsKey(allowedFile));
            var success = result[allowedFile].Match(
                ok => ok,
                ex => throw ex);
            Assert.True(success);
            Assert.False(File.Exists(allowedFile));
        }
        catch
        {
            // Cleanup on failure
            if (File.Exists(allowedFile))
                File.Delete(allowedFile);
            throw;
        }
    }

    [Fact]
    public async Task DeleteFilesAsync_ShouldRejectMixedValidAndInvalidPaths()
    {
        // Arrange
        var allowedFile = Path.Combine(testDownloadDir, "allowed.txt");
        File.Create(allowedFile).Close();
        
        var outsidePath = Path.Combine(Path.GetTempPath(), "outside.txt");
        File.Create(outsidePath).Close();

        try
        {
        // Act & Assert
        // Should reject entire request if any path is invalid
        await Assert.ThrowsAsync<slskd.UnauthorizedException>(() =>
            fileService.DeleteFilesAsync(allowedFile, outsidePath));
        }
        finally
        {
            File.Delete(allowedFile);
            File.Delete(outsidePath);
        }
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("downloads/../../secret.txt")]
    [InlineData("downloads/..\\..\\secret.txt")]
    public async Task DeleteFilesAsync_ShouldRejectVariousTraversalPatterns(string traversalPath)
    {
        // Arrange
        var absoluteTraversal = Path.GetFullPath(Path.Combine(testDownloadDir, traversalPath));

        // Act & Assert
        await Assert.ThrowsAsync<slskd.UnauthorizedException>(() =>
            fileService.DeleteFilesAsync(absoluteTraversal));
    }
    }
}
