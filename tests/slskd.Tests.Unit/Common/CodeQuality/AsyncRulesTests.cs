// <copyright file="AsyncRulesTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.CodeQuality
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Common.CodeQuality;
    using Xunit;

    /// <summary>
    ///     Tests for H-CODE01: AsyncRules implementation.
    /// </summary>
    public class AsyncRulesTests
    {
        [Fact]
        public async Task ValidateCancellationHandlingAsync_WithProperCancellation_ReturnsTrue()
        {
            Task TestOperationAsync(CancellationToken ct)
            {
                var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                ct.Register(static state =>
                {
                    var tuple = ((TaskCompletionSource<object?> Completion, CancellationToken Token))state!;
                    tuple.Completion.TrySetCanceled(tuple.Token);
                }, (completion, ct));
                return completion.Task;
            }

            var result = await AsyncRules.ValidateCancellationHandlingAsync(TestOperationAsync, TimeSpan.FromMilliseconds(100));
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateCancellationHandlingAsync_WithIgnoredCancellation_ReturnsFalse()
        {
            Task TestOperationAsync(CancellationToken ct)
            {
                return new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }

            var result = await AsyncRules.ValidateCancellationHandlingAsync(TestOperationAsync, TimeSpan.FromMilliseconds(100));

            Assert.False(result);
        }

        [Fact]
        public void HasCancellationPropagation_WithCancellationToken_ReturnsTrue()
        {
            // Arrange
            var method = typeof(TestClass).GetMethod(nameof(TestClass.MethodWithCancellationAsync));

            // Act
            var result = AsyncRules.HasCancellationPropagation(method);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasCancellationPropagation_WithoutCancellationToken_ReturnsFalse()
        {
            // Arrange
            var method = typeof(TestClass).GetMethod(nameof(TestClass.MethodWithoutCancellationAsync));

            // Act
            var result = AsyncRules.HasCancellationPropagation(method);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ScanMethod_WithSuspiciousNaming_ReturnsViolation()
        {
            // Arrange
            var method = typeof(TestClass).GetMethod(nameof(TestClass.AsyncMethodWithSyncName));

            // Act
            var violations = AsyncRules.ScanMethod(method).ToList();

            // Assert
            Assert.Single(violations);
            Assert.Equal(AsyncViolationType.SuspiciousNaming, violations[0].ViolationType);
            Assert.Equal(ViolationSeverity.Warning, violations[0].Severity);
        }

        [Fact]
        public void ScanAssembly_WithTestAssembly_ReturnsViolations()
        {
            // Arrange
            var assembly = typeof(TestClass).Assembly;

            // Act
            var violations = AsyncRules.ScanAssembly(assembly).ToList();

            // Assert
            // Should find at least the suspicious naming violation
            Assert.Contains(violations, v => v.ViolationType == AsyncViolationType.SuspiciousNaming);
        }

        /// <summary>
        ///     Test class with various method signatures for testing.
        /// </summary>
        private class TestClass
        {
            public async Task MethodWithCancellationAsync(CancellationToken ct)
            {
                await Task.Delay(1, ct);
            }

            public async Task MethodWithoutCancellationAsync()
            {
                await Task.Delay(1);
            }

            public async Task AsyncMethodWithSyncName()
            {
                await Task.Delay(1);
            }
        }
    }
}
