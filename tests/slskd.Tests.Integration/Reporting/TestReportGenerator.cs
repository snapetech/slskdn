namespace slskd.Tests.Integration.Reporting;

using System.Text;

/// <summary>
/// Test result visualization and reporting.
/// </summary>
public class TestReportGenerator
{
    public static string GenerateMarkdownReport(TestRunResults results)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Integration Test Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated**: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine($"**Duration**: {results.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total**: {results.TotalTests}");
        sb.AppendLine($"- **Passed**: ✅ {results.PassedTests}");
        sb.AppendLine($"- **Failed**: ❌ {results.FailedTests}");
        sb.AppendLine($"- **Skipped**: ⏭️ {results.SkippedTests}");
        sb.AppendLine($"- **Success Rate**: {results.SuccessRate:P2}");
        sb.AppendLine();

        // By Category
        sb.AppendLine("## Results by Category");
        sb.AppendLine();
        sb.AppendLine("| Category | Passed | Failed | Skipped | Total |");
        sb.AppendLine("|----------|--------|--------|---------|-------|");

        foreach (var category in results.Categories)
        {
            sb.AppendLine($"| {category.Name} | {category.Passed} | {category.Failed} | {category.Skipped} | {category.Total} |");
        }

        sb.AppendLine();

        // Failed Tests
        if (results.FailedTests > 0)
        {
            sb.AppendLine("## ❌ Failed Tests");
            sb.AppendLine();

            foreach (var failure in results.Failures)
            {
                sb.AppendLine($"### {failure.TestName}");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(failure.ErrorMessage);
                sb.AppendLine("```");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(failure.StackTrace))
                {
                    sb.AppendLine("<details>");
                    sb.AppendLine("<summary>Stack Trace</summary>");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine(failure.StackTrace);
                    sb.AppendLine("```");
                    sb.AppendLine("</details>");
                    sb.AppendLine();
                }
            }
        }

        // Performance Metrics
        if (results.PerformanceMetrics.Count > 0)
        {
            sb.AppendLine("## 📊 Performance Metrics");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value | Target | Status |");
            sb.AppendLine("|--------|-------|--------|--------|");

            foreach (var metric in results.PerformanceMetrics)
            {
                var status = metric.Value <= metric.Target ? "✅" : "⚠️";
                sb.AppendLine($"| {metric.Name} | {metric.Value:F2}{metric.Unit} | {metric.Target:F2}{metric.Unit} | {status} |");
            }

            sb.AppendLine();
        }

        // Coverage
        if (results.CodeCoverage != null)
        {
            sb.AppendLine("## 🔍 Code Coverage");
            sb.AppendLine();
            sb.AppendLine($"- **Line Coverage**: {results.CodeCoverage.LineRate:P2}");
            sb.AppendLine($"- **Branch Coverage**: {results.CodeCoverage.BranchRate:P2}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string GenerateHtmlReport(TestRunResults results)
    {
        return $"""
        <!DOCTYPE html>
        <html>
        <head>
            <title>Integration Test Report</title>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 20px; }}
                .summary {{ background: #f0f0f0; padding: 15px; border-radius: 5px; }}
                .passed {{ color: green; }}
                .failed {{ color: red; }}
                table {{ border-collapse: collapse; width: 100%; margin: 20px 0; }}
                th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                th {{ background-color: #4CAF50; color: white; }}
            </style>
        </head>
        <body>
            <h1>Integration Test Report</h1>
            <div class="summary">
                <p><strong>Generated:</strong> {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}</p>
                <p><strong>Duration:</strong> {results.TotalDuration.TotalSeconds:F2}s</p>
                <p><strong>Total:</strong> {results.TotalTests}</p>
                <p class="passed"><strong>Passed:</strong> ✅ {results.PassedTests}</p>
                <p class="failed"><strong>Failed:</strong> ❌ {results.FailedTests}</p>
                <p><strong>Success Rate:</strong> {results.SuccessRate:P2}</p>
            </div>

            <h2>Results by Category</h2>
            <table>
                <tr>
                    <th>Category</th>
                    <th>Passed</th>
                    <th>Failed</th>
                    <th>Skipped</th>
                    <th>Total</th>
                </tr>
                {string.Join("", results.Categories.Select(c => $@"
                <tr>
                    <td>{c.Name}</td>
                    <td>{c.Passed}</td>
                    <td>{c.Failed}</td>
                    <td>{c.Skipped}</td>
                    <td>{c.Total}</td>
                </tr>"))}
            </table>

            {(results.FailedTests > 0 ? $@"
            <h2>❌ Failed Tests</h2>
            <ul>
                {string.Join("", results.Failures.Select(f => $"<li><strong>{f.TestName}</strong>: {f.ErrorMessage}</li>"))}
            </ul>" : "")}
        </body>
        </html>
        """;
    }
}

public class TestRunResults
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double SuccessRate => TotalTests > 0 ? (double)PassedTests / TotalTests : 0;
    public List<TestCategoryResult> Categories { get; set; } = new();
    public List<TestFailure> Failures { get; set; } = new();
    public List<PerformanceMetric> PerformanceMetrics { get; set; } = new();
    public CodeCoverageInfo? CodeCoverage { get; set; }
}

public class TestCategoryResult
{
    public string Name { get; set; } = string.Empty;
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Total { get; set; }
}

public class TestFailure
{
    public string TestName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}

public class PerformanceMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Target { get; set; }
    public string Unit { get; set; } = "ms";
}

public class CodeCoverageInfo
{
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
}
