// <copyright file="SlskdnAnalyzer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.CodeQuality
{
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Diagnostics;

    /// <summary>
    ///     Roslyn analyzer for slskdN code quality rules.
    /// </summary>
    /// <remarks>
    ///     H-CODE02: Introduce Static Analysis and Linting.
    ///     Provides compile-time analysis for slskdN-specific code quality rules.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SlskdnAnalyzer : DiagnosticAnalyzer
    {
        // Diagnostic descriptors for different rules
        public static readonly DiagnosticDescriptor BlockingAsyncCallRule = new(
            id: "SLKDN001",
            title: "Blocking Async Call",
            messageFormat: "Avoid blocking calls on async operations. Use 'await' instead of '{0}'.",
            category: "Async",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Blocking calls on async operations can cause deadlocks. Use await instead.");

        public static readonly DiagnosticDescriptor DangerousApiUsageRule = new(
            id: "SLKDN002",
            title: "Dangerous API Usage",
            messageFormat: "Usage of potentially dangerous API '{0}' detected.",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Review usage of dangerous APIs for security implications.");

        public static readonly DiagnosticDescriptor SqlInjectionRiskRule = new(
            id: "SLKDN003",
            title: "SQL Injection Risk",
            messageFormat: "Potential SQL injection vulnerability detected in string interpolation.",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Use parameterized queries instead of string interpolation for SQL operations.");

        public static readonly DiagnosticDescriptor MissingNullCheckRule = new(
            id: "SLKDN004",
            title: "Missing Null Check",
            messageFormat: "Parameter '{0}' should be null-checked.",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Reference type parameters should be validated for null values.");

        public static readonly DiagnosticDescriptor EmptyCatchBlockRule = new(
            id: "SLKDN005",
            title: "Empty Catch Block",
            messageFormat: "Empty catch block swallows exceptions.",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty catch blocks should be avoided as they swallow exceptions without handling them.");

        public static readonly DiagnosticDescriptor InefficientStringConcatRule = new(
            id: "SLKDN006",
            title: "Inefficient String Concatenation",
            messageFormat: "String concatenation in loop detected. Use StringBuilder instead.",
            category: "Performance",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "String concatenation in loops creates many temporary strings. Use StringBuilder for efficiency.");

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                BlockingAsyncCallRule,
                DangerousApiUsageRule,
                SqlInjectionRiskRule,
                MissingNullCheckRule,
                EmptyCatchBlockRule,
                InefficientStringConcatRule);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Register syntax node actions
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeInterpolatedString, SyntaxKind.InterpolatedStringExpression);
            context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
            context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.AddExpression);
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax)context.Node;

            // Check for blocking async calls
            if (IsBlockingAsyncCall(invocation))
            {
                var diagnostic = Diagnostic.Create(
                    BlockingAsyncCallRule,
                    invocation.GetLocation(),
                    invocation.ToString());
                context.ReportDiagnostic(diagnostic);
            }

            // Check for dangerous API usage
            if (IsDangerousApi(invocation))
            {
                var methodName = GetMethodName(invocation);
                var diagnostic = Diagnostic.Create(
                    DangerousApiUsageRule,
                    invocation.GetLocation(),
                    methodName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeInterpolatedString(SyntaxNodeAnalysisContext context)
        {
            var interpolatedString = (Microsoft.CodeAnalysis.CSharp.Syntax.InterpolatedStringExpressionSyntax)context.Node;

            // Check for potential SQL injection
            if (ContainsSqlKeywords(interpolatedString.ToString()))
            {
                var diagnostic = Diagnostic.Create(
                    SqlInjectionRiskRule,
                    interpolatedString.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax)context.Node;

            // Check for empty catch blocks
            if (catchClause.Block?.Statements.Count == 0)
            {
                var diagnostic = Diagnostic.Create(
                    EmptyCatchBlockRule,
                    catchClause.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
        {
            var binaryExpr = (Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax)context.Node;

            // Check for string concatenation in loops
            if (binaryExpr.OperatorToken.Text == "+" &&
                IsInLoop(binaryExpr) &&
                IsStringConcatenation(binaryExpr))
            {
                var diagnostic = Diagnostic.Create(
                    InefficientStringConcatRule,
                    binaryExpr.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax)context.Node;

            // Check for missing null checks on reference type parameters
            foreach (var parameter in methodDeclaration.ParameterList.Parameters)
            {
                var parameterType = context.SemanticModel.GetTypeInfo(parameter.Type!).Type;
                if (parameterType != null && !parameterType.IsValueType && !IsNullableValueType(parameterType))
                {
                    var parameterName = parameter.Identifier.Text;

                    // Check if parameter is used without null check
                    if (ShouldCheckForNull(parameterName) && !HasNullCheck(methodDeclaration, parameterName))
                    {
                        var diagnostic = Diagnostic.Create(
                            MissingNullCheckRule,
                            parameter.GetLocation(),
                            parameterName);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsBlockingAsyncCall(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
        {
            var expression = invocation.Expression.ToString();

            // Check for .Result, .Wait(), .GetAwaiter().GetResult()
            return expression.EndsWith(".Result") ||
                   expression.EndsWith(".Wait()") ||
                   expression.Contains(".GetAwaiter().GetResult()");
        }

        private static bool IsDangerousApi(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
        {
            var methodName = GetMethodName(invocation);

            return methodName is "ExecuteSqlRaw" or "FromSqlRaw" or "ExecuteSql" or
                   "FromSql" or "ProcessStart" or "ExecuteCommand";
        }

        private static string GetMethodName(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
        {
            return invocation.Expression switch
            {
                Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess =>
                    memberAccess.Name.Identifier.Text,
                Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax identifier =>
                    identifier.Identifier.Text,
                _ => invocation.Expression.ToString()
            };
        }

        private static bool ContainsSqlKeywords(string text)
        {
            var sqlKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER" };
            return sqlKeywords.Any(keyword =>
                text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsInLoop(Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax binaryExpr)
        {
            // Walk up the syntax tree to check if we're inside a loop
            var current = binaryExpr.Parent;
            while (current != null)
            {
                if (current is Microsoft.CodeAnalysis.CSharp.Syntax.ForStatementSyntax ||
                    current is Microsoft.CodeAnalysis.CSharp.Syntax.ForEachStatementSyntax ||
                    current is Microsoft.CodeAnalysis.CSharp.Syntax.WhileStatementSyntax ||
                    current is Microsoft.CodeAnalysis.CSharp.Syntax.DoStatementSyntax)
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }

        private static bool IsStringConcatenation(Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax binaryExpr)
        {
            // Simple check - in a real analyzer, we'd check semantic types
            return binaryExpr.Left.ToString().Contains("\"") || binaryExpr.Right.ToString().Contains("\"") ||
                   binaryExpr.Left is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax ||
                   binaryExpr.Right is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax;
        }

        private static bool IsNullableValueType(ITypeSymbol type)
        {
            return type.IsValueType && type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }

        private static bool ShouldCheckForNull(string parameterName)
        {
            // Skip certain parameter names that are commonly not null-checked
            var skipNames = new[] { "cancellationToken", "ct", "logger" };
            return !skipNames.Contains(parameterName, StringComparer.OrdinalIgnoreCase);
        }

        private static bool HasNullCheck(Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method, string parameterName)
        {
            // Simple check for null comparison in method body
            // A real implementation would do more sophisticated analysis
            var methodBody = method.Body;
            if (methodBody == null)
            {
                return false;
            }

            return methodBody.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax>()
                .Any(b => b.OperatorToken.Text is "==" or "!=" &&
                         ((b.Left.ToString() == parameterName && b.Right.ToString() == "null") ||
                          (b.Right.ToString() == parameterName && b.Left.ToString() == "null")));
        }
    }
}


