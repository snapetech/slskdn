// <copyright file="RealmMigrationToolTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Migration
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Mesh.Realm.Migration;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-05: RealmMigrationTool.
    /// </summary>
    public class RealmMigrationToolTests : IDisposable
    {
        private readonly Mock<ILogger<RealmMigrationTool>> _loggerMock = new();
        private readonly string _tempDirectory;
        private readonly RealmMigrationTool _tool;

        public RealmMigrationToolTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
            _tool = new RealmMigrationTool(_loggerMock.Object);
        }

        [Fact]
        public async Task ExportPodDataAsync_WithValidPath_CreatesExportStructure()
        {
            // Arrange
            var exportPath = Path.Combine(_tempDirectory, "export-test");

            // Act
            var result = await _tool.ExportPodDataAsync(exportPath);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(exportPath, result.ExportPath);
            Assert.True(Directory.Exists(exportPath));
            Assert.True(result.IncludedData.Any(x => x.Contains("Pod configuration", StringComparison.Ordinal)));
            Assert.True(result.IncludedData.Any(x => x.Contains("User preferences", StringComparison.Ordinal)));
            Assert.True(result.IncludedData.Any(x => x.Contains("Content metadata", StringComparison.Ordinal)));
            Assert.True(result.IncludedData.Any(x => x.Contains("Public social connection metadata", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task ExportPodDataAsync_WithSensitiveData_IncludesWarnings()
        {
            // Arrange
            var exportPath = Path.Combine(_tempDirectory, "export-sensitive");

            // Act
            var result = await _tool.ExportPodDataAsync(exportPath, includeSensitiveData: true);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.Warnings.Any(w => w.Contains("Sensitive data export requested", StringComparison.Ordinal)));
            Assert.True(result.IncludedData.Any(x => x.Contains("Sensitive authentication data", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task ExportPodDataAsync_WithoutSensitiveData_ExcludesSensitiveData()
        {
            // Arrange
            var exportPath = Path.Combine(_tempDirectory, "export-no-sensitive");

            // Act
            var result = await _tool.ExportPodDataAsync(exportPath, includeSensitiveData: false);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.ExcludedData.Any(x => x.Contains("Private keys and sensitive authentication data", StringComparison.Ordinal)));
            Assert.True(result.ExcludedData.Any(x => x.Contains("Encrypted user credentials", StringComparison.Ordinal)));
            Assert.True(result.Warnings.Any(w => w.Contains("Sensitive data not exported", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task ImportPodDataAsync_WithValidExport_Succeeds()
        {
            // Arrange
            var exportPath = Path.Combine(_tempDirectory, "import-test");
            var exportResult = await _tool.ExportPodDataAsync(exportPath);
            Assert.True(exportResult.Success);

            var importPath = Path.Combine(_tempDirectory, "import-from-export");

            // Act
            var importResult = await _tool.ImportPodDataAsync(exportPath, "target-realm");

            // Assert
            Assert.True(importResult.Success);
            Assert.Equal("target-realm", importResult.TargetRealmId);
            Assert.True(importResult.ImportedData.Any(x => x.Contains("Pod configuration", StringComparison.Ordinal)));
            Assert.True(importResult.ImportedData.Any(x => x.Contains("User preferences", StringComparison.Ordinal)));
            Assert.True(importResult.ImportedData.Any(x => x.Contains("Content catalog", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task ImportPodDataAsync_WithNonexistentPath_Fails()
        {
            // Arrange
            var nonexistentPath = Path.Combine(_tempDirectory, "nonexistent");

            // Act
            var result = await _tool.ImportPodDataAsync(nonexistentPath, "target-realm");

            // Assert
            Assert.False(result.Success);
            Assert.True(result.Errors.Any(e => e.Contains("does not exist", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task ImportPodDataAsync_WithCrossRealmImport_IncludesWarnings()
        {
            // Arrange - Create export with one realm
            var exportPath = Path.Combine(_tempDirectory, "cross-realm-export");
            await _tool.ExportPodDataAsync(exportPath);

            // Act - Import to different realm
            var result = await _tool.ImportPodDataAsync(exportPath, "different-realm");

            // Assert
            Assert.True(result.Warnings.Any(w => w.Contains("Cross-realm data import", StringComparison.Ordinal)));
        }

        [Fact]
        public void GenerateMigrationGuide_WithValidRealms_CreatesComprehensiveGuide()
        {
            // Arrange
            var currentRealm = "production-realm";
            var targetRealm = "new-production-realm";

            // Act
            var guide = _tool.GenerateMigrationGuide(currentRealm, targetRealm);

            // Assert
            Assert.Equal(currentRealm, guide.CurrentRealmId);
            Assert.Equal(targetRealm, guide.TargetRealmId);
            Assert.True(guide.Steps.Count > 5);
            Assert.True(guide.Prerequisites.Count > 2);
            Assert.True(guide.PostMigrationTasks.Count > 2);
            Assert.True(guide.Warnings.Count > 3);

            // Verify step ordering
            for (var i = 0; i < guide.Steps.Count; i++)
            {
                Assert.Equal(i + 1, guide.Steps[i].Order);
            }

            // Verify total duration calculation
            Assert.True(guide.TotalEstimatedDuration > TimeSpan.Zero);
        }

        [Fact]
        public void GenerateMigrationGuide_WithCrossRealmMigration_IncludesExtraStep()
        {
            // Arrange
            var currentRealm = "realm-a";
            var targetRealm = "realm-b"; // Different realm

            // Act
            var guide = _tool.GenerateMigrationGuide(currentRealm, targetRealm);

            // Assert
            Assert.True(guide.Warnings.Any(w => w.Contains("Cross-realm migration detected", StringComparison.Ordinal)));
            Assert.True(guide.Steps[^1].Title?.Contains("Review cross-realm compatibility", StringComparison.Ordinal) == true);
        }

        [Fact]
        public void GenerateMigrationGuide_IncludesPrerequisites()
        {
            // Arrange
            var guide = _tool.GenerateMigrationGuide("old", "new");

            // Act & Assert
            Assert.True(guide.Prerequisites.Any(p => p.Contains("Backup all important data", StringComparison.Ordinal)));
            Assert.True(guide.Prerequisites.Any(p => p.Contains("Ensure new realm infrastructure", StringComparison.Ordinal)));
            Assert.True(guide.Prerequisites.Any(p => p.Contains("Notify users of planned downtime", StringComparison.Ordinal)));
        }

        [Fact]
        public void GenerateMigrationGuide_IncludesPostMigrationTasks()
        {
            // Arrange
            var guide = _tool.GenerateMigrationGuide("old", "new");

            // Act & Assert
            Assert.True(guide.PostMigrationTasks.Any(t => t.Contains("Re-establish ActivityPub follows", StringComparison.Ordinal)));
            Assert.True(guide.PostMigrationTasks.Any(t => t.Contains("Update documentation", StringComparison.Ordinal)));
        }

        [Fact]
        public void GenerateMigrationGuide_IncludesBreakingChangeWarnings()
        {
            // Arrange
            var guide = _tool.GenerateMigrationGuide("old", "new");

            // Act & Assert
            Assert.True(guide.Warnings.Any(w => w.Contains("break all existing social and federation relationships", StringComparison.Ordinal)));
            Assert.True(guide.Warnings.Any(w => w.Contains("Migration is one-way", StringComparison.Ordinal)));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}


