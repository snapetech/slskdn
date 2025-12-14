// <copyright file="LoggingHygieneTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Security
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Common.Security;
    using Xunit;

    /// <summary>
    ///     Tests for H-GLOBAL01: Logging and Telemetry Hygiene Audit.
    ///     Ensures that sensitive data is never logged in plain text.
    /// </summary>
    public class LoggingHygieneTests
    {
        private readonly Mock<ILogger<TestLogger>> _loggerMock;
        private readonly TestLogger _testLogger;

        public LoggingHygieneTests()
        {
            _loggerMock = new Mock<ILogger<TestLogger>>();
            _testLogger = new TestLogger(_loggerMock.Object);
        }

        [Fact]
        public void LogFilePath_UsesSanitizedPath()
        {
            // Arrange
            var fullPath = "/home/user/secret/document.pdf";

            // Act
            _testLogger.LogFilePath(fullPath);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("document.pdf") && !v.ToString().Contains("/home/user")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogIpAddress_UsesSanitizedIp()
        {
            // Arrange
            var ip = "192.168.1.100";

            // Act
            _testLogger.LogIpAddress(ip);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => !v.ToString().Contains("192.168.1.100") && v.ToString().Length > 0),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogSensitiveData_UsesRedactedPlaceholder()
        {
            // Arrange
            var sensitiveData = "super-secret-api-key-12345";

            // Act
            _testLogger.LogSensitiveData(sensitiveData);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("[redacted-23-chars]")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogExternalIdentifier_UsesSanitizedIdentifier()
        {
            // Arrange
            var identifier = "user_john_doe_123456";

            // Act
            _testLogger.LogExternalIdentifier(identifier);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("u***6 (18 chars)") && !v.ToString().Contains("user_john_doe_123456")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        /// <summary>
        ///     Test class to verify logging hygiene patterns.
        /// </summary>
        private class TestLogger
        {
            private readonly ILogger<TestLogger> _logger;

            public TestLogger(ILogger<TestLogger> logger)
            {
                _logger = logger;
            }

            public void LogFilePath(string path)
            {
                _logger.LogInformation("Processing file: {SanitizedPath}", LoggingSanitizer.SanitizeFilePath(path));
            }

            public void LogIpAddress(string ip)
            {
                _logger.LogInformation("Connection from: {SanitizedIp}", LoggingSanitizer.SanitizeIpAddress(ip));
            }

            public void LogSensitiveData(string data)
            {
                _logger.LogInformation("Data: {Redacted}", LoggingSanitizer.SanitizeSensitiveData(data));
            }

            public void LogExternalIdentifier(string identifier)
            {
                _logger.LogInformation("User: {SanitizedId}", LoggingSanitizer.SanitizeExternalIdentifier(identifier));
            }
        }
    }
}

