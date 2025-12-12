// <copyright file="VirtualSoulfindOptionsTests.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Configuration
{
    using slskd.VirtualSoulfind.v2.Configuration;
    using slskd.VirtualSoulfind.v2.Planning;
    using Xunit;

    /// <summary>
    ///     Tests for <see cref="VirtualSoulfindOptions"/>.
    /// </summary>
    public class VirtualSoulfindOptionsTests
    {
        [Fact]
        public void DefaultOptions_HasReasonableDefaults()
        {
            // Arrange & Act
            var options = new VirtualSoulfindOptions();

            // Assert
            Assert.True(options.Enabled);
            Assert.Equal(PlanningMode.SoulseekFriendly, options.DefaultMode);
            Assert.Equal(3, options.MaxConcurrentExecutions);
            Assert.Equal(10, options.ProcessorBatchSize);
            Assert.Equal(5000, options.ProcessorIntervalMs);
            Assert.NotNull(options.Backends);
            Assert.NotNull(options.WorkBudget);
        }

        [Fact]
        public void SoulseekBackendLimits_DefaultsComplyWithH08()
        {
            // Arrange & Act
            var limits = new SoulseekBackendLimits();

            // Assert - H-08 compliance
            Assert.Equal(10, limits.MaxSearchesPerMinute);
            Assert.Equal(2, limits.MaxParallelSearches);
            Assert.Equal(5, limits.MaxBrowsesPerMinute);
            Assert.Equal(50 * 1024, limits.MinUploadSpeedBytesPerSec); // 50 KB/s
            Assert.Equal(10_000, limits.SearchTimeoutMs);
        }

        [Fact]
        public void MeshBackendLimits_HasReasonableDefaults()
        {
            // Arrange & Act
            var limits = new MeshBackendLimits();

            // Assert
            Assert.Equal(30, limits.MaxQueriesPerMinute);
            Assert.Equal(5000, limits.QueryTimeoutMs);
            Assert.Equal(0.5f, limits.MinTrustScore);
        }

        [Fact]
        public void TorrentBackendLimits_HasReasonableDefaults()
        {
            // Arrange & Act
            var limits = new TorrentBackendLimits();

            // Assert
            Assert.Equal(20, limits.MaxDhtQueriesPerMinute);
            Assert.Equal(3, limits.MinSeeders);
        }

        [Fact]
        public void HttpBackendLimits_HasReasonableDefaults()
        {
            // Arrange & Act
            var limits = new HttpBackendLimits();

            // Assert
            Assert.Equal(60, limits.MaxRequestsPerMinute);
            Assert.Equal(30_000, limits.RequestTimeoutMs);
            Assert.False(limits.RequireHttps);
        }

        [Fact]
        public void WorkBudgetLimits_HasReasonableDefaults()
        {
            // Arrange & Act
            var limits = new WorkBudgetLimits();

            // Assert
            Assert.Equal(1000, limits.DefaultBudgetPerExecution);
            Assert.Equal(5000, limits.MaxBudgetPerExecution);
            Assert.True(limits.MaxBudgetPerExecution > limits.DefaultBudgetPerExecution);
        }

        [Fact]
        public void Options_CanBeCustomized()
        {
            // Arrange & Act
            var options = new VirtualSoulfindOptions
            {
                Enabled = false,
                DefaultMode = PlanningMode.OfflinePlanning,
                MaxConcurrentExecutions = 5,
                ProcessorBatchSize = 20,
                ProcessorIntervalMs = 10_000,
                Backends = new BackendLimits
                {
                    Soulseek = new SoulseekBackendLimits
                    {
                        MaxSearchesPerMinute = 5,
                        MaxParallelSearches = 1,
                    },
                },
                WorkBudget = new WorkBudgetLimits
                {
                    DefaultBudgetPerExecution = 2000,
                    MaxBudgetPerExecution = 10_000,
                },
            };

            // Assert
            Assert.False(options.Enabled);
            Assert.Equal(PlanningMode.OfflinePlanning, options.DefaultMode);
            Assert.Equal(5, options.MaxConcurrentExecutions);
            Assert.Equal(20, options.ProcessorBatchSize);
            Assert.Equal(10_000, options.ProcessorIntervalMs);
            Assert.Equal(5, options.Backends.Soulseek.MaxSearchesPerMinute);
            Assert.Equal(1, options.Backends.Soulseek.MaxParallelSearches);
            Assert.Equal(2000, options.WorkBudget.DefaultBudgetPerExecution);
            Assert.Equal(10_000, options.WorkBudget.MaxBudgetPerExecution);
        }

        [Fact]
        public void SoulseekLimits_CanBeConfiguredForHighVolume()
        {
            // Arrange & Act - User wants more aggressive Soulseek usage
            var limits = new SoulseekBackendLimits
            {
                MaxSearchesPerMinute = 20,
                MaxParallelSearches = 5,
                MaxBrowsesPerMinute = 10,
                MinUploadSpeedBytesPerSec = 100 * 1024, // 100 KB/s
            };

            // Assert
            Assert.Equal(20, limits.MaxSearchesPerMinute);
            Assert.Equal(5, limits.MaxParallelSearches);
            Assert.Equal(10, limits.MaxBrowsesPerMinute);
            Assert.Equal(100 * 1024, limits.MinUploadSpeedBytesPerSec);
        }

        [Fact]
        public void SoulseekLimits_CanBeConfiguredForConservative()
        {
            // Arrange & Act - User wants very conservative Soulseek usage
            var limits = new SoulseekBackendLimits
            {
                MaxSearchesPerMinute = 3,
                MaxParallelSearches = 1,
                MaxBrowsesPerMinute = 1,
                MinUploadSpeedBytesPerSec = 10 * 1024, // 10 KB/s
            };

            // Assert
            Assert.Equal(3, limits.MaxSearchesPerMinute);
            Assert.Equal(1, limits.MaxParallelSearches);
            Assert.Equal(1, limits.MaxBrowsesPerMinute);
            Assert.Equal(10 * 1024, limits.MinUploadSpeedBytesPerSec);
        }

        [Fact]
        public void MeshLimits_CanRequireHighTrust()
        {
            // Arrange & Act - User only trusts high-quality mesh sources
            var limits = new MeshBackendLimits
            {
                MinTrustScore = 0.8f,
                MaxQueriesPerMinute = 60,
            };

            // Assert
            Assert.Equal(0.8f, limits.MinTrustScore);
            Assert.Equal(60, limits.MaxQueriesPerMinute);
        }

        [Fact]
        public void HttpLimits_CanRequireHttps()
        {
            // Arrange & Act - User requires HTTPS-only
            var limits = new HttpBackendLimits
            {
                RequireHttps = true,
                MaxRequestsPerMinute = 120,
                RequestTimeoutMs = 60_000,
            };

            // Assert
            Assert.True(limits.RequireHttps);
            Assert.Equal(120, limits.MaxRequestsPerMinute);
            Assert.Equal(60_000, limits.RequestTimeoutMs);
        }

        [Fact]
        public void WorkBudget_CanBeConfiguredForLowResource()
        {
            // Arrange & Act - User wants to limit resource usage
            var limits = new WorkBudgetLimits
            {
                DefaultBudgetPerExecution = 500,
                MaxBudgetPerExecution = 1000,
            };

            // Assert
            Assert.Equal(500, limits.DefaultBudgetPerExecution);
            Assert.Equal(1000, limits.MaxBudgetPerExecution);
        }

        [Fact]
        public void WorkBudget_CanBeConfiguredForHighPerformance()
        {
            // Arrange & Act - User has resources and wants speed
            var limits = new WorkBudgetLimits
            {
                DefaultBudgetPerExecution = 5000,
                MaxBudgetPerExecution = 20_000,
            };

            // Assert
            Assert.Equal(5000, limits.DefaultBudgetPerExecution);
            Assert.Equal(20_000, limits.MaxBudgetPerExecution);
        }

        [Fact]
        public void BackendLimits_InitializesAllBackends()
        {
            // Arrange & Act
            var limits = new BackendLimits();

            // Assert
            Assert.NotNull(limits.Soulseek);
            Assert.NotNull(limits.Mesh);
            Assert.NotNull(limits.Torrent);
            Assert.NotNull(limits.Http);
        }

        [Fact]
        public void Options_ProcessorSettings_AreReasonable()
        {
            // Arrange & Act
            var options = new VirtualSoulfindOptions();

            // Assert - Ensure processor doesn't overwhelm the system
            Assert.True(options.ProcessorIntervalMs >= 1000, "Processor interval should be at least 1 second");
            Assert.True(options.ProcessorBatchSize > 0, "Batch size must be positive");
            Assert.True(options.ProcessorBatchSize <= 100, "Batch size should be reasonable");
            Assert.True(options.MaxConcurrentExecutions > 0, "Must allow at least one concurrent execution");
            Assert.True(options.MaxConcurrentExecutions <= 10, "Concurrent executions should be reasonable");
        }

        [Fact]
        public void Options_AllPlanningModes_AreSupported()
        {
            // Arrange & Act - Test all planning modes can be set
            var modes = new[]
            {
                PlanningMode.SoulseekFriendly,
                PlanningMode.OfflinePlanning,
                PlanningMode.MeshOnly,
            };

            foreach (var mode in modes)
            {
                var options = new VirtualSoulfindOptions { DefaultMode = mode };

                // Assert
                Assert.Equal(mode, options.DefaultMode);
            }
        }
    }
}
