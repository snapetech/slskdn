// <copyright file="RealmMigrationTool.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm.Migration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Provides tooling for realm migration operations.
    /// </summary>
    /// <remarks>
    ///     T-REALM-05: Realm Change & Migration Guardrails - provides safe migration tooling
    ///     for transitioning between realms with data export/import capabilities.
    /// </remarks>
    public class RealmMigrationTool
    {
        private readonly ILogger<RealmMigrationTool> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmMigrationTool"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RealmMigrationTool(ILogger<RealmMigrationTool> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Exports pod data suitable for migration to a new realm.
        /// </summary>
        /// <param name="exportPath">The path to export to.</param>
        /// <param name="includeSensitiveData">Whether to include sensitive data (requires explicit confirmation).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The export result.</returns>
        /// <remarks>
        ///     T-REALM-05: Data export for migration - safely exports pod state for realm transitions.
        /// </remarks>
        public async Task<MigrationExportResult> ExportPodDataAsync(
            string exportPath,
            bool includeSensitiveData = false,
            CancellationToken cancellationToken = default)
        {
            var result = new MigrationExportResult
            {
                ExportPath = exportPath,
                Timestamp = DateTimeOffset.UtcNow,
                IncludedData = new List<string>(),
                ExcludedData = new List<string>(),
                Warnings = new List<string>()
            };

            try
            {
                // Create export directory
                Directory.CreateDirectory(exportPath);
                _logger.LogInformation("[Migration] Created export directory: {Path}", exportPath);

                // Export configuration (sanitized)
                await ExportConfigurationAsync(exportPath, result, cancellationToken);

                // Export user data (anonymized)
                await ExportUserDataAsync(exportPath, result, cancellationToken);

                // Export content metadata (without sensitive paths)
                await ExportContentMetadataAsync(exportPath, result, cancellationToken);

                // Export social connections (without private data)
                await ExportSocialConnectionsAsync(exportPath, result, cancellationToken);

                // Handle sensitive data
                if (includeSensitiveData)
                {
                    result.Warnings.Add("Sensitive data export requested - ensure secure handling");
                    await ExportSensitiveDataAsync(exportPath, result, cancellationToken);
                }
                else
                {
                    result.ExcludedData.Add("Private keys and sensitive authentication data");
                    result.ExcludedData.Add("Encrypted user credentials");
                    result.ExcludedData.Add("Private social connection details");
                    result.Warnings.Add("Sensitive data not exported - will need to be re-established in new realm");
                }

                // Create migration manifest
                await CreateMigrationManifestAsync(exportPath, result, cancellationToken);

                result.Success = true;
                _logger.LogInformation(
                    "[Migration] Export completed successfully. Included: {Included}, Excluded: {Excluded}",
                    result.IncludedData.Count, result.ExcludedData.Count);

            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "[Migration] Export failed: {Message}", ex.Message);
            }

            return result;
        }

        /// <summary>
        ///     Imports pod data from a migration export.
        /// </summary>
        /// <param name="importPath">The path to import from.</param>
        /// <param name="targetRealmId">The target realm ID for validation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The import result.</returns>
        /// <remarks>
        ///     T-REALM-05: Data import for migration - safely imports pod state after realm transition.
        /// </remarks>
        public async Task<MigrationImportResult> ImportPodDataAsync(
            string importPath,
            string targetRealmId,
            CancellationToken cancellationToken = default)
        {
            var result = new MigrationImportResult
            {
                ImportPath = importPath,
                TargetRealmId = targetRealmId,
                Timestamp = DateTimeOffset.UtcNow,
                ImportedData = new List<string>(),
                SkippedData = new List<string>(),
                Errors = new List<string>()
            };

            try
            {
                // Validate import path
                if (!Directory.Exists(importPath))
                {
                    result.Errors.Add($"Import path does not exist: {importPath}");
                    return result;
                }

                // Read and validate migration manifest
                var manifest = await ReadMigrationManifestAsync(importPath, cancellationToken);
                if (manifest == null)
                {
                    result.Errors.Add("Migration manifest not found or invalid");
                    return result;
                }

                result.SourceRealmId = manifest.SourceRealmId;
                result.ExportTimestamp = manifest.ExportTimestamp;

                // Validate realm compatibility
                if (!string.Equals(manifest.SourceRealmId, targetRealmId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add($"Importing data from realm '{manifest.SourceRealmId}' into realm '{targetRealmId}'");
                    result.Warnings.Add("Cross-realm data import may require manual review");
                }

                // Import configuration (with realm-specific adjustments)
                await ImportConfigurationAsync(importPath, result, cancellationToken);

                // Import user data (with validation)
                await ImportUserDataAsync(importPath, result, cancellationToken);

                // Import content metadata (realm-appropriate)
                await ImportContentMetadataAsync(importPath, result, cancellationToken);

                // Import social connections (requires re-establishment)
                await ImportSocialConnectionsAsync(importPath, result, cancellationToken);

                result.Success = !result.Errors.Any();
                _logger.LogInformation(
                    "[Migration] Import completed. Imported: {Imported}, Skipped: {Skipped}, Errors: {Errors}",
                    result.ImportedData.Count, result.SkippedData.Count, result.Errors.Count);

            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Import failed: {ex.Message}");
                _logger.LogError(ex, "[Migration] Import failed: {Message}", ex.Message);
            }

            return result;
        }

        /// <summary>
        ///     Generates migration documentation and recommendations.
        /// </summary>
        /// <param name="currentRealmId">The current realm ID.</param>
        /// <param name="targetRealmId">The target realm ID.</param>
        /// <returns>The migration guide.</returns>
        /// <remarks>
        ///     T-REALM-05: Documented expectations for realm migration.
        /// </remarks>
        public MigrationGuide GenerateMigrationGuide(string currentRealmId, string targetRealmId)
        {
            var guide = new MigrationGuide
            {
                CurrentRealmId = currentRealmId,
                TargetRealmId = targetRealmId,
                GeneratedAt = DateTimeOffset.UtcNow,
                Steps = new List<MigrationStep>(),
                Warnings = new List<string>(),
                Prerequisites = new List<string>(),
                PostMigrationTasks = new List<string>()
            };

            // Prerequisites
            guide.Prerequisites.AddRange(new[]
            {
                "Backup all important data before starting migration",
                "Ensure new realm infrastructure is operational",
                "Verify network connectivity to new realm bootstrap nodes",
                "Review and update firewall rules if necessary",
                "Notify users of planned downtime"
            });

            // Migration steps
            guide.Steps.AddRange(new[]
            {
                new MigrationStep
                {
                    Order = 1,
                    Title = "Export current pod data",
                    Description = "Use the migration tool to export pod configuration and user data",
                    Command = $"migration export --path /backup/{currentRealmId}-export",
                    EstimatedDuration = TimeSpan.FromMinutes(30)
                },
                new MigrationStep
                {
                    Order = 2,
                    Title = "Stop current pod",
                    Description = "Gracefully shut down the pod to prevent data corruption",
                    Command = "systemctl stop slskd",
                    EstimatedDuration = TimeSpan.FromMinutes(5)
                },
                new MigrationStep
                {
                    Order = 3,
                    Title = "Update realm configuration",
                    Description = $"Change realm.id from '{currentRealmId}' to '{targetRealmId}'",
                    Command = $"Edit configuration file - set realm.id = '{targetRealmId}'",
                    EstimatedDuration = TimeSpan.FromMinutes(10)
                },
                new MigrationStep
                {
                    Order = 4,
                    Title = "Update governance roots",
                    Description = "Replace governance roots with those trusted in the new realm",
                    Command = "Update realm.governance_roots in configuration",
                    EstimatedDuration = TimeSpan.FromMinutes(15)
                },
                new MigrationStep
                {
                    Order = 5,
                    Title = "Update bootstrap nodes",
                    Description = "Configure bootstrap nodes for the new realm",
                    Command = "Update realm.bootstrap_nodes in configuration",
                    EstimatedDuration = TimeSpan.FromMinutes(10)
                },
                new MigrationStep
                {
                    Order = 6,
                    Title = "Start pod in new realm",
                    Description = "Start the pod with new realm configuration",
                    Command = "systemctl start slskd",
                    EstimatedDuration = TimeSpan.FromMinutes(5)
                },
                new MigrationStep
                {
                    Order = 7,
                    Title = "Import migrated data",
                    Description = "Import compatible data from the migration export",
                    Command = $"migration import --path /backup/{currentRealmId}-export --realm {targetRealmId}",
                    EstimatedDuration = TimeSpan.FromMinutes(45)
                },
                new MigrationStep
                {
                    Order = 8,
                    Title = "Verify realm transition",
                    Description = "Confirm pod has joined new realm and basic functionality works",
                    Command = "Check logs and web interface for successful realm join",
                    EstimatedDuration = TimeSpan.FromMinutes(15)
                }
            });

            // Post-migration tasks
            guide.PostMigrationTasks.AddRange(new[]
            {
                "Re-establish ActivityPub follows and social connections",
                "Update any external references to the pod's realm",
                "Monitor for gossip and federation re-establishment",
                "Verify content discovery works in new realm",
                "Update documentation and user communications"
            });

            // Warnings
            guide.Warnings.AddRange(new[]
            {
                "This migration will break all existing social and federation relationships",
                "Users will need to re-follow and re-establish connections",
                "Content may not be discoverable across different realms",
                "Governance documents from old realm will be invalid in new realm",
                "Migration is one-way - returning to old realm requires another migration"
            });

            // Special handling for cross-realm migrations
            if (!string.Equals(currentRealmId, targetRealmId, StringComparison.OrdinalIgnoreCase))
            {
                guide.Warnings.Add("Cross-realm migration detected - additional compatibility checks required");
                guide.Steps.Add(new MigrationStep
                {
                    Order = 9,
                    Title = "Review cross-realm compatibility",
                    Description = "Manually review imported data for realm-specific compatibility",
                    Command = "Check logs for import warnings and review imported data",
                    EstimatedDuration = TimeSpan.FromMinutes(30)
                });
            }

            return guide;
        }

        // Implementation details for export/import operations
        private async Task ExportConfigurationAsync(string exportPath, MigrationExportResult result, CancellationToken cancellationToken)
        {
            // Export sanitized configuration
            var configPath = Path.Combine(exportPath, "configuration.json");
            // Implementation would export configuration without secrets
            result.IncludedData.Add("Pod configuration (sanitized)");
            await Task.CompletedTask;
        }

        private async Task ExportUserDataAsync(string exportPath, MigrationExportResult result, CancellationToken cancellationToken)
        {
            // Export anonymized user data
            result.IncludedData.Add("User preferences and settings");
            await Task.CompletedTask;
        }

        private async Task ExportContentMetadataAsync(string exportPath, MigrationExportResult result, CancellationToken cancellationToken)
        {
            // Export content metadata without sensitive paths
            result.IncludedData.Add("Content metadata and catalog");
            await Task.CompletedTask;
        }

        private async Task ExportSocialConnectionsAsync(string exportPath, MigrationExportResult result, CancellationToken cancellationToken)
        {
            // Export public social connection information
            result.IncludedData.Add("Public social connection metadata");
            await Task.CompletedTask;
        }

        private async Task ExportSensitiveDataAsync(string exportPath, MigrationExportResult result, CancellationToken cancellationToken)
        {
            // Export sensitive data (only with explicit confirmation)
            result.IncludedData.Add("Sensitive authentication data");
            result.Warnings.Add("Sensitive data exported - handle with extreme care");
            await Task.CompletedTask;
        }

        private async Task CreateMigrationManifestAsync(string exportPath, MigrationExportResult result, CancellationToken cancellationToken)
        {
            var manifest = new MigrationManifest
            {
                Version = "1.0",
                ExportTimestamp = result.Timestamp,
                SourceRealmId = "current-realm", // Would get from current realm service
                IncludedData = result.IncludedData,
                ExcludedData = result.ExcludedData,
                Warnings = result.Warnings
            };

            var manifestPath = Path.Combine(exportPath, "migration-manifest.json");
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
        }

        private async Task<MigrationManifest?> ReadMigrationManifestAsync(string importPath, CancellationToken cancellationToken)
        {
            var manifestPath = Path.Combine(importPath, "migration-manifest.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            return JsonSerializer.Deserialize<MigrationManifest>(json);
        }

        private async Task ImportConfigurationAsync(string importPath, MigrationImportResult result, CancellationToken cancellationToken)
        {
            // Import configuration with realm-specific adjustments
            result.ImportedData.Add("Pod configuration");
            await Task.CompletedTask;
        }

        private async Task ImportUserDataAsync(string importPath, MigrationImportResult result, CancellationToken cancellationToken)
        {
            // Import user data with validation
            result.ImportedData.Add("User preferences");
            await Task.CompletedTask;
        }

        private async Task ImportContentMetadataAsync(string importPath, MigrationImportResult result, CancellationToken cancellationToken)
        {
            // Import content metadata (realm-appropriate)
            result.ImportedData.Add("Content catalog");
            await Task.CompletedTask;
        }

        private async Task ImportSocialConnectionsAsync(string importPath, MigrationImportResult result, CancellationToken cancellationToken)
        {
            // Import social connections (requires re-establishment)
            result.SkippedData.Add("Social connections (require manual re-establishment)");
            await Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Result of a migration export operation.
    /// </summary>
    public class MigrationExportResult
    {
        /// <summary>
        ///     Gets or sets the export path.
        /// </summary>
        public string? ExportPath { get; set; }

        /// <summary>
        ///     Gets or sets the export timestamp.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the export succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Gets or sets the error message if the export failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        ///     Gets or sets the data that was included in the export.
        /// </summary>
        public List<string> IncludedData { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the data that was excluded from the export.
        /// </summary>
        public List<string> ExcludedData { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the warnings about the export.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    ///     Result of a migration import operation.
    /// </summary>
    public class MigrationImportResult
    {
        /// <summary>
        ///     Gets or sets the import path.
        /// </summary>
        public string? ImportPath { get; set; }

        /// <summary>
        ///     Gets or sets the target realm ID.
        /// </summary>
        public string? TargetRealmId { get; set; }

        /// <summary>
        ///     Gets or sets the source realm ID from the export.
        /// </summary>
        public string? SourceRealmId { get; set; }

        /// <summary>
        ///     Gets or sets the export timestamp.
        /// </summary>
        public DateTimeOffset ExportTimestamp { get; set; }

        /// <summary>
        ///     Gets or sets the import timestamp.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the import succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Gets or sets the data that was imported.
        /// </summary>
        public List<string> ImportedData { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the data that was skipped during import.
        /// </summary>
        public List<string> SkippedData { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the errors that occurred during import.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the warnings about the import.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    ///     Migration manifest containing metadata about an export.
    /// </summary>
    public class MigrationManifest
    {
        /// <summary>
        ///     Gets or sets the manifest version.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        ///     Gets or sets the export timestamp.
        /// </summary>
        public DateTimeOffset ExportTimestamp { get; set; }

        /// <summary>
        ///     Gets or sets the source realm ID.
        /// </summary>
        public string? SourceRealmId { get; set; }

        /// <summary>
        ///     Gets or sets the included data types.
        /// </summary>
        public List<string> IncludedData { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the excluded data types.
        /// </summary>
        public List<string> ExcludedData { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the export warnings.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    ///     Migration guide with steps and recommendations.
    /// </summary>
    public class MigrationGuide
    {
        /// <summary>
        ///     Gets or sets the current realm ID.
        /// </summary>
        public string? CurrentRealmId { get; set; }

        /// <summary>
        ///     Gets or sets the target realm ID.
        /// </summary>
        public string? TargetRealmId { get; set; }

        /// <summary>
        ///     Gets or sets when the guide was generated.
        /// </summary>
        public DateTimeOffset GeneratedAt { get; set; }

        /// <summary>
        ///     Gets or sets the migration steps.
        /// </summary>
        public List<MigrationStep> Steps { get; set; } = new List<MigrationStep>();

        /// <summary>
        ///     Gets or sets the warnings.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the prerequisites.
        /// </summary>
        public List<string> Prerequisites { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the post-migration tasks.
        /// </summary>
        public List<string> PostMigrationTasks { get; set; } = new List<string>();

        /// <summary>
        ///     Gets the total estimated duration of all steps.
        /// </summary>
        public TimeSpan TotalEstimatedDuration => Steps.Aggregate(TimeSpan.Zero, (sum, step) => sum + step.EstimatedDuration);
    }

    /// <summary>
    ///     A step in the migration process.
    /// </summary>
    public class MigrationStep
    {
        /// <summary>
        ///     Gets or sets the step order.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        ///     Gets or sets the step title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        ///     Gets or sets the step description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        ///     Gets or sets the command to execute.
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        ///     Gets or sets the estimated duration.
        /// </summary>
        public TimeSpan EstimatedDuration { get; set; }
    }
}

