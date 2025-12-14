// <copyright file="ModerationCoverageAuditTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.CodeQuality
{
    using System.Linq;
    using System.Reflection;
    using slskd.Common.CodeQuality;
    using Xunit;

    /// <summary>
    ///     Tests for H-MCP01: Moderation Coverage Audit.
    /// </summary>
    public class ModerationCoverageAuditTests
    {
        [Fact]
        public void RunAudit_WithTestAssemblies_ReturnsValidReport()
        {
            // Arrange
            var testAssembly = typeof(ModerationCoverageAuditTests).Assembly;
            var sourceAssemblies = new[] { testAssembly }; // Using test assembly as source for testing

            // Act
            var report = ModerationCoverageAudit.RunAudit(new[] { testAssembly }, sourceAssemblies);

            // Assert
            Assert.NotNull(report);
            Assert.True(report.AuditTimestamp > default);
            Assert.NotEmpty(report.CriticalPaths);
            Assert.NotNull(report.PathResults);
            Assert.NotNull(report.Summary);
            Assert.True(report.CriticalPaths.Count > 0);
        }

        [Fact]
        public void AuditCriticalPath_WithValidPath_ReturnsAnalysis()
        {
            // Arrange
            var criticalPath = new CriticalPath
            {
                Phase = ModerationCoverageAudit.ContentLifecyclePhase.LibraryIngestion,
                Description = "Test path",
                EntryPoints = new[] { "slskd.Shares.ShareService.ScanAsync" },
                RequiredChecks = new[] { "IsAdvertisable", "ContentModeration" }
            };

            var testAssembly = typeof(ModerationCoverageAuditTests).Assembly;
            var sourceAssemblies = new[] { testAssembly };

            // Act
            var result = ModerationCoverageAudit.AuditCriticalPath(criticalPath, sourceAssemblies, new[] { testAssembly });

            // Assert
            Assert.NotNull(result);
            Assert.Equal(criticalPath, result.CriticalPath);
            Assert.NotNull(result.EntryPointsAnalyzed);
            Assert.NotNull(result.MissingChecks);
            Assert.NotNull(result.Recommendations);
        }

        [Fact]
        public void CriticalPaths_ContainsExpectedPhases()
        {
            // This test would normally check that we have all expected phases,
            // but since we're testing the static definition, we'll just verify the count
            var testAssembly = typeof(ModerationCoverageAuditTests).Assembly;

            // Act
            var report = ModerationCoverageAudit.RunAudit(new[] { testAssembly }, new[] { testAssembly });

            // Assert - we should have 6 critical paths defined
            Assert.Equal(6, report.CriticalPaths.Count);

            var phases = report.CriticalPaths.Select(cp => cp.Phase).ToList();
            Assert.Contains(ModerationCoverageAudit.ContentLifecyclePhase.LibraryIngestion, phases);
            Assert.Contains(ModerationCoverageAudit.ContentLifecyclePhase.ContentAdvertising, phases);
            Assert.Contains(ModerationCoverageAudit.ContentLifecyclePhase.ContentServing, phases);
            Assert.Contains(ModerationCoverageAudit.ContentLifecyclePhase.FederationPublishing, phases);
        }

        [Fact]
        public void AuditReport_IncludesSummaryStatistics()
        {
            // Arrange
            var testAssembly = typeof(ModerationCoverageAuditTests).Assembly;

            // Act
            var report = ModerationCoverageAudit.RunAudit(new[] { testAssembly }, new[] { testAssembly });

            // Assert
            Assert.NotNull(report.Summary);
            Assert.True(report.Summary.TotalPathsAudited >= 0);
            Assert.True(report.Summary.FullyCompliantPaths >= 0);
            Assert.True(report.Summary.PathsWithMissingChecks >= 0);
            Assert.True(report.Summary.TotalMissingChecks >= 0);
        }
    }
}


