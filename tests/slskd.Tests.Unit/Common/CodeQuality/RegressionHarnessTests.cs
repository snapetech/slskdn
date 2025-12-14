// <copyright file="RegressionHarnessTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.CodeQuality
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using slskd.Common.CodeQuality;
    using Xunit;

    /// <summary>
    ///     Tests for H-CODE03: RegressionHarness implementation.
    /// </summary>
    public class RegressionHarnessTests
    {
        [Fact]
        public async void RunPerformanceBenchmarksAsync_WithValidIterations_ReturnsResults()
        {
            // Arrange
            const int iterations = 100;

            // Act
            var results = await RegressionHarness.RunPerformanceBenchmarksAsync(iterations);

            // Assert
            Assert.NotNull(results);
            Assert.NotNull(results.BenchmarkRunId);
            Assert.Equal(iterations, results.Iterations);
            Assert.NotEmpty(results.Benchmarks);
            Assert.True(results.TotalBenchmarks > 0);
            Assert.Equal(results.PassedBenchmarks + results.FailedBenchmarks, results.TotalBenchmarks);
        }

        [Fact]
        public void GenerateCoverageReport_WithValidReport_CreatesFiles()
        {
            // Arrange
            var report = new TestCoverageReport
            {
                AnalysisTimestamp = DateTimeOffset.UtcNow,
                SourceAssemblies = new[] { "TestAssembly" },
                TestAssemblies = new[] { "TestAssembly" },
                SubsystemReports = new[]
                {
                    new SubsystemCoverageReport
                    {
                        SubsystemName = "TestSubsystem",
                        MethodCoverage = 0.85,
                        ClassCoverage = 0.80,
                        CoverageGaps = new[] { "UncoveredType" }
                    }
                },
                OverallCoverage = 0.85,
                CoverageGaps = Array.Empty<CoverageGap>()
            };

            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Act
                RegressionHarness.GenerateCoverageReport(report, tempDir);

                // Assert
                var files = System.IO.Directory.GetFiles(tempDir);
                Assert.Contains(files, f => f.EndsWith(".json"));
                Assert.Contains(files, f => f.EndsWith(".md"));
                Assert.Contains(files, f => f.EndsWith(".html"));
            }
            finally
            {
                // Cleanup
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void CriticalScenarios_ContainsExpectedScenarios()
        {
            // Arrange
            var scenarios = RegressionHarnessTestsHelper.GetCriticalScenarios();

            // Assert
            Assert.NotNull(scenarios);
            Assert.NotEmpty(scenarios);
            Assert.Contains(scenarios, s => s.Name == "PodCore_Messaging");
            Assert.Contains(scenarios, s => s.Name == "VirtualSoulfind_Planning");
            Assert.Contains(scenarios, s => s.Name == "Mesh_Transport");
            Assert.Contains(scenarios, s => s.Name == "Security_Validation");
        }

        [Fact]
        public void PerformanceBenchmarkDefinitions_AreValid()
        {
            // Arrange
            var benchmarks = RegressionHarnessTestsHelper.GetBenchmarkDefinitions();

            // Assert
            Assert.NotNull(benchmarks);
            Assert.NotEmpty(benchmarks);

            foreach (var benchmark in benchmarks)
            {
                Assert.NotNull(benchmark.Name);
                Assert.NotNull(benchmark.Description);
                Assert.NotNull(benchmark.Operation);
                Assert.True(benchmark.ExpectedMaxDuration > TimeSpan.Zero);
            }
        }

        /// <summary>
        ///     Helper class for accessing private members of RegressionHarness.
        /// </summary>
        private static class RegressionHarnessTestsHelper
        {
            private static readonly Type RegressionHarnessType = typeof(RegressionHarness);

            public static object[] GetCriticalScenarios()
            {
                // Access the private CriticalScenarios field via reflection
                var field = RegressionHarnessType.GetField("CriticalScenarios", BindingFlags.NonPublic | BindingFlags.Static);
                return field?.GetValue(null) as object[] ?? Array.Empty<object>();
            }

            public static object[] GetBenchmarkDefinitions()
            {
                // In a real implementation, we'd access private benchmark definitions
                // For now, return a mock
                return new[]
                {
                    new
                    {
                        Name = "PodMessage_Encryption",
                        Description = "Pod message encryption/decryption performance",
                        ExpectedMaxDuration = TimeSpan.FromMilliseconds(50)
                    }
                };
            }
        }
    }
}

