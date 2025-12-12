// Soulseek Backend Tests
namespace slskd.Tests.Unit.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Soulseek;
    using slskd.Common.Security;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    public class SoulseekBackendTests
    {
        private readonly Mock<ISoulseekClient> _mockClient;
        private readonly Mock<ISoulseekSafetyLimiter> _mockLimiter;
        private readonly Mock<ILogger<SoulseekBackend>> _mockLogger;
        private readonly Mock<IOptionsMonitor<SoulseekBackendOptions>> _mockOptions;
        private readonly SoulseekBackendOptions _defaultOptions;

        public SoulseekBackendTests()
        {
            _mockClient = new Mock<ISoulseekClient>();
            _mockLimiter = new Mock<ISoulseekSafetyLimiter>();
            _mockLogger = new Mock<ILogger<SoulseekBackend>>();
            _mockOptions = new Mock<IOptionsMonitor<SoulseekBackendOptions>>();

            _defaultOptions = new SoulseekBackendOptions
            {
                Enabled = true,
                SearchTimeoutSeconds = 15,
                MaxResponsesPerSearch = 100,
                MaxFilesPerResponse = 50,
                MaxCandidatesPerItem = 20,
                MinimumUploadSpeed = 100_000,
                MinimumTrustScore = 30.0f,
            };

            _mockOptions.Setup(o => o.CurrentValue).Returns(_defaultOptions);
        }

        [Fact]
        public void Type_IsSoulseek()
        {
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            Assert.Equal(ContentBackendType.Soulseek, backend.Type);
        }

        [Fact]
        public void SupportedDomain_IsMusic()
        {
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            Assert.Equal(ContentDomain.Music, backend.SupportedDomain);
        }

        [Fact]
        public async Task FindCandidates_WhenDisabled_ReturnsEmpty()
        {
            _defaultOptions.Enabled = false;
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            var results = await backend.FindCandidatesAsync(ContentItemId.NewId());
            
            Assert.Empty(results);
        }

        [Fact]
        public async Task FindCandidates_WhenRateLimited_ReturnsEmpty()
        {
            _mockLimiter.Setup(l => l.TryConsumeSearch(It.IsAny<string>())).Returns(false);
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            var results = await backend.FindCandidatesAsync(ContentItemId.NewId());
            
            Assert.Empty(results);
            _mockLimiter.Verify(l => l.TryConsumeSearch("virtualsoulfind-v2"), Times.Once);
        }

        [Fact]
        public async Task FindCandidates_EnforcesSafetyLimiter()
        {
            // This test verifies H-08 integration
            _mockLimiter.Setup(l => l.TryConsumeSearch(It.IsAny<string>())).Returns(false);

            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            await backend.FindCandidatesAsync(ContentItemId.NewId());
            
            // Verify safety limiter was called BEFORE any search would happen
            _mockLimiter.Verify(l => l.TryConsumeSearch("virtualsoulfind-v2"), Times.Once);
            
            // Verify search was NOT attempted when rate limited
            _mockClient.Verify(
                c => c.SearchAsync(
                    It.IsAny<SearchQuery>(),
                    It.IsAny<Action<SearchResponse>>(),
                    It.IsAny<SearchScope>(),
                    It.IsAny<int>(),
                    It.IsAny<SearchOptions>(),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ValidateCandidate_WithValidSoulseekCandidate_ReturnsValid()
        {
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Soulseek,
                BackendRef = "someuser|path/to/file.mp3",
                TrustScore = 0.75f, // 0-1 range
                ExpectedQuality = 0.80f, // 0-1 range
            };

            var result = await backend.ValidateCandidateAsync(candidate);

            Assert.True(result.IsValid);
            Assert.Equal(0.75f, result.TrustScore);
            Assert.Equal(0.80f, result.QualityScore);
        }

        [Fact]
        public async Task ValidateCandidate_WithNonSoulseekBackend_ReturnsInvalid()
        {
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Http,
                BackendRef = "http://example.com/file.mp3",
            };

            var result = await backend.ValidateCandidateAsync(candidate);

            Assert.False(result.IsValid);
            Assert.Contains("Not a Soulseek", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_WhenDisabled_ReturnsInvalid()
        {
            _defaultOptions.Enabled = false;
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Soulseek,
                BackendRef = "user|file.mp3",
            };

            var result = await backend.ValidateCandidateAsync(candidate);

            Assert.False(result.IsValid);
            Assert.Contains("disabled", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_WithInvalidBackendRef_ReturnsInvalid()
        {
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Soulseek,
                BackendRef = "invalid-no-pipe",
            };

            var result = await backend.ValidateCandidateAsync(candidate);

            Assert.False(result.IsValid);
            Assert.Contains("format", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_WithLowTrustScore_ReturnsInvalid()
        {
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Soulseek,
                BackendRef = "user|file.mp3",
                TrustScore = 0.10f, // Below minimum (0.30)
            };

            var result = await backend.ValidateCandidateAsync(candidate);

            Assert.False(result.IsValid);
            Assert.Contains("Trust score", result.InvalidityReason);
            Assert.Contains("below minimum", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_ParsesBackendRefCorrectly()
        {
            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Soulseek,
                BackendRef = "testuser|music/artist/track.mp3",
                TrustScore = 0.50f,
            };

            var result = await backend.ValidateCandidateAsync(candidate);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Options_DefaultsAreReasonable()
        {
            var opts = new SoulseekBackendOptions();

            Assert.True(opts.Enabled);
            Assert.Equal(15, opts.SearchTimeoutSeconds);
            Assert.Equal(100, opts.MaxResponsesPerSearch);
            Assert.Equal(50, opts.MaxFilesPerResponse);
            Assert.Equal(20, opts.MaxCandidatesPerItem);
            Assert.Equal(100_000, opts.MinimumUploadSpeed);
            Assert.Equal(30.0f, opts.MinimumTrustScore);
        }

        [Fact]
        public async Task FindCandidates_H08Integration_Critical()
        {
            // This is THE MOST CRITICAL test for H-08 compliance
            // Verify that the safety limiter is ALWAYS checked before searching
            
            var callCount = 0;
            _mockLimiter
                .Setup(l => l.TryConsumeSearch("virtualsoulfind-v2"))
                .Returns(() =>
                {
                    callCount++;
                    return false; // Always reject
                });

            var backend = new SoulseekBackend(_mockClient.Object, _mockLimiter.Object, _mockOptions.Object, _mockLogger.Object);
            
            // Try multiple searches
            for (int i = 0; i < 5; i++)
            {
                await backend.FindCandidatesAsync(ContentItemId.NewId());
            }

            // Safety limiter should have been called EXACTLY 5 times
            Assert.Equal(5, callCount);
            
            // No actual searches should have been performed
            _mockClient.Verify(
                c => c.SearchAsync(
                    It.IsAny<SearchQuery>(),
                    It.IsAny<Action<SearchResponse>>(),
                    It.IsAny<SearchScope>(),
                    It.IsAny<int>(),
                    It.IsAny<SearchOptions>(),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Never,
                "H-08 VIOLATION: Search was attempted despite rate limit!");
        }
    }
}
