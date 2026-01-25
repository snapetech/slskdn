// <copyright file="BuildTimeAnalyzerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.CodeQuality
{
    using System.Linq;
    using slskd.Common.CodeQuality;
    using Xunit;

    /// <summary>
    ///     Tests for H-CODE02: BuildTimeAnalyzer implementation.
    /// </summary>
    public class BuildTimeAnalyzerTests
    {
        [Fact]
        public void AnalyzeSourceCode_WithBlockingAsyncCall_ReturnsViolation()
        {
            // Arrange
            const string sourceCode = @"
public class TestClass
{
    public async System.Threading.Tasks.Task MyMethod()
    {
        var result = SomeAsyncMethod().Result; // Blocking call
    }

    private System.Threading.Tasks.Task<string> SomeAsyncMethod()
    {
        return System.Threading.Tasks.Task.FromResult(""test"");
    }
}";

            // Act
            var violations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, "TestFile.cs").ToList();

            // Assert
            Assert.Contains(violations, v => v.Rule == "BlockingAsyncCall");
            Assert.Contains(violations, v => v.Message.Contains("Blocking async call detected"));
        }

        [Fact]
        public void AnalyzeSourceCode_WithWaitCall_ReturnsViolation()
        {
            // Arrange
            const string sourceCode = @"
public class TestClass
{
    public void MyMethod()
    {
        SomeAsyncMethod().Wait(); // Blocking call
    }

    private System.Threading.Tasks.Task SomeAsyncMethod()
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}";

            // Act
            var violations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, "TestFile.cs").ToList();

            // Assert
            Assert.Contains(violations, v => v.Rule == "BlockingAsyncCall");
            Assert.Contains(violations, v => v.Message.Contains("Blocking async call detected"));
        }

        [Fact]
        public void AnalyzeSourceCode_WithSqlInjection_ReturnsViolation()
        {
            // Arrange
            const string sourceCode = @"
public class TestClass
{
    public void MyMethod(string userInput)
    {
        var query = $""SELECT * FROM Users WHERE Name = '{userInput}'""; // SQL injection
    }
}";

            // Act
            var violations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, "TestFile.cs").ToList();

            // Assert
            Assert.Contains(violations, v => v.Rule == "PotentialSqlInjection");
        }

        [Fact]
        public void AnalyzeSourceCode_WithEmptyCatchBlock_ReturnsViolation()
        {
            // Arrange
            const string sourceCode = @"
public class TestClass
{
    public void MyMethod()
    {
        try
        {
            DoSomething();
        }
        catch
        {
            // Empty catch block
        }
    }

    private void DoSomething() { }
}";

            // Act
            var violations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, "TestFile.cs").ToList();

            // Assert
            Assert.Contains(violations, v => v.Rule == "EmptyCatchBlock");
        }

        [Fact]
        public void AnalyzeSourceCode_WithStringConcatInLoop_ReturnsViolation()
        {
            // Arrange
            const string sourceCode = @"
public class TestClass
{
    public void MyMethod()
    {
        var result = string.Empty;
        for (int i = 0; i < 10; i++)
        {
            result = result + i.ToString(); // Inefficient string concat in loop
        }
    }
}";

            // Act
            var violations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, "TestFile.cs").ToList();

            // Assert
            Assert.Contains(violations, v => v.Rule == "InefficientStringConcatenation");
        }

        [Fact]
        public void AnalyzeSourceCode_WithMissingNullCheck_ReturnsViolation()
        {
            // Arrange
            const string sourceCode = @"
public class TestClass
{
    public void MyMethod(string input)
    {
        // No null check for input parameter
        var length = input.Length;
    }
}";

            // Act
            var violations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, "TestFile.cs").ToList();

            // Assert
            Assert.Contains(violations, v => v.Rule == "MissingNullCheck");
        }

        [Fact]
        public void AnalyzeSourceCode_WithValidCode_ReturnsNoViolations()
        {
            // Arrange
            const string sourceCode = @"
using System.Threading.Tasks;

public class TestClass
{
    public async Task MyMethod(string input)
    {
        if (input == null)
        {
            throw new System.ArgumentNullException(nameof(input));
        }

        await Task.Delay(100);
    }
}";

            // Act
            var violations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, "TestFile.cs").ToList();

            // Assert
            // Should not have critical violations
            Assert.DoesNotContain(violations, v => v.Severity == ViolationSeverity.Error);
        }

        [Fact]
        public void AnalyzeSourceCode_WithDangerousApiUsage_ReturnsViolation()
        {
            // Arrange
            const string sourceCode = @"
public class TestClass
{
    public void MyMethod()
    {
        // Simulate dangerous API usage (member access so analyzer detects DangerousApiUsage)
        this.ExecuteSqlRaw(""SELECT * FROM Users"");
    }

    private void ExecuteSqlRaw(string sql) { }
}";

            // Act
            var violations = BuildTimeAnalyzer.AnalyzeSourceCode(sourceCode, "TestFile.cs").ToList();

            // Assert
            Assert.Contains(violations, v => v.Rule == "DangerousApiUsage");
        }
    }
}


