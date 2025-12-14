// <copyright file="BuildTimeAnalyzer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.CodeQuality
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Build-time code analysis using Roslyn syntax trees.
    /// </summary>
    /// <remarks>
    ///     H-CODE02: Introduce Static Analysis and Linting.
    ///     Provides compile-time analysis for security and quality issues.
    /// </remarks>
    public static class BuildTimeAnalyzer
    {
        /// <summary>
        ///     Analyzes C# source code for violations.
        /// </summary>
        /// <param name="sourceCode">The C# source code to analyze.</param>
        /// <param name="filePath">The file path for error reporting.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>List of violations found in the source code.</returns>
        public static IEnumerable<CodeAnalysisViolation> AnalyzeSourceCode(string sourceCode, string filePath, ILogger? logger = null)
        {
            var violations = new List<CodeAnalysisViolation>();

            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = syntaxTree.GetRoot();

                // Analyze for blocking async calls
                violations.AddRange(AnalyzeBlockingAsyncCalls(root, filePath));

                // Analyze for insecure string operations
                violations.AddRange(AnalyzeInsecureStringOperations(root, filePath));

                // Analyze for missing null checks
                violations.AddRange(AnalyzeMissingNullChecks(root, filePath));

                // Analyze for improper exception handling
                violations.AddRange(AnalyzeExceptionHandling(root, filePath));

                // Analyze for security issues
                violations.AddRange(AnalyzeSecurityIssues(root, filePath));

            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to analyze source file {FilePath}", filePath);
            }

            return violations;
        }

        private static IEnumerable<CodeAnalysisViolation> AnalyzeBlockingAsyncCalls(SyntaxNode root, string filePath)
        {
            var violations = new List<CodeAnalysisViolation>();

            // Find .Result calls
            var resultCalls = root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(m => m.Name.Identifier.Text == "Result")
                .Select(m => m.Parent)
                .OfType<MemberAccessExpressionSyntax>();

            foreach (var call in resultCalls)
            {
                violations.Add(new CodeAnalysisViolation
                {
                    FilePath = filePath,
                    LineNumber = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Rule = "BlockingAsyncCall",
                    Severity = ViolationSeverity.Error,
                    Message = "Blocking async call detected (.Result)",
                    CodeSnippet = call.ToString(),
                    Recommendation = "Use 'await' instead of .Result to avoid deadlocks"
                });
            }

            // Find .Wait() calls
            var waitCalls = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression is MemberAccessExpressionSyntax m &&
                           m.Name.Identifier.Text == "Wait");

            foreach (var call in waitCalls)
            {
                violations.Add(new CodeAnalysisViolation
                {
                    FilePath = filePath,
                    LineNumber = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Rule = "BlockingAsyncCall",
                    Severity = ViolationSeverity.Error,
                    Message = "Blocking async call detected (.Wait())",
                    CodeSnippet = call.ToString(),
                    Recommendation = "Use 'await' instead of .Wait() to avoid deadlocks"
                });
            }

            // Find .GetAwaiter().GetResult() calls
            var getAwaiterCalls = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression is MemberAccessExpressionSyntax m &&
                           m.Name.Identifier.Text == "GetResult" &&
                           m.Expression is InvocationExpressionSyntax inner &&
                           inner.Expression is MemberAccessExpressionSyntax innerMember &&
                           innerMember.Name.Identifier.Text == "GetAwaiter");

            foreach (var call in getAwaiterCalls)
            {
                violations.Add(new CodeAnalysisViolation
                {
                    FilePath = filePath,
                    LineNumber = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Rule = "BlockingAsyncCall",
                    Severity = ViolationSeverity.Error,
                    Message = "Blocking async call detected (.GetAwaiter().GetResult())",
                    CodeSnippet = call.ToString(),
                    Recommendation = "Use 'await' instead of .GetAwaiter().GetResult()"
                });
            }

            return violations;
        }

        private static IEnumerable<CodeAnalysisViolation> AnalyzeInsecureStringOperations(SyntaxNode root, string filePath)
        {
            var violations = new List<CodeAnalysisViolation>();

            // Find string concatenation in loops (potential inefficiency)
            var stringConcatInLoops = root.DescendantNodes()
                .OfType<ForStatementSyntax>()
                .Concat(root.DescendantNodes().OfType<ForEachStatementSyntax>())
                .Concat(root.DescendantNodes().OfType<WhileStatementSyntax>())
                .SelectMany(loop => loop.DescendantNodes()
                    .OfType<BinaryExpressionSyntax>()
                    .Where(b => b.OperatorToken.Text == "+" &&
                               (b.Left is IdentifierNameSyntax || b.Right is IdentifierNameSyntax)));

            foreach (var concat in stringConcatInLoops)
            {
                violations.Add(new CodeAnalysisViolation
                {
                    FilePath = filePath,
                    LineNumber = concat.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Rule = "InefficientStringConcatenation",
                    Severity = ViolationSeverity.Warning,
                    Message = "String concatenation in loop detected",
                    CodeSnippet = concat.ToString(),
                    Recommendation = "Use StringBuilder for string concatenation in loops"
                });
            }

            return violations;
        }

        private static IEnumerable<CodeAnalysisViolation> AnalyzeMissingNullChecks(SyntaxNode root, string filePath)
        {
            var violations = new List<CodeAnalysisViolation>();

            // Find method parameters that should be null-checked
            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methodDeclarations)
            {
                var parameters = method.ParameterList.Parameters;

                foreach (var param in parameters)
                {
                    var paramName = param.Identifier.Text;
                    var paramType = param.Type?.ToString();

                    // Check if reference type parameter is used without null check
                    if (IsReferenceType(paramType) && ShouldBeNullChecked(paramName))
                    {
                        var methodBody = method.Body;
                        if (methodBody != null)
                        {
                            var hasNullCheck = HasNullCheck(methodBody, paramName);

                            if (!hasNullCheck)
                            {
                                violations.Add(new CodeAnalysisViolation
                                {
                                    FilePath = filePath,
                                    LineNumber = param.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                    Rule = "MissingNullCheck",
                                    Severity = ViolationSeverity.Warning,
                                    Message = $"Parameter '{paramName}' should be null-checked",
                                    CodeSnippet = param.ToString(),
                                    Recommendation = "Add null check at method start: if (param == null) throw new ArgumentNullException(nameof(param));"
                                });
                            }
                        }
                    }
                }
            }

            return violations;
        }

        private static IEnumerable<CodeAnalysisViolation> AnalyzeExceptionHandling(SyntaxNode root, string filePath)
        {
            var violations = new List<CodeAnalysisViolation>();

            // Find empty catch blocks
            var catchClauses = root.DescendantNodes().OfType<CatchClauseSyntax>();

            foreach (var catchClause in catchClauses)
            {
                var block = catchClause.Block;
                if (block?.Statements.Count == 0)
                {
                    violations.Add(new CodeAnalysisViolation
                    {
                        FilePath = filePath,
                        LineNumber = catchClause.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        Rule = "EmptyCatchBlock",
                        Severity = ViolationSeverity.Warning,
                        Message = "Empty catch block swallows exceptions",
                        CodeSnippet = catchClause.ToString(),
                        Recommendation = "Add exception handling logic or remove empty catch block"
                    });
                }
            }

            // Find catch blocks that only log and rethrow (could be simplified)
            var loggingCatches = root.DescendantNodes().OfType<CatchClauseSyntax>()
                .Where(c => c.Block?.Statements.Count == 1 &&
                           c.Block.Statements[0] is ExpressionStatementSyntax expr &&
                           expr.Expression is InvocationExpressionSyntax invocation &&
                           invocation.Expression is MemberAccessExpressionSyntax member &&
                           (member.Name.Identifier.Text.Contains("Log") ||
                            member.Name.Identifier.Text.Contains("Write")));

            foreach (var catchClause in loggingCatches)
            {
                violations.Add(new CodeAnalysisViolation
                {
                    FilePath = filePath,
                    LineNumber = catchClause.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Rule = "LoggingOnlyCatch",
                    Severity = ViolationSeverity.Info,
                    Message = "Catch block only logs and may rethrow",
                    CodeSnippet = catchClause.ToString(),
                    Recommendation = "Consider removing catch block if only logging, or add handling logic"
                });
            }

            return violations;
        }

        private static IEnumerable<CodeAnalysisViolation> AnalyzeSecurityIssues(SyntaxNode root, string filePath)
        {
            var violations = new List<CodeAnalysisViolation>();

            // Find potential SQL injection vulnerabilities
            var stringInterpolations = root.DescendantNodes()
                .OfType<InterpolatedStringExpressionSyntax>()
                .Where(i => i.ToString().Contains("SELECT", StringComparison.OrdinalIgnoreCase) ||
                           i.ToString().Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
                           i.ToString().Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                           i.ToString().Contains("DELETE", StringComparison.OrdinalIgnoreCase));

            foreach (var interpolation in stringInterpolations)
            {
                violations.Add(new CodeAnalysisViolation
                {
                    FilePath = filePath,
                    LineNumber = interpolation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Rule = "PotentialSqlInjection",
                    Severity = ViolationSeverity.Error,
                    Message = "String interpolation with SQL keywords detected",
                    CodeSnippet = interpolation.ToString(),
                    Recommendation = "Use parameterized queries instead of string interpolation for SQL"
                });
            }

            // Find use of dangerous APIs
            var dangerousInvocations = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression is MemberAccessExpressionSyntax m &&
                           (m.Name.Identifier.Text == "ExecuteSqlRaw" ||
                            m.Name.Identifier.Text == "FromSqlRaw" ||
                            m.Name.Identifier.Text == "ProcessStart" ||
                            m.Name.Identifier.Text == "ExecuteCommand"));

            foreach (var invocation in dangerousInvocations)
            {
                violations.Add(new CodeAnalysisViolation
                {
                    FilePath = filePath,
                    LineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Rule = "DangerousApiUsage",
                    Severity = ViolationSeverity.Warning,
                    Message = $"Potentially dangerous API usage: {invocation.Expression}",
                    CodeSnippet = invocation.ToString(),
                    Recommendation = "Review usage for security implications and consider safer alternatives"
                });
            }

            return violations;
        }

        private static bool IsReferenceType(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            // Simple check for reference types (not comprehensive)
            return !typeName.Contains("int") && !typeName.Contains("bool") &&
                   !typeName.Contains("long") && !typeName.Contains("short") &&
                   !typeName.Contains("byte") && !typeName.Contains("char") &&
                   !typeName.Contains("float") && !typeName.Contains("double") &&
                   !typeName.Contains("decimal");
        }

        private static bool ShouldBeNullChecked(string paramName)
        {
            return !paramName.Contains("cancellationToken", StringComparison.OrdinalIgnoreCase) &&
                   !paramName.Contains("ct", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasNullCheck(BlockSyntax methodBody, string paramName)
        {
            return methodBody.DescendantNodes()
                .OfType<BinaryExpressionSyntax>()
                .Any(b => b.OperatorToken.Text == "!=" &&
                         ((b.Left is IdentifierNameSyntax left && left.Identifier.Text == paramName) ||
                          (b.Right is IdentifierNameSyntax right && right.Identifier.Text == paramName)) &&
                         ((b.Left is LiteralExpressionSyntax leftLit && leftLit.Token.Text == "null") ||
                          (b.Right is LiteralExpressionSyntax rightLit && rightLit.Token.Text == "null")));
        }
    }

    /// <summary>
    ///     Represents a code analysis violation found during build-time analysis.
    /// </summary>
    public sealed class CodeAnalysisViolation
    {
        /// <summary>
        ///     Gets the file path where the violation was found.
        /// </summary>
        public string? FilePath { get; init; }

        /// <summary>
        ///     Gets the line number where the violation was found.
        /// </summary>
        public int LineNumber { get; init; }

        /// <summary>
        ///     Gets the rule that was violated.
        /// </summary>
        public string? Rule { get; init; }

        /// <summary>
        ///     Gets the severity of the violation.
        /// </summary>
        public ViolationSeverity Severity { get; init; }

        /// <summary>
        ///     Gets the violation message.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        ///     Gets the code snippet that caused the violation.
        /// </summary>
        public string? CodeSnippet { get; init; }

        /// <summary>
        ///     Gets the recommended fix.
        /// </summary>
        public string? Recommendation { get; init; }
    }
}

