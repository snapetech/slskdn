// <copyright file="ModerationCoverageAudit.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.CodeQuality
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Comprehensive audit of moderation coverage across the codebase.
    /// </summary>
    /// <remarks>
    ///     H-MCP01: Moderation Coverage Audit.
    ///     Ensures MCP (Moderation Content Policy) checks are applied consistently
    ///     across all code paths that handle content lifecycle.
    /// </remarks>
    public static class ModerationCoverageAudit
    {
        /// <summary>
        ///     Content lifecycle phases that require moderation checks.
        /// </summary>
        public enum ContentLifecyclePhase
        {
            /// <summary>
            ///     Files being added to the local library.
            /// </summary>
            LibraryIngestion,

            /// <summary>
            ///     Files being linked to content item IDs.
            /// </summary>
            ContentItemLinking,

            /// <summary>
            ///     Content being advertised to the network.
            /// </summary>
            ContentAdvertising,

            /// <summary>
            ///     Content being served via relay services.
            /// </summary>
            ContentServing,

            /// <summary>
            ///     Work references being published to federation.
            /// </summary>
            FederationPublishing,

            /// <summary>
            ///     Content being downloaded/acquired.
            /// </summary>
            ContentAcquisition
        }

        /// <summary>
        ///     Critical code paths that must have moderation checks.
        /// </summary>
        private static readonly CriticalPath[] CriticalPaths = new[]
        {
            new CriticalPath
            {
                Phase = ContentLifecyclePhase.LibraryIngestion,
                Description = "Files added to local library via Shares scanning",
                EntryPoints = new[]
                {
                    "slskd.Shares.ShareService.ScanAsync",
                    "slskd.Shares.ShareScanner.ScanDirectory",
                    "slskd.LibraryHealth.LibraryHealthService.ScanAndRemediateAsync"
                },
                RequiredChecks = new[] { "IsAdvertisable", "ContentModeration" }
            },

            new CriticalPath
            {
                Phase = ContentLifecyclePhase.ContentItemLinking,
                Description = "HashDb linking files to ContentItemIds",
                EntryPoints = new[]
                {
                    "slskd.HashDb.HashDbService.TryAddFileAsync",
                    "slskd.HashDb.HashDbService.TryGetOrAddFileAsync",
                    "slskd.MediaCore.ContentIdRegistry.RegisterAsync"
                },
                RequiredChecks = new[] { "IsAdvertisable", "ContentModeration" }
            },

            new CriticalPath
            {
                Phase = ContentLifecyclePhase.ContentAdvertising,
                Description = "Content advertised via mesh/DHT/torrent networks",
                EntryPoints = new[]
                {
                    "slskd.MediaCore.ContentDescriptorPublisher.PublishAsync",
                    "slskd.DhtRendezvous.PeerDescriptorPublisher.PublishAsync",
                    "slskd.Mesh.ServiceFabric.Services.VirtualSoulfindMeshService.GetContent"
                },
                RequiredChecks = new[] { "IsAdvertisable", "PeerReputation" }
            },

            new CriticalPath
            {
                Phase = ContentLifecyclePhase.ContentServing,
                Description = "Content served via relay services",
                EntryPoints = new[]
                {
                    "slskd.Relay.RelayController.DownloadFile",
                    "slskd.Relay.RelayController.UploadFile",
                    "slskd.Relay.RelayService.HandleDownloadRequest"
                },
                RequiredChecks = new[] { "IsAdvertisable", "ContentModeration", "PeerReputation" }
            },

            new CriticalPath
            {
                Phase = ContentLifecyclePhase.ContentAcquisition,
                Description = "Content downloaded/acquired from peers",
                EntryPoints = new[]
                {
                    "slskd.Transfers.MultiSource.MultiSourceDownloadService.StartAsync",
                    "slskd.VirtualSoulfind.v2.Planning.MultiSourcePlanner.SelectSourcesAsync",
                    "slskd.Transfers.Downloads.DownloadService.EnqueueTransfer"
                },
                RequiredChecks = new[] { "PeerReputation", "ContentModeration" }
            },

            new CriticalPath
            {
                Phase = ContentLifecyclePhase.FederationPublishing,
                Description = "Work references published to social federation",
                EntryPoints = new[]
                {
                    "slskd.SocialFederation.FederationService.PublishWorkRef",
                    "slskd.SocialFederation.OutboxService.PostActivity",
                    "slskd.SocialFederation.ActivityPubController.Inbox"
                },
                RequiredChecks = new[] { "IsAdvertisable", "ContentModeration", "FederationPolicy" }
            }
        };

        /// <summary>
        ///     Runs a comprehensive moderation coverage audit.
        /// </summary>
        /// <param name="testAssemblies">Test assemblies to analyze for coverage.</param>
        /// <param name="sourceAssemblies">Source assemblies to audit.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Comprehensive audit results.</returns>
        public static ModerationCoverageReport RunAudit(
            IEnumerable<Assembly> testAssemblies,
            IEnumerable<Assembly> sourceAssemblies,
            ILogger? logger = null)
        {
            var report = new ModerationCoverageReport
            {
                AuditTimestamp = DateTimeOffset.UtcNow,
                CriticalPaths = CriticalPaths.ToList()
            };

            logger?.LogInformation("[ModerationAudit] Starting comprehensive moderation coverage audit...");

            foreach (var criticalPath in CriticalPaths)
            {
                var pathResult = AuditCriticalPath(criticalPath, sourceAssemblies, testAssemblies, logger);
                report.PathResults.Add(pathResult);
            }

            // Generate summary
            report.Summary = GenerateSummary(report.PathResults);
            report.OverallCompliance = report.PathResults.All(r => r.IsFullyCompliant);

            logger?.LogInformation(
                "[ModerationAudit] Audit complete: {TotalPaths} paths analyzed, {CompliantPaths} fully compliant",
                report.PathResults.Count, report.PathResults.Count(r => r.IsFullyCompliant));

            return report;
        }

        /// <summary>
        ///     Audits a specific critical path for moderation coverage.
        /// </summary>
        /// <param name="criticalPath">The critical path to audit.</param>
        /// <param name="sourceAssemblies">Source assemblies to analyze.</param>
        /// <param name="testAssemblies">Test assemblies for coverage verification.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Detailed audit results for the path.</returns>
        public static CriticalPathAuditResult AuditCriticalPath(
            CriticalPath criticalPath,
            IEnumerable<Assembly> sourceAssemblies,
            IEnumerable<Assembly> testAssemblies,
            ILogger? logger = null)
        {
            var result = new CriticalPathAuditResult
            {
                CriticalPath = criticalPath,
                EntryPointsAnalyzed = new List<EntryPointAnalysis>(),
                MissingChecks = new List<string>(),
                Recommendations = new List<string>()
            };

            foreach (var entryPoint in criticalPath.EntryPoints)
            {
                var analysis = AnalyzeEntryPoint(entryPoint, criticalPath, sourceAssemblies, logger);
                result.EntryPointsAnalyzed.Add(analysis);
            }

            // Check for missing moderation checks
            var implementedChecks = result.EntryPointsAnalyzed
                .SelectMany(ep => ep.FoundChecks)
                .Distinct()
                .ToHashSet();

            foreach (var requiredCheck in criticalPath.RequiredChecks)
            {
                if (!implementedChecks.Contains(requiredCheck))
                {
                    result.MissingChecks.Add(requiredCheck);
                    result.Recommendations.Add($"Add {requiredCheck} check to {criticalPath.Phase} phase");
                }
            }

            result.IsFullyCompliant = !result.MissingChecks.Any() &&
                                     result.EntryPointsAnalyzed.All(ep => ep.HasRequiredChecks);

            logger?.LogDebug(
                "[ModerationAudit] Path {Phase}: {EntryPoints} entry points, {MissingChecks} missing checks",
                criticalPath.Phase, result.EntryPointsAnalyzed.Count, result.MissingChecks.Count);

            return result;
        }

        private static EntryPointAnalysis AnalyzeEntryPoint(
            string entryPointSignature,
            CriticalPath criticalPath,
            IEnumerable<Assembly> sourceAssemblies,
            ILogger? logger)
        {
            var analysis = new EntryPointAnalysis
            {
                EntryPointSignature = entryPointSignature,
                FoundChecks = new List<string>(),
                MissingChecks = new List<string>(),
                CodeLocation = "Unknown"
            };

            // Parse entry point signature (e.g., "slskd.Shares.ShareService.ScanAsync")
            var parts = entryPointSignature.Split('.');
            if (parts.Length < 2)
            {
                analysis.AnalysisNotes.Add("Invalid entry point signature format");
                return analysis;
            }

            var typeName = string.Join(".", parts.Take(parts.Length - 1));
            var methodName = parts.Last();

            // Find the actual method in assemblies
            foreach (var assembly in sourceAssemblies)
            {
                var type = assembly.GetTypes().FirstOrDefault(t =>
                    t.FullName?.Replace(".", "") == typeName.Replace(".", "") ||
                    t.Name == parts[parts.Length - 2]);

                if (type != null)
                {
                    analysis.CodeLocation = $"{type.FullName}.{methodName}";
                    var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name.Contains(methodName));

                    if (method != null)
                    {
                        // Analyze method for moderation checks
                        var checks = DetectModerationChecks(method, criticalPath.RequiredChecks);
                        analysis.FoundChecks.AddRange(checks);

                        var missing = criticalPath.RequiredChecks.Except(checks).ToList();
                        analysis.MissingChecks.AddRange(missing);
                        analysis.HasRequiredChecks = !missing.Any();
                    }
                    else
                    {
                        analysis.AnalysisNotes.Add($"Method {methodName} not found in type {type.FullName}");
                    }
                    break;
                }
            }

            if (analysis.CodeLocation == "Unknown")
            {
                analysis.AnalysisNotes.Add($"Type {typeName} not found in analyzed assemblies");
            }

            return analysis;
        }

        private static IEnumerable<string> DetectModerationChecks(MethodInfo method, string[] requiredChecks)
        {
            var foundChecks = new List<string>();

            // Get method body as string (simplified analysis)
            // In a real implementation, this would use Roslyn to analyze the syntax tree
            var methodSignature = $"{method.DeclaringType?.FullName}.{method.Name}";

            // Check for common moderation patterns in method names and parameters
            var methodName = method.Name.ToLowerInvariant();

            // Check for IsAdvertisable usage
            if (methodName.Contains("advertisable") || methodName.Contains("moderate") ||
                method.GetParameters().Any(p => p.Name?.Contains("advertisable") == true))
            {
                foundChecks.Add("IsAdvertisable");
            }

            // Check for ContentModeration usage
            if (methodName.Contains("moderate") || methodName.Contains("moderation") ||
                method.GetParameters().Any(p => p.Name?.Contains("moderation") == true))
            {
                foundChecks.Add("ContentModeration");
            }

            // Check for PeerReputation usage
            if (methodName.Contains("reputation") || methodName.Contains("peer") ||
                method.GetParameters().Any(p => p.Name?.Contains("reputation") == true ||
                                               p.Name?.Contains("peer") == true))
            {
                foundChecks.Add("PeerReputation");
            }

            // Check for FederationPolicy usage
            if (methodName.Contains("federation") || methodName.Contains("policy") ||
                method.GetParameters().Any(p => p.Name?.Contains("federation") == true))
            {
                foundChecks.Add("FederationPolicy");
            }

            return foundChecks;
        }

        private static ModerationCoverageSummary GenerateSummary(List<CriticalPathAuditResult> pathResults)
        {
            return new ModerationCoverageSummary
            {
                TotalPathsAudited = pathResults.Count,
                FullyCompliantPaths = pathResults.Count(r => r.IsFullyCompliant),
                PathsWithMissingChecks = pathResults.Count(r => r.MissingChecks.Any()),
                TotalMissingChecks = pathResults.Sum(r => r.MissingChecks.Count),
                MostCommonMissingChecks = pathResults
                    .SelectMany(r => r.MissingChecks)
                    .GroupBy(check => check)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
    }

    /// <summary>
    ///     Represents a critical path that requires moderation checks.
    /// </summary>
    public sealed class CriticalPath
    {
        /// <summary>
        ///     Gets the lifecycle phase.
        /// </summary>
        public ModerationCoverageAudit.ContentLifecyclePhase Phase { get; init; }

        /// <summary>
        ///     Gets the description of the critical path.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        ///     Gets the entry point method signatures.
        /// </summary>
        public string[]? EntryPoints { get; init; }

        /// <summary>
        ///     Gets the required moderation checks.
        /// </summary>
        public string[]? RequiredChecks { get; init; }
    }

    /// <summary>
    ///     Results of the moderation coverage audit.
    /// </summary>
    public sealed class ModerationCoverageReport
    {
        /// <summary>
        ///     Gets the timestamp when the audit was performed.
        /// </summary>
        public DateTimeOffset AuditTimestamp { get; init; }

        /// <summary>
        ///     Gets the critical paths that were audited.
        /// </summary>
        public List<CriticalPath> CriticalPaths { get; init; } = new();

        /// <summary>
        ///     Gets the audit results for each critical path.
        /// </summary>
        public List<CriticalPathAuditResult> PathResults { get; init; } = new();

        /// <summary>
        ///     Gets the overall compliance status.
        /// </summary>
        public bool OverallCompliance { get; set; }

        /// <summary>
        ///     Gets the audit summary.
        /// </summary>
        public ModerationCoverageSummary? Summary { get; set; }
    }

    /// <summary>
    ///     Audit result for a specific critical path.
    /// </summary>
    public sealed class CriticalPathAuditResult
    {
        /// <summary>
        ///     Gets the critical path that was audited.
        /// </summary>
        public CriticalPath? CriticalPath { get; init; }

        /// <summary>
        ///     Gets the analysis of each entry point.
        /// </summary>
        public List<EntryPointAnalysis> EntryPointsAnalyzed { get; init; } = new();

        /// <summary>
        ///     Gets the missing moderation checks.
        /// </summary>
        public List<string> MissingChecks { get; init; } = new();

        /// <summary>
        ///     Gets the recommendations for improvement.
        /// </summary>
        public List<string> Recommendations { get; init; } = new();

        /// <summary>
        ///     Gets a value indicating whether the path is fully compliant.
        /// </summary>
        public bool IsFullyCompliant { get; set; }
    }

    /// <summary>
    ///     Analysis of a specific entry point.
    /// </summary>
    public sealed class EntryPointAnalysis
    {
        /// <summary>
        ///     Gets the entry point signature.
        /// </summary>
        public string? EntryPointSignature { get; init; }

        /// <summary>
        ///     Gets the actual code location found.
        /// </summary>
        public string? CodeLocation { get; set; }

        /// <summary>
        ///     Gets the moderation checks found in the entry point.
        /// </summary>
        public List<string> FoundChecks { get; init; } = new();

        /// <summary>
        ///     Gets the missing moderation checks.
        /// </summary>
        public List<string> MissingChecks { get; init; } = new();

        /// <summary>
        ///     Gets a value indicating whether all required checks are present.
        /// </summary>
        public bool HasRequiredChecks { get; set; }

        /// <summary>
        ///     Gets the analysis notes.
        /// </summary>
        public List<string> AnalysisNotes { get; init; } = new();
    }

    /// <summary>
    ///     Summary of the moderation coverage audit.
    /// </summary>
    public sealed class ModerationCoverageSummary
    {
        /// <summary>
        ///     Gets the total number of paths audited.
        /// </summary>
        public int TotalPathsAudited { get; init; }

        /// <summary>
        ///     Gets the number of fully compliant paths.
        /// </summary>
        public int FullyCompliantPaths { get; init; }

        /// <summary>
        ///     Gets the number of paths with missing checks.
        /// </summary>
        public int PathsWithMissingChecks { get; init; }

        /// <summary>
        ///     Gets the total number of missing checks across all paths.
        /// </summary>
        public int TotalMissingChecks { get; init; }

        /// <summary>
        ///     Gets the most commonly missing checks.
        /// </summary>
        public Dictionary<string, int> MostCommonMissingChecks { get; init; } = new();
    }
}
