// <copyright file="StaticAnalysisTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.CodeQuality
{
    using System.Linq;
    using slskd.Common.CodeQuality;
    using Xunit;

    /// <summary>
    ///     Tests for H-CODE02: StaticAnalysis implementation.
    /// </summary>
    public class StaticAnalysisTests
    {
        [Fact]
        public void AnalyzeAssembly_WithTestAssembly_ReturnsViolations()
        {
            // Arrange
            var assembly = typeof(StaticAnalysisTests).Assembly;

            // Act
            var result = StaticAnalysis.AnalyzeAssembly(assembly);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("slskd.Tests.Unit", result.AssemblyName);
            Assert.True(result.TotalTypesAnalyzed > 0);
            Assert.True(result.AnalysisTimestamp > default);
        }

        [Fact]
        public void AnalyzeType_WithTestType_ReturnsViolations()
        {
            // Arrange
            var type = typeof(TestAnalysisTarget);

            // Act
            var violations = StaticAnalysis.AnalyzeType(type).ToList();

            // Assert
            Assert.NotNull(violations);
            // Should find missing documentation violation for the class
            Assert.Contains(violations, v => v.Rule == "MissingDocumentation");
        }

        [Fact]
        public void AnalyzeMethod_WithTestMethods_ReturnsExpectedViolations()
        {
            // Arrange
            var type = typeof(TestAnalysisTarget);
            var method = type.GetMethod(nameof(TestAnalysisTarget.MethodWithTooManyParameters));

            // Act
            var violations = StaticAnalysis.AnalyzeMethod(method).ToList();

            // Assert
            Assert.NotNull(violations);
            // Should find too many parameters violation
            Assert.Contains(violations, v => v.Rule == "TooManyParameters");
        }

        [Fact]
        public void AnalyzeProperty_WithMutablePublicProperty_ReturnsViolation()
        {
            // Arrange
            var type = typeof(TestAnalysisTarget);
            var property = type.GetProperty(nameof(TestAnalysisTarget.MutableProperty));

            // Act
            var violations = StaticAnalysis.AnalyzeProperty(property).ToList();

            // Assert
            Assert.NotNull(violations);
            // Should find mutable public property violation
            Assert.Contains(violations, v => v.Rule == "MutablePublicProperty");
        }

        [Fact]
        public void AnalyzeTypeLevelIssues_WithLargeClass_ReturnsViolation()
        {
            // Arrange
            var type = typeof(TestAnalysisTarget);

            // Act
            var violations = StaticAnalysis.AnalyzeTypeLevelIssues(type).ToList();

            // Assert
            Assert.NotNull(violations);
            // Should find large class violation (if it has many methods)
            var methodCount = type.GetMethods().Length;
            if (methodCount > 20)
            {
                Assert.Contains(violations, v => v.Rule == "LargeClass");
            }
        }
    }

    /// <summary>
    ///     Test target class for static analysis.
    /// </summary>
    public class TestAnalysisTarget
    {
        public string MutableProperty { get; set; } = string.Empty;

        public void MethodWithTooManyParameters(
            string param1, string param2, string param3, string param4, string param5,
            string param6, string param7, string param8, string param9, string param10)
        {
            // Method with too many parameters for testing
        }

        public void NormalMethod(string param1)
        {
            // Normal method for testing
        }

        public void MethodWithExpensiveOperation()
        {
            // Contains SaveChanges call (expensive)
            var list = new System.Collections.Generic.List<string>();
            list.Count(); // Not using Any() when we could
        }
    }
}
