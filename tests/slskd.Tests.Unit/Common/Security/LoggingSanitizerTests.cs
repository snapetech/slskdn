// <copyright file="LoggingSanitizerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Security
{
    using System.Net;
    using slskd.Common.Security;
    using Xunit;

    /// <summary>
    ///     Tests for H-GLOBAL01: LoggingSanitizer implementation.
    /// </summary>
    public class LoggingSanitizerTests
    {
        [Fact]
        public void SanitizeFilePath_WithFullPath_ReturnsOnlyFilename()
        {
            // Arrange
            var fullPath = "/home/user/documents/secret.pdf";

            // Act
            var result = LoggingSanitizer.SanitizeFilePath(fullPath);

            // Assert
            Assert.Equal("secret.pdf", result);
        }

        [Fact]
        public void SanitizeFilePath_WithWindowsPath_ReturnsOnlyFilename()
        {
            // Arrange
            var fullPath = @"C:\Users\user\Desktop\confidential.docx";

            // Act
            var result = LoggingSanitizer.SanitizeFilePath(fullPath);

            // Assert
            Assert.Equal("confidential.docx", result);
        }

        [Fact]
        public void SanitizeFilePath_WithEmptyPath_ReturnsPlaceholder()
        {
            // Act
            var result = LoggingSanitizer.SanitizeFilePath(string.Empty);

            // Assert
            Assert.Equal("[empty]", result);
        }

        [Fact]
        public void SanitizeIpAddress_WithValidIp_ReturnsHashedValue()
        {
            // Arrange
            var ip = "192.168.1.100";

            // Act
            var result = LoggingSanitizer.SanitizeIpAddress(ip);

            // Assert
            Assert.NotEqual(ip, result);
            Assert.Equal(16, result.Length); // Should be 16 chars (8 bytes as hex)
            Assert.Matches("^[a-f0-9]{16}$", result);
        }

        [Fact]
        public void SanitizeIpAddress_WithIpAddressObject_ReturnsHashedValue()
        {
            // Arrange
            var ip = IPAddress.Parse("10.0.0.1");

            // Act
            var result = LoggingSanitizer.SanitizeIpAddress(ip);

            // Assert
            Assert.NotEqual("10.0.0.1", result);
            Assert.Equal(16, result.Length);
        }

        [Fact]
        public void SanitizeExternalIdentifier_WithLongIdentifier_ReturnsSanitized()
        {
            // Arrange
            var identifier = "john_doe_12345";

            // Act
            var result = LoggingSanitizer.SanitizeExternalIdentifier(identifier);

            // Assert
            Assert.Equal("j***5 (13 chars)", result);
        }

        [Fact]
        public void SanitizeExternalIdentifier_WithShortIdentifier_ReturnsSanitized()
        {
            // Arrange
            var identifier = "ab";

            // Act
            var result = LoggingSanitizer.SanitizeExternalIdentifier(identifier);

            // Assert
            Assert.Equal("a* (2 chars)", result);
        }

        [Fact]
        public void SanitizeHash_WithLongHash_ReturnsTruncated()
        {
            // Arrange
            var hash = "a1b2c3d4e5f678901234567890abcdef1234567890abcdef";

            // Act
            var result = LoggingSanitizer.SanitizeHash(hash);

            // Assert
            Assert.Equal("a1b2c3d4...bcdef123", result);
        }

        [Fact]
        public void SanitizeHash_WithShortHash_ReturnsUnchanged()
        {
            // Arrange
            var hash = "abc123";

            // Act
            var result = LoggingSanitizer.SanitizeHash(hash);

            // Assert
            Assert.Equal("abc123", result);
        }

        [Fact]
        public void SanitizeUrl_WithFullUrl_ReturnsSchemeAndHostOnly()
        {
            // Arrange
            var url = "https://api.example.com/users/12345/profile?token=secret";

            // Act
            var result = LoggingSanitizer.SanitizeUrl(url);

            // Assert
            Assert.Equal("https://api.example.com", result);
        }

        [Fact]
        public void SanitizeSensitiveData_WithData_ReturnsRedactedPlaceholder()
        {
            // Arrange
            var data = "super-secret-token-12345";

            // Act
            var result = LoggingSanitizer.SanitizeSensitiveData(data);

            // Assert
            Assert.Equal("[redacted-23-chars]", result);
        }

        [Fact]
        public void SafeContext_CreatesSafeLoggingObject()
        {
            // Arrange
            var identifier = "sensitive-user-id-123";

            // Act
            var result = LoggingSanitizer.SafeContext("user", identifier);

            // Assert
            var context = result as dynamic;
            Assert.Equal("user", context.Context);
            Assert.Equal("s***3 (19 chars)", context.Id);
        }
    }
}

