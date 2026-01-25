// <copyright file="RegressionBuildTask.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.CodeQuality
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    /// <summary>
    ///     MSBuild task for regression testing.
    /// </summary>
    /// <remarks>
    ///     H-CODE03: Test Coverage & Regression Harness.
    ///     Integrates regression testing into the build pipeline.
    /// </remarks>
    public class RegressionBuildTask : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        ///     Gets or sets the test assembly paths.
        /// </summary>
        [Required]
        public string[]? TestAssemblyPaths { get; set; }

        /// <summary>
        ///     Gets or sets the output directory for reports.
        /// </summary>
        public string? OutputDirectory { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to run performance benchmarks.
        /// </summary>
        public bool RunBenchmarks { get; set; } = true;

        /// <summary>
        ///     Gets or sets the benchmark iterations.
        /// </summary>
        public int BenchmarkIterations { get; set; } = 1000;

        /// <summary>
        ///     Executes the build task.
        /// </summary>
        /// <returns>True if the task succeeded.</returns>
        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.Normal, "Running slskdN Regression Test Suite...");

                // Load test assemblies
                var testAssemblies = LoadAssemblies(TestAssemblyPaths);

                if (!testAssemblies.Any())
                {
                    Log.LogWarning("No test assemblies found. Skipping regression tests.");
                    return true;
                }

                // Run regression suite
                var regressionResults = RunRegressionSuiteAsync(testAssemblies).GetAwaiter().GetResult();

                // Run performance benchmarks if requested
                PerformanceBenchmarkResults? benchmarkResults = null;
                if (RunBenchmarks)
                {
                    Log.LogMessage(MessageImportance.Normal, "Running performance benchmarks...");
                    benchmarkResults = RunPerformanceBenchmarksAsync(BenchmarkIterations).GetAwaiter().GetResult();
                }

                // Generate reports
                if (!string.IsNullOrEmpty(OutputDirectory))
                {
                    GenerateReports(regressionResults, benchmarkResults, OutputDirectory);
                }

                // Report results
                ReportRegressionResults(regressionResults, benchmarkResults);

                // Check if build should fail
                var shouldFail = ShouldFailBuild(regressionResults, benchmarkResults);
                if (shouldFail)
                {
                    Log.LogError("Regression testing failed. Fix critical issues before proceeding.");
                    return false;
                }

                Log.LogMessage(MessageImportance.Normal, "Regression testing completed successfully.");
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
                        Log.LogMessage(MessageImportance.Low, "Loaded test assembly: {Assembly}", assembly.GetName().Name);
                    }
                    else
                    {
                        Log.LogWarning("Test assembly file not found: {Path}", path);
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to load test assembly {Path}: {Message}", path, ex.Message);
                }
            }

            return assemblies;
        }

        private async Task<RegressionHarnessResults> RunRegressionSuiteAsync(List<Assembly> testAssemblies)
        {
            // Note: In a real implementation, this would use proper dependency injection
            // For now, we'll run with basic assembly loading
            return await RegressionHarness.RunRegressionSuiteAsync(testAssemblies);
        }

        private async Task<PerformanceBenchmarkResults> RunPerformanceBenchmarksAsync(int iterations)
        {
            return await RegressionHarness.RunPerformanceBenchmarksAsync(iterations);
        }

        private void GenerateReports(
            RegressionHarnessResults regressionResults,
            PerformanceBenchmarkResults? benchmarkResults,
            string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            // Generate regression results JSON
            var regressionJsonPath = Path.Combine(outputDirectory, $"regression-results-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
            var regressionJson = System.Text.Json.JsonSerializer.Serialize(regressionResults, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(regressionJsonPath, regressionJson);

            // Generate benchmark results JSON if available
            if (benchmarkResults != null)
            {
                var benchmarkJsonPath = Path.Combine(outputDirectory, $"benchmark-results-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
                var benchmarkJson = System.Text.Json.JsonSerializer.Serialize(benchmarkResults, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(benchmarkJsonPath, benchmarkJson);
            }

            // Generate summary markdown
            var summaryPath = Path.Combine(outputDirectory, $"regression-summary-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.md");
            var summaryContent = GenerateSummaryMarkdown(regressionResults, benchmarkResults);
            File.WriteAllText(summaryPath, summaryContent);

            Log.LogMessage(MessageImportance.Normal, "Generated regression reports: JSON, Markdown");
        }

        private void ReportRegressionResults(
            RegressionHarnessResults regressionResults,
            PerformanceBenchmarkResults? benchmarkResults)
        {
            Log.LogMessage(MessageImportance.Normal, "Regression Test Results:");
            Log.LogMessage(MessageImportance.Normal, $"  Test Run ID: {regressionResults.TestRunId}");
            Log.LogMessage(MessageImportance.Normal, $"  Duration: {regressionResults.Duration.TotalSeconds:F1}s");
            Log.LogMessage(MessageImportance.Normal, $"  Scenarios: {regressionResults.PassedScenarios}/{regressionResults.TotalScenarios} passed ({regressionResults.SuccessRate:P1})");

            foreach (var scenario in regressionResults.Scenarios)
            {
                var status = scenario.Success ? "✅ PASSED" : "❌ FAILED";
                var perfNote = scenario.PerformanceRegression ? " (⚠️ Performance regression)" : "";
                Log.LogMessage(MessageImportance.Normal,
                    $"    {scenario.ScenarioName}: {status} in {scenario.Duration.TotalSeconds:F1}s{perfNote}");

                if (!scenario.Success && !string.IsNullOrEmpty(scenario.ErrorMessage))
                {
                    Log.LogMessage(MessageImportance.Normal, $"      Error: {scenario.ErrorMessage}");
                }

                if (scenario.PerformanceRegression && !string.IsNullOrEmpty(scenario.PerformanceNotes))
                {
                    Log.LogMessage(MessageImportance.Normal, $"      Performance: {scenario.PerformanceNotes}");
                }
            }

            if (regressionResults.PerformanceRegressions.Any())
            {
                Log.LogMessage(MessageImportance.Normal, $"  Performance Regressions: {regressionResults.PerformanceRegressions.Count}");
                foreach (var regression in regressionResults.PerformanceRegressions)
                {
                    Log.LogMessage(MessageImportance.Normal,
                        $"    {regression.ScenarioName}: {regression.RegressionFactor:F1}x slower than expected");
                }
            }

            if (benchmarkResults != null)
            {
                Log.LogMessage(MessageImportance.Normal, "Performance Benchmark Results:");
                Log.LogMessage(MessageImportance.Normal, $"  Iterations: {benchmarkResults.Iterations}");
                Log.LogMessage(MessageImportance.Normal, $"  Benchmarks: {benchmarkResults.PassedBenchmarks}/{benchmarkResults.TotalBenchmarks} passed");

                foreach (var benchmark in benchmarkResults.Benchmarks)
                {
                    var status = benchmark.Success ? "✅ PASSED" : "❌ FAILED";
                    Log.LogMessage(MessageImportance.Normal,
                        $"    {benchmark.BenchmarkName}: {status} ({benchmark.AverageDuration.TotalMilliseconds:F2}ms avg)");

                    if (!benchmark.Success && !string.IsNullOrEmpty(benchmark.PerformanceNotes))
                    {
                        Log.LogMessage(MessageImportance.Normal, $"      Issue: {benchmark.PerformanceNotes}");
                    }
                }
            }
        }

        private bool ShouldFailBuild(
            RegressionHarnessResults regressionResults,
            PerformanceBenchmarkResults? benchmarkResults)
        {
            // Fail if any critical scenarios failed
            var criticalFailures = regressionResults.Scenarios
                .Where(s => !s.Success)
                .ToList();

            if (criticalFailures.Any())
            {
                foreach (var failure in criticalFailures)
                {
                    Log.LogError($"Critical scenario failed: {failure.ScenarioName}");
                }
                return true;
            }

            // Fail if there are significant performance regressions
            var majorRegressions = regressionResults.PerformanceRegressions
                .Where(r => r.RegressionFactor > 2.0) // More than 2x slower
                .ToList();

            if (majorRegressions.Any())
            {
                foreach (var regression in majorRegressions)
                {
                    Log.LogError($"Major performance regression: {regression.ScenarioName} ({regression.RegressionFactor:F1}x slower)");
                }
                return true;
            }

            // Fail if benchmarks failed
            if (benchmarkResults != null && benchmarkResults.FailedBenchmarks > 0)
            {
                Log.LogError($"{benchmarkResults.FailedBenchmarks} performance benchmarks failed");
                return true;
            }

            return false;
        }

        private string GenerateSummaryMarkdown(
            RegressionHarnessResults regressionResults,
            PerformanceBenchmarkResults? benchmarkResults)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("# slskdN Regression Test Summary");
            sb.AppendLine($"**Generated:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine($"**Test Run ID:** {regressionResults.TestRunId}");
            sb.AppendLine();

            sb.AppendLine("## Regression Test Results");
            sb.AppendLine($"**Duration:** {regressionResults.Duration.TotalSeconds:F1} seconds");
            sb.AppendLine($"**Scenarios:** {regressionResults.PassedScenarios}/{regressionResults.TotalScenarios} passed");
            sb.AppendLine($"**Success Rate:** {regressionResults.SuccessRate:P1}");
            sb.AppendLine();

            sb.AppendLine("### Scenario Details");
            sb.AppendLine("| Scenario | Status | Duration | Notes |");
            sb.AppendLine("|----------|--------|----------|-------|");

            foreach (var scenario in regressionResults.Scenarios)
            {
                var status = scenario.Success ? "✅ Passed" : "❌ Failed";
                var duration = $"{scenario.Duration.TotalSeconds:F1}s";
                var notes = scenario.PerformanceRegression ? "⚠️ Performance regression" :
                           !scenario.Success ? scenario.ErrorMessage : "";
                sb.AppendLine($"| {scenario.ScenarioName} | {status} | {duration} | {notes} |");
            }

            sb.AppendLine();

            if (regressionResults.PerformanceRegressions.Any())
            {
                sb.AppendLine("### Performance Regressions");
                sb.AppendLine("| Scenario | Regression Factor | Expected | Actual |");
                sb.AppendLine("|----------|-------------------|----------|--------|");

                foreach (var regression in regressionResults.PerformanceRegressions)
                {
                    sb.AppendLine($"| {regression.ScenarioName} | {regression.RegressionFactor:F1}x | {regression.ExpectedDuration.TotalSeconds:F1}s | {regression.ActualDuration.TotalSeconds:F1}s |");
                }

                sb.AppendLine();
            }

            if (benchmarkResults != null)
            {
                sb.AppendLine("## Performance Benchmarks");
                sb.AppendLine($"**Iterations per Benchmark:** {benchmarkResults.Iterations}");
                sb.AppendLine($"**Benchmarks:** {benchmarkResults.PassedBenchmarks}/{benchmarkResults.TotalBenchmarks} passed");
                sb.AppendLine();

                sb.AppendLine("### Benchmark Details");
                sb.AppendLine("| Benchmark | Status | Avg Duration | Notes |");
                sb.AppendLine("|-----------|--------|--------------|-------|");

                foreach (var benchmark in benchmarkResults.Benchmarks)
                {
                    var status = benchmark.Success ? "✅ Passed" : "❌ Failed";
                    var duration = $"{benchmark.AverageDuration.TotalMilliseconds:F2}ms";
                    var notes = benchmark.Success ? "" : benchmark.PerformanceNotes ?? "Failed";
                    sb.AppendLine($"| {benchmark.BenchmarkName} | {status} | {duration} | {notes} |");
                }
            }

            return sb.ToString();
        }
    }
}


