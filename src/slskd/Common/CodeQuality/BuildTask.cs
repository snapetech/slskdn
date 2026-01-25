// <copyright file="BuildTask.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.CodeQuality
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     MSBuild task for running static analysis during build.
    /// </summary>
    /// <remarks>
    ///     H-CODE02: Introduce Static Analysis and Linting.
    ///     Integrates static analysis into the build pipeline.
    /// </remarks>
    public class CodeAnalysisBuildTask : Task
    {
        /// <summary>
        ///     Gets or sets the project directory.
        /// </summary>
        [Required]
        public string? ProjectDirectory { get; set; }

        /// <summary>
        ///     Gets or sets the output assembly path.
        /// </summary>
        [Required]
        public string? AssemblyPath { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to treat warnings as errors.
        /// </summary>
        public bool TreatWarningsAsErrors { get; set; }

        /// <summary>
        ///     Gets or sets the maximum number of violations allowed.
        /// </summary>
        public int MaxViolations { get; set; } = 100;

        /// <summary>
        ///     Gets or sets the paths to exclude from analysis.
        /// </summary>
        public string[]? ExcludedPaths { get; set; }

        /// <summary>
        ///     Executes the build task.
        /// </summary>
        /// <returns>True if the task succeeded.</returns>
        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.Normal, "Running slskdN Code Analysis...");

                var defaultConfig = AnalyzerConfiguration.Default;
                var config = new AnalyzerConfig
                {
                    Rules = defaultConfig.Rules,
                    TreatWarningsAsErrors = TreatWarningsAsErrors,
                    MaxViolationsPerFile = defaultConfig.MaxViolationsPerFile,
                    ExcludedPaths = (ExcludedPaths != null && ExcludedPaths.Length > 0) ? ExcludedPaths : defaultConfig.ExcludedPaths,
                    ExcludedRules = defaultConfig.ExcludedRules
                };

                var result = RunAnalysis(config);

                // Report results
                ReportResults(result, config);

                // Check if build should fail
                var shouldFail = ShouldFailBuild(result, config);
                if (shouldFail)
                {
                    Log.LogError("Code analysis failed. Fix violations or adjust configuration.");
                    return false;
                }

                Log.LogMessage(MessageImportance.Normal, "Code analysis completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: false, showDetail: true, file: null);
                return false;
            }
        }

        private StaticAnalysisResult RunAnalysis(AnalyzerConfig config)
        {
            var violations = new List<AnalysisViolation>();

            // Analyze the built assembly
            if (!string.IsNullOrEmpty(AssemblyPath) && File.Exists(AssemblyPath))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(AssemblyPath);
                    violations.AddRange(StaticAnalysis.AnalyzeAssembly(assembly).Violations);
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to analyze assembly {Assembly}: {Message}", AssemblyPath, ex.Message);
                }
            }

            // Analyze source files
            if (!string.IsNullOrEmpty(ProjectDirectory) && Directory.Exists(ProjectDirectory))
            {
                violations.AddRange(AnalyzeSourceFiles(ProjectDirectory, config));
            }

            return new StaticAnalysisResult
            {
                AssemblyName = Path.GetFileNameWithoutExtension(AssemblyPath),
                TotalTypesAnalyzed = 0, // Would need reflection analysis
                Violations = violations,
                AnalysisTimestamp = DateTimeOffset.UtcNow
            };
        }

        private IEnumerable<AnalysisViolation> AnalyzeSourceFiles(string projectDirectory, AnalyzerConfig config)
        {
            var violations = new List<AnalysisViolation>();
            var csFiles = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);

            foreach (var csFile in csFiles)
            {
                // Check if file should be excluded
                if (IsExcludedPath(csFile, config.ExcludedPaths))
                {
                    continue;
                }

                try
                {
                    var sourceCode = File.ReadAllText(csFile);
                    var fileViolations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, csFile);

                    foreach (var violation in fileViolations)
                    {
                        if (AnalyzerConfiguration.IsRuleEnabled(violation.Rule ?? string.Empty))
                        {
                            violations.Add(new AnalysisViolation
                            {
                                Location = $"{Path.GetFileName(csFile)}:{violation.LineNumber}",
                                Rule = violation.Rule,
                                Severity = violation.Severity,
                                Message = violation.Message,
                                Recommendation = violation.Recommendation
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to analyze file {File}: {Message}", csFile, ex.Message);
                }
            }

            return violations;
        }

        private bool IsExcludedPath(string filePath, IEnumerable<string> excludedPaths)
        {
            var relativePath = GetRelativePath(filePath, ProjectDirectory ?? string.Empty);
            return excludedPaths.Any(excluded =>
                relativePath.Contains(excluded, StringComparison.OrdinalIgnoreCase));
        }

        private string GetRelativePath(string fullPath, string basePath)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                return fullPath;
            }

            var baseUri = new Uri(Path.GetFullPath(basePath) + Path.DirectorySeparatorChar);
            var fullUri = new Uri(Path.GetFullPath(fullPath));

            var relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString());
        }

        private void ReportResults(StaticAnalysisResult result, AnalyzerConfig config)
        {
            var violationsBySeverity = result.ViolationsBySeverity;

            Log.LogMessage(MessageImportance.Normal, "Analysis Results:");
            Log.LogMessage(MessageImportance.Normal, $"  Total Violations: {result.Violations.Count}");

            foreach (var kvp in violationsBySeverity.OrderByDescending(v => v.Key))
            {
                var severity = kvp.Key;
                var count = kvp.Value;
                Log.LogMessage(MessageImportance.Normal, $"  {severity}: {count}");
            }

            // Log individual violations
            foreach (var violation in result.Violations.OrderByDescending(v => v.Severity))
            {
                var importance = violation.Severity switch
                {
                    ViolationSeverity.Error => MessageImportance.High,
                    ViolationSeverity.Warning => MessageImportance.Normal,
                    ViolationSeverity.Info => MessageImportance.Low,
                    _ => MessageImportance.Low
                };

                Log.LogMessage(importance, $"{violation.Severity}: {violation.Rule} - {violation.Message}");
                Log.LogMessage(importance, $"  Location: {violation.Location}");

                if (!string.IsNullOrEmpty(violation.Recommendation))
                {
                    Log.LogMessage(importance, $"  Recommendation: {violation.Recommendation}");
                }
            }
        }

        private bool ShouldFailBuild(StaticAnalysisResult result, AnalyzerConfig config)
        {
            // Check total violations
            if (result.Violations.Count > MaxViolations)
            {
                Log.LogError($"Too many violations: {result.Violations.Count} > {MaxViolations}");
                return true;
            }

            // Check for errors
            var errorCount = result.Violations.Count(v => v.Severity == ViolationSeverity.Error);
            if (errorCount > 0)
            {
                Log.LogError($"Found {errorCount} error-level violations");
                return true;
            }

            // Check warnings as errors
            if (config.TreatWarningsAsErrors)
            {
                var warningCount = result.Violations.Count(v => v.Severity == ViolationSeverity.Warning);
                if (warningCount > 0)
                {
                    Log.LogError($"Found {warningCount} warning-level violations (treated as errors)");
                    return true;
                }
            }

            return false;
        }
    }
}


