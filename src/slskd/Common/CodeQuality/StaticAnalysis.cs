// <copyright file="StaticAnalysis.cs" company="slskdN Team">
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
    ///     Static analysis utilities for code quality enforcement.
    /// </summary>
    /// <remarks>
    ///     H-CODE02: Introduce Static Analysis and Linting.
    ///     Provides comprehensive static analysis for security, performance, and maintainability.
    /// </remarks>
    public static class StaticAnalysis
    {
        /// <summary>
        ///     Runs comprehensive static analysis on an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to analyze.</param>
        /// <param name="logger">Optional logger for analysis results.</param>
        /// <returns>Analysis results with violations and recommendations.</returns>
        public static StaticAnalysisResult AnalyzeAssembly(Assembly assembly, ILogger? logger = null)
        {
            var violations = new List<AnalysisViolation>();

            foreach (var type in assembly.GetTypes())
            {
                violations.AddRange(AnalyzeType(type, logger));
            }

            var result = new StaticAnalysisResult
            {
                AssemblyName = assembly.GetName().Name ?? "Unknown",
                TotalTypesAnalyzed = assembly.GetTypes().Length,
                Violations = violations,
                AnalysisTimestamp = DateTimeOffset.UtcNow
            };

            logger?.LogInformation(
                "[StaticAnalysis] Completed analysis of {Assembly}: {Types} types, {Violations} violations",
                result.AssemblyName, result.TotalTypesAnalyzed, violations.Count);

            return result;
        }

        /// <summary>
        ///     Analyzes a type for code quality violations.
        /// </summary>
        /// <param name="type">The type to analyze.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>List of violations found in the type.</returns>
        public static IEnumerable<AnalysisViolation> AnalyzeType(Type type, ILogger? logger = null)
        {
            var violations = new List<AnalysisViolation>();

            // Skip compiler-generated types
            if (type.Name.Contains('<') || type.Name.Contains('>') ||
                type.Name.StartsWith("_") || type.IsNestedPrivate)
            {
                return violations;
            }

            // Analyze methods
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                violations.AddRange(AnalyzeMethod(method, logger));
            }

            // Analyze properties
            foreach (var property in type.GetProperties())
            {
                violations.AddRange(AnalyzeProperty(property, logger));
            }

            // Analyze type-level issues
            violations.AddRange(AnalyzeTypeLevelIssues(type, logger));

            return violations;
        }

        /// <summary>
        ///     Analyzes a method for code quality violations.
        /// </summary>
        /// <param name="method">The method to analyze.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>List of violations found in the method.</returns>
        public static IEnumerable<AnalysisViolation> AnalyzeMethod(MethodInfo method, ILogger? logger = null)
        {
            var violations = new List<AnalysisViolation>();
            var location = $"{method.DeclaringType?.FullName}.{method.Name}";

            // Skip compiler-generated and property accessors
            if (method.Name.Contains('<') || method.Name.Contains('>') ||
                method.Name.StartsWith("get_") || method.Name.StartsWith("set_") ||
                method.Name.StartsWith("<") || method.IsSpecialName)
            {
                return violations;
            }

            // Check for async method naming violations
            if (AsyncRules.HasSuspiciousAsyncNaming(method))
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "AsyncNaming",
                    Severity = ViolationSeverity.Warning,
                    Message = "Method returns Task but has 'Sync' in name",
                    Recommendation = "Rename method to remove 'Sync' suffix or make it synchronous"
                });
            }

            // Check for missing cancellation token parameters
            if (AsyncRules.MethodNeedsCancellationToken(method))
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "MissingCancellationToken",
                    Severity = ViolationSeverity.Warning,
                    Message = "Long-running method should accept CancellationToken parameter",
                    Recommendation = "Add CancellationToken parameter and pass it to async operations"
                });
            }

            // Check for security issues
            violations.AddRange(AnalyzeMethodSecurity(method, location, logger));

            // Check for performance issues
            violations.AddRange(AnalyzeMethodPerformance(method, location, logger));

            return violations;
        }

        /// <summary>
        ///     Analyzes a property for code quality violations.
        /// </summary>
        /// <param name="property">The property to analyze.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>List of violations found in the property.</returns>
        public static IEnumerable<AnalysisViolation> AnalyzeProperty(PropertyInfo property, ILogger? logger = null)
        {
            var violations = new List<AnalysisViolation>();
            var location = $"{property.DeclaringType?.FullName}.{property.Name}";

            // Check for mutable public properties (encapsulation violation)
            if (property.CanWrite && property.SetMethod?.IsPublic == true)
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "MutablePublicProperty",
                    Severity = ViolationSeverity.Info,
                    Message = "Public mutable property violates encapsulation principles",
                    Recommendation = "Consider making setter private or using immutable types"
                });
            }

            // Check for properties that expose sensitive types
            if (SecurityRules.ExposesSensitiveData(property))
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "ExposesSensitiveData",
                    Severity = ViolationSeverity.Warning,
                    Message = "Property exposes potentially sensitive data type",
                    Recommendation = "Consider using secure access patterns or sanitization"
                });
            }

            return violations;
        }

        /// <summary>
        ///     Analyzes type-level code quality issues.
        /// </summary>
        /// <param name="type">The type to analyze.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>List of type-level violations.</returns>
        public static IEnumerable<AnalysisViolation> AnalyzeTypeLevelIssues(Type type, ILogger? logger = null)
        {
            var violations = new List<AnalysisViolation>();
            var location = type.FullName ?? "Unknown";

            // Check for missing XML documentation
            if (string.IsNullOrEmpty(type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description) &&
                !type.Name.Contains('<') && !type.IsNested)
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "MissingDocumentation",
                    Severity = ViolationSeverity.Info,
                    Message = "Public type lacks XML documentation",
                    Recommendation = "Add XML documentation comments to public types"
                });
            }

            // Check for large classes (maintainability issue)
            var methodCount = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Length;
            if (methodCount > 20)
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "LargeClass",
                    Severity = ViolationSeverity.Info,
                    Message = $"Class has {methodCount} public methods (consider splitting responsibilities)",
                    Recommendation = "Consider extracting some methods into separate classes"
                });
            }

            // Check for security issues at type level
            violations.AddRange(AnalyzeTypeSecurity(type, location, logger));

            return violations;
        }

        private static IEnumerable<AnalysisViolation> AnalyzeMethodSecurity(MethodInfo method, string location, ILogger? logger)
        {
            var violations = new List<AnalysisViolation>();

            // Check for dangerous method patterns
            if (SecurityRules.IsDangerousMethod(method))
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "DangerousMethod",
                    Severity = ViolationSeverity.Error,
                    Message = $"Method uses dangerous pattern: {method.Name}",
                    Recommendation = "Review method for security implications and consider safer alternatives"
                });
            }

            // Check for proper parameter validation
            var parameters = method.GetParameters();
            foreach (var param in parameters)
            {
                if (SecurityRules.ParameterNeedsValidation(param))
                {
                    violations.Add(new AnalysisViolation
                    {
                        Location = location,
                        Rule = "MissingParameterValidation",
                        Severity = ViolationSeverity.Warning,
                        Message = $"Parameter '{param.Name}' should be validated for security",
                        Recommendation = "Add input validation for parameter"
                    });
                }
            }

            return violations;
        }

        private static IEnumerable<AnalysisViolation> AnalyzeMethodPerformance(MethodInfo method, string location, ILogger? logger)
        {
            var violations = new List<AnalysisViolation>();

            // Check for potentially expensive operations in hot paths
            if (PerformanceRules.IsExpensiveOperation(method))
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "ExpensiveOperation",
                    Severity = ViolationSeverity.Info,
                    Message = "Method contains potentially expensive operation",
                    Recommendation = "Consider caching, optimization, or moving to background thread"
                });
            }

            // Check for large parameter lists (maintainability)
            var paramCount = method.GetParameters().Length;
            if (paramCount > 7)
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "TooManyParameters",
                    Severity = ViolationSeverity.Info,
                    Message = $"Method has {paramCount} parameters (consider parameter object)",
                    Recommendation = "Use parameter object or reduce parameter count"
                });
            }

            return violations;
        }

        private static IEnumerable<AnalysisViolation> AnalyzeTypeSecurity(Type type, string location, ILogger? logger)
        {
            var violations = new List<AnalysisViolation>();

            // Check for types that store sensitive data
            if (SecurityRules.TypeStoresSensitiveData(type))
            {
                violations.Add(new AnalysisViolation
                {
                    Location = location,
                    Rule = "StoresSensitiveData",
                    Severity = ViolationSeverity.Warning,
                    Message = "Type appears to store sensitive data",
                    Recommendation = "Ensure proper encryption and access controls"
                });
            }

            return violations;
        }
    }

    /// <summary>
    ///     Security analysis rules.
    /// </summary>
    internal static class SecurityRules
    {
        private static readonly string[] DangerousMethodNames = new[]
        {
            "ExecuteSqlRaw", "FromSqlRaw", "ExecuteSql", "FromSql",
            "DangerousGet", "ProcessStart", "ExecuteCommand",
            "Deserialize", "FromBase64String"
        };

        private static readonly Type[] SensitiveTypes = new[]
        {
            typeof(string), // Could contain passwords, tokens, etc.
            typeof(byte[]), // Could contain keys, encrypted data
            typeof(System.Security.SecureString)
        };

        public static bool IsDangerousMethod(MethodInfo method)
        {
            return DangerousMethodNames.Any(name =>
                method.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        public static bool ParameterNeedsValidation(ParameterInfo param)
        {
            // SQL parameters, file paths, URLs, etc. need validation
            var paramType = param.ParameterType;
            return paramType == typeof(string) &&
                   (param.Name?.Contains("sql", StringComparison.OrdinalIgnoreCase) == true ||
                    param.Name?.Contains("path", StringComparison.OrdinalIgnoreCase) == true ||
                    param.Name?.Contains("url", StringComparison.OrdinalIgnoreCase) == true ||
                    param.Name?.Contains("query", StringComparison.OrdinalIgnoreCase) == true);
        }

        public static bool ExposesSensitiveData(PropertyInfo property)
        {
            return SensitiveTypes.Contains(property.PropertyType) &&
                   property.Name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                   property.Name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                   property.Name.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                   property.Name.Contains("token", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TypeStoresSensitiveData(Type type)
        {
            return type.GetProperties().Any(ExposesSensitiveData) ||
                   type.Name.Contains("Credential", StringComparison.OrdinalIgnoreCase) ||
                   type.Name.Contains("Auth", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     Performance analysis rules.
    /// </summary>
    internal static class PerformanceRules
    {
        private static readonly string[] ExpensiveOperationNames = new[]
        {
            "SaveChanges", "SaveChangesAsync", "ToList", "ToArray",
            "Count", "Any", "First", "Single", "Last",
            "ComputeHash", "Encrypt", "Decrypt"
        };

        public static bool IsExpensiveOperation(MethodInfo method)
        {
            return ExpensiveOperationNames.Any(name =>
                method.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Result of static analysis.
    /// </summary>
    public sealed class StaticAnalysisResult
    {
        /// <summary>
        ///     Gets the name of the analyzed assembly.
        /// </summary>
        public string? AssemblyName { get; init; }

        /// <summary>
        ///     Gets the total number of types analyzed.
        /// </summary>
        public int TotalTypesAnalyzed { get; init; }

        /// <summary>
        ///     Gets the list of violations found.
        /// </summary>
        public IReadOnlyList<AnalysisViolation> Violations { get; init; } = Array.Empty<AnalysisViolation>();

        /// <summary>
        ///     Gets the timestamp when analysis was performed.
        /// </summary>
        public DateTimeOffset AnalysisTimestamp { get; init; }

        /// <summary>
        ///     Gets the violations grouped by severity.
        /// </summary>
        public Dictionary<ViolationSeverity, int> ViolationsBySeverity =>
            Violations.GroupBy(v => v.Severity)
                     .ToDictionary(g => g.Key, g => g.Count);
    }

    /// <summary>
    ///     Represents a static analysis violation.
    /// </summary>
    public sealed class AnalysisViolation
    {
        /// <summary>
        ///     Gets the location of the violation (type.method, etc.).
        /// </summary>
        public string? Location { get; init; }

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
        ///     Gets the recommended fix.
        /// </summary>
        public string? Recommendation { get; init; }
    }
}

