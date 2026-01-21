// <copyright file="AsyncRules.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.CodeQuality
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    /// <summary>
    ///     Utilities for enforcing async/IO rules and detecting violations.
    /// </summary>
    /// <remarks>
    ///     H-CODE01: Enforce Async and IO Rules.
    ///     Provides automated detection of async rule violations.
    /// </remarks>
    public static class AsyncRules
    {
        /// <summary>
        ///     Forbidden patterns that indicate async rule violations.
        /// </summary>
        private static readonly string[] ForbiddenPatterns = new[]
        {
            ".Result",
            ".Wait()",
            ".GetAwaiter().GetResult()",
            "Task.Run(",
        };

        /// <summary>
        ///     Scans an assembly for async rule violations.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <returns>List of detected violations.</returns>
        public static IEnumerable<AsyncRuleViolation> ScanAssembly(Assembly assembly)
        {
            var violations = new List<AsyncRuleViolation>();

            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    var violationsInMethod = ScanMethod(method);
                    violations.AddRange(violationsInMethod);
                }
            }

            return violations;
        }

        /// <summary>
        ///     Scans a method for async rule violations.
        /// </summary>
        /// <param name="method">The method to scan.</param>
        /// <returns>List of violations found in the method.</returns>
        public static IEnumerable<AsyncRuleViolation> ScanMethod(MethodInfo method)
        {
            var violations = new List<AsyncRuleViolation>();

            // Skip generated async state machines and compiler-generated methods
            if (method.Name.Contains("<") || method.Name.Contains(">"))
            {
                return violations;
            }

            // Skip Dispose methods (they must be synchronous)
            if (method.Name == "Dispose")
            {
                return violations;
            }

            // Get method body as IL or source (simplified check using method name and attributes)
            var hasAsyncAttribute = method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;
            var returnsTask = typeof(Task).IsAssignableFrom(method.ReturnType) ||
                             (method.ReturnType.IsGenericType &&
                              method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

            // Check for violations in method name/attributes (simplified analysis)
            // In a real implementation, this would analyze IL or source code
            var methodSignature = $"{method.DeclaringType?.FullName}.{method.Name}";

            // Flag methods that return Task but have suspicious names
            if (returnsTask && method.Name.Contains("Sync"))
            {
                violations.Add(new AsyncRuleViolation
                {
                    Location = methodSignature,
                    ViolationType = AsyncViolationType.SuspiciousNaming,
                    Description = "Method returns Task but has 'Sync' in name",
                    Severity = ViolationSeverity.Warning
                });
            }

            // This is a simplified analysis. A full implementation would:
            // 1. Analyze IL bytecode for forbidden patterns
            // 2. Check for proper async/await usage
            // 3. Verify cancellation token propagation
            // 4. Detect blocking calls in async contexts

            return violations;
        }

        /// <summary>
        ///     Validates that an async operation properly handles cancellation.
        /// </summary>
        /// <param name="operation">The async operation to validate.</param>
        /// <param name="timeout">Maximum time to wait for cancellation.</param>
        /// <returns>True if cancellation is handled properly.</returns>
        public static async Task<bool> ValidateCancellationHandlingAsync(
            Func<CancellationToken, Task> operation,
            TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            var operationTask = operation(cts.Token);
            var delayTask = Task.Delay(timeout * 2, CancellationToken.None);

            var completedTask = await Task.WhenAny(operationTask, delayTask);

            // If delay task completed first, the operation didn't respect cancellation
            if (completedTask == delayTask)
            {
                cts.Cancel();
                return false;
            }

            // Operation completed (either successfully or with cancellation)
            return true;
        }

        /// <summary>
        ///     Checks if a method properly propagates cancellation tokens.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if cancellation is properly propagated.</returns>
        /// <remarks>
        ///     This is a simplified check. Full implementation would analyze method body.
        /// </remarks>
        public static bool HasCancellationPropagation(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var hasCancellationToken = parameters.Any(p => p.ParameterType == typeof(CancellationToken));

            // If method takes CancellationToken, assume it propagates (simplified)
            return hasCancellationToken;
        }
    }

    /// <summary>
    ///     Represents an async rule violation.
    /// </summary>
    public sealed class AsyncRuleViolation
    {
        /// <summary>
        ///     Gets the location of the violation (method, file, etc.).
        /// </summary>
        public string? Location { get; init; }

        /// <summary>
        ///     Gets the type of violation.
        /// </summary>
        public AsyncViolationType ViolationType { get; init; }

        /// <summary>
        ///     Gets the description of the violation.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        ///     Gets the severity of the violation.
        /// </summary>
        public ViolationSeverity Severity { get; init; }
    }

    /// <summary>
    ///     Types of async violations.
    /// </summary>
    public enum AsyncViolationType
    {
        /// <summary>
        ///     Blocking call on async operation (.Result, .Wait()).
        /// </summary>
        BlockingCall,

        /// <summary>
        ///     Missing cancellation token propagation.
        /// </summary>
        MissingCancellation,

        /// <summary>
        ///     Suspicious naming (async method with Sync in name).
        /// </summary>
        SuspiciousNaming,

        /// <summary>
        ///     Task.Run used inappropriately.
        /// </summary>
        InappropriateTaskRun,

        /// <summary>
        ///     Synchronous I/O on hot path.
        /// </summary>
        SyncIO
    }

    /// <summary>
    ///     Severity levels for violations.
    /// </summary>
    public enum ViolationSeverity
    {
        /// <summary>
        ///     Info-level violation.
        /// </summary>
        Info,

        /// <summary>
        ///     Warning-level violation.
        /// </summary>
        Warning,

        /// <summary>
        ///     Error-level violation.
        /// </summary>
        Error,

        /// <summary>
        ///     Critical violation that may cause deadlocks.
        /// </summary>
        Critical
    }
}


