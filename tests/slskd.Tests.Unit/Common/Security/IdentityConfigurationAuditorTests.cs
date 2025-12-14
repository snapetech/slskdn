// <copyright file="IdentityConfigurationAuditorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Security
{
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Common.Security;
    using slskd.Core;
    using Xunit;

    /// <summary>
    ///     Tests for H-ID01: IdentityConfigurationAuditor implementation.
    /// </summary>
    public class IdentityConfigurationAuditorTests
    {
        private readonly Mock<ILogger> _loggerMock;

        public IdentityConfigurationAuditorTests()
        {
            _loggerMock = new Mock<ILogger>();
        }

        [Fact]
        public void AuditConfiguration_CompliantConfiguration_ReturnsValidResult()
        {
            // Arrange
            var options = new Options
            {
                Soulseek = new Options.SoulseekOptions
                {
                    Username = "soulseek_user",
                    Password = "soulseek_pass"
                },
                Web = new Options.WebOptions
                {
                    Auth = new Options.WebOptions.WebAuthenticationOptions
                    {
                        Username = "web_admin",
                        Password = "web_pass"
                    }
                },
                Metrics = new Options.MetricsOptions
                {
                    Auth = new Options.MetricsOptions.MetricsAuthenticationOptions
                    {
                        Username = "metrics_user",
                        Password = "metrics_pass"
                    }
                }
            };

            // Act
            var result = IdentityConfigurationAuditor.AuditConfiguration(options, _loggerMock.Object);

            // Assert
            Assert.True(result.IsCompliant);
            Assert.Empty(result.Violations);
        }

        [Fact]
        public void AuditConfiguration_SoulseekUsernameLooksLikeMeshId_ReturnsViolation()
        {
            // Arrange
            var options = new Options
            {
                Soulseek = new Options.SoulseekOptions
                {
                    Username = "abc123def456", // Looks like a mesh ID
                    Password = "password"
                }
            };

            // Act
            var result = IdentityConfigurationAuditor.AuditConfiguration(options, _loggerMock.Object);

            // Assert
            Assert.False(result.IsCompliant);
            Assert.Single(result.Violations);
            Assert.Equal("Soulseek", result.Violations[0].Category);
            Assert.Contains("resembles other identity type", result.Violations[0].Issue);
        }

        [Fact]
        public void AuditConfiguration_WebUsernameMatchesSoulseekUsername_ReturnsViolation()
        {
            // Arrange
            var options = new Options
            {
                Soulseek = new Options.SoulseekOptions
                {
                    Username = "shared_user",
                    Password = "soulseek_pass"
                },
                Web = new Options.WebOptions
                {
                    Auth = new Options.WebOptions.WebAuthenticationOptions
                    {
                        Username = "shared_user", // Same as Soulseek
                        Password = "web_pass"
                    }
                }
            };

            // Act
            var result = IdentityConfigurationAuditor.AuditConfiguration(options, _loggerMock.Object);

            // Assert
            Assert.False(result.IsCompliant);
            Assert.Single(result.Violations);
            Assert.Equal("WebAuth", result.Violations[0].Category);
            Assert.Contains("matches other identity type", result.Violations[0].Issue);
        }

        [Fact]
        public void AuditConfiguration_ProxyUsernameMatchesSoulseekUsername_ReturnsViolation()
        {
            // Arrange
            var options = new Options
            {
                Soulseek = new Options.SoulseekOptions
                {
                    Username = "soulseek_user",
                    Password = "soulseek_pass",
                    Connection = new Options.SoulseekOptions.ConnectionOptions
                    {
                        Proxy = new Options.SoulseekOptions.ConnectionOptions.ProxyOptions
                        {
                            Enabled = true,
                            Username = "soulseek_user", // Same as Soulseek username
                            Password = "proxy_pass"
                        }
                    }
                }
            };

            // Act
            var result = IdentityConfigurationAuditor.AuditConfiguration(options, _loggerMock.Object);

            // Assert
            Assert.False(result.IsCompliant);
            Assert.Single(result.Violations);
            Assert.Equal("Proxy", result.Violations[0].Category);
            Assert.Contains("Proxy username matches Soulseek username", result.Violations[0].Issue);
        }

        [Fact]
        public void AuditConfiguration_MetricsUsernameLooksLikeLocalUser_ReturnsViolation()
        {
            // Arrange
            var options = new Options
            {
                Metrics = new Options.MetricsOptions
                {
                    Auth = new Options.MetricsOptions.MetricsAuthenticationOptions
                    {
                        Username = "admin@localhost", // Looks like local user
                        Password = "metrics_pass"
                    }
                }
            };

            // Act
            var result = IdentityConfigurationAuditor.AuditConfiguration(options, _loggerMock.Object);

            // Assert
            Assert.False(result.IsCompliant);
            Assert.Single(result.Violations);
            Assert.Equal("Metrics", result.Violations[0].Category);
            Assert.Contains("matches other identity type", result.Violations[0].Issue);
        }
    }
}
