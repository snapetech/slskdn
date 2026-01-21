// LAN Backend tests
namespace slskd.Tests.Unit.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    public class LanBackendTests
    {
        [Fact]
        public void LanBackend_HasCorrectType()
        {
            var backend = CreateLanBackend();
            Assert.Equal(ContentBackendType.Lan, backend.Type);
            Assert.Null(backend.SupportedDomain);
        }

        [Fact]
        public async Task FindCandidates_Disabled_ReturnsEmpty()
        {
            var options = new LanBackendOptions { Enabled = false };
            var backend = CreateLanBackend(options);
            var itemId = ContentItemId.NewId();
            
            var candidates = await backend.FindCandidatesAsync(itemId);
            
            Assert.Empty(candidates);
        }

        [Fact]
        public async Task ValidateCandidate_AcceptsUncPathInAllowedRange()
        {
            var options = new LanBackendOptions
            {
                Enabled = true,
                AllowedNetworks = new List<string> { "192.168.0.0/16" }
            };
            var backend = CreateLanBackend(options);
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Lan,
                BackendRef = "\\\\192.168.1.100\\share\\music\\file.flac",
                TrustScore = 0.9f,
                ExpectedQuality = 95,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateCandidate_AcceptsSmbUri()
        {
            var options = new LanBackendOptions
            {
                Enabled = true,
                AllowedNetworks = new List<string> { "10.0.0.0/8" }
            };
            var backend = CreateLanBackend(options);
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Lan,
                BackendRef = "smb://10.20.30.40/share/music/file.flac",
                TrustScore = 0.9f,
                ExpectedQuality = 95,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsIpOutsideAllowedRange()
        {
            var options = new LanBackendOptions
            {
                Enabled = true,
                AllowedNetworks = new List<string> { "192.168.0.0/16" }
            };
            var backend = CreateLanBackend(options);
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Lan,
                BackendRef = "\\\\8.8.8.8\\share\\file.flac",
                TrustScore = 0.9f,
                ExpectedQuality = 95,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.False(result.IsValid);
            Assert.Contains("not in allowed networks", result.InvalidityReason);
        }

        [Fact]
        public async Task ValidateCandidate_RejectsNonLanCandidate()
        {
            var backend = CreateLanBackend();
            var candidate = new SourceCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ItemId = ContentItemId.NewId(),
                Backend = ContentBackendType.Http,
                BackendRef = "https://example.com/file.flac",
                TrustScore = 0.9f,
                ExpectedQuality = 95,
            };
            
            var result = await backend.ValidateCandidateAsync(candidate);
            
            Assert.False(result.IsValid);
            Assert.Contains("Not a LAN", result.InvalidityReason);
        }

        private LanBackend CreateLanBackend(LanBackendOptions options = null)
        {
            options ??= new LanBackendOptions
            {
                Enabled = true,
                AllowedNetworks = new List<string> { "192.168.0.0/16", "10.0.0.0/8" }
            };
            var registry = new InMemorySourceRegistry();
            var optionsMonitor = new Mock<IOptionsMonitor<LanBackendOptions>>();
            optionsMonitor.Setup(o => o.CurrentValue).Returns(options);
            return new LanBackend(registry, optionsMonitor.Object);
        }
    }
}
