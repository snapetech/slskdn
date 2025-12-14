// <copyright file="TestCoverageBuildTask.cs" company="slskdN Team">
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

    /// <summary>
    ///     MSBuild task for test coverage analysis.
    /// </summary>
    /// <remarks>
    ///     H-CODE03: Test Coverage & Regression Harness.
    ///     Integrates test coverage analysis into the build pipeline.
    /// </remarks>
    public class TestCoverageBuildTask : Task
    {
        /// <summary>
        ///     Gets or sets the project directory.
        /// </summary>
        [Required]
        public string? ProjectDirectory { get; set; }

        /// <summary>
        ///     Gets or sets the test assembly paths.
        /// </summary>
        [Required]
        public string[]? TestAssemblyPaths { get; set; }

        /// <summary>
        ///     Gets or sets the source assembly paths.
        /// </summary>
        [Required]
        public string[]? SourceAssemblyPaths { get; set; }

        /// <summary>
        ///     Gets or sets the output directory for reports.
        /// </summary>
        public string? OutputDirectory { get; set; }

        /// <summary>
        ///     Gets or sets the minimum required coverage percentage.
        /// </summary>
        public double MinimumCoverage { get; set; } = 0.75; // 75%

        /// <summary>
        ///     Gets or sets a value indicating whether to fail the build on low coverage.
        /// </summary>
        public bool FailOnLowCoverage { get; set; } = true;

        /// <summary>
        ///     Gets or sets the maximum number of uncovered methods to report.
        /// </summary>
        public int MaxUncoveredMethods { get; set; } = 50;

        /// <summary>
        ///     Executes the build task.
        /// </summary>
        /// <returns>True if the task succeeded.</returns>
        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.Normal, "Running slskdN Test Coverage Analysis...");

                // Load assemblies
                var testAssemblies = LoadAssemblies(TestAssemblyPaths);
                var sourceAssemblies = LoadAssemblies(SourceAssemblyPaths);

                if (!testAssemblies.Any() || !sourceAssemblies.Any())
                {
                    Log.LogWarning("No test or source assemblies found. Skipping coverage analysis.");
                    return true;
                }

                // Run coverage analysis
                var coverageReport = TestCoverage.AnalyzeCoverage(testAssemblies, sourceAssemblies);

                // Generate reports
                if (!string.IsNullOrEmpty(OutputDirectory))
                {
                    RegressionHarness.GenerateCoverageReport(coverageReport, OutputDirectory);
                }

                // Report results
                ReportCoverageResults(coverageReport);

                // Check coverage thresholds
                var shouldFail = ShouldFailBuild(coverageReport);
                if (shouldFail)
                {
                    Log.LogError("Test coverage analysis failed. Address coverage gaps before proceeding.");
                    return false;
                }

                Log.LogMessage(MessageImportance.Normal, "Test coverage analysis completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: false, showDetail: true, file: null);
                return false;
            }
        }

        private List<Assembly> LoadAssemblies(string[]? assemblyPaths)
        {
            var assemblies = new List<Assembly>();

            if (assemblyPaths == null)
            {
                return assemblies;
            }

            foreach (var path in assemblyPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var assembly = Assembly.LoadFrom(path);
                        assemblies.Add(assembly);
                        Log.LogMessage(MessageImportance.Low, "Loaded assembly: {Assembly}", assembly.GetName().Name);
                    }
                    else
                    {
                        Log.LogWarning("Assembly file not found: {Path}", path);
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to load assembly {Path}: {Message}", path, ex.Message);
                }
            }

            return assemblies;
        }

        private void ReportCoverageResults(TestCoverageReport report)
        {
            Log.LogMessage(MessageImportance.Normal, "Coverage Analysis Results:");
            Log.LogMessage(MessageImportance.Normal, $"  Overall Coverage: {report.OverallCoverage:P1}");
            Log.LogMessage(MessageImportance.Normal, $"  Subsystems Analyzed: {report.SubsystemReports.Count}");

            foreach (var subsystem in report.SubsystemReports.OrderByDescending(r => r.MethodCoverage))
            {
                var status = subsystem.MethodCoverage >= 0.8 ? "✅ Good" :
                            subsystem.MethodCoverage >= 0.6 ? "⚠️ Needs Work" : "❌ Critical";

                Log.LogMessage(MessageImportance.Normal,
                    $"  {subsystem.SubsystemName}: {subsystem.MethodCoverage:P1} method coverage, {subsystem.ClassCoverage:P1} class coverage - {status}");
            }

            if (report.CoverageGaps.Any())
            {
                Log.LogMessage(MessageImportance.Normal, $"  Coverage Gaps: {report.CoverageGaps.Count} subsystems below threshold");

                foreach (var gap in report.CoverageGaps)
                {
                    Log.LogMessage(MessageImportance.Normal, $"    {gap.Subsystem}: {gap.CoveragePercentage:P1} coverage");
                    if (gap.UncoveredTypes?.Any() == true)
                    {
                        var typesToShow = gap.UncoveredTypes.Take(5).ToList();
                        Log.LogMessage(MessageImportance.Low, $"      Uncovered types: {string.Join(", ", typesToShow)}");
                        if (gap.UncoveredTypes.Count > 5)
                        {
                            Log.LogMessage(MessageImportance.Low, $"      ... and {gap.UncoveredTypes.Count - 5} more");
                        }
                    }
                }
            }

            // Report uncovered methods
            var uncoveredMethods = TestCoverage.IdentifyUncoveredMethods(
                report.SourceAssemblies.Select(name => Assembly.Load(name)).ToList(),
                report.TestAssemblies.Select(name => Assembly.Load(name)).ToList())
                .Take(MaxUncoveredMethods)
                .ToList();

            if (uncoveredMethods.Any())
            {
                Log.LogMessage(MessageImportance.Normal, $"  High-Risk Uncovered Methods: {uncoveredMethods.Count}");

                foreach (var method in uncoveredMethods.Where(m => m.RiskLevel == MethodRiskLevel.High))
                {
                    Log.LogMessage(MessageImportance.Normal, $"    🔴 HIGH RISK: {method.TypeName}.{method.MethodName}");
                }

                foreach (var method in uncoveredMethods.Where(m => m.RiskLevel == MethodRiskLevel.Medium))
                {
                    Log.LogMessage(MessageImportance.Normal, $"    🟡 MEDIUM RISK: {method.TypeName}.{method.MethodName}");
                }
            }
        }

        private bool ShouldFailBuild(TestCoverageReport report)
        {
            // Check overall coverage
            if (report.OverallCoverage < MinimumCoverage)
            {
                Log.LogError($"Overall test coverage {report.OverallCoverage:P1} is below minimum required {MinimumCoverage:P1}");
                return FailOnLowCoverage;
            }

            // Check critical subsystems
            var criticalSubsystems = TestCoverage.CriticalSubsystems;
            var lowCoverageSubsystems = report.SubsystemReports
                .Where(r => criticalSubsystems.Contains(r.SubsystemName ?? string.Empty) && r.MethodCoverage < 0.7)
                .ToList();

            if (lowCoverageSubsystems.Any())
            {
                foreach (var subsystem in lowCoverageSubsystems)
                {
                    Log.LogError($"Critical subsystem {subsystem.SubsystemName} has low coverage: {subsystem.MethodCoverage:P1}");
                }
                return FailOnLowCoverage;
            }

            return false;
        }
    }
}
