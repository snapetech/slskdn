// <copyright file="IdentityConfigurationAuditorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Security
{
    using System.Linq;
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
            // Arrange: usernames must not match Soulseek/Mesh/LocalUser; use characters outside those formats
            var options = new Options
            {
                Soulseek = new Options.SoulseekOptions
                {
                    Username = "ab",
                    Password = "soulseek_pass"
                },
                Web = new Options.WebOptions
                {
                    Authentication = new Options.WebOptions.WebAuthenticationOptions
                    {
                        Username = "web#admin",
                        Password = "web_pass"
                    }
                },
                Metrics = new Options.MetricsOptions
                {
                    Authentication = new Options.MetricsOptions.MetricsAuthenticationOptions
                    {
                        Username = "metrics#user",
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
            // Arrange: omit Web/Metrics to avoid default "slskd" being audited; use empty Authentication.Username
            var options = new Options
            {
                Soulseek = new Options.SoulseekOptions
                {
                    Username = "abc123def456", // Resembles LocalUser (alphanumeric, 3–50)
                    Password = "password"
                },
                Web = new Options.WebOptions { Authentication = new Options.WebOptions.WebAuthenticationOptions { Username = "" } },
                Metrics = new Options.MetricsOptions { Authentication = new Options.MetricsOptions.MetricsAuthenticationOptions { Username = "" } }
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
            // Arrange: "ab" avoids Soulseek resembling LocalUser; Metrics.Username="" avoids default "slskd"
            var options = new Options
            {
                Soulseek = new Options.SoulseekOptions
                {
                    Username = "ab",
                    Password = "soulseek_pass"
                },
                Web = new Options.WebOptions
                {
                    Authentication = new Options.WebOptions.WebAuthenticationOptions
                    {
                        Username = "ab", // Same as Soulseek; matches Soulseek identity
                        Password = "web_pass"
                    }
                },
                Metrics = new Options.MetricsOptions { Authentication = new Options.MetricsOptions.MetricsAuthenticationOptions { Username = "" } }
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
            // Arrange: "s" avoids Soulseek resembling LocalUser; Web/Metrics.Username="" avoids default "slskd".
            // Prod emits both "Proxy username matches Soulseek identity" and "Proxy username matches Soulseek username".
            var options = new Options
            {
                Soulseek = new Options.SoulseekOptions
                {
                    Username = "s",
                    Password = "soulseek_pass",
                    Connection = new Options.SoulseekOptions.ConnectionOptions
                    {
                        Proxy = new Options.SoulseekOptions.ConnectionOptions.ProxyOptions
                        {
                            Enabled = true,
                            Username = "s", // Same as Soulseek username
                            Password = "proxy_pass"
                        }
                    }
                },
                Web = new Options.WebOptions { Authentication = new Options.WebOptions.WebAuthenticationOptions { Username = "" } },
                Metrics = new Options.MetricsOptions { Authentication = new Options.MetricsOptions.MetricsAuthenticationOptions { Username = "" } }
            };

            // Act
            var result = IdentityConfigurationAuditor.AuditConfiguration(options, _loggerMock.Object);

            // Assert
            Assert.False(result.IsCompliant);
            var proxyViolations = result.Violations.Where(v => v.Category == "Proxy").ToList();
            Assert.Equal(2, proxyViolations.Count);
            Assert.Contains(proxyViolations, v => v.Issue == "Proxy username matches Soulseek username");
            Assert.Contains(proxyViolations, v => v.Issue == "Proxy username matches Soulseek identity");
        }

        [Fact]
        public void AuditConfiguration_MetricsUsernameLooksLikeLocalUser_ReturnsViolation()
        {
            // Arrange: Web.Username="" avoids default "slskd" being flagged
            var options = new Options
            {
                Web = new Options.WebOptions { Authentication = new Options.WebOptions.WebAuthenticationOptions { Username = "" } },
                Metrics = new Options.MetricsOptions
                {
                    Authentication = new Options.MetricsOptions.MetricsAuthenticationOptions
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


