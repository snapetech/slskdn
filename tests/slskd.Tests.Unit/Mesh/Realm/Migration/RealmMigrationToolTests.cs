// <copyright file="RealmMigrationToolTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Migration
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
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
            Assert.Contains("Pod configuration", result.IncludedData);
            Assert.Contains("User preferences", result.IncludedData);
            Assert.Contains("Content metadata", result.IncludedData);
            Assert.Contains("Public social connection metadata", result.IncludedData);
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
            Assert.Contains("Sensitive data export requested", result.Warnings);
            Assert.Contains("Sensitive authentication data", result.IncludedData);
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
            Assert.Contains("Private keys and sensitive authentication data", result.ExcludedData);
            Assert.Contains("Encrypted user credentials", result.ExcludedData);
            Assert.Contains("Sensitive data not exported", result.Warnings);
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
            Assert.Contains("Pod configuration", importResult.ImportedData);
            Assert.Contains("User preferences", importResult.ImportedData);
            Assert.Contains("Content catalog", importResult.ImportedData);
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
            Assert.Contains("does not exist", result.Errors[0]);
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
            Assert.Contains("Cross-realm data import", result.Warnings);
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
            Assert.Contains("Cross-realm migration detected", guide.Warnings);
            Assert.Contains("Review cross-realm compatibility", guide.Steps[^1].Title);
        }

        [Fact]
        public void GenerateMigrationGuide_IncludesPrerequisites()
        {
            // Arrange
            var guide = _tool.GenerateMigrationGuide("old", "new");

            // Act & Assert
            Assert.Contains("Backup all important data", guide.Prerequisites);
            Assert.Contains("Ensure new realm infrastructure", guide.Prerequisites);
            Assert.Contains("Notify users of planned downtime", guide.Prerequisites);
        }

        [Fact]
        public void GenerateMigrationGuide_IncludesPostMigrationTasks()
        {
            // Arrange
            var guide = _tool.GenerateMigrationGuide("old", "new");

            // Act & Assert
            Assert.Contains("Re-establish ActivityPub follows", guide.PostMigrationTasks);
            Assert.Contains("Update documentation", guide.PostMigrationTasks);
        }

        [Fact]
        public void GenerateMigrationGuide_IncludesBreakingChangeWarnings()
        {
            // Arrange
            var guide = _tool.GenerateMigrationGuide("old", "new");

            // Act & Assert
            Assert.Contains("break all existing social and federation relationships", guide.Warnings);
            Assert.Contains("Migration is one-way", guide.Warnings);
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

